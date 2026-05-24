using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SecParser.Core.Abstractions;
using SecParser.Core.Models;
using SecParser.UI.Configuration;

namespace SecParser.UI.Services;

/// <summary>
/// Encapsulates the background EVTX parse pipeline so the view-model can stay
/// focused on presentation state. Buffers records on a background thread and
/// flushes batches to the UI thread via <see cref="Dispatcher.BeginInvoke"/>
/// at <see cref="DispatcherPriority.Background"/>, which keeps the UI
/// responsive during large loads.
/// </summary>
public interface ILogLoadingService
{
    Task LoadAsync(
        string filePath,
        ILogLoadSink sink,
        CancellationToken cancellationToken);
}

/// <summary>
/// Callback surface the loading service uses to publish parsed records.
/// Implementations are invoked on the UI thread.
/// </summary>
public interface ILogLoadSink
{
    /// <summary>True if the given username belongs to a system account that
    /// should currently be hidden.</summary>
    bool ShouldHideSystemAccount(bool isSystemAccount);

    void OnUserDiscovered(string userName, bool isSystemAccount);

    void OnBatch(IReadOnlyList<SecurityEventRecord> batch, int totalProcessed, int totalWarnings);

    void OnCompleted(int totalProcessed, int totalWarnings);
}

public sealed class LogLoadingService : ILogLoadingService
{
    private readonly IEvtxLogParser _parser;
    private readonly SecParserOptions _options;

    public LogLoadingService(IEvtxLogParser parser, SecParserOptions options)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task LoadAsync(
        string filePath,
        ILogLoadSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);

        await Task.Run(async () =>
        {
            var buffer = new List<SecurityEventRecord>(_options.LoadBatchSize);
            var seenUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processed = 0;
            var warnings = 0;

            await foreach (var record in _parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false))
            {
                buffer.Add(record);
                processed++;
                if (record.HasParseWarning)
                {
                    warnings++;
                }

                if (!string.IsNullOrEmpty(record.Username) && seenUsers.Add(record.Username))
                {
                    var userName = record.Username;
                    var isSystem = record.IsSystemAccount;
                    DispatchBackground(() => sink.OnUserDiscovered(userName, isSystem));
                }

                if (buffer.Count >= _options.LoadBatchSize)
                {
                    var batch = buffer.ToArray();
                    buffer.Clear();
                    var snapshotProcessed = processed;
                    var snapshotWarnings = warnings;
                    DispatchBackground(() => sink.OnBatch(batch, snapshotProcessed, snapshotWarnings));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (buffer.Count > 0)
            {
                var batch = buffer.ToArray();
                var snapshotProcessed = processed;
                var snapshotWarnings = warnings;
                DispatchBackground(() => sink.OnBatch(batch, snapshotProcessed, snapshotWarnings));
            }

            DispatchBackground(() => sink.OnCompleted(processed, warnings));
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void DispatchBackground(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }
}
