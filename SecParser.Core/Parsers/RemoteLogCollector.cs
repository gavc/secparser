using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;
using SecParser.Core.Abstractions;
using SecParser.Core.Diagnostics;

namespace SecParser.Core.Parsers;

public class RemoteLogCollector : IRemoteLogCollector
{
    private const string LogCategory = nameof(RemoteLogCollector);

    private readonly IAppLogger _logger;

    public RemoteLogCollector() : this(null) { }

    public RemoteLogCollector(IAppLogger? logger)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    /// <summary>
    /// Connects to a remote machine, exports its Security event log to a
    /// timestamped local .evtx file, and returns the local file path.
    /// Credentials are optional — if omitted, the current user's identity is used.
    /// </summary>
    public async Task<RemoteLogCollectionResult> CollectAsync(
        string computerName,
        string? domain = null,
        string? username = null,
        SecureString? password = null,
        SessionAuthentication authentication = SessionAuthentication.Default,
        CancellationToken cancellationToken = default)
    {
        var validatedHost = PathValidation.NormalizeAndValidateHost(computerName);

        var localPath = BuildLocalPath(validatedHost);
        var collectedAt = DateTimeOffset.UtcNow;
        var credentialMode = string.IsNullOrWhiteSpace(username) ? "CurrentUser" : "ExplicitUser";
        _logger.Information(LogCategory, $"Begin remote collection from '{validatedHost}' ({credentialMode}, auth={authentication}) → '{localPath}'.");

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var session = BuildSession(validatedHost, domain, username, password, authentication);
            using var cancelRegistration = cancellationToken.Register(session.Dispose);

            try
            {
                // ExportLog exports the entire named log channel to a self-contained .evtx file.
                // The wildcard query "*" retrieves all events.
                session.ExportLog(
                    path: "Security",
                    pathType: PathType.LogName,
                    query: "*",
                    targetFilePath: localPath);
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (EventLogException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(localPath);
        var sha256 = await ComputeSha256Async(localPath, cancellationToken).ConfigureAwait(false);
        var manifestPath = await WriteManifestAsync(
            localPath,
            validatedHost,
            collectedAt,
            fileInfo.Length,
            sha256,
            username,
            authentication,
            cancellationToken).ConfigureAwait(false);

        _logger.Information(LogCategory, $"Completed remote collection from '{validatedHost}': {fileInfo.Length} bytes, sha256={sha256}.");
        return new RemoteLogCollectionResult(
            localPath,
            manifestPath,
            validatedHost,
            collectedAt,
            fileInfo.Length,
            sha256,
            authentication.ToString());
    }

    private static EventLogSession BuildSession(
        string computerName,
        string? domain,
        string? username,
        SecureString? password,
        SessionAuthentication authentication)
    {
        bool hasCredentials = !string.IsNullOrWhiteSpace(username) && password != null;

        return hasCredentials
            ? new EventLogSession(
                computerName,
                domain ?? ".",
                username,
                password,
                authentication)
            : new EventLogSession(computerName);
    }

    /// <summary>
    /// Builds the local output path for a collected log under
    /// <c>%MyDocuments%\SecParser\CollectedLogs\</c>. The computer name is
    /// stripped of any invalid file-name characters and the resolved full path
    /// is verified to remain under the output root to defend against
    /// directory-traversal payloads in the host name.
    /// </summary>
    private static string BuildLocalPath(string computerName)
    {
        var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var saveFolder = Path.Combine(docsFolder, "SecParser", "CollectedLogs");
        Directory.CreateDirectory(saveFolder);

        var safeName = string.Concat(computerName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrEmpty(safeName))
        {
            safeName = "remote";
        }
        if (safeName.Length > 64)
        {
            safeName = safeName.Substring(0, 64);
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss'Z'", CultureInfo.InvariantCulture);
        var fileName = $"{safeName}_Security_{timestamp}.evtx";

        return PathValidation.CombineAndEnsureUnderRoot(saveFolder, fileName);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<string> WriteManifestAsync(
        string logFilePath,
        string computerName,
        DateTimeOffset collectedAt,
        long fileSizeBytes,
        string sha256,
        string? username,
        SessionAuthentication authentication,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.ChangeExtension(logFilePath, ".manifest.txt");
        var builder = new StringBuilder();
        builder.AppendLine("SecParser Remote Collection Manifest");
        builder.AppendLine(CultureInfo.InvariantCulture, $"ComputerName: {computerName}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"CollectedAtUtc: {collectedAt:O}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"LogFileName: {Path.GetFileName(logFilePath)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"FileSizeBytes: {fileSizeBytes}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Sha256: {sha256}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"CredentialMode: {(string.IsNullOrWhiteSpace(username) ? "CurrentUser" : "ExplicitUser")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Authentication: {authentication}");
        builder.AppendLine("Channel: Security");
        builder.AppendLine("Query: *");

        await File.WriteAllTextAsync(manifestPath, builder.ToString(), cancellationToken).ConfigureAwait(false);
        return manifestPath;
    }
}
