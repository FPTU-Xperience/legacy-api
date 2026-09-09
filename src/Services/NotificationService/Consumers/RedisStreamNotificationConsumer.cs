using System.Diagnostics;
using System.Text.Json;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationService.Data;
using NotificationService.Models;
using StackExchange.Redis;

namespace NotificationService.Consumers;

/// <summary>
/// Consumes events from Redis Streams using XREADGROUP.
/// Creates Notifications in the database and acknowledges messages after successful processing.
/// </summary>
public sealed class RedisStreamNotificationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDatabase _db;
    private readonly RedisStreamOptions _options;
    private readonly string _consumerName;
    private readonly ILogger<RedisStreamNotificationConsumer> _logger;

    public RedisStreamNotificationConsumer(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisStreamOptions> options,
        ILogger<RedisStreamNotificationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _db = connectionMultiplexer.GetDatabase();
        _consumerName = $"{_options.ConsumerNamePrefix}-{Environment.MachineName}-{Environment.ProcessId}";
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = TimeSpan.FromSeconds(5);
        // Keep retrying until Redis becomes available. A temporary startup outage
        // must not disable notifications for the lifetime of the service.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConsumerGroupAsync(stoppingToken);
                _logger.LogInformation(
                    "Redis Stream consumer started. Stream: '{StreamName}', Group: '{ConsumerGroup}', Consumer: '{ConsumerName}'",
                    _options.StreamName,
                    _options.ConsumerGroup,
                    _consumerName);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Redis consumer failed to initialize. Retrying in {Delay}s...",
                    retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
                retryDelay = TimeSpan.FromSeconds(Math.Min(30, retryDelay.TotalSeconds * 1.5));
            }
        }

        // Phase 2: Main consumption loop — only new messages from here on
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReadNewMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Redis consumer loop. Will retry after poll interval.");
                await Task.Delay(_options.PollIntervalMs, stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerGroupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                StreamPosition.Beginning,
                createStream: true);
            _logger.LogInformation("Created consumer group '{ConsumerGroup}' on stream '{StreamName}'",
                _options.ConsumerGroup, _options.StreamName);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Consumer group '{ConsumerGroup}' already exists on stream '{StreamName}'",
                _options.ConsumerGroup, _options.StreamName);
        }
    }

    /// <summary>
    /// Reads ALL existing messages in the stream (from beginning) during startup.
    /// This prevents losing messages that were published before the consumer group was created.
    /// After all existing messages are processed, the consumer switches to '>' (NewMessages).
    /// </summary>
    private async Task CatchUpExistingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Read from the beginning of the stream (id "0-0")
            var entries = await _db.StreamReadGroupAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                _consumerName,
                StreamPosition.Beginning,
                count: _options.BatchSize);

            if (entries.Length == 0)
            {
                _logger.LogDebug("No existing messages in stream '{StreamName}' during catch-up", _options.StreamName);
                return;
            }

            _logger.LogInformation(
                "Catch-up phase: found {Count} existing messages in stream '{StreamName}'",
                entries.Length, _options.StreamName);

            var processed = 0;
            var errors = 0;
            foreach (var entry in entries)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    await ProcessMessageAsync(entry, cancellationToken);
                    processed++;
                }
                catch
                {
                    errors++;
                    // Continue processing other messages
                }
            }

            _logger.LogInformation(
                "Catch-up phase completed. Processed: {Processed}, Errors: {Errors}",
                processed, errors);

            // If there are more messages, recursively catch up (paginate)
            if (processed > 0)
            {
                await CatchUpExistingMessagesAsync(cancellationToken);
            }
        }
        catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // Group doesn't exist yet — this shouldn't happen since we create it above,
            // but handle gracefully
            _logger.LogWarning("Consumer group disappeared during catch-up. Will retry on next loop.");
        }
        catch (RedisException)
        {
            // Stream might not exist yet — that's fine, no catch-up needed
            _logger.LogDebug("Stream '{StreamName}' does not exist yet, skipping catch-up", _options.StreamName);
        }
    }

    private async Task ReadNewMessagesAsync(CancellationToken stoppingToken)
    {
        // '>' means: only messages delivered to this consumer group AFTER the last acknowledged message
        var entries = await _db.StreamReadGroupAsync(
            _options.StreamName,
            _options.ConsumerGroup,
            _consumerName,
            StreamPosition.NewMessages,
            count: _options.BatchSize);

        if (entries.Length == 0)
        {
            await Task.Delay(_options.PollIntervalMs, stoppingToken);
            return;
        }

        var batchStopwatch = Stopwatch.StartNew();
        foreach (var entry in entries)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await ProcessMessageAsync(entry, stoppingToken);
        }
        batchStopwatch.Stop();

        _logger.LogDebug(
            "Processed batch of {Count} messages in {ElapsedMs}ms by consumer '{ConsumerName}'",
            entries.Length, batchStopwatch.ElapsedMilliseconds, _consumerName);
    }

    private async Task ProcessMessageAsync(StreamEntry entry, CancellationToken cancellationToken)
    {
        var redisEntryId = entry.Id;
        Guid eventId;
        string routingKey;
        string payload;
        string correlationId;

        try
        {
            var fields = entry.Values.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);

            if (!fields.TryGetValue("eventId", out var eventIdStr) || !Guid.TryParse(eventIdStr, out eventId))
            {
                _logger.LogWarning("Stream entry {RedisEntryId} has no valid eventId, acknowledging and skipping.", redisEntryId);
                await AcknowledgeMessageAsync(redisEntryId);
                return;
            }

            routingKey = fields.GetValueOrDefault("eventType", "");
            payload = fields.GetValueOrDefault("payload", "{}");
            correlationId = fields.GetValueOrDefault("correlationId", Guid.NewGuid().ToString("N"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read fields from stream entry {RedisEntryId}", redisEntryId);
            return; // Do not ACK — message stays pending
        }

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            var msgStopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

                // Idempotency: skip if already processed
                var alreadyProcessed = await db.ProcessedEvents
                    .AnyAsync(x => x.EventId == eventId, cancellationToken);
                if (alreadyProcessed)
                {
                    _logger.LogDebug("Skipping duplicate event {EventId} (already processed)", eventId);
                    await AcknowledgeMessageAsync(redisEntryId);
                    return;
                }

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                var notifications = CreateNotifications(routingKey, root);

                db.Notifications.AddRange(notifications);
                db.ProcessedEvents.Add(new ProcessedEvent
                {
                    EventId = eventId,
                    RoutingKey = routingKey,
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });

                await db.SaveChangesAsync(cancellationToken);
                await AcknowledgeMessageAsync(redisEntryId);

                msgStopwatch.Stop();
                _logger.LogInformation(
                    "Processed event {EventId} ({EventType}) -> {NotificationCount} notification(s) created. RedisEntryId: {RedisEntryId}. Duration: {DurationMs}ms",
                    eventId, routingKey, notifications.Count, redisEntryId, msgStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                msgStopwatch.Stop();

                // Inspect delivery attempts from pending messages info
                try
                {
                    var pending = await _db.StreamPendingMessagesAsync(
                        _options.StreamName,
                        _options.ConsumerGroup,
                        1,
                        _consumerName,
                        redisEntryId,
                        redisEntryId);

                    var deliveryCount = pending.Length > 0 ? pending[0].DeliveryCount : 1;

                    if (deliveryCount >= _options.MaxDeliveryAttempts)
                    {
                        await MoveToDeadLetterQueueAsync(entry, eventId, routingKey, ex.Message, (int)deliveryCount, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(ex,
                            "Failed to process event {EventId} ({EventType}) from RedisEntryId {RedisEntryId}. Attempt {Attempt}/{Max}. Message remains pending.",
                            eventId, routingKey, redisEntryId, deliveryCount, _options.MaxDeliveryAttempts);
                    }
                }
                catch (Exception pendingEx)
                {
                    _logger.LogError(pendingEx,
                        "Failed to check pending status for event {EventId}. Message remains pending. Original error: {Error}",
                        eventId, ex.Message);
                }
            }
        }
    }

    private async Task MoveToDeadLetterQueueAsync(
        StreamEntry entry,
        Guid eventId,
        string routingKey,
        string errorMessage,
        int deliveryCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var dlqValues = entry.Values.ToList();
            dlqValues.Add(new("errorReason", errorMessage));
            dlqValues.Add(new("failedAtUtc", DateTimeOffset.UtcNow.ToString("O")));
            dlqValues.Add(new("deliveryCount", deliveryCount.ToString()));
            dlqValues.Add(new("originalStream", _options.StreamName));
            dlqValues.Add(new("failedConsumer", _consumerName));

            await _db.StreamAddAsync(_options.DeadLetterStreamName, dlqValues.ToArray());

            // Record in ProcessedEvents to prevent re-execution
            using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            if (!await db.ProcessedEvents.AnyAsync(x => x.EventId == eventId, cancellationToken))
            {
                db.ProcessedEvents.Add(new ProcessedEvent
                {
                    EventId = eventId,
                    RoutingKey = $"{routingKey}:DLQ",
                    ProcessedAtUtc = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            await AcknowledgeMessageAsync(entry.Id);
            _logger.LogWarning(
                "Event {EventId} ({EventType}) from {RedisEntryId} exceeded max delivery attempts ({Attempts}/{Max}). Moved to DLQ '{DlqStream}' and acknowledged on main stream.",
                eventId, routingKey, entry.Id, deliveryCount, _options.MaxDeliveryAttempts, _options.DeadLetterStreamName);
        }
        catch (Exception dlqEx)
        {
            _logger.LogError(dlqEx, "Failed to move event {EventId} to DLQ '{DlqStream}'", eventId, _options.DeadLetterStreamName);
        }
    }

    private async Task AcknowledgeMessageAsync(RedisValue entryId)
    {
        await _db.StreamAcknowledgeAsync(_options.StreamName, _options.ConsumerGroup, entryId);
    }

    internal static IReadOnlyList<Notification> CreateNotifications(string routingKey, JsonElement root)
    {
        if (routingKey == EventRoutingKeys.ActivityCreated)
        {
            var recipients = GetIntArray(root, "recipientUserIds");
            if (recipients.Count > 0)
            {
                return recipients.Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    EventType = routingKey,
                    Title = "Hoạt động mới",
                    Message = $"{GetString(root, "clubName")} đã lên lịch hoạt động {GetString(root, "title")}."
                }).ToArray();
            }

            return [new Notification
            {
                RecipientRole = AuthRoles.ClubMember,
                EventType = routingKey,
                Title = "Hoạt động mới",
                Message = $"{GetString(root, "clubName")} đã lên lịch hoạt động {GetString(root, "title")}."
            }];
        }

        if (routingKey == EventRoutingKeys.ReportCreated)
        {
            return GetIntArray(root, "recipientUserIds")
                .Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    EventType = routingKey,
                    Title = "Bản nháp báo cáo mới",
                    Message = $"Một báo cáo mới của {GetString(root, "clubName")} đã được tạo cho kỳ {GetString(root, "period")}."
                })
                .ToArray();
        }

        if (routingKey == EventRoutingKeys.ReportSubmitted)
        {
            var workflowStage = GetString(root, "workflowStage");
            var recipients = GetIntArray(root, "recipientUserIds");
            if (recipients.Count > 0)
            {
                var title = workflowStage switch
                {
                    "FinanceReview" => "Báo cáo sự kiện sắp tới cần lập ngân sách",
                    "ManagerReview" => "Hồ sơ sự kiện đang chờ chủ nhiệm xét duyệt",
                    _ => "Báo cáo đang chờ chủ nhiệm xét duyệt"
                };
                var message = workflowStage == "FinanceReview"
                    ? $"Vui lòng lập báo cáo ngân sách cho {GetString(root, "clubName")} - {GetString(root, "period")}."
                    : $"Vui lòng xét duyệt hồ sơ sự kiện và ngân sách của {GetString(root, "clubName")} - {GetString(root, "period")}.";
                return recipients.Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    EventType = routingKey,
                    Title = title,
                    Message = message
                }).ToArray();
            }

            if (workflowStage == "FinalReview")
            {
                return [new Notification
                {
                    RecipientRole = AuthRoles.StudentAffairsAdmin,
                    EventType = routingKey,
                    Title = "Hồ sơ sự kiện đang chờ phê duyệt cuối",
                    Message = $"{GetString(root, "clubName")} đã nộp hồ sơ sự kiện và ngân sách cho kỳ {GetString(root, "period")}."
                }];
            }
        }

        if (routingKey == EventRoutingKeys.BudgetProposalSubmitted)
        {
            var recipients = GetIntArray(root, "recipientUserIds");
            if (recipients.Count > 0)
            {
                return recipients.Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    EventType = routingKey,
                    Title = "Đề xuất ngân sách đang chờ chủ nhiệm xét duyệt",
                    Message = $"{GetString(root, "clubName")} đã nộp đề xuất ngân sách {GetString(root, "requestedAmount")} VNĐ."
                }).ToArray();
            }

            return [new Notification
            {
                RecipientRole = AuthRoles.StudentAffairsAdmin,
                EventType = routingKey,
                Title = "Đề xuất ngân sách đang chờ phê duyệt cuối",
                Message = $"{GetString(root, "clubName")} đã nộp đề xuất ngân sách {GetString(root, "requestedAmount")} VNĐ."
            }];
        }

        return [routingKey switch
        {
            EventRoutingKeys.ClubCreated => new Notification
            {
                RecipientRole = AuthRoles.StudentAffairsAdmin,
                EventType = routingKey,
                Title = "Câu lạc bộ mới được tạo",
                Message = $"{GetString(root, "clubName")} ({GetString(root, "clubCode")}) đã được thêm vào hệ thống."
            },
            EventRoutingKeys.UserRegistered => new Notification
            {
                RecipientUserId = GetInt(root, "userId"),
                EventType = routingKey,
                Title = "Chào mừng đến với FPTU Club Hub",
                Message = $"Xin chào {GetString(root, "fullName")}. Tài khoản thành viên của bạn đã sẵn sàng."
            },
            EventRoutingKeys.ReportSubmitted => new Notification
            {
                RecipientUserId = GetInt(root, "recipientUserId"),
                RecipientRole = GetInt(root, "recipientUserId") is null
                    ? (GetString(root, "status") == "Submitted" ? AuthRoles.ClubManager : AuthRoles.StudentAffairsAdmin)
                    : null,
                EventType = routingKey,
                Title = GetString(root, "status") == "Submitted" ? "Báo cáo đang chờ chủ nhiệm xét duyệt" : "Báo cáo đang chờ phê duyệt cuối",
                Message = $"{GetString(root, "clubName")} đã nộp báo cáo kỳ {GetString(root, "period")}."
            },
            EventRoutingKeys.ReportApproved => new Notification
            {
                RecipientUserId = GetInt(root, "recipientUserId"),
                EventType = routingKey,
                Title = "Báo cáo đã được phê duyệt",
                Message = $"Báo cáo kỳ {GetString(root, "period")} của {GetString(root, "clubName")} đã được phê duyệt."
            },
            EventRoutingKeys.ReportRejected => new Notification
            {
                RecipientUserId = GetInt(root, "recipientUserId"),
                EventType = routingKey,
                Title = "Báo cáo cần chỉnh sửa",
                Message = $"Báo cáo kỳ {GetString(root, "period")} của {GetString(root, "clubName")} bị từ chối: {GetString(root, "feedback")}"
            },
            EventRoutingKeys.KpiCalculated => new Notification
            {
                RecipientRole = AuthRoles.ClubManager,
                EventType = routingKey,
                Title = "Điểm KPI đã được cập nhật",
                Message = $"Điểm KPI của {GetString(root, "clubName")} trong kỳ {GetString(root, "period")} là {GetString(root, "points")} điểm."
            },
            EventRoutingKeys.BudgetApproved => new Notification
            {
                RecipientUserId = GetInt(root, "recipientUserId"),
                EventType = routingKey,
                Title = "Ngân sách đã được phê duyệt",
                Message = $"Ngân sách của {GetString(root, "clubName")} đã được phê duyệt với số tiền {GetString(root, "approvedAmount")} VNĐ."
            },
            EventRoutingKeys.SettlementOverdue => new Notification
            {
                RecipientRole = AuthRoles.Treasurer,
                EventType = routingKey,
                Title = "Quyết toán quá hạn",
                Message = $"{GetString(root, "clubName")} có quyết toán quá hạn cho đề xuất #{GetString(root, "proposalId")}."
            },
            EventRoutingKeys.ExportCompleted => new Notification
            {
                RecipientUserId = GetInt(root, "requestedByUserId"),
                EventType = routingKey,
                Title = "Tệp xuất đã sẵn sàng",
                Message = $"Tệp xuất {GetString(root, "exportType")} đã sẵn sàng: {GetString(root, "fileName")}."
            },
            EventRoutingKeys.ReportDeadlineReminder => new Notification
            {
                RecipientRole = AuthRoles.StudentAffairsAdmin,
                EventType = routingKey,
                Title = "Nhắc hạn nộp báo cáo",
                Message = $"Một số câu lạc bộ chưa nộp báo cáo cho kỳ {GetString(root, "period")}."
            },
            _ => new Notification
            {
                RecipientRole = AuthRoles.Admin,
                EventType = routingKey,
                Title = "Sự kiện hệ thống",
                Message = "Hệ thống vừa ghi nhận một sự kiện mới."
            }
        }];
    }

    private static string GetString(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out var value) ? value.ToString() : string.Empty;
    }

    private static int? GetInt(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number) ? number : null;
    }

    private static IReadOnlyList<int> GetIntArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _))
            .Select(item => item.GetInt32())
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }
}
