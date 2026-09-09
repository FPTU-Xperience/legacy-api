using System.Security.Cryptography;
using ClosedXML.Excel;
using ExportService.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ExportService.Services;

public sealed record GeneratedExportFile(
    string FileName,
    string ContentType,
    string FilePath,
    long SizeBytes,
    string Checksum);

public sealed class ExportFileGenerator(IConfiguration configuration)
{
    public static GeneratedExportFile Generate(ExportRequest request, string storagePath)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Exports:StoragePath"] = storagePath })
            .Build();
        return new ExportFileGenerator(config).Generate(request);
    }

    public GeneratedExportFile Generate(ExportRequest request)
    {
        var storagePath = Path.GetFullPath(configuration["Exports:StoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "exports"));
        Directory.CreateDirectory(storagePath);

        var extension = request.ExportType switch
        {
            ExportTypes.Pdf => "pdf",
            ExportTypes.Docx => "docx",
            _ => "xlsx"
        };
        var safeScope = string.Concat(request.Scope
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeScope))
        {
            safeScope = "report";
        }

        var fileName = $"clubreport-{safeScope}-{request.Id}.{extension}";
        var filePath = Path.Combine(storagePath, fileName);
        var contentType = request.ExportType switch
        {
            ExportTypes.Pdf => "application/pdf",
            ExportTypes.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        if (request.ExportType == ExportTypes.Pdf)
        {
            GeneratePdf(request, filePath);
        }
        else if (request.ExportType == ExportTypes.Docx)
        {
            GenerateDocx(request, filePath);
        }
        else
        {
            GenerateExcel(request, filePath);
        }

        var fileInfo = new FileInfo(filePath);
        using var stream = fileInfo.OpenRead();
        var checksum = Convert.ToHexString(SHA256.HashData(stream));
        return new GeneratedExportFile(fileName, contentType, filePath, fileInfo.Length, checksum);
    }

    private static void GeneratePdf(ExportRequest request, string filePath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        
        ExportService.Contracts.ReportExportSnapshot? snapshot = null;
        if (!string.IsNullOrWhiteSpace(request.SnapshotJson))
        {
            try
            {
                snapshot = System.Text.Json.JsonSerializer.Deserialize<ExportService.Contracts.ReportExportSnapshot>(
                    request.SnapshotJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        QuestPDF.Fluent.Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(11));
                
                if (snapshot != null)
                {
                    page.Header().Text($"Club Activity Report: {snapshot.ClubName}").SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);
                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Spacing(15);
                        column.Item().Text($"Period: {snapshot.Period}").SemiBold().FontSize(14);
                        column.Item().Text($"Status: {snapshot.Status}");
                        if (snapshot.SubmittedAtUtc.HasValue)
                        {
                            column.Item().Text($"Submitted at: {snapshot.SubmittedAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                        
                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        
                        if (!string.IsNullOrWhiteSpace(snapshot.ExecutiveSummary))
                        {
                            column.Item().Text("Executive Summary").SemiBold().FontSize(14);
                            column.Item().Text(snapshot.ExecutiveSummary);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(snapshot.Achievements))
                        {
                            column.Item().Text("Achievements").SemiBold().FontSize(14);
                            column.Item().Text(snapshot.Achievements);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(snapshot.Challenges))
                        {
                            column.Item().Text("Challenges").SemiBold().FontSize(14);
                            column.Item().Text(snapshot.Challenges);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(snapshot.Recommendations))
                        {
                            column.Item().Text("Recommendations").SemiBold().FontSize(14);
                            column.Item().Text(snapshot.Recommendations);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(snapshot.NextPeriodPlan))
                        {
                            column.Item().Text("Next Period Plan").SemiBold().FontSize(14);
                            column.Item().Text(snapshot.NextPeriodPlan);
                        }
                        
                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        
                        column.Item().Text("Activities Summary").SemiBold().FontSize(14);
                        column.Item().Text($"Total Activities: {snapshot.TotalActivities}");
                        column.Item().Text($"Total Participants: {snapshot.TotalParticipants}");
                        column.Item().Text($"Total Budget Spent: {snapshot.TotalBudgetSpent:C}");
                        
                        if (snapshot.Details != null && snapshot.Details.Count > 0)
                        {
                            column.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });
                                
                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).Padding(2).Text("Activity Name").SemiBold();
                                    header.Cell().BorderBottom(1).Padding(2).Text("Date").SemiBold();
                                    header.Cell().BorderBottom(1).Padding(2).Text("Participants").SemiBold();
                                    header.Cell().BorderBottom(1).Padding(2).Text("Budget").SemiBold();
                                });
                                
                                foreach (var detail in snapshot.Details)
                                {
                                    table.Cell().Padding(2).Text(detail.ActivityName);
                                    table.Cell().Padding(2).Text(detail.ActivityDate?.ToString("yyyy-MM-dd") ?? "N/A");
                                    table.Cell().Padding(2).Text(detail.ParticipantCount.ToString());
                                    table.Cell().Padding(2).Text(detail.BudgetSpent?.ToString("C") ?? "N/A");
                                }
                            });
                        }
                        
                        if (snapshot.Feedback != null && snapshot.Feedback.Count > 0)
                        {
                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            column.Item().Text("Feedback").SemiBold().FontSize(14);
                            foreach (var f in snapshot.Feedback)
                            {
                                column.Item().PaddingLeft(10).Column(fCol =>
                                {
                                    fCol.Item().Text($"{f.ReviewerName} ({f.CreatedAtUtc:yyyy-MM-dd HH:mm})").SemiBold();
                                    fCol.Item().Text(f.Message).Italic();
                                });
                            }
                        }
                    });
                }
                else
                {
                    // Fallback to metadata
                    page.Header().Text("ClubReportHub - Data Export Report").SemiBold().FontSize(18);
                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Text($"Request ID: {request.Id}");
                        column.Item().Text($"File type: {request.ExportType}");
                        column.Item().Text($"Scope: {request.Scope}");
                        column.Item().Text($"Reporting period: {request.Period ?? "All"}");
                        column.Item().Text($"Club: {(request.ClubId?.ToString() ?? "All")}");
                        column.Item().Text($"Requested by: {request.RequestedByName}");
                        column.Item().Text($"Created at: {request.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                    });
                }
                
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void GenerateExcel(ExportRequest request, string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Export");
        
        ExportService.Contracts.ReportExportSnapshot? snapshot = null;
        if (!string.IsNullOrWhiteSpace(request.SnapshotJson))
        {
            try { snapshot = System.Text.Json.JsonSerializer.Deserialize<ExportService.Contracts.ReportExportSnapshot>(request.SnapshotJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }
        }

        if (snapshot != null)
        {
            worksheet.Cell("A1").Value = $"Club Activity Report: {snapshot.ClubName}";
            worksheet.Range("A1:D1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
            
            worksheet.Cell("A3").Value = "Period:";
            worksheet.Cell("A3").Style.Font.SetBold();
            worksheet.Cell("B3").Value = snapshot.Period;
            
            worksheet.Cell("A4").Value = "Status:";
            worksheet.Cell("A4").Style.Font.SetBold();
            worksheet.Cell("B4").Value = snapshot.Status;
            
            int row = 6;
            
            if (!string.IsNullOrWhiteSpace(snapshot.ExecutiveSummary))
            {
                worksheet.Cell(row, 1).Value = "Executive Summary";
                worksheet.Cell(row, 1).Style.Font.SetBold();
                worksheet.Cell(row + 1, 1).Value = snapshot.ExecutiveSummary;
                row += 3;
            }
            
            worksheet.Cell(row, 1).Value = "Activities Summary";
            worksheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(14);
            row++;
            
            worksheet.Cell(row, 1).Value = "Total Activities:";
            worksheet.Cell(row, 2).Value = snapshot.TotalActivities;
            row++;
            worksheet.Cell(row, 1).Value = "Total Participants:";
            worksheet.Cell(row, 2).Value = snapshot.TotalParticipants;
            row++;
            worksheet.Cell(row, 1).Value = "Total Budget Spent:";
            worksheet.Cell(row, 2).Value = snapshot.TotalBudgetSpent;
            row += 2;
            
            if (snapshot.Details != null && snapshot.Details.Count > 0)
            {
                worksheet.Cell(row, 1).Value = "Activity Name";
                worksheet.Cell(row, 2).Value = "Date";
                worksheet.Cell(row, 3).Value = "Participants";
                worksheet.Cell(row, 4).Value = "Budget";
                worksheet.Range($"A{row}:D{row}").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
                row++;
                
                foreach (var detail in snapshot.Details)
                {
                    worksheet.Cell(row, 1).Value = detail.ActivityName;
                    worksheet.Cell(row, 2).Value = detail.ActivityDate?.ToString("yyyy-MM-dd") ?? "N/A";
                    worksheet.Cell(row, 3).Value = detail.ParticipantCount;
                    worksheet.Cell(row, 4).Value = detail.BudgetSpent ?? 0m;
                    row++;
                }
            }
        }
        else
        {
            worksheet.Cell("A1").Value = "ClubReportHub - Data Export Report";
            worksheet.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16);

            var rows = new (string Label, object Value)[]
            {
                ("Request ID", request.Id),
                ("File type", request.ExportType),
                ("Scope", request.Scope),
                ("Reporting period", request.Period ?? "All"),
                ("Club", request.ClubId?.ToString() ?? "All"),
                ("Requested by", request.RequestedByName),
                ("Created at", request.CreatedAtUtc.UtcDateTime)
            };

            for (var index = 0; index < rows.Length; index++)
            {
                var r = index + 3;
                worksheet.Cell(r, 1).Value = rows[index].Label;
                worksheet.Cell(r, 1).Style.Font.Bold = true;
                worksheet.Cell(r, 2).Value = XLCellValue.FromObject(rows[index].Value);
            }
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
    
    private static void GenerateDocx(ExportRequest request, string filePath)
    {
        using var wordDocument = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
        var body = mainPart.Document.AppendChild(new Body());
        
        ExportService.Contracts.ReportExportSnapshot? snapshot = null;
        if (!string.IsNullOrWhiteSpace(request.SnapshotJson))
        {
            try { snapshot = System.Text.Json.JsonSerializer.Deserialize<ExportService.Contracts.ReportExportSnapshot>(request.SnapshotJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }
        }

        if (snapshot != null)
        {
            body.AppendChild(new Paragraph(new Run(new Text($"Club Activity Report: {snapshot.ClubName}")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "36" }) }));
            body.AppendChild(new Paragraph(new Run(new Text($"Period: {snapshot.Period}"))));
            body.AppendChild(new Paragraph(new Run(new Text($"Status: {snapshot.Status}"))));
            
            if (!string.IsNullOrWhiteSpace(snapshot.ExecutiveSummary))
            {
                body.AppendChild(new Paragraph(new Run(new Text("Executive Summary")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" }) }));
                body.AppendChild(new Paragraph(new Run(new Text(snapshot.ExecutiveSummary))));
            }
            
            body.AppendChild(new Paragraph(new Run(new Text("Activities Summary")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" }) }));
            body.AppendChild(new Paragraph(new Run(new Text($"Total Activities: {snapshot.TotalActivities}"))));
            body.AppendChild(new Paragraph(new Run(new Text($"Total Participants: {snapshot.TotalParticipants}"))));
            body.AppendChild(new Paragraph(new Run(new Text($"Total Budget Spent: {snapshot.TotalBudgetSpent:C}"))));

            if (snapshot.Details != null && snapshot.Details.Count > 0)
            {
                body.AppendChild(new Paragraph(new Run(new Text("Detailed Activities")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" }) }));
                foreach (var detail in snapshot.Details)
                {
                    body.AppendChild(new Paragraph(new Run(new Text($"Activity: {detail.ActivityName}")) { RunProperties = new RunProperties(new Bold()) }));
                    body.AppendChild(new Paragraph(new Run(new Text($"  - Date: {detail.ActivityDate?.ToString("yyyy-MM-dd") ?? "N/A"}"))));
                    body.AppendChild(new Paragraph(new Run(new Text($"  - Participants: {detail.ParticipantCount}"))));
                    body.AppendChild(new Paragraph(new Run(new Text($"  - Budget Spent: {detail.BudgetSpent:C}"))));
                }
            }

            if (snapshot.Feedback != null && snapshot.Feedback.Count > 0)
            {
                body.AppendChild(new Paragraph(new Run(new Text("Reviewer Feedback")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" }) }));
                foreach (var fb in snapshot.Feedback)
                {
                    body.AppendChild(new Paragraph(new Run(new Text($"Reviewer: {fb.ReviewerName}")) { RunProperties = new RunProperties(new Bold()) }));
                    if (!string.IsNullOrWhiteSpace(fb.Message))
                    {
                        body.AppendChild(new Paragraph(new Run(new Text($"Message: {fb.Message}"))));
                    }
                }
            }
        }
        else
        {
            body.AppendChild(new Paragraph(new Run(new Text("ClubReportHub - Data Export Report")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "32" }) }));
            body.AppendChild(new Paragraph(new Run(new Text($"Request ID: {request.Id}"))));
            body.AppendChild(new Paragraph(new Run(new Text($"Scope: {request.Scope}"))));
        }
        
        mainPart.Document.Save();
    }
}
