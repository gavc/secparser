using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SecParser.Core.Abstractions;
using SecParser.Core.Diagnostics;
using SecParser.Core.Models;

namespace SecParser.UI.Services;

public interface IExportCoordinator
{
    Task<ExportOutcome> ExportAsync(
        IRecordExporter exporter,
        IReadOnlyCollection<SecurityEventRecord> records,
        string filePath,
        string formatLabel);
}

public readonly record struct ExportOutcome(bool Success, string? ErrorMessage, Guid? CorrelationId);

/// <summary>
/// Runs an export against an immutable record snapshot and wraps the expected
/// I/O failures so the view-model can react without owning the try/catch.
/// </summary>
public sealed class ExportCoordinator : IExportCoordinator
{
    private const string LogCategory = nameof(ExportCoordinator);

    private readonly IAppLogger _logger;

    public ExportCoordinator(IAppLogger logger)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<ExportOutcome> ExportAsync(
        IRecordExporter exporter,
        IReadOnlyCollection<SecurityEventRecord> records,
        string filePath,
        string formatLabel)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        try
        {
            await exporter.ExportAsync(records, filePath).ConfigureAwait(false);
            return new ExportOutcome(true, null, null);
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or InvalidOperationException
                                       or ArgumentException)
        {
            var correlationId = Guid.NewGuid();
            _logger.Error(LogCategory, $"{formatLabel} export to '{filePath}' failed.", ex, correlationId);
            return new ExportOutcome(false, ex.Message, correlationId);
        }
    }
}
