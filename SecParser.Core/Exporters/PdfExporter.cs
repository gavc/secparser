using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
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
            var exportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            await Task.Run(() =>
            {
                var document = CreateDocument(
                    detailRecords,
                    users,
                    dateRange,
                    lastLogon,
                    lastLogoff,
                    recordList.Count,
                    warningCount,
                    exportTimestamp);

                var renderer = new PdfDocumentRenderer
                {
                    Document = document
                };

                renderer.RenderDocument();
                renderer.PdfDocument.Save(filePath);
            });
        }

        private static Document CreateDocument(
            IReadOnlyCollection<SecurityEventRecord> detailRecords,
            string users,
            string dateRange,
            string lastLogon,
            string lastLogoff,
            int totalRecordCount,
            int warningCount,
            string exportTimestamp)
        {
            var document = new Document();
            document.Info.Title = "SecParser Export";

            var normalStyle = document.Styles["Normal"]!;
            normalStyle.Font.Name = "Arial";
            normalStyle.Font.Size = 8;

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = Orientation.Landscape;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.5);

            AddHeader(section);
            AddFooter(section, exportTimestamp);
            AddSummary(section, users, dateRange, lastLogon, lastLogoff, totalRecordCount, warningCount, detailRecords.Count);
            AddDetailsTable(section, detailRecords);

            return document;
        }

        private static void AddHeader(Section section)
        {
            var paragraph = section.Headers.Primary.AddParagraph("SecParser Export");
            paragraph.Format.Font.Size = 16;
            paragraph.Format.Font.Bold = true;
            paragraph.Format.Font.Color = Colors.DarkBlue;
            paragraph.Format.SpaceAfter = Unit.FromCentimeter(0.4);
        }

        private static void AddFooter(Section section, string exportTimestamp)
        {
            var footer = section.Footers.Primary.AddTable();
            footer.Borders.Visible = false;
            footer.AddColumn(Unit.FromCentimeter(9.2));
            footer.AddColumn(Unit.FromCentimeter(9.3));
            footer.AddColumn(Unit.FromCentimeter(9.2));

            var row = footer.AddRow();

            row.Cells[0].AddParagraph($"Exported: {exportTimestamp}");
            row.Cells[0].Format.Alignment = ParagraphAlignment.Left;

            var pageNumber = row.Cells[1].AddParagraph();
            pageNumber.Format.Alignment = ParagraphAlignment.Center;
            pageNumber.AddText("Page ");
            pageNumber.AddPageField();
            pageNumber.AddText(" of ");
            pageNumber.AddNumPagesField();
        }

        private static void AddSummary(
            Section section,
            string users,
            string dateRange,
            string lastLogon,
            string lastLogoff,
            int totalRecordCount,
            int warningCount,
            int detailRecordCount)
        {
            var table = section.AddTable();
            table.Borders.Visible = false;
            table.Shading.Color = Colors.LightGray;
            table.AddColumn(Unit.FromCentimeter(27.7));

            var row = table.AddRow();
            row.Cells[0].Format.SpaceBefore = Unit.FromCentimeter(0.15);
            row.Cells[0].Format.SpaceAfter = Unit.FromCentimeter(0.15);
            row.Cells[0].Format.LeftIndent = Unit.FromCentimeter(0.15);
            row.Cells[0].Format.RightIndent = Unit.FromCentimeter(0.15);

            AddSummaryLine(row.Cells[0], "High Level Summary", isHeading: true);
            AddSummaryLine(row.Cells[0], $"User(s) Filtered: {users}");
            AddSummaryLine(row.Cells[0], $"Date Range: {dateRange}");
            AddSummaryLine(row.Cells[0], $"Most Recent Logon: {lastLogon}");
            AddSummaryLine(row.Cells[0], $"Most Recent Logoff: {lastLogoff}");
            AddSummaryLine(row.Cells[0], $"Total Records Extracted: {totalRecordCount}");
            AddSummaryLine(row.Cells[0], $"Parse Warnings: {warningCount}");

            if (totalRecordCount > detailRecordCount)
            {
                AddSummaryLine(row.Cells[0], $"Detail Rows Included: first {detailRecordCount} records");
            }

            section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.25);
        }

        private static void AddSummaryLine(Cell cell, string text, bool isHeading = false)
        {
            var paragraph = cell.AddParagraph(text);
            paragraph.Format.Font.Bold = isHeading;
            paragraph.Format.Font.Size = isHeading ? 12 : 8;
            paragraph.Format.SpaceAfter = Unit.FromPoint(2);
        }

        private static void AddDetailsTable(Section section, IEnumerable<SecurityEventRecord> detailRecords)
        {
            var table = section.AddTable();
            table.Borders.Width = 0.25;
            table.Rows.LeftIndent = 0;

            table.AddColumn(Unit.FromCentimeter(4.0));
            table.AddColumn(Unit.FromCentimeter(6.0));
            table.AddColumn(Unit.FromCentimeter(4.0));
            table.AddColumn(Unit.FromCentimeter(4.0));
            table.AddColumn(Unit.FromCentimeter(3.5));
            table.AddColumn(Unit.FromCentimeter(6.2));

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Shading.Color = Colors.LightGray;
            AddCell(header, 0, "Time Created");
            AddCell(header, 1, "Event");
            AddCell(header, 2, "Target");
            AddCell(header, 3, "Actor");
            AddCell(header, 4, "IP Address");
            AddCell(header, 5, "Logon Type");

            foreach (var record in detailRecords)
            {
                var row = table.AddRow();
                row.VerticalAlignment = VerticalAlignment.Top;

                var eventText = record.EventId.ToString();
                if (!string.IsNullOrEmpty(record.EventDescription))
                {
                    eventText += $" - {record.EventDescription}";
                }

                var logonText = record.LogonType ?? string.Empty;
                if (!string.IsNullOrEmpty(record.LogonTypeDescription))
                {
                    logonText += string.IsNullOrEmpty(logonText)
                        ? record.LogonTypeDescription
                        : $" - {record.LogonTypeDescription}";
                }

                AddCell(row, 0, record.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                AddCell(row, 1, eventText);
                AddCell(row, 2, record.TargetUserName ?? record.Username ?? string.Empty);
                AddCell(row, 3, record.SubjectUserName ?? string.Empty);
                AddCell(row, 4, record.IpAddress ?? string.Empty);
                AddCell(row, 5, logonText);
            }
        }

        private static void AddCell(Row row, int columnIndex, string text)
        {
            var cell = row.Cells[columnIndex];
            cell.Format.Font.Size = 7.5;
            cell.Format.SpaceBefore = Unit.FromPoint(1);
            cell.Format.SpaceAfter = Unit.FromPoint(1);
            cell.Format.LeftIndent = Unit.FromPoint(2);
            cell.Format.RightIndent = Unit.FromPoint(2);
            cell.AddParagraph(text);
        }
    }
}
