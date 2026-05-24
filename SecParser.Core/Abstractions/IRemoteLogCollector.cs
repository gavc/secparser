using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using SecParser.Core.Parsers;

namespace SecParser.Core.Abstractions;

/// <summary>
/// Exports a remote machine's Security event log to a local .evtx file with a
/// SHA-256 manifest for chain-of-custody.
/// </summary>
public interface IRemoteLogCollector
{
    /// <summary>
    /// Connects to a remote machine, exports its Security event log to a
    /// timestamped local file, and returns the local file path and manifest details.
    /// </summary>
    /// <param name="authentication">RPC authentication mechanism used when
    /// explicit credentials are supplied. Defaults to
    /// <see cref="SessionAuthentication.Default"/> which lets Windows negotiate.</param>
    Task<RemoteLogCollectionResult> CollectAsync(
        string computerName,
        string? domain = null,
        string? username = null,
        SecureString? password = null,
        SessionAuthentication authentication = SessionAuthentication.Default,
        CancellationToken cancellationToken = default);
}
