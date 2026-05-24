using System;

namespace SecParser.Core.Parsers;

/// <summary>
/// Result of a remote Security event log collection operation, including the local
/// file path, the SHA-256 manifest path, and integrity metadata.
/// </summary>
public sealed record RemoteLogCollectionResult(
    string LogFilePath,
    string ManifestFilePath,
    string ComputerName,
    DateTimeOffset CollectedAt,
    long FileSizeBytes,
    string Sha256,
    string Authentication);
