using System;

namespace SecParser.Core.Models
{
    public class SecurityEventRecord
    {
        public DateTime? TimeCreated { get; set; }
        public long? RecordId { get; set; }
        public int EventId { get; set; }
        public string? TaskCategory { get; set; }
        
        // Friendly Descriptions
        public string? EventDescription { get; set; }
        public string? LogonTypeDescription { get; set; }
        
        // Key forensic fields
        public string? Computer { get; set; }
        public string? Username { get; set; }
        public string? SubjectUserName { get; set; }
        public string? SubjectDomainName { get; set; }
        public string? SubjectUserSid { get; set; }
        public string? SubjectLogonId { get; set; }
        public string? TargetUserName { get; set; }
        public string? TargetDomainName { get; set; }
        public string? TargetUserSid { get; set; }
        public string? TargetLogonId { get; set; }
        public string? LogonGuid { get; set; }
        public string? WorkstationName { get; set; }
        public string? IpAddress { get; set; }
        public string? IpPort { get; set; }
        public string? ProcessName { get; set; }
        public string? ProcessId { get; set; }
        public string? CommandLine { get; set; }
        public string? LogonType { get; set; }
        public string? Status { get; set; }
        public string? SubStatus { get; set; }
        public string? FailureReason { get; set; }
        public string? AuthenticationPackageName { get; set; }
        public string? PackageName { get; set; }
        public string? ServiceName { get; set; }
        public string? TicketEncryptionType { get; set; }
        public string? ShareName { get; set; }
        public string? RelativeTargetName { get; set; }
        public string? ObjectName { get; set; }
        public string? ObjectType { get; set; }
        public string? AccessMask { get; set; }
        public string? AccessList { get; set; }
        public string? MemberName { get; set; }
        public string? MemberSid { get; set; }
        public string? GroupName { get; set; }
        public string? GroupDomain { get; set; }
        public bool IsSystemAccount { get; set; }
        public bool HasParseWarning { get; set; }
        public string? ParseWarning { get; set; }
        
        public string? Actor => SubjectUserName;
        public string? Target => TargetUserName;
    }
}
