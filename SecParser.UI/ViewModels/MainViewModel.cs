using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SecParser.Core.Exporters;
using SecParser.Core.Models;
using SecParser.Core.Parsers;

namespace SecParser.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly EvtxLogParser _parser;
        private readonly ExcelExporter _excelExporter;
        private readonly PdfExporter _pdfExporter;
        private readonly RemoteLogCollector _remoteCollector;
        private readonly List<SecurityEventRecord> _allEvents = new();
        private readonly List<SecurityEventRecord> _filteredEventCache = new();
        
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
        }

        [ObservableProperty]
        private bool isCancellationRequested;

        partial void OnIsCancellationRequestedChanged(bool value) => OnPropertyChanged(nameof(CancelButtonVisibility));

        private CancellationTokenSource? _loadCts;

        [ObservableProperty]
        private string userFilter = string.Empty;

        private bool _isClearing;

        partial void OnUserFilterChanged(string value)
        {
            if (!_isClearing)
            {
                ApplyFilter();
            }
        }

        [ObservableProperty]
        private string selectedUsersSummary = "None selected";

        [ObservableProperty]
        private bool showSystemAccounts;

        partial void OnShowSystemAccountsChanged(bool value)
        {
            RebuildAvailableUsers();
            ApplyFilter();
        }

        [ObservableProperty]
        private UserFilterItem? selectedUserItem;
        
        partial void OnSelectedUserItemChanged(UserFilterItem? value)
        {
            if (value != null)
            {
                // Toggle the checkbox if they clicked the row itself (whitespace)
                value.IsSelected = !value.IsSelected;
                
                // Clear the combobox's underlying selection instantly so it doesn't overwrite text
                Application.Current.Dispatcher.BeginInvoke(new Action(() => 
                {
                    SelectedUserItem = null;
                    UpdateSelectedUsersSummary();
                    OnPropertyChanged(nameof(SelectedUsersSummary));
                }));
            }
        }

        public Visibility ProgressBarVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CancelButtonVisibility => _loadCts != null && IsLoading && !IsCancellationRequested
            ? Visibility.Visible
            : Visibility.Collapsed;

        public ObservableCollection<UserFilterItem> AvailableUsers { get; } = new();

        public ObservableCollection<ParseDiagnostic> ParseDiagnostics { get; } = new();
        public ObservableCollection<SecurityEventRecord> FilteredEvents { get; } = new();

        public string PageSummary => FilteredCount == 0
            ? "Page 0 of 0"
            : $"Page {CurrentPage} of {TotalPages}";

        public MainViewModel()
        {
            _parser = new EvtxLogParser();
            _excelExporter = new ExcelExporter();
            _pdfExporter = new PdfExporter();
            _remoteCollector = new RemoteLogCollector();
        }

        [RelayCommand]
        private async Task OpenLogAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Windows Event Logs (*.evtx)|*.evtx|All Files (*.*)|*.*",
                Title = "Select Event Log File"
            };

            if (dialog.ShowDialog() == true)
            {
                await ParseAndLoadFileAsync(dialog.FileName);
            }
        }

        [RelayCommand]
        private async Task OpenRemoteLogAsync()
        {
            var dialog = new RemoteLogDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
                return;

            RemoteLogCollector.RemoteLogCollectionResult collection;
            var tokenSource = BeginOperation();
            var token = tokenSource.Token;

            try
            {
                IsLoading = true;
                StatusMessage = $"Connecting to {dialog.ComputerName} and exporting Security log...";

                collection = await _remoteCollector.CollectAsync(
                    dialog.ComputerName!,
                    dialog.Domain,
                    dialog.Username,
                    dialog.Password,
                    token);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Remote collection cancelled.";
                EndOperation(tokenSource);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to collect remote log from {dialog.ComputerName}:\n\n{ex.Message}",
                    "Remote Collection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Remote collection failed.";
                EndOperation(tokenSource);
                return;
            }

            EndOperation(tokenSource);

            var loaded = await ParseAndLoadFileAsync(collection.LogFilePath);
            if (!loaded)
                return;

            MessageBox.Show(
                $"Remote log collected and saved to:\n{collection.LogFilePath}\n\nManifest:\n{collection.ManifestFilePath}\n\nSHA-256:\n{collection.Sha256}",
                "Collection Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<bool> ParseAndLoadFileAsync(string filePath)
        {
            var tokenSource = BeginOperation();
            var token = tokenSource.Token;

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {System.IO.Path.GetFileName(filePath)}...";
                
                _allEvents.Clear();
                _filteredEventCache.Clear();
                FilteredEvents.Clear();
                ParseDiagnostics.Clear();
                Application.Current.Dispatcher.Invoke(() => AvailableUsers.Clear());
                ProcessedCount = 0;
                ParseWarningCount = 0;
                FilteredCount = 0;
                CurrentPage = 1;
                TotalPages = 1;

                // Load in background
                await Task.Run(async () =>
                {
                    List<SecurityEventRecord> tempBuffer = new();
                    HashSet<string> seenUsers = new(StringComparer.OrdinalIgnoreCase);
                    int localCount = 0;
                    int localWarningCount = 0;

                    // Asynchronous generation
                    await foreach (var record in _parser.ParseAsync(filePath, token))
                    {
                        tempBuffer.Add(record);
                        localCount++;
                        if (record.HasParseWarning)
                        {
                            localWarningCount++;
                        }

                        if (!string.IsNullOrEmpty(record.Username) && seenUsers.Add(record.Username))
                        {
                            var newUsername = record.Username;
                            var isSystem = record.IsSystemAccount;

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (ShowSystemAccounts || !isSystem)
                                {
                                    AvailableUsers.Add(new UserFilterItem 
                                    { 
                                        UserName = newUsername, 
                                        IsSelected = false,
                                        SelectionChangedCallback = UpdateSelectedUsersSummary
                                    });
                                }
                            });
                        }

                        // Dispatch to UI in batches so it doesn't freeze but still updates
                        if (tempBuffer.Count >= 1000)
                        {
                            var batch = tempBuffer.ToList();
                            tempBuffer.Clear();

                            Application.Current.Dispatcher.Invoke(() =>
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
                                ProcessedCount = localCount;
                                ParseWarningCount = localWarningCount;
                                FilteredCount = CountFilteredEvents();
                                TotalPages = CalculateTotalPages(FilteredCount);
                            });
                        }
                    }

                    // Flush remaining
                    if (tempBuffer.Count > 0)
                    {
                        var batch = tempBuffer.ToList();
                        Application.Current.Dispatcher.Invoke(() =>
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
                            ProcessedCount = localCount;
                            ParseWarningCount = localWarningCount;
                            FilteredCount = CountFilteredEvents();
                            TotalPages = CalculateTotalPages(FilteredCount);
                        });
                    }

                    token.ThrowIfCancellationRequested();
                });

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
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Error loading file.";
                return false;
            }
            finally
            {
                EndOperation(tokenSource);
            }
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
                if (joined.Length > 40)
                {
                    SelectedUsersSummary = joined.Substring(0, 37) + "...";
                }
                else
                {
                    SelectedUsersSummary = joined;
                }
            }
            
            if (!_isClearing)
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

            // Summarize but prevent redundant filter triggering
            _isClearing = true;
            UpdateSelectedUsersSummary();
            _isClearing = false;
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

            if (selectedUsers.Any())
            {
                q = q.Where(e => !string.IsNullOrEmpty(e.Username) && selectedUsers.Contains(e.Username));
            }
            else if (!string.IsNullOrWhiteSpace(UserFilter))
            {
                // Fallback to text search if no checkboxes selected
                q = q.Where(e => !string.IsNullOrEmpty(e.Username) && 
                                 e.Username.Contains(UserFilter, StringComparison.OrdinalIgnoreCase));
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
            _isClearing = true;
            try
            {
                UserFilter = string.Empty;
                foreach (var user in AvailableUsers)
                {
                    user.IsSelected = false;
                }
            }
            finally
            {
                _isClearing = false;
            }
            ApplyFilter();
            StatusMessage = "Filter cleared.";
        }

        [RelayCommand]
        private async Task ExportExcelAsync()
        {
            if (!_filteredEventCache.Any())
            {
                MessageBox.Show("No data to export.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"SecurityEventsExport_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                // Snapshot on the UI thread before any await to avoid a race with
                // RebuildFilteredCache() clearing the list on the UI thread while
                // the exporter iterates it on a background thread.
                var exportSnapshot = _filteredEventCache.ToList();
                IsLoading = true;
                StatusMessage = "Exporting to Excel...";

                try
                {
                    await _excelExporter.ExportAsync(exportSnapshot, dialog.FileName);
                    StatusMessage = "Export successful.";
                    MessageBox.Show("Export completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusMessage = "Export failed.";
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            if (!_filteredEventCache.Any())
            {
                MessageBox.Show("No data to export.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"SecurityEventsExport_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                // Snapshot on the UI thread before any await — same reason as Excel export.
                var exportSnapshot = _filteredEventCache.ToList();
                IsLoading = true;
                StatusMessage = "Exporting to PDF...";

                try
                {
                    await _pdfExporter.ExportAsync(exportSnapshot, dialog.FileName);
                    StatusMessage = "Export successful.";
                    MessageBox.Show("Export completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusMessage = "Export failed.";
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private void Exit()
        {
            Application.Current.Shutdown();
        }
    }
}
