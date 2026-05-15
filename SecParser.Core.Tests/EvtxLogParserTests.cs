using SecParser.Core.Models;
using SecParser.Core.Parsers;

namespace SecParser.Core.Tests;

public class EvtxLogParserTests
{
    [Fact]
    public void PopulateFromXml_SeparatesTargetAndSubjectForLogon()
    {
        var record = new SecurityEventRecord { EventId = 4624 };

        EvtxLogParser.PopulateFromXml(record, BuildEventXml(
            ("SubjectUserSid", "S-1-5-18"),
            ("SubjectUserName", "SYSTEM"),
            ("SubjectDomainName", "NT AUTHORITY"),
            ("SubjectLogonId", "0x3e7"),
            ("TargetUserSid", "S-1-5-21-1000"),
            ("TargetUserName", "alice"),
            ("TargetDomainName", "CONTOSO"),
            ("TargetLogonId", "0x8ab12"),
            ("LogonType", "10"),
            ("IpAddress", "10.1.2.3"),
            ("IpPort", "55123"),
            ("WorkstationName", "WS-01"),
            ("ProcessName", @"C:\Windows\System32\winlogon.exe")));

        Assert.Equal("alice", record.Username);
        Assert.Equal("alice", record.TargetUserName);
        Assert.Equal("CONTOSO", record.TargetDomainName);
        Assert.Equal("SYSTEM", record.SubjectUserName);
        Assert.Equal("NT AUTHORITY", record.SubjectDomainName);
        Assert.Equal("0x8ab12", record.TargetLogonId);
        Assert.Equal("10.1.2.3", record.IpAddress);
        Assert.Equal("Remote Interactive (RDP)", record.LogonTypeDescription);
        Assert.False(record.IsSystemAccount);
        Assert.False(record.HasParseWarning);
    }

    [Fact]
    public void PopulateFromXml_PrefersSubjectForProcessEvents()
    {
        var record = new SecurityEventRecord { EventId = 4688 };

        EvtxLogParser.PopulateFromXml(record, BuildEventXml(
            ("SubjectUserName", "bob"),
            ("SubjectDomainName", "CONTOSO"),
            ("TargetUserName", "ignored-target"),
            ("NewProcessName", @"C:\Tools\script.exe"),
            ("NewProcessId", "0x1234"),
            ("CommandLine", @"script.exe -audit")));

        Assert.Equal("bob", record.Username);
        Assert.Equal("bob", record.SubjectUserName);
        Assert.Equal(@"C:\Tools\script.exe", record.ProcessName);
        Assert.Equal("0x1234", record.ProcessId);
        Assert.Equal(@"script.exe -audit", record.CommandLine);
    }

    [Fact]
    public void PopulateFromXml_ExtractsEventSpecificSecurityFields()
    {
        var record = new SecurityEventRecord { EventId = 5145 };

        EvtxLogParser.PopulateFromXml(record, BuildEventXml(
            ("SubjectUserName", "alice"),
            ("ShareName", @"\\*\C$"),
            ("RelativeTargetName", @"Windows\System32\config\SAM"),
            ("ObjectName", @"\Device\HarddiskVolume1\Windows\System32\config\SAM"),
            ("ObjectType", "File"),
            ("AccessMask", "0x1"),
            ("AccessList", "%%4416")));

        Assert.Equal(@"\\*\C$", record.ShareName);
        Assert.Equal(@"Windows\System32\config\SAM", record.RelativeTargetName);
        Assert.Equal("File", record.ObjectType);
        Assert.Equal("0x1", record.AccessMask);
        Assert.Equal("%%4416", record.AccessList);
    }

    [Theory]
    [InlineData("SYSTEM")]
    [InlineData("DWM-1")]
    [InlineData("SERVER01$")]
    public void CheckIfSystemAccount_IdentifiesBuiltInAndMachineAccounts(string username)
    {
        Assert.True(EvtxLogParser.CheckIfSystemAccount(username));
    }

    [Fact]
    public void PopulateFromXml_RecordsParseWarningForBadXml()
    {
        var record = new SecurityEventRecord { EventId = 4624 };

        EvtxLogParser.PopulateFromXml(record, "<Event><EventData>");

        Assert.True(record.HasParseWarning);
        Assert.False(string.IsNullOrWhiteSpace(record.ParseWarning));
    }

    [Theory]
    [InlineData(1102, "Audit Log Cleared")]
    [InlineData(4688, "Process Created")]
    [InlineData(4728, "Member Added To Security-Enabled Global Group")]
    [InlineData(4741, "Computer Account Created")]
    [InlineData(4769, "Kerberos Service Ticket Requested")]
    [InlineData(5156, "Windows Filtering Platform Allowed Connection")]
    public void GetEventDescription_ReturnsFriendlyDescription(int eventId, string expected)
    {
        Assert.Equal(expected, EvtxLogParser.GetEventDescription(eventId));
    }

    private static string BuildEventXml(params (string Name, string Value)[] data)
    {
        var rows = string.Concat(data.Select(d => $"<Data Name=\"{d.Name}\">{System.Security.SecurityElement.Escape(d.Value)}</Data>"));
        return $"""
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <EventData>{rows}</EventData>
            </Event>
            """;
    }
}
