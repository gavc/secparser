using System;

namespace SecParser.Core.Diagnostics;

/// <summary>
/// No-op logger used by tests and as a safe default when no logger is injected.
/// </summary>
public sealed class NullAppLogger : IAppLogger
{
    public static readonly NullAppLogger Instance = new();

    private NullAppLogger() { }

    public void Log(AppLogLevel level, string category, string message, Exception? exception = null, Guid? correlationId = null)
    {
        // intentionally empty
    }
}
