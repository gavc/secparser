using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecParser.Core.Abstractions;
using SecParser.Core.Diagnostics;
using SecParser.Core.Models;
using SecParser.Core.Parsers;
using SecParser.UI.Configuration;
using SecParser.UI.Services;

namespace SecParser.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable, ILogLoadSink
{
    private const string LogCategory = nameof(MainViewModel);

    private readonly IAppLogger _logger;
    private readonly IRemoteLogCollector _remoteCollector;
    private readonly IReadOnlyList<IRecordExporter> _exporters;
    private readonly IRecordExporter _excelExporter;
    private readonly IRecordExporter _pdfExporter;
    private readonly IFileDialogService _fileDialog;
    private readonly IUserDialogService _userDialog;
    private readonly ILogLoadingService _loadingService;
    private readonly IExportCoordinator _exportCoordinator;
    private readonly SecParserOptions _options;
    private readonly List<SecurityEventRecord> _allEvents = new();
    private readonly List<SecurityEventRecord> _filteredEventCache = new();

    private CancellationTokenSource? _loadCts;
    private int _suspendFilterDepth;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private int processedCount;

    [ObservableProperty]
    private int parseWarningCount;

    [ObservableProperty]
    private int filteredCount;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private int pageSize = 1000;

    partial void OnCurrentPageChanged(int value) => OnPropertyChanged(nameof(PageSummary));

    partial void OnTotalPagesChanged(int value) => OnPropertyChanged(nameof(PageSummary));

    partial void OnFilteredCountChanged(int value) => OnPropertyChanged(nameof(PageSummary));

    [ObservableProperty]
    private bool isLoading;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ProgressBarVisibility));
        OnPropertyChanged(nameof(CancelButtonVisibility));
        OnPropertyChanged(nameof(CanStartOperation));
        NotifyOperationCommands();
    }

    [ObservableProperty]
    private bool isCancellationRequested;

    partial void OnIsCancellationRequestedChanged(bool value) => OnPropertyChanged(nameof(CancelButtonVisibility));

    [ObservableProperty]
    private bool isUserFilterOpen;

    [ObservableProperty]
    private string userSearchText = string.Empty;

    partial void OnIsUserFilterOpenChanged(bool value)
    {
        if (value) RebuildFilteredAvailableUsers();
    }

    partial void OnUserSearchTextChanged(string value) => RebuildFilteredAvailableUsers();

    [ObservableProperty]
    private string selectedUsersSummary = "None selected";

    [ObservableProperty]
    private bool showSystemAccounts;

    partial void OnShowSystemAccountsChanged(bool value)
    {
        RebuildAvailableUsers();
        ApplyFilter();
    }

    public Visibility ProgressBarVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CancelButtonVisibility => _loadCts != null && IsLoading && !IsCancellationRequested
        ? Visibility.Visible
        : Visibility.Collapsed;

    public ObservableCollection<UserFilterItem> AvailableUsers { get; } = new();
    public ObservableCollection<UserFilterItem> FilteredAvailableUsers { get; } = new();

    public ObservableCollection<ParseDiagnostic> ParseDiagnostics { get; } = new();
    public ObservableCollection<SecurityEventRecord> FilteredEvents { get; } = new();

    public string PageSummary => FilteredCount == 0
        ? "Page 0 of 0"
        : $"Page {CurrentPage} of {TotalPages}";

    /// <summary>Guard used by Open / Export commands so the user cannot
    /// kick off a second long-running operation while one is in flight.</summary>
    public bool CanStartOperation => !IsLoading;

    public MainViewModel() : this(
        NullAppLogger.Instance,
        new SecParser.Core.Parsers.RemoteLogCollector(),
        new IRecordExporter[]
        {
            new SecParser.Core.Exporters.ExcelExporter(),
            new SecParser.Core.Exporters.PdfExporter(),
        },
        new FileDialogService(),
        new UserDialogService(),
        new LogLoadingService(new SecParser.Core.Parsers.EvtxLogParser(), new SecParserOptions()),
        new ExportCoordinator(NullAppLogger.Instance),
        new SecParserOptions())
    { }

    public MainViewModel(
        IAppLogger logger,
        IRemoteLogCollector remoteCollector,
        IEnumerable<IRecordExporter> exporters,
        IFileDialogService fileDialog,
        IUserDialogService userDialog,
        ILogLoadingService loadingService,
        IExportCoordinator exportCoordinator,
        SecParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(exporters);
        _logger = logger ?? NullAppLogger.Instance;
        _remoteCollector = remoteCollector ?? throw new ArgumentNullException(nameof(remoteCollector));
        _exporters = exporters.ToList();
        _fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
        _userDialog = userDialog ?? throw new ArgumentNullException(nameof(userDialog));
        _loadingService = loadingService ?? throw new ArgumentNullException(nameof(loadingService));
        _exportCoordinator = exportCoordinator ?? throw new ArgumentNullException(nameof(exportCoordinator));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _excelExporter = _exporters.FirstOrDefault(e =>
            string.Equals(e.DefaultExtension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No Excel (.xlsx) exporter registered.");
        _pdfExporter = _exporters.FirstOrDefault(e =>
            string.Equals(e.DefaultExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No PDF (.pdf) exporter registered.");

        PageSize = _options.PageSize;
    }

    public void Dispose()
    {
        _loadCts?.Dispose();
        _loadCts = null;
        GC.SuppressFinalize(this);
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task OpenLogAsync()
    {
        var path = _fileDialog.PromptOpenFile(
            "Windows Event Logs (*.evtx)|*.evtx|All Files (*.*)|*.*",
            "Select Event Log File");

        if (!string.IsNullOrEmpty(path))
        {
            await ParseAndLoadFileAsync(path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task OpenRemoteLogAsync()
    {
        using var request = _userDialog.PromptRemoteLogConnection();
        if (request is null)
            return;

        RemoteLogCollectionResult collection;
        var tokenSource = BeginOperation();
        var token = tokenSource.Token;

        try
        {
            IsLoading = true;
            StatusMessage = $"Connecting to {request.ComputerName} and exporting Security log...";

            collection = await _remoteCollector.CollectAsync(
                request.ComputerName,
                request.Domain,
                request.Username,
                request.Password,
                System.Diagnostics.Eventing.Reader.SessionAuthentication.Default,
                token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Remote collection cancelled.";
            EndOperation(tokenSource);
            return;
        }
        catch (Exception ex) when (ex is EventLogException
                                       or UnauthorizedAccessException
                                       or IOException
                                       or ArgumentException
                                       or InvalidOperationException)
        {
            var correlationId = Guid.NewGuid();
            _logger.Error(LogCategory, $"Remote collection from '{request.ComputerName}' failed.", ex, correlationId);
            _userDialog.ShowError(
                $"Failed to collect remote log from {request.ComputerName}:\n\n{ex.Message}\n\nCorrelation ID: {correlationId:N}",
                "Remote Collection Failed");
            StatusMessage = "Remote collection failed.";
            EndOperation(tokenSource);
            return;
        }

        EndOperation(tokenSource);

        var loaded = await ParseAndLoadFileAsync(collection.LogFilePath);
        if (!loaded)
            return;

        _userDialog.ShowInfo(
            $"Remote log collected and saved to:\n{collection.LogFilePath}\n\nManifest:\n{collection.ManifestFilePath}\n\nSHA-256:\n{collection.Sha256}",
            "Collection Complete");
    }

    private async Task<bool> ParseAndLoadFileAsync(string filePath)
    {
        var tokenSource = BeginOperation();
        var token = tokenSource.Token;

        try
        {
            IsLoading = true;
            StatusMessage = $"Loading {Path.GetFileName(filePath)}...";

            _allEvents.Clear();
            _filteredEventCache.Clear();
            FilteredEvents.Clear();
            ParseDiagnostics.Clear();
            AvailableUsers.Clear();
            ProcessedCount = 0;
            ParseWarningCount = 0;
            FilteredCount = 0;
            CurrentPage = 1;
            TotalPages = 1;

            await _loadingService.LoadAsync(filePath, this, token);

            ApplyFilter();

            StatusMessage = ParseWarningCount == 0
                ? "Loaded successfully."
                : $"Loaded with {ParseWarningCount} parse warning(s).";

            return true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Loading cancelled.";
            return false;
        }
        catch (Exception ex) when (ex is EventLogException
                                       or UnauthorizedAccessException
                                       or IOException
                                       or XmlException
                                       or InvalidOperationException)
        {
            var correlationId = Guid.NewGuid();
            _logger.Error(LogCategory, $"Failed to parse '{filePath}'.", ex, correlationId);
            _userDialog.ShowError($"Error parsing file: {ex.Message}\n\nCorrelation ID: {correlationId:N}", "Error");
            StatusMessage = "Error loading file.";
            return false;
        }
        finally
        {
            EndOperation(tokenSource);
        }
    }

    // --- ILogLoadSink (callbacks dispatched on the UI thread) ---

    bool ILogLoadSink.ShouldHideSystemAccount(bool isSystemAccount) => !ShowSystemAccounts && isSystemAccount;

    void ILogLoadSink.OnUserDiscovered(string userName, bool isSystemAccount)
    {
        if (!ShowSystemAccounts && isSystemAccount)
            return;

        AvailableUsers.Add(new UserFilterItem
        {
            UserName = userName,
            IsSelected = false,
            SelectionChangedCallback = UpdateSelectedUsersSummary
        });
    }

    void ILogLoadSink.OnBatch(IReadOnlyList<SecurityEventRecord> batch, int totalProcessed, int totalWarnings)
    {
        foreach (var item in batch)
        {
            _allEvents.Add(item);
            if (item.HasParseWarning)
            {
                ParseDiagnostics.Add(CreateDiagnostic(item));
            }

            if ((ShowSystemAccounts || !item.IsSystemAccount) && FilteredEvents.Count < PageSize)
            {
                FilteredEvents.Add(item);
            }
        }
        ProcessedCount = totalProcessed;
        ParseWarningCount = totalWarnings;
        FilteredCount = CountFilteredEvents();
        TotalPages = CalculateTotalPages(FilteredCount);
    }

    void ILogLoadSink.OnCompleted(int totalProcessed, int totalWarnings)
    {
        ProcessedCount = totalProcessed;
        ParseWarningCount = totalWarnings;
    }

    [RelayCommand]
    private void CancelOperation()
    {
        if (_loadCts == null || IsCancellationRequested)
            return;

        IsCancellationRequested = true;
        StatusMessage = "Cancellation requested...";
        _loadCts.Cancel();
    }

    private CancellationTokenSource BeginOperation()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        IsCancellationRequested = false;
        OnPropertyChanged(nameof(CancelButtonVisibility));
        return _loadCts;
    }

    private void EndOperation(CancellationTokenSource tokenSource)
    {
        if (ReferenceEquals(_loadCts, tokenSource))
        {
            _loadCts.Dispose();
            _loadCts = null;
        }

        IsCancellationRequested = false;
        IsLoading = false;
        OnPropertyChanged(nameof(CancelButtonVisibility));
    }

    private void NotifyOperationCommands()
    {
        OpenLogCommand.NotifyCanExecuteChanged();
        OpenRemoteLogCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Suspends side-effect-bearing filter rebuilds for the lifetime of the
    /// returned scope. Nests safely; only the outermost scope's disposal
    /// re-enables filtering. Replaces the prior ad-hoc <c>_isClearing</c> flag.
    /// </summary>
    private FilterSuspension SuspendFilter() => new(this);

    private bool IsFilterSuspended => _suspendFilterDepth > 0;

    private readonly struct FilterSuspension : IDisposable
    {
        private readonly MainViewModel _owner;

        public FilterSuspension(MainViewModel owner)
        {
            _owner = owner;
            _owner._suspendFilterDepth++;
        }

        public void Dispose() => _owner._suspendFilterDepth--;
    }

    private void UpdateSelectedUsersSummary()
    {
        var selected = AvailableUsers.Where(u => u.IsSelected).Select(u => u.UserName).ToList();
        if (selected.Count == 0)
        {
            SelectedUsersSummary = "None selected";
        }
        else
        {
            var joined = string.Join(", ", selected);
            var limit = _options.UserSummaryEllipsisLength;
            if (joined.Length > limit)
            {
                SelectedUsersSummary = string.Concat(joined.AsSpan(0, Math.Max(0, limit - 3)), "...");
            }
            else
            {
                SelectedUsersSummary = joined;
            }
        }

        if (!IsFilterSuspended)
        {
            ApplyFilter();
        }
    }

    private void RebuildAvailableUsers()
    {
        var previouslySelected = AvailableUsers.Where(u => u.IsSelected).Select(u => u.UserName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        AvailableUsers.Clear();

        var uniqueUsers = _allEvents
            .Where(e => !string.IsNullOrEmpty(e.Username) && (ShowSystemAccounts || !e.IsSystemAccount))
            .Select(e => e.Username!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var user in uniqueUsers)
        {
            AvailableUsers.Add(new UserFilterItem
            {
                UserName = user,
                IsSelected = previouslySelected.Contains(user),
                SelectionChangedCallback = UpdateSelectedUsersSummary
            });
        }

        using (SuspendFilter())
        {
            UpdateSelectedUsersSummary();
        }
        RebuildFilteredAvailableUsers();
    }

    private void RebuildFilteredAvailableUsers()
    {
        FilteredAvailableUsers.Clear();
        var search = UserSearchText;
        foreach (var user in AvailableUsers)
        {
            if (string.IsNullOrEmpty(search) ||
                user.UserName.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableUsers.Add(user);
            }
        }
    }

    [RelayCommand]
    private void SelectAllUsers()
    {
        using (SuspendFilter())
        {
            foreach (var user in FilteredAvailableUsers)
                user.IsSelected = true;
        }
        UpdateSelectedUsersSummary();
        ApplyFilter();
    }

    [RelayCommand]
    private void ClearAllUsers()
    {
        using (SuspendFilter())
        {
            foreach (var user in AvailableUsers)
                user.IsSelected = false;
        }
        UpdateSelectedUsersSummary();
        ApplyFilter();
    }

    [RelayCommand]
    private void ApplyFilter()
    {
        CurrentPage = 1;
        RebuildFilteredCache();
        LoadCurrentPage();
        StatusMessage = $"Filter applied. Displaying {FilteredEvents.Count} of {FilteredCount} matching records ({ProcessedCount} total).";
    }

    private void RebuildFilteredCache()
    {
        _filteredEventCache.Clear();
        var q = _allEvents.AsEnumerable();

        if (!ShowSystemAccounts)
        {
            q = q.Where(e => !e.IsSystemAccount);
        }

        var selectedUsers = AvailableUsers.Where(u => u.IsSelected).Select(u => u.UserName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedUsers.Count > 0)
        {
            q = q.Where(e => !string.IsNullOrEmpty(e.Username) && selectedUsers.Contains(e.Username));
        }

        foreach (var item in q)
        {
            _filteredEventCache.Add(item);
        }

        FilteredCount = _filteredEventCache.Count;
        TotalPages = CalculateTotalPages(FilteredCount);
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }
    }

    private void LoadCurrentPage()
    {
        FilteredEvents.Clear();

        if (FilteredCount == 0)
        {
            CurrentPage = 1;
            return;
        }

        var skip = (CurrentPage - 1) * PageSize;
        foreach (var item in _filteredEventCache.Skip(skip).Take(PageSize))
        {
            FilteredEvents.Add(item);
        }
    }

    private int CountFilteredEvents()
    {
        return _allEvents.Count(e => ShowSystemAccounts || !e.IsSystemAccount);
    }

    private int CalculateTotalPages(int count)
    {
        return Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
    }

    private static ParseDiagnostic CreateDiagnostic(SecurityEventRecord record)
    {
        return new ParseDiagnostic
        {
            TimeCreated = record.TimeCreated,
            RecordId = record.RecordId,
            EventId = record.EventId,
            Computer = record.Computer,
            Message = record.ParseWarning
        };
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage >= TotalPages)
            return;

        CurrentPage++;
        LoadCurrentPage();
        StatusMessage = $"Displaying {FilteredEvents.Count} records on {PageSummary}.";
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage <= 1)
            return;

        CurrentPage--;
        LoadCurrentPage();
        StatusMessage = $"Displaying {FilteredEvents.Count} records on {PageSummary}.";
    }

    [RelayCommand]
    private void ClearFilter()
    {
        using (SuspendFilter())
        {
            UserSearchText = string.Empty;
            foreach (var user in AvailableUsers)
                user.IsSelected = false;
        }
        IsUserFilterOpen = false;
        UpdateSelectedUsersSummary();
        ApplyFilter();
        StatusMessage = "Filter cleared.";
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task ExportExcelAsync() => await ExportAsync(_excelExporter, "Excel");

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task ExportPdfAsync() => await ExportAsync(_pdfExporter, "PDF");

    private async Task ExportAsync(IRecordExporter exporter, string formatLabel)
    {
        if (_filteredEventCache.Count == 0)
        {
            _userDialog.ShowWarning("No data to export.", "Warning");
            return;
        }

        var filePath = _fileDialog.PromptSaveFile(
            exporter.FilterMask,
            exporter.DefaultExtension,
            CreateExportFileName(exporter.DefaultExtension));

        if (string.IsNullOrEmpty(filePath))
            return;

        // Snapshot on the UI thread before any await to avoid a race with
        // RebuildFilteredCache() clearing the list on the UI thread while
        // the exporter iterates it on a background thread.
        var exportSnapshot = _filteredEventCache.ToArray();
        IsLoading = true;
        StatusMessage = $"Exporting to {formatLabel}...";

        try
        {
            var outcome = await _exportCoordinator.ExportAsync(exporter, exportSnapshot, filePath, formatLabel);
            if (outcome.Success)
            {
                StatusMessage = "Export successful.";
                _userDialog.ShowInfo("Export completed successfully.", "Success");
            }
            else
            {
                StatusMessage = "Export failed.";
                _userDialog.ShowError(
                    $"Export failed: {outcome.ErrorMessage}\n\nCorrelation ID: {outcome.CorrelationId:N}",
                    "Export Error");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Exit() => Application.Current.Shutdown();

    private static string CreateExportFileName(string extension)
    {
        return $"SecurityEventsExport_{DateTime.Now:yyMMdd_HHmmss}{extension}";
    }
}
