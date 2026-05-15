using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Linq;
using SecParser.Core.Models;

namespace SecParser.Core.Parsers
{
    public class EvtxLogParser
    {
        private static readonly IReadOnlyDictionary<int, string> EventDescriptions = new Dictionary<int, string>
        {
            // System / audit lifecycle
            [1100] = "Event Logging Service Shut Down",
            [1102] = "Audit Log Cleared",
            [1107] = "Event Logging Service Error",
            [1108] = "Event Processing Error",
            [4608] = "Windows Is Starting Up",
            [4610] = "Authentication Package Loaded By LSASS",
            [4611] = "Trusted Logon Process Registered With LSASS",
            [4614] = "Notification Package Loaded By SAM",
            [4622] = "Security Package Loaded By LSASS",

            // Logon / logoff / session activity
            [4624] = "Account Logged On",
            [4625] = "Account Failed To Log On",
            [4626] = "User Or Device Claims Information",
            [4627] = "Group Membership Information",
            [4634] = "Account Logged Off",
            [4647] = "User Initiated Logoff",
            [4648] = "Logon Attempted With Explicit Credentials",
            [4649] = "Replay Attack Detected",
            [4675] = "SIDs Were Filtered",
            [4778] = "Session Reconnected To Window Station",
            [4779] = "Session Disconnected From Window Station",
            [4800] = "Workstation Locked",
            [4801] = "Workstation Unlocked",
            [4802] = "Screen Saver Invoked",
            [4803] = "Screen Saver Dismissed",

            // Account logon / Kerberos / NTLM
            [4768] = "Kerberos TGT Requested",
            [4769] = "Kerberos Service Ticket Requested",
            [4770] = "Kerberos Service Ticket Renewed",
            [4771] = "Kerberos Pre-Authentication Failed",
            [4772] = "Kerberos Authentication Ticket Request Failed",
            [4774] = "Account Mapped For Logon",
            [4775] = "Account Could Not Be Mapped For Logon",
            [4776] = "Credential Validation Attempted",
            [4777] = "Credential Validation Failed",

            // Privilege use
            [4672] = "Special Privileges Assigned To New Logon",
            [4673] = "Privileged Service Called",
            [4674] = "Privileged Object Operation Attempted",
            [4703] = "User Right Adjusted",

            // Process and detailed tracking
            [4688] = "Process Created",
            [4689] = "Process Exited",
            [4692] = "DPAPI Master Key Backup Attempted",
            [4693] = "DPAPI Master Key Recovery Attempted",
            [4694] = "Auditable Protected Data Protection Attempted",
            [4695] = "Auditable Protected Data Unprotection Attempted",
            [4696] = "Primary Token Assigned To Process",
            [5712] = "RPC Attempted",
            [6416] = "Plug And Play Device Connected Or Removed",

            // Object access / shares / scheduled tasks
            [4656] = "Handle To Object Requested",
            [4657] = "Registry Value Modified",
            [4658] = "Handle To Object Closed",
            [4659] = "Handle To Object Requested With Delete Intent",
            [4660] = "Object Deleted",
            [4661] = "Handle To Object Requested",
            [4662] = "Operation Performed On Object",
            [4663] = "Object Access Attempted",
            [4664] = "Hard Link Creation Attempted",
            [4670] = "Permissions On Object Changed",
            [4671] = "Blocked TBS Ordinal Access Attempted",
            [4690] = "Handle Duplication Attempted",
            [4691] = "Indirect Object Access Requested",
            [4698] = "Scheduled Task Created",
            [4699] = "Scheduled Task Deleted",
            [4700] = "Scheduled Task Enabled",
            [4701] = "Scheduled Task Disabled",
            [4702] = "Scheduled Task Updated",
            [4985] = "Transaction State Changed",
            [5051] = "File Virtualized",
            [5140] = "Network Share Object Accessed",
            [5145] = "Network Share Object Checked For Access",

            // User account management
            [4720] = "User Account Created",
            [4722] = "User Account Enabled",
            [4723] = "Password Change Attempted",
            [4724] = "Password Reset Attempted",
            [4725] = "User Account Disabled",
            [4726] = "User Account Deleted",
            [4738] = "User Account Changed",
            [4740] = "User Account Locked Out",
            [4765] = "SID History Added To Account",
            [4766] = "SID History Add Attempt Failed",
            [4767] = "User Account Unlocked",
            [4780] = "ACL Set On Administrator Account Members",
            [4781] = "Account Name Changed",
            [4782] = "Password Hash Accessed",
            [4793] = "Password Policy Checking API Called",
            [4794] = "DSRM Administrator Password Set Attempted",
            [5376] = "Credential Manager Credentials Backed Up",
            [5377] = "Credential Manager Credentials Restored",

            // Computer account management
            [4741] = "Computer Account Created",
            [4742] = "Computer Account Changed",
            [4743] = "Computer Account Deleted",

            // Security-enabled group management
            [4727] = "Security-Enabled Global Group Created",
            [4728] = "Member Added To Security-Enabled Global Group",
            [4729] = "Member Removed From Security-Enabled Global Group",
            [4730] = "Security-Enabled Global Group Deleted",
            [4731] = "Security-Enabled Local Group Created",
            [4732] = "Member Added To Security-Enabled Local Group",
            [4733] = "Member Removed From Security-Enabled Local Group",
            [4734] = "Security-Enabled Local Group Deleted",
            [4735] = "Security-Enabled Local Group Changed",
            [4737] = "Security-Enabled Global Group Changed",
            [4754] = "Security-Enabled Universal Group Created",
            [4755] = "Security-Enabled Universal Group Changed",
            [4756] = "Member Added To Security-Enabled Universal Group",
            [4757] = "Member Removed From Security-Enabled Universal Group",
            [4758] = "Security-Enabled Universal Group Deleted",
            [4764] = "Group Type Changed",

            // Distribution / security-disabled group management
            [4744] = "Security-Disabled Local Group Created",
            [4745] = "Security-Disabled Local Group Changed",
            [4746] = "Member Added To Security-Disabled Local Group",
            [4747] = "Member Removed From Security-Disabled Local Group",
            [4748] = "Security-Disabled Local Group Deleted",
            [4749] = "Security-Disabled Global Group Created",
            [4750] = "Security-Disabled Global Group Changed",
            [4751] = "Member Added To Security-Disabled Global Group",
            [4752] = "Member Removed From Security-Disabled Global Group",
            [4753] = "Security-Disabled Global Group Deleted",
            [4759] = "Security-Disabled Universal Group Created",
            [4760] = "Security-Disabled Universal Group Changed",
            [4761] = "Member Added To Security-Disabled Universal Group",
            [4762] = "Member Removed From Security-Disabled Universal Group",
            [4763] = "Security-Disabled Universal Group Deleted",

            // Policy change
            [4704] = "User Right Assigned",
            [4705] = "User Right Removed",
            [4706] = "Domain Trust Created",
            [4707] = "Domain Trust Removed",
            [4709] = "IPsec Services Started",
            [4710] = "IPsec Services Disabled",
            [4711] = "IPsec Policy Changed",
            [4712] = "IPsec Services Potentially Serious Failure",
            [4714] = "Encrypted Data Recovery Policy Changed",
            [4716] = "Trusted Domain Information Modified",
            [4717] = "System Security Access Granted To Account",
            [4718] = "System Security Access Removed From Account",
            [4719] = "System Audit Policy Changed",
            [4739] = "Domain Policy Changed",
            [4902] = "Per-User Audit Policy Table Created",
            [4904] = "Security Event Source Registration Attempted",
            [4905] = "Security Event Source Unregistration Attempted",
            [4906] = "CrashOnAuditFail Value Changed",
            [4907] = "Object Auditing Settings Changed",
            [4912] = "Per-User Audit Policy Changed",

            // Directory service access / changes
            [4931] = "Active Directory Replica Destination Naming Context Modified",
            [4932] = "Active Directory Replica Synchronization Began",
            [4933] = "Active Directory Replica Synchronization Ended",
            [5136] = "Directory Service Object Modified",
            [5137] = "Directory Service Object Created",
            [5138] = "Directory Service Object Undeleted",
            [5139] = "Directory Service Object Moved",
            [5141] = "Directory Service Object Deleted",

            // Windows Filtering Platform / firewall
            [4946] = "Windows Firewall Exception List Rule Added",
            [4947] = "Windows Firewall Exception List Rule Modified",
            [4948] = "Windows Firewall Exception List Rule Deleted",
            [4956] = "Windows Firewall Active Profile Changed",
            [5024] = "Windows Firewall Service Started",
            [5031] = "Windows Firewall Blocked Incoming Application",
            [5033] = "Windows Firewall Driver Started",
            [5038] = "Code Integrity Invalid Image Hash",
            [5040] = "IPsec Authentication Set Added",
            [5041] = "IPsec Authentication Set Modified",
            [5042] = "IPsec Authentication Set Deleted",
            [5043] = "IPsec Connection Security Rule Added",
            [5044] = "IPsec Connection Security Rule Modified",
            [5045] = "IPsec Connection Security Rule Deleted",
            [5046] = "IPsec Crypto Set Added",
            [5047] = "IPsec Crypto Set Modified",
            [5048] = "IPsec Crypto Set Deleted",
            [5059] = "Key Migration Operation Performed",
            [5150] = "Windows Filtering Platform Blocked Packet",
            [5151] = "More Restrictive WFP Filter Blocked Packet",
            [5152] = "Windows Filtering Platform Blocked Packet",
            [5153] = "More Restrictive WFP Filter Blocked Packet",
            [5154] = "Windows Filtering Platform Permitted Listening Port",
            [5155] = "Windows Filtering Platform Blocked Listening Port",
            [5156] = "Windows Filtering Platform Allowed Connection",
            [5157] = "Windows Filtering Platform Blocked Connection",
            [5158] = "Windows Filtering Platform Permitted Bind",
            [5159] = "Windows Filtering Platform Blocked Bind",
            [5440] = "WFP Callout Present At Startup",
            [5441] = "WFP Filter Present At Startup",
            [5442] = "WFP Provider Present At Startup",
            [5443] = "WFP Provider Context Present At Startup",
            [5444] = "WFP Sub-Layer Present At Startup",
            [5446] = "WFP Callout Changed",
            [5448] = "WFP Provider Changed",
            [5449] = "WFP Provider Context Changed",
            [5450] = "WFP Sub-Layer Changed",
            [5451] = "IPsec Quick Mode Security Association Established",
            [5452] = "IPsec Quick Mode Security Association Ended",
            [5456] = "IPsec Policy Applied",
            [5457] = "IPsec Policy Apply Failed",
            [5458] = "Cached IPsec Policy Applied",

            // Certificate services
            [4870] = "Certificate Services Revoked Certificate",
            [4886] = "Certificate Services Received Certificate Request",
            [4887] = "Certificate Services Approved Certificate Request And Issued Certificate",
            [4888] = "Certificate Services Denied Certificate Request",
            [4893] = "Certificate Services Archived Key",
            [4898] = "Certificate Services Loaded Template",

            // Network policy server / 802.1x
            [6272] = "Network Policy Server Granted Access",
            [6273] = "Network Policy Server Denied Access",
            [6278] = "Network Policy Server Granted Full Access",
            [5632] = "Wireless Network Authentication Request",
            [6144] = "Security Policy Applied Successfully",
            [6145] = "Security Policy Processing Error",
            [8001] = "WLAN Service Authentication Succeeded",
            [8002] = "WLAN Service Authentication Failed",
            [8003] = "WLAN Service Connection Succeeded",
            [8004] = "WLAN Service Connection Failed",
            [8005] = "WLAN Service Disconnection Succeeded",
            [8006] = "WLAN Service Disconnection Failed",
            [8007] = "WLAN Service Security Started",
            [8222] = "Network Policy Server EAP Quarantine State Changed",

            // Application control / device control
            [4825] = "User Denied Access To Remote Desktop",
            [4826] = "Boot Configuration Data Loaded",
            [6419] = "Request Made To Disable Device",
            [6420] = "Device Disabled",
            [6421] = "Request Made To Enable Device",
            [6422] = "Device Enabled",
            [6423] = "Installation Of Device Forbidden By Policy",
            [6424] = "Device Installation Allowed After Policy Check",
            [30004] = "Windows Defender Application Control Policy Changed"
        };

        public async IAsyncEnumerable<SecurityEventRecord> ParseAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield(); // Ensure it runs asynchronously
            
            using var reader = new EventLogReader(filePath, PathType.FilePath);
            
            EventRecord? record;
            while ((record = reader.ReadEvent()) != null)
            {
                using (record)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var secEvent = new SecurityEventRecord
                    {
                        TimeCreated = record.TimeCreated,
                        RecordId = record.RecordId,
                        EventId = record.Id,
                        EventDescription = GetEventDescription(record.Id),
                        Computer = record.MachineName,
                        TaskCategory = record.TaskDisplayName
                    };

                    PopulateFromEventXml(secEvent, record.ToXml);

                    yield return secEvent;
                }
            }
        }

        public static string? GetEventDescription(int eventId)
        {
            return EventDescriptions.TryGetValue(eventId, out var description)
                ? description
                : null;
        }

        public static string? GetLogonTypeDescription(string? logonType)
        {
            if (string.IsNullOrEmpty(logonType)) return null;

            return logonType switch
            {
                "2" => "Interactive (Local)",
                "3" => "Network (Shared Folder/RPC)",
                "4" => "Batch (Scheduled Task)",
                "5" => "Service (Windows Service)",
                "7" => "Unlock (Lock Screen Returning)",
                "8" => "Network Cleartext (IIS/CredSSP)",
                "9" => "New Credentials (RunAs)",
                "10" => "Remote Interactive (RDP)",
                "11" => "Cached Interactive (Offline)",
                "12" => "Cached Remote Interactive",
                "13" => "Cached Unlock",
                _ => "Unknown/Other"
            };
        }

        public static void PopulateFromEventXml(SecurityEventRecord secEvent, Func<string> readXml)
        {
            try
            {
                PopulateFromXml(secEvent, readXml());
            }
            catch (Exception ex) when (IsRecoverableEventReadException(ex))
            {
                AddParseWarning(secEvent, $"Failed to read event XML: {ex.Message}");
            }
        }

        public static void PopulateFromXml(SecurityEventRecord secEvent, string xmlString)
        {
            try
            {
                var xdoc = XDocument.Parse(xmlString);
                var ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;

                secEvent.EventDescription ??= GetEventDescription(secEvent.EventId);
                
                var eventData = xdoc.Descendants(ns + "EventData").FirstOrDefault();
                if (eventData != null)
                {
                    foreach (var data in eventData.Elements(ns + "Data"))
                    {
                        var name = data.Attribute("Name")?.Value;
                        var value = CleanValue(data.Value);

                        if (string.IsNullOrEmpty(name)) continue;

                        switch (name)
                        {
                            case "SubjectUserSid":
                                secEvent.SubjectUserSid = value;
                                break;
                            case "SubjectUserName":
                                secEvent.SubjectUserName = value;
                                break;
                            case "SubjectDomainName":
                                secEvent.SubjectDomainName = value;
                                break;
                            case "SubjectLogonId":
                                secEvent.SubjectLogonId = value;
                                break;
                            case "TargetUserSid":
                                secEvent.TargetUserSid = value;
                                break;
                            case "TargetUserName":
                                secEvent.TargetUserName = value;
                                break;
                            case "TargetDomainName":
                                secEvent.TargetDomainName = value;
                                break;
                            case "TargetLogonId":
                                secEvent.TargetLogonId = value;
                                break;
                            case "LogonGuid":
                                secEvent.LogonGuid = value;
                                break;
                            case "WorkstationName":
                                secEvent.WorkstationName = value;
                                break;
                            case "IpAddress":
                                secEvent.IpAddress = value;
                                break;
                            case "IpPort":
                                secEvent.IpPort = value;
                                break;
                            case "ProcessName":
                            case "NewProcessName":
                                secEvent.ProcessName = value;
                                break;
                            case "ProcessId":
                            case "NewProcessId":
                                secEvent.ProcessId = value;
                                break;
                            case "CommandLine":
                            case "ProcessCommandLine":
                                secEvent.CommandLine = value;
                                break;
                            case "LogonType":
                                secEvent.LogonType = value;
                                break;
                            case "Status":
                                secEvent.Status = value;
                                break;
                            case "SubStatus":
                                secEvent.SubStatus = value;
                                break;
                            case "FailureReason":
                                secEvent.FailureReason = value;
                                break;
                            case "AuthenticationPackageName":
                                secEvent.AuthenticationPackageName = value;
                                break;
                            case "PackageName":
                                secEvent.PackageName = value;
                                break;
                            case "ServiceName":
                                secEvent.ServiceName = value;
                                break;
                            case "TicketEncryptionType":
                                secEvent.TicketEncryptionType = value;
                                break;
                            case "ShareName":
                                secEvent.ShareName = value;
                                break;
                            case "RelativeTargetName":
                                secEvent.RelativeTargetName = value;
                                break;
                            case "ObjectName":
                                secEvent.ObjectName = value;
                                break;
                            case "ObjectType":
                                secEvent.ObjectType = value;
                                break;
                            case "AccessMask":
                                secEvent.AccessMask = value;
                                break;
                            case "AccessList":
                            case "Accesses":
                                secEvent.AccessList = value;
                                break;
                            case "MemberName":
                                secEvent.MemberName = value;
                                break;
                            case "MemberSid":
                                secEvent.MemberSid = value;
                                break;
                            case "GroupName":
                            case "TargetSidName":
                                secEvent.GroupName = value;
                                break;
                            case "GroupDomain":
                                secEvent.GroupDomain = value;
                                break;
                        }
                    }

                    secEvent.Username = ResolveForensicUsername(secEvent.EventId, secEvent.TargetUserName, secEvent.SubjectUserName);
                    secEvent.IsSystemAccount = CheckIfSystemAccount(secEvent.Username);
                    secEvent.LogonTypeDescription = GetLogonTypeDescription(secEvent.LogonType);
                }
            }
            catch (Exception ex) when (ex is System.Xml.XmlException || ex is InvalidOperationException)
            {
                AddParseWarning(secEvent, ex.Message);
            }
        }

        public static string? ResolveForensicUsername(int eventId, string? target, string? subject)
        {
            var candidates = eventId switch
            {
                4688 or 4689 or 4672 => new[] { subject, target },
                _ => new[] { target, subject }
            };

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && c != "-")
                {
                    return c;
                }
            }
            return null; 
        }

        public static bool CheckIfSystemAccount(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            return username.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) || 
                   username.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) || 
                   username.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase) ||
                   username.Equals("ANONYMOUS LOGON", StringComparison.OrdinalIgnoreCase) ||
                   username.StartsWith("UMFD-", StringComparison.OrdinalIgnoreCase) ||
                   username.StartsWith("DWM-", StringComparison.OrdinalIgnoreCase) ||
                   username.EndsWith("$");
        }

        private static string? CleanValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-")
                return null;

            return value.Trim();
        }

        private static bool IsRecoverableEventReadException(Exception ex)
        {
            return ex is EventLogException ||
                   ex is InvalidOperationException ||
                   ex is System.Xml.XmlException ||
                   ex is IOException;
        }

        private static void AddParseWarning(SecurityEventRecord secEvent, string message)
        {
            secEvent.HasParseWarning = true;
            secEvent.ParseWarning = message;
            secEvent.LogonTypeDescription = GetLogonTypeDescription(secEvent.LogonType);
        }
    }
}
