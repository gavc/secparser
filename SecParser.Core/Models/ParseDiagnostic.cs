using System;

namespace SecParser.Core.Models;

public class ParseDiagnostic
{
    public DateTime? TimeCreated { get; set; }
    public long? RecordId { get; set; }
    public int EventId { get; set; }
    public string? Computer { get; set; }
    public string? Message { get; set; }
}
