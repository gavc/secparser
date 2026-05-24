using System.Collections.Generic;
using System.Threading;
using SecParser.Core.Models;

namespace SecParser.Core.Abstractions;

/// <summary>
/// Parses Windows Security Event Log (.evtx) files into <see cref="SecurityEventRecord"/> instances.
/// </summary>
public interface IEvtxLogParser
{
    /// <summary>
    /// Asynchronously enumerates the records in the specified .evtx file.
    /// </summary>
    /// <param name="filePath">Absolute path to a readable .evtx file.</param>
    /// <param name="cancellationToken">Token used to cancel iteration.</param>
    IAsyncEnumerable<SecurityEventRecord> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
