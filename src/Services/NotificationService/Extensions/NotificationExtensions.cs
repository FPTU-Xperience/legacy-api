using ClubReportHub.Shared.Auth;
using NotificationService.Contracts;
using NotificationService.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace NotificationService.Extensions;

public static class NotificationExtensions
{
    public static NotificationResponse ToResponse(Notification notification)
    {
        var (title, message) = NormalizeLegacyNotification(notification);
        return new NotificationResponse(
            notification.Id,
            notification.RecipientUserId,
            notification.RecipientRole,
            notification.EventType,
            title,
            message,
            notification.IsRead,
            notification.CreatedAtUtc);
    }

    public static IQueryable<Notification>? ScopeNotificationQuery(
        IQueryable<Notification> query,
        ClaimsPrincipal user,
        IReadOnlyCollection<string> roles,
        int? recipientUserId,
        string? recipientRole)
    {
        var normalizedRole = NormalizeRole(recipientRole);
        if (IsNotificationAdmin(user))
        {
            return ApplyRequestedRecipientFilters(query, recipientUserId, normalizedRole);
        }

        var userId = user.GetUserId();
        if (recipientUserId.HasValue && recipientUserId.Value != userId)
        {
            return null;
        }

        if (normalizedRole is not null && !roles.Contains(normalizedRole))
        {
            return null;
        }

        query = query.Where(x => x.RecipientUserId == userId || (x.RecipientRole != null && roles.Contains(x.RecipientRole)));
        return ApplyRequestedRecipientFilters(query, recipientUserId, normalizedRole);
    }

    public static IQueryable<Notification> ApplyRequestedRecipientFilters(
        IQueryable<Notification> query,
        int? recipientUserId,
        string? recipientRole)
    {
        if (recipientUserId.HasValue && recipientRole is not null)
        {
            return query.Where(x => x.RecipientUserId == recipientUserId || x.RecipientRole == recipientRole);
        }

        if (recipientUserId.HasValue)
        {
            return query.Where(x => x.RecipientUserId == recipientUserId);
        }

        if (recipientRole is not null)
        {
            return query.Where(x => x.RecipientRole == recipientRole);
        }

        return query;
    }

    public static bool CanAccessNotification(
        ClaimsPrincipal user,
        IReadOnlyCollection<string> roles,
        Notification notification)
    {
        if (IsNotificationAdmin(user))
        {
            return true;
        }

        var userId = user.GetUserId();
        return notification.RecipientUserId == userId
            || (notification.RecipientRole is not null && roles.Contains(notification.RecipientRole));
    }

    public static bool IsNotificationAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole(AuthRoles.Admin);
    }

    public static async Task<string[]> GetRecipientRolesAsync(
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var roles = user.Claims
            .Where(x => x.Type == ClaimTypes.Role || x.Type == "role")
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        if (access.Any(item => item.IsManager)) roles.Add(AuthRoles.ClubManager);
        if (access.Any(item => item.IsTreasurer)) roles.Add(AuthRoles.Treasurer);
        if (access.Any(item => item.IsApprovedMember || item.IsManager)) roles.Add(AuthRoles.ClubMember);

        return roles.ToArray();
    }

    public static string? NormalizeRole(string? role)
    {
        return string.IsNullOrWhiteSpace(role) ? null : role.Trim();
    }

    public static (string Title, string Message) NormalizeLegacyNotification(Notification notification)
    {
        var eventType = notification.EventType.ToLowerInvariant();
        var message = notification.Message;

        return eventType switch
        {
            "club.created" => (
                "Câu lạc bộ mới được tạo",
                TranslateLegacyMessage(message,
                    (@"^(.*?) \((.*?)\) has been added to the system\.$", "$1 ($2) đã được thêm vào hệ thống."))),
            "user.registered" => (
                "Chào mừng đến với FPTU Club Hub",
                TranslateLegacyMessage(message,
                    (@"^Hello (.*?)\. Your member account is ready\.$", "Xin chào $1. Tài khoản thành viên của bạn đã sẵn sàng."))),
            "activity.created" => (
                "Hoạt động mới",
                TranslateLegacyMessage(message,
                    (@"^(.*?) has scheduled the activity (.*?)\.$", "$1 đã lên lịch hoạt động $2."))),
            "report.created" => (
                "Bản nháp báo cáo mới",
                TranslateLegacyMessage(message,
                    (@"^A new report for (.*?) was created for period (.*?)\.$", "Một báo cáo mới của $1 đã được tạo cho kỳ $2."))),
            "report.submitted" => (
                TranslateReportSubmittedTitle(notification.Title),
                TranslateLegacyMessage(message,
                    (@"^Create the budget report for (.*?) - (.*?)\.$", "Vui lòng lập báo cáo ngân sách cho $1 - $2."),
                    (@"^Review the combined event and budget package for (.*?) - (.*?)\.$", "Vui lòng xét duyệt hồ sơ sự kiện và ngân sách của $1 - $2."),
                    (@"^(.*?) submitted the combined event and budget package for (.*?)\.$", "$1 đã nộp hồ sơ sự kiện và ngân sách cho kỳ $2."),
                    (@"^(.*?) submitted the report for period (.*?)\.$", "$1 đã nộp báo cáo kỳ $2."))),
            "report.approved" => (
                "Báo cáo đã được phê duyệt",
                TranslateLegacyMessage(message,
                    (@"^The (.*?) report for (.*?) has been approved\.$", "Báo cáo kỳ $1 của $2 đã được phê duyệt."))),
            "report.rejected" => (
                "Báo cáo cần chỉnh sửa",
                TranslateLegacyMessage(message,
                    (@"^The (.*?) report for (.*?) was rejected: (.*)$", "Báo cáo kỳ $1 của $2 bị từ chối: $3"))),
            "kpi.calculated" => (
                "Điểm KPI đã được cập nhật",
                TranslateLegacyMessage(message,
                    (@"^The KPI score for (.*?) in period (.*?) is (.*?)\.$", "Điểm KPI của $1 trong kỳ $2 là $3 điểm."))),
            "budget.proposal.submitted" => (
                notification.Title.Contains("final", StringComparison.OrdinalIgnoreCase)
                    ? "Đề xuất ngân sách đang chờ phê duyệt cuối"
                    : "Đề xuất ngân sách đang chờ chủ nhiệm xét duyệt",
                TranslateLegacyMessage(message,
                    (@"^(.*?) submitted a budget proposal for (.*?) VND\.$", "$1 đã nộp đề xuất ngân sách $2 VNĐ."),
                    (@"^(.*?) requested a budget of (.*?) VND\.$", "$1 đề xuất ngân sách $2 VNĐ."))),
            "budget.approved" => (
                "Ngân sách đã được phê duyệt",
                TranslateLegacyMessage(message,
                    (@"^The budget for (.*?) was approved for (.*?) VND\.$", "Ngân sách của $1 đã được phê duyệt với số tiền $2 VNĐ."))),
            "settlement.overdue" => (
                "Quyết toán quá hạn",
                TranslateLegacyMessage(message,
                    (@"^(.*?) has an overdue settlement for proposal #(.*?)\.$", "$1 có quyết toán quá hạn cho đề xuất #$2."))),
            "export.completed" => (
                "Tệp xuất đã sẵn sàng",
                TranslateLegacyMessage(message,
                    (@"^The (.*?) export is ready: (.*?)\.$", "Tệp xuất $1 đã sẵn sàng: $2."))),
            "report.deadline.reminder" => (
                "Nhắc hạn nộp báo cáo",
                TranslateLegacyMessage(message,
                    (@"^Some clubs have not yet submitted their reports for period (.*?)\.$", "Một số câu lạc bộ chưa nộp báo cáo cho kỳ $1."))),
            _ => (
                notification.Title == "System event" ? "Sự kiện hệ thống" : notification.Title,
                message == "The system recorded a new event."
                    ? "Hệ thống vừa ghi nhận một sự kiện mới."
                    : message)
        };
    }

    public static string TranslateReportSubmittedTitle(string title)
    {
        if (title.Equals("Báo cáo chờ phê duyệt", StringComparison.OrdinalIgnoreCase))
        {
            return "Báo cáo đang chờ phê duyệt cuối";
        }

        if (title.Contains("budget", StringComparison.OrdinalIgnoreCase)
            || title.Contains("ngân sách", StringComparison.OrdinalIgnoreCase))
        {
            return "Báo cáo sự kiện sắp tới cần lập ngân sách";
        }

        if (title.Contains("owner", StringComparison.OrdinalIgnoreCase)
            || title.Contains("chủ nhiệm", StringComparison.OrdinalIgnoreCase))
        {
            return "Hồ sơ sự kiện đang chờ chủ nhiệm xét duyệt";
        }

        if (title.Contains("final", StringComparison.OrdinalIgnoreCase)
            || title.Contains("phê duyệt cuối", StringComparison.OrdinalIgnoreCase))
        {
            return "Báo cáo đang chờ phê duyệt cuối";
        }

        return "Báo cáo đang chờ chủ nhiệm xét duyệt";
    }

    public static string TranslateLegacyMessage(
        string message,
        params (string Pattern, string Replacement)[] translations)
    {
        foreach (var (pattern, replacement) in translations)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
            {
                return Regex.Replace(message, pattern, replacement, RegexOptions.IgnoreCase);
            }
        }

        return message;
    }
}
