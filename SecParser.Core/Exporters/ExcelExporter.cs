using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SecParser.Core.Abstractions;
using SecParser.Core.Diagnostics;
using SecParser.Core.Models;
using ClosedXML.Excel;
using System.Threading.Tasks;

namespace SecParser.Core.Exporters;

public class ExcelExporter : IRecordExporter
{
        private const string LogCategory = nameof(ExcelExporter);

        private readonly IAppLogger _logger;

        public ExcelExporter() : this(null) { }

        public ExcelExporter(IAppLogger? logger)
        {
            _logger = logger ?? NullAppLogger.Instance;
        }

        public string DisplayName => "Excel Workbook";
        public string FilterMask => "Excel Workbook (*.xlsx)|*.xlsx";
        public string DefaultExtension => ".xlsx";

        public async Task ExportAsync(IEnumerable<SecurityEventRecord> records, string filePath)
        {
            PathValidation.EnsureValidExportPath(filePath, DefaultExtension);

            await Task.Run(() =>
            {
                var recordList = records.ToList();
                _logger.Information(LogCategory, $"Begin Excel export of {recordList.Count} record(s) to '{filePath}'.");
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Security Events");

                // Headers
                worksheet.Cell(1, 1).Value = "Time Created";
                worksheet.Cell(1, 2).Value = "Event ID";
                worksheet.Cell(1, 3).Value = "Event Description";
                worksheet.Cell(1, 4).Value = "Computer";
                worksheet.Cell(1, 5).Value = "Username";
                worksheet.Cell(1, 6).Value = "Target User";
                worksheet.Cell(1, 7).Value = "Target Domain";
                worksheet.Cell(1, 8).Value = "Subject User";
                worksheet.Cell(1, 9).Value = "Subject Domain";
                worksheet.Cell(1, 10).Value = "Target Logon ID";
                worksheet.Cell(1, 11).Value = "Subject Logon ID";
                worksheet.Cell(1, 12).Value = "IP Address";
                worksheet.Cell(1, 13).Value = "IP Port";
                worksheet.Cell(1, 14).Value = "Workstation";
                worksheet.Cell(1, 15).Value = "Process Name";
                worksheet.Cell(1, 16).Value = "Process ID";
                worksheet.Cell(1, 17).Value = "Category";
                worksheet.Cell(1, 18).Value = "Logon Type";
                worksheet.Cell(1, 19).Value = "Logon Type Description";
                worksheet.Cell(1, 20).Value = "Command Line";
                worksheet.Cell(1, 21).Value = "Status";
                worksheet.Cell(1, 22).Value = "SubStatus";
                worksheet.Cell(1, 23).Value = "Failure Reason";
                worksheet.Cell(1, 24).Value = "Auth Package";
                worksheet.Cell(1, 25).Value = "Package Name";
                worksheet.Cell(1, 26).Value = "Service Name";
                worksheet.Cell(1, 27).Value = "Ticket Encryption Type";
                worksheet.Cell(1, 28).Value = "Share Name";
                worksheet.Cell(1, 29).Value = "Relative Target";
                worksheet.Cell(1, 30).Value = "Object Name";
                worksheet.Cell(1, 31).Value = "Object Type";
                worksheet.Cell(1, 32).Value = "Access Mask";
                worksheet.Cell(1, 33).Value = "Access List";
                worksheet.Cell(1, 34).Value = "Member Name";
                worksheet.Cell(1, 35).Value = "Member SID";
                worksheet.Cell(1, 36).Value = "Group Name";
                worksheet.Cell(1, 37).Value = "Group Domain";
                worksheet.Cell(1, 38).Value = "Parse Warning";

                var headerRow = worksheet.Range("A1:AL1");
                headerRow.Style.Font.Bold = true;
                headerRow.SetAutoFilter();

                int row = 2;
                foreach (var record in recordList)
                {
                    worksheet.Cell(row, 1).Value = record.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    worksheet.Cell(row, 2).Value = record.EventId;
                    worksheet.Cell(row, 3).Value = record.EventDescription;
                    worksheet.Cell(row, 4).Value = record.Computer;
                    worksheet.Cell(row, 5).Value = record.Username;
                    worksheet.Cell(row, 6).Value = record.TargetUserName;
                    worksheet.Cell(row, 7).Value = record.TargetDomainName;
                    worksheet.Cell(row, 8).Value = record.SubjectUserName;
                    worksheet.Cell(row, 9).Value = record.SubjectDomainName;
                    worksheet.Cell(row, 10).Value = record.TargetLogonId;
                    worksheet.Cell(row, 11).Value = record.SubjectLogonId;
                    worksheet.Cell(row, 12).Value = record.IpAddress;
                    worksheet.Cell(row, 13).Value = record.IpPort;
                    worksheet.Cell(row, 14).Value = record.WorkstationName;
                    worksheet.Cell(row, 15).Value = record.ProcessName;
                    worksheet.Cell(row, 16).Value = record.ProcessId;
                    worksheet.Cell(row, 17).Value = record.TaskCategory;
                    worksheet.Cell(row, 18).Value = record.LogonType;
                    worksheet.Cell(row, 19).Value = record.LogonTypeDescription;
                    worksheet.Cell(row, 20).Value = record.CommandLine;
                    worksheet.Cell(row, 21).Value = record.Status;
                    worksheet.Cell(row, 22).Value = record.SubStatus;
                    worksheet.Cell(row, 23).Value = record.FailureReason;
                    worksheet.Cell(row, 24).Value = record.AuthenticationPackageName;
                    worksheet.Cell(row, 25).Value = record.PackageName;
                    worksheet.Cell(row, 26).Value = record.ServiceName;
                    worksheet.Cell(row, 27).Value = record.TicketEncryptionType;
                    worksheet.Cell(row, 28).Value = record.ShareName;
                    worksheet.Cell(row, 29).Value = record.RelativeTargetName;
                    worksheet.Cell(row, 30).Value = record.ObjectName;
                    worksheet.Cell(row, 31).Value = record.ObjectType;
                    worksheet.Cell(row, 32).Value = record.AccessMask;
                    worksheet.Cell(row, 33).Value = record.AccessList;
                    worksheet.Cell(row, 34).Value = record.MemberName;
                    worksheet.Cell(row, 35).Value = record.MemberSid;
                    worksheet.Cell(row, 36).Value = record.GroupName;
                    worksheet.Cell(row, 37).Value = record.GroupDomain;
                    worksheet.Cell(row, 38).Value = record.ParseWarning;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                AddDiagnosticsWorksheet(workbook, recordList.Where(r => r.HasParseWarning));
                workbook.SaveAs(filePath);
            }).ConfigureAwait(false);
            _logger.Information(LogCategory, $"Completed Excel export to '{filePath}'.");
        }

        private static void AddDiagnosticsWorksheet(XLWorkbook workbook, IEnumerable<SecurityEventRecord> warningRecords)
        {
            var diagnostics = warningRecords.ToList();
            if (diagnostics.Count == 0)
                return;

            var worksheet = workbook.Worksheets.Add("Parse Diagnostics");
            worksheet.Cell(1, 1).Value = "Time Created";
            worksheet.Cell(1, 2).Value = "Record ID";
            worksheet.Cell(1, 3).Value = "Event ID";
            worksheet.Cell(1, 4).Value = "Computer";
            worksheet.Cell(1, 5).Value = "Warning";

            var headerRow = worksheet.Range("A1:E1");
            headerRow.Style.Font.Bold = true;
            headerRow.SetAutoFilter();

            var row = 2;
            foreach (var record in diagnostics)
            {
                worksheet.Cell(row, 1).Value = record.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                worksheet.Cell(row, 2).Value = record.RecordId;
                worksheet.Cell(row, 3).Value = record.EventId;
                worksheet.Cell(row, 4).Value = record.Computer;
                worksheet.Cell(row, 5).Value = record.ParseWarning;
                row++;
            }

            worksheet.Columns().AdjustToContents();
        }
    }
