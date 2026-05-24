using System;
using System.Security;

namespace SecParser.UI.Services;

/// <summary>
/// User-supplied remote machine credentials and target captured from the
/// remote-log dialog. Wraps the <see cref="Password"/> <see cref="SecureString"/>
/// so the view-model can dispose it deterministically via <c>using</c>; the
/// dialog hands ownership over on confirmation.
/// </summary>
public sealed class RemoteLogConnectionRequest : IDisposable
{
    private SecureString? _password;
    private bool _disposed;

    public RemoteLogConnectionRequest(
        string computerName,
        string? domain,
        string? username,
        SecureString? password)
    {
        ComputerName = computerName;
        Domain = domain;
        Username = username;
        _password = password;
    }

    public string ComputerName { get; }
    public string? Domain { get; }
    public string? Username { get; }

    public SecureString? Password
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _password;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _password?.Dispose();
        _password = null;
        _disposed = true;
    }
}
