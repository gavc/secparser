using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SecParser.Core.Diagnostics;

/// <summary>
/// Thread-safe file logger that writes UTF-8 tab-delimited lines to a daily rolling
/// file at <c>{folder}/secparser-yyyyMMdd.log</c>. Older files are purged on construction.
/// </summary>
/// <remarks>
/// Each call opens and closes the current file, so the log remains readable while the
/// app is running and is robust against process crashes. The expected volume (interactive
/// desktop app) makes the open/close overhead negligible.
/// </remarks>
public sealed class FileAppLogger : IAppLogger
{
    private const string FilePrefix = "secparser-";
    private const string FileSuffix = ".log";

    private readonly string _folder;
    private readonly int _retentionDays;
    private readonly AppLogLevel _minimumLevel;
    private readonly object _writeLock = new();

    public FileAppLogger(string folder, int retentionDays = 14, AppLogLevel minimumLevel = AppLogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (retentionDays < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention must be at least one day.");

        _folder = folder;
        _retentionDays = retentionDays;
        _minimumLevel = minimumLevel;

        Directory.CreateDirectory(_folder);
        TryCleanupOldFiles();
    }

    /// <summary>
    /// Resolves the default log folder under the user's local app data, ensuring the
    /// directory exists.
    /// </summary>
    public static string GetDefaultLogFolder()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "SecParser", "logs");
    }

    public void Log(AppLogLevel level, string category, string message, Exception? exception = null, Guid? correlationId = null)
    {
        if (level < _minimumLevel)
            return;

        var line = FormatLine(level, category, message, exception, correlationId);
        var path = GetCurrentFilePath();

        lock (_writeLock)
        {
            try
            {
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Logging must never throw into the caller. Best effort only.
            }
            catch (UnauthorizedAccessException)
            {
                // ditto
            }
        }
    }

    private string GetCurrentFilePath()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(_folder, FilePrefix + date + FileSuffix);
    }

    private static string FormatLine(AppLogLevel level, string category, string message, Exception? exception, Guid? correlationId)
    {
        var builder = new StringBuilder(256);
        builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        builder.Append('\t');
        builder.Append(level);
        builder.Append('\t');
        builder.Append(Sanitize(category));
        builder.Append('\t');
        builder.Append(correlationId?.ToString("N", CultureInfo.InvariantCulture) ?? string.Empty);
        builder.Append('\t');
        builder.Append(Sanitize(message));
        if (exception is not null)
        {
            builder.Append('\t');
            builder.Append(Sanitize(exception.ToString()));
        }
        builder.Append(Environment.NewLine);
        return builder.ToString();
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        // Keep the log line-delimited and tab-delimited by replacing structural characters.
        return value.Replace("\r", " ", StringComparison.Ordinal)
                    .Replace("\n", " ", StringComparison.Ordinal)
                    .Replace("\t", "    ", StringComparison.Ordinal);
    }

    private void TryCleanupOldFiles()
    {
        try
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-_retentionDays);
            var files = Directory.EnumerateFiles(_folder, FilePrefix + "*" + FileSuffix);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var stamp = name[FilePrefix.Length..];
                if (stamp.Length != 8 ||
                    !DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fileDate))
                {
                    continue;
                }

                if (fileDate.Date < cutoff)
                {
                    try { File.Delete(file); }
                    catch (IOException) { /* leave it for next run */ }
                    catch (UnauthorizedAccessException) { /* leave it for next run */ }
                }
            }
        }
        catch (IOException)
        {
            // Cleanup is best-effort; never fail construction.
        }
        catch (UnauthorizedAccessException)
        {
            // ditto
        }
    }
}
