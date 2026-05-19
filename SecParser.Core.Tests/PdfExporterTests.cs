using SecParser.Core.Exporters;
using SecParser.Core.Models;

namespace SecParser.Core.Tests;

public class PdfExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesPdfFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"secparser-{Guid.NewGuid():N}.pdf");

        try
        {
            var records = new[]
            {
                new SecurityEventRecord
                {
                    TimeCreated = new DateTime(2026, 5, 19, 10, 30, 0),
                    EventId = 4624,
                    EventDescription = "Logon",
                    Username = "alice",
                    TargetUserName = "alice",
                    SubjectUserName = "SYSTEM",
                    IpAddress = "10.1.2.3",
                    LogonType = "10",
                    LogonTypeDescription = "Remote Interactive (RDP)"
                }
            };

            await new PdfExporter().ExportAsync(records, filePath);

            Assert.True(File.Exists(filePath));
            Assert.True(new FileInfo(filePath).Length > 0);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(filePath), 0, 4));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_WithManyRecords_WritesMultiPagePdf()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"secparser-{Guid.NewGuid():N}.pdf");

        try
        {
            var records = Enumerable.Range(0, 250)
                .Select(index => new SecurityEventRecord
                {
                    TimeCreated = new DateTime(2026, 5, 19, 10, 30, 0).AddSeconds(index),
                    EventId = 5379,
                    EventDescription = "Credential Manager Credentials Were Read",
                    Username = "gavco",
                    TargetUserName = "gavco",
                    SubjectUserName = "gavco"
                })
                .ToArray();

            await new PdfExporter().ExportAsync(records, filePath);

            using var document = PdfSharp.Pdf.IO.PdfReader.Open(filePath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            Assert.True(document.PageCount > 1);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
