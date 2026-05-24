# SecParser - Security Log Event Viewer

SecParser is a native Windows desktop application for parsing and reviewing Windows Security Event Logs (`.evtx`). It is built for lightweight incident review and user-event tracking, with local parsing, filtering, export, and optional remote Security log collection.

## Features

* **Responsive Parsing:** Reads `.evtx` files on a background task and batches UI updates so the application remains usable during large imports.
* **Intelligent Data Extraction:** Rapidly pulls vital forensic indicators including:
  * Event Time
  * Computer / Hostname
  * Originating IP Address
  * Target and subject usernames/domains
  * Logon IDs and logon GUIDs where present
  * Executing Process details
  * Workstation and network port details where present
* **Automated Forensic Context:** 
  * Automatically maps common Microsoft Security event IDs to human-readable *Event Descriptions*, including logon/logoff, Kerberos/NTLM, process tracking, account and group management, object access, policy changes, firewall/WFP, directory service changes, certificate services, and NPS/WLAN events.
  * Decodes numerical Windows Logon Types (e.g., Type 2, Type 3, Type 10) into actionable definitions like `Interactive (Local)`, `Network`, or `Remote Interactive (RDP)`.
  * Tracks parser warnings instead of silently discarding malformed event payloads.
* **Dynamic Multi-Select Filtering:** Automatically catalogs every unique user present within the loaded log and builds a smart popup filter panel. Click the `Users:` button to open the panel, type to narrow the list, then check any combination of users — the grid updates instantly. **Select All** and **Clear All** buttons let you bulk-toggle the visible users in one click.
* **Professional Exports:**
  * **Excel (.xlsx):** Export the actively filtered grid directly to formatted Excel tables with native auto-filters using `ClosedXML`.
  * **PDF Reports:** Generate landscape-oriented PDF reports via `PDFsharp` and `MigraDoc`. Includes a high-level summary, parse-warning count, time range, and recent logon/logoff context. PDF detail rows are capped for very large filtered result sets.
* **Remote Collection:** Export the Security log from a remote machine using `EventLogSession`, save it under `Documents\SecParser\CollectedLogs`, calculate a SHA-256 hash, and write a collection manifest recording host, UTC timestamp, file size, hash, credential mode (current user / explicit), and the negotiated RPC authentication mechanism.
* **Hardened Input Handling:** EVTX inputs are validated up front (extension, 1 GiB size cap, binary `ElfFile\0` magic header) and export paths are checked for valid extensions and reserved Windows device names. Remote host names are normalised and length-clamped, and the resulting collected-log path is verified to stay under `Documents\SecParser\CollectedLogs` to defend against directory-traversal payloads. Passwords entered into the remote-log dialog are held in a `SecureString` and zeroed deterministically after the call completes.
* **Diagnostic Logging & Crash Capture:** All parsers, exporters, and the remote collector log start/end and failure events to a daily-rolling text file under `%LocalAppData%\SecParser\logs\secparser-yyyyMMdd.log` (14-day retention). Unhandled WPF dispatcher, AppDomain, and task-scheduler exceptions are caught, logged with a correlation ID, and surfaced to the user with that ID for support triage.

## Architecture & Technologies

* **Framework:** .NET 10 (Targeting `net10.0-windows10.0.19041.0`)
* **UI:** Windows Presentation Foundation (WPF) providing native Windows 11 UX stability.
* **Pattern:** MVVM powered by `CommunityToolkit.Mvvm`. The UI host is built on `Microsoft.Extensions.Hosting` with constructor-injected services (`IEvtxLogParser`, `IRemoteLogCollector`, `IRecordExporter`, `ILogLoadingService`, `IExportCoordinator`, `IFileDialogService`, `IUserDialogService`, `IAppLogger`).
* **Build & Quality:** Central Package Management, warnings-as-errors, .NET analyzers in `Recommended` mode, deterministic builds, and Source Link enabled repo-wide.
* **Core Libraries:**
  * `System.Diagnostics.EventLog` - Deep, native EVTX querying.
  * `ClosedXML` - Open-source, enterprise-ready Excel generation.
  * `PDFsharp` / `MigraDoc` WPF build - MIT-licensed PDF report generation.

## Prerequisites

* **OS:** Windows 10 (Build 19041 or higher) or Windows 11.
* **Runtime:** [.NET 10.0 SDK](https://dotnet.microsoft.com/)

## License

SecParser is released under the MIT License. See [LICENSE](LICENSE).

PDF export is powered by `PDFsharp` and `MigraDoc`, which are published under
the MIT License. Review third-party package metadata before redistributing
modified builds:

* [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)

## Getting Started

1. **Clone the repository:**
   ```powershell
   git clone <repository-url>
   cd SecParser
   ```

2. **Build the solution:**
   ```powershell
   dotnet build
   ```

3. **Run the Application:**
   ```powershell
   dotnet run --project SecParser.UI
   ```

## Release Build

Version metadata is centralized in `Directory.Build.props`.

To produce a repeatable Windows x64 Release artifact:

```powershell
.\scripts\build-release.ps1
```

The script restores, builds, runs tests, publishes `SecParser.UI`, creates a zip
under `artifacts\`, and writes a SHA-256 checksum file.

## Usage Guide

1. **Load a Log:** Click `File` -> `Open Local Log...` and select any standard Windows `.evtx` file.
2. **Review Progress:** Watch the Status Bar at the bottom; events are rendered into the DataGrid dynamically in real-time batches. 
3. **Filter by User:** Click the `Users:` button on the toolbar to open the filter popup. Type in the search box to narrow the list of accounts, then check the boxes next to the individuals you wish to audit — the grid updates instantly. Use **Select All** or **Clear All** to bulk-toggle the visible accounts.
4. **System Accounts:** By default, built-in Windows service and machine accounts (SYSTEM, DWM-*, UMFD-*, machine accounts ending in `$`, etc.) are hidden from both the dropdown and the grid. To include them, toggle `Settings` -> `Show System Accounts`.
5. **Export Findings:** Click `Export` on the top menu bar to dump your current filtered perspective out to `.xlsx` or a `.pdf` report.

## Roadmap

* Add event-specific views for account management, Kerberos, NTLM, process creation, and RDP session tracking.
* Add time-range and event-ID query controls for remote collection.
* Persist user preferences (page size, default export folder, last-used filters).

## Event ID Sources

The friendly event descriptions are based on Microsoft Learn references for Windows Security events, including the Microsoft Sentinel Windows Security event set and Advanced Audit Policy Configuration event tables.

---
*Created to streamline Windows Security auditing and user-event review.*
