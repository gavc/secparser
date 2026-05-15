using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SecParser.Core.Models;

namespace SecParser.Core.Exporters
{
    public class PdfExporter
    {
        public async Task ExportAsync(IEnumerable<SecurityEventRecord> records, string filePath)
        {
            var recordList = records.ToList();
            const int maxDetailRows = 5000;
            var detailRecords = recordList.Take(maxDetailRows).ToList();
            var users = string.Join(", ", recordList.Where(r => !string.IsNullOrEmpty(r.Username)).Select(r => r.Username).Distinct());
            if (string.IsNullOrEmpty(users)) users = "None";

            var orderedRecords = recordList.Where(r => r.TimeCreated.HasValue).OrderBy(r => r.TimeCreated).ToList();
            var dateRange = orderedRecords.Any() ? $"{orderedRecords.First().TimeCreated:yyyy-MM-dd HH:mm:ss} to {orderedRecords.Last().TimeCreated:yyyy-MM-dd HH:mm:ss}" : "N/A";

            var lastLogon = orderedRecords.LastOrDefault(r => r.EventId == 4624)?.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None";
            var lastLogoff = orderedRecords.LastOrDefault(r => r.EventId == 4634 || r.EventId == 4647)?.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None";
            var warningCount = recordList.Count(r => r.HasParseWarning);

            await Task.Run(() =>
            {
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial));

                        page.Header().Text("SecParser Export")
                            .SemiBold().FontSize(16).FontColor(Colors.Blue.Darken2);

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                        {
                            // High Level Summary
                            col.Item().PaddingBottom(10).Background(Colors.Grey.Lighten4).Padding(5).Column(summary =>
                            {
                                summary.Item().Text("High Level Summary").Bold().FontSize(12);
                                summary.Item().Text($"User(s) Filtered: {users}");
                                summary.Item().Text($"Date Range: {dateRange}");
                                summary.Item().Text($"Most Recent Logon: {lastLogon}");
                                summary.Item().Text($"Most Recent Logoff: {lastLogoff}");
                                summary.Item().Text($"Total Records Extracted: {recordList.Count}");
                                summary.Item().Text($"Parse Warnings: {warningCount}");
                                if (recordList.Count > detailRecords.Count)
                                {
                                    summary.Item().Text($"Detail Rows Included: first {detailRecords.Count} records");
                                }
                            });

                            col.Item().Table(table =>
                            {
                                // Define columns
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Time
                                    columns.RelativeColumn(4); // Event (ID + Desc)
                                    columns.RelativeColumn(3); // Target
                                    columns.RelativeColumn(3); // Actor
                                    columns.RelativeColumn(3); // IP
                                    columns.RelativeColumn(5); // Logon Type (ID + Desc)
                                });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Text("Time Created").Bold();
                                header.Cell().Text("Event").Bold();
                                header.Cell().Text("Target").Bold();
                                header.Cell().Text("Actor").Bold();
                                header.Cell().Text("IP Address").Bold();
                                header.Cell().Text("Logon Type").Bold();
                                
                                header.Cell().ColumnSpan(6).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                            });

                            // Content
                            foreach (var record in detailRecords)
                            {
                                var eventStr = record.EventId.ToString();
                                if (!string.IsNullOrEmpty(record.EventDescription))
                                    eventStr += $" - {record.EventDescription}";

                                var logonStr = record.LogonType ?? "";
                                if (!string.IsNullOrEmpty(record.LogonTypeDescription))
                                    logonStr += string.IsNullOrEmpty(logonStr) ? record.LogonTypeDescription : $" - {record.LogonTypeDescription}";

                                table.Cell().Text(record.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
                                table.Cell().Text(eventStr);
                                table.Cell().Text(record.TargetUserName ?? record.Username ?? "");
                                table.Cell().Text(record.SubjectUserName ?? "");
                                table.Cell().Text(record.IpAddress ?? "");
                                table.Cell().Text(logonStr);
                            }
                        }); // close Table
                        }); // close Column

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                })
                .GeneratePdf(filePath);
            });
        }
    }
}
