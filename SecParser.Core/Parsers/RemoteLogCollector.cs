using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;

namespace SecParser.Core.Parsers
{
    public class RemoteLogCollector
    {
        public sealed record RemoteLogCollectionResult(
            string LogFilePath,
            string ManifestFilePath,
            string ComputerName,
            DateTimeOffset CollectedAt,
            long FileSizeBytes,
            string Sha256);

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
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(computerName))
                throw new ArgumentException("Computer name or IP must be provided.", nameof(computerName));

            var localPath = BuildLocalPath(computerName);
            var collectedAt = DateTimeOffset.UtcNow;

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EventLogSession session = BuildSession(computerName, domain, username, password);

                using (session)
                {
                    // ExportLog exports the entire named log channel to a self-contained .evtx file.
                    // The wildcard query "*" retrieves all events.
                    session.ExportLog(
                        path: "Security",
                        pathType: PathType.LogName,
                        query: "*",
                        targetFilePath: localPath);
                }
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(localPath);
            var sha256 = await ComputeSha256Async(localPath, cancellationToken);
            var manifestPath = await WriteManifestAsync(
                localPath,
                computerName,
                collectedAt,
                fileInfo.Length,
                sha256,
                username,
                cancellationToken);

            return new RemoteLogCollectionResult(localPath, manifestPath, computerName, collectedAt, fileInfo.Length, sha256);
        }

        private static EventLogSession BuildSession(
            string computerName,
            string? domain,
            string? username,
            SecureString? password)
        {
            bool hasCredentials = !string.IsNullOrWhiteSpace(username) && password != null;

            return hasCredentials
                ? new EventLogSession(
                    computerName,
                    domain ?? ".",
                    username,
                    password,
                    SessionAuthentication.Default)
                : new EventLogSession(computerName);
        }

        private static string BuildLocalPath(string computerName)
        {
            var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var saveFolder = Path.Combine(docsFolder, "SecParser", "CollectedLogs");
            Directory.CreateDirectory(saveFolder);

            // Sanitise computer name so it is safe to use in a file name
            var safeName = string.Concat(computerName.Split(Path.GetInvalidFileNameChars()));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss'Z'");

            return Path.Combine(saveFolder, $"{safeName}_Security_{timestamp}.evtx");
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }

        private static async Task<string> WriteManifestAsync(
            string logFilePath,
            string computerName,
            DateTimeOffset collectedAt,
            long fileSizeBytes,
            string sha256,
            string? username,
            CancellationToken cancellationToken)
        {
            var manifestPath = Path.ChangeExtension(logFilePath, ".manifest.txt");
            var builder = new StringBuilder();
            builder.AppendLine("SecParser Remote Collection Manifest");
            builder.AppendLine($"ComputerName: {computerName}");
            builder.AppendLine($"CollectedAtUtc: {collectedAt:O}");
            builder.AppendLine($"LogFileName: {Path.GetFileName(logFilePath)}");
            builder.AppendLine($"FileSizeBytes: {fileSizeBytes}");
            builder.AppendLine($"Sha256: {sha256}");
            builder.AppendLine($"CredentialMode: {(string.IsNullOrWhiteSpace(username) ? "CurrentUser" : "ExplicitUser")}");
            builder.AppendLine("Channel: Security");
            builder.AppendLine("Query: *");

            await File.WriteAllTextAsync(manifestPath, builder.ToString(), cancellationToken);
            return manifestPath;
        }
    }
}
