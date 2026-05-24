# Changelog

All notable changes to SecParser will be documented in this file.

## [Unreleased]

### Added — Phase 4: security & forensic hardening

- New `SecParser.Core.Diagnostics.PathValidation` helper providing:
  - `EnsureValidEvtxFile(path)` — pre-flight check that rejects missing files, the wrong extension, files over a 1 GiB safety cap, and anything missing the binary-XML `ElfFile\0` magic header.
  - `EnsureValidExportPath(path, extension)` — rejects exports that don't match the expected extension or that target a reserved Windows device name (`CON`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`, …).
  - `NormalizeAndValidateHost(name)` — trims, length-clamps to 253 characters (DNS limit), and runs `Uri.CheckHostName` to reject syntactically invalid host names / IP literals.
  - `CombineAndEnsureUnderRoot(root, segments)` — resolves the full path and verifies it stays under the expected root, defending against directory-traversal payloads in user-supplied names.
- `EvtxLogParser.ParseAsync` now calls `EnsureValidEvtxFile` before opening the reader, so malformed or oversized inputs fail fast with a clear message instead of an opaque interop exception.
- `ExcelExporter.ExportAsync` / `PdfExporter.ExportAsync` now call `EnsureValidExportPath` before writing.
- `RemoteLogCollector` now:
  - Validates the host name via `NormalizeAndValidateHost` before doing anything else.
  - Clamps the sanitised host segment to 64 characters and runs the resolved output path through `CombineAndEnsureUnderRoot` so a crafted host name cannot escape `%MyDocuments%\SecParser\CollectedLogs\`.
  - Accepts a `SessionAuthentication` parameter (default `Default`) and records the chosen mechanism in both the log line and the manifest.
- `RemoteLogCollectionResult` gains an `Authentication` property.

### Changed — Phase 4: credential handling

- `RemoteLogConnectionRequest` is now an `IDisposable` class (was a `record`). It owns the captured `SecureString` and disposes it deterministically. `MainViewModel.OpenRemoteLogAsync` now wraps the request in a `using` so the password is zeroed once the call completes (success, cancellation, or failure).
- Manifest text now includes the negotiated `Authentication` line; the existing `CredentialMode` (`CurrentUser` / `ExplicitUser`) line is preserved.

### Added — Phase 3: view-model split, threading, and tunables

- New `SecParser.UI.Configuration.SecParserOptions` POCO centralises previously-hard-coded magic numbers (`LoadBatchSize`, `PageSize`, `UserSummaryEllipsisLength`). Registered as a singleton; Phase 5 will hydrate it from persisted settings.
- New `ILogLoadingService` / `LogLoadingService` owns the background EVTX parse pipeline. It buffers records on a background thread and flushes batches to the UI thread via `Dispatcher.BeginInvoke` at `DispatcherPriority.Background` (previously a blocking `Dispatcher.Invoke` per batch), keeping the UI responsive during large loads. UI state callbacks are routed through a new `ILogLoadSink` interface implemented by `MainViewModel`.
- New `IExportCoordinator` / `ExportCoordinator` wraps the export call and the expected I/O failure catches, returning a `ExportOutcome` record so the view-model no longer owns export-error try/catch logic.

### Changed — Phase 3: MainViewModel

- `MainViewModel` now consumes `ILogLoadingService`, `IExportCoordinator`, and `SecParserOptions` via constructor injection. `IEvtxLogParser` is no longer a direct dependency (it is owned by the loading service).
- File-Open, Remote-Open, and both Export commands now use `[RelayCommand(CanExecute = nameof(CanStartOperation))]` and are automatically disabled while `IsLoading == true`. `OnIsLoadingChanged` raises `NotifyCanExecuteChanged` on each affected command.
- Replaced the ad-hoc `_isClearing` flag with a nesting-safe `SuspendFilter()` IDisposable scope (`using (SuspendFilter()) { … }`).
- `Dispatcher.Invoke` (synchronous, foreground priority) was removed from the load pipeline; `LogLoadingService` now uses `BeginInvoke(Background)` exclusively.
- The user-summary ellipsis length and per-page count are now driven from `SecParserOptions` instead of inline literals.

### Added — Phase 2: dependency injection and service abstractions

- Added `Microsoft.Extensions.Hosting` (10.0.0) and `Microsoft.Extensions.DependencyInjection` (10.0.0) to `SecParser.UI`. `SecParser.Core` remains DI-framework-free.
- New Core abstractions in `SecParser.Core/Abstractions/`:
  - `IEvtxLogParser` — implemented by `EvtxLogParser`.
  - `IRemoteLogCollector` — implemented by `RemoteLogCollector`.
  - `IRecordExporter` (`DisplayName` / `FilterMask` / `DefaultExtension` / `ExportAsync`) — implemented by `ExcelExporter` and `PdfExporter`.
- Promoted `RemoteLogCollectionResult` from a nested record on `RemoteLogCollector` to a public top-level record in `SecParser.Core.Parsers`.
- New UI service abstractions in `SecParser.UI/Services/`:
  - `IFileDialogService` + `FileDialogService` — abstracts `OpenFileDialog` / `SaveFileDialog`.
  - `IUserDialogService` + `UserDialogService` — abstracts `MessageBox` and the remote-log dialog. Returns a `RemoteLogConnectionRequest` DTO.
- `App.OnStartup` now builds a `Microsoft.Extensions.Hosting` `IHost`, registers all services, and resolves `MainWindow` (with `MainViewModel` as its `DataContext`) from the container. `IHost` is disposed in `OnExit`.
- `MainViewModel` now implements `IDisposable` (disposes its `CancellationTokenSource`) and takes all collaborators via constructor injection: `IAppLogger`, `IEvtxLogParser`, `IRemoteLogCollector`, `IEnumerable<IRecordExporter>`, `IFileDialogService`, `IUserDialogService`. A parameterless ctor is retained for design-time / direct instantiation and wires up the production defaults.
- Consolidated `ExportExcelAsync` and `ExportPdfAsync` command bodies into a single `ExportAsync(IRecordExporter, string)` helper; both `[RelayCommand]` entrypoints are now one-liners.

### Changed — Phase 2: editorconfig

- Re-enabled `CA1001` (`Types that own disposable fields should be disposable`) as `warning` now that `MainViewModel` is `IDisposable`.

### Added — Phase 1: diagnostic logging and crash handling

- Introduced lightweight in-repo logging abstraction (`SecParser.Core.Diagnostics`):
  - `IAppLogger` interface + `AppLoggerExtensions` (`Trace`/`Debug`/`Information`/`Warning`/`Error` helpers).
  - `NullAppLogger` singleton for tests and design-time/parameterless construction.
  - `FileAppLogger` — thread-safe, daily-rolling, tab-delimited text logger writing to `%LocalAppData%\SecParser\logs\secparser-yyyyMMdd.log` with 14-day retention. Logging never throws; IO failures are swallowed.
- Constructor-injected `IAppLogger` on all `SecParser.Core` services (`EvtxLogParser`, `ExcelExporter`, `PdfExporter`, `RemoteLogCollector`); each has a parameterless overload that delegates to `NullAppLogger.Instance` so existing tests and callers are unaffected.
- Logging at operation start/end in parsers and exporters, including event count and parse-warning totals.
- WPF crash plumbing (`App.OnStartup`):
  - `FileAppLogger` constructed at startup; falls back to no-op logger if the log folder cannot be created.
  - Application version logged at startup; exit code logged on shutdown.
  - `DispatcherUnhandledException`, `AppDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException` now log the exception with a generated correlation ID and present users with a short message including that ID. UI exceptions are marked handled; task exceptions are marked observed.
  - `MainWindow` is constructed explicitly in `OnStartup` with the live logger injected into `MainViewModel`; `App.xaml` no longer hard-wires `StartupUri`.

### Changed — Phase 1: narrower exception handling

- Narrowed broad `catch (Exception ex)` blocks in `MainViewModel` (remote collection, file parsing, Excel export, PDF export) to the specific exceptions actually thrown by the underlying APIs (`EventLogException`, `UnauthorizedAccessException`, `IOException`, `XmlException`, `InvalidOperationException`, `ArgumentException`). Each catch now logs via `IAppLogger.Error` with a correlation ID and surfaces that ID in the user-facing message.
- Re-enabled `CA1031` (`Do not catch general exception types`) as `warning` for `SecParser.UI` in `.editorconfig`.

### Changed — Phase 0: foundation hardening

- Pinned .NET SDK with `global.json` (10.0.108, `latestFeature` roll-forward).
- Added Central Package Management (`Directory.Packages.props`) with transitive pinning; removed inline `<PackageReference Version=...>` from all csprojs.
- Reworked `Directory.Build.props`: nullable-on, implicit usings, `TreatWarningsAsErrors=true`, latest .NET analyzers (`AnalysisMode=Recommended`), deterministic builds, Source Link via `Microsoft.SourceLink.GitHub`, portable PDBs, embedded untracked sources.
- Added repository-wide `.editorconfig`: file-scoped namespaces, naming rules (async-ends-in-Async), per-rule CA severities (CA1031/1305/1310/1862 as warning), and per-project sections for Core/Tests/UI.
- Added WPF application manifest with PerMonitorV2 DPI awareness, long-path support, asInvoker execution level, and Win10/11 compatibility GUID.
- Migrated all source files to file-scoped namespaces.
- Added `CultureInfo.InvariantCulture` to all `DateTime`/`int.ToString` and `StringBuilder.AppendLine` formatting calls in `SecParser.Core` (CA1305).
- Added `.ConfigureAwait(false)` to all awaits in `SecParser.Core` (CA2007).
- Replaced `string.EndsWith("$")` with `EndsWith('$')` (CA1310/CA1866).
- Replaced `IEnumerable.Any()` checks with `Count > 0` on materialised collections (CA1860).
- Replaced `Substring(0, n) + suffix` with span-based `string.Concat(AsSpan(...), suffix)` (CA1845).

## 0.1.0 - Unreleased

### Added

- Initial WPF application for local `.evtx` parsing, filtering, paging, and export.
- Remote Security log collection with SHA-256 manifest output.
- Excel and PDF export support.
- Parser diagnostics for malformed event XML and unreadable event payloads.
- UI cancellation for long-running parsing and remote collection operations.
- GitHub CI, Dependabot configuration, MIT license, security policy, and third-party notices.
