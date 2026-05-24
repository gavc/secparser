using System;

namespace SecParser.Core.Diagnostics;

/// <summary>
/// Minimal application logger abstraction. Implementations must be thread-safe.
/// Categories are short tags (typically the source type name) used to group messages.
/// </summary>
public interface IAppLogger
{
    void Log(AppLogLevel level, string category, string message, Exception? exception = null, Guid? correlationId = null);
}

/// <summary>
/// Convenience helpers that forward to <see cref="IAppLogger.Log"/>.
/// </summary>
public static class AppLoggerExtensions
{
    public static void Trace(this IAppLogger logger, string category, string message)
        => logger.Log(AppLogLevel.Trace, category, message);

    public static void Debug(this IAppLogger logger, string category, string message)
        => logger.Log(AppLogLevel.Debug, category, message);

    public static void Information(this IAppLogger logger, string category, string message)
        => logger.Log(AppLogLevel.Information, category, message);

    public static void Warning(this IAppLogger logger, string category, string message, Exception? exception = null)
        => logger.Log(AppLogLevel.Warning, category, message, exception);

    public static void Error(this IAppLogger logger, string category, string message, Exception? exception = null, Guid? correlationId = null)
        => logger.Log(AppLogLevel.Error, category, message, exception, correlationId);
}
