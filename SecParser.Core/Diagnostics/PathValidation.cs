using System;
using System.IO;
using System.Linq;

namespace SecParser.Core.Diagnostics;

/// <summary>
/// Centralised, security-focused validation helpers used by parsers,
/// exporters, and the remote-log collector. Methods throw
/// <see cref="ArgumentException"/> / <see cref="InvalidOperationException"/>
/// on rejection so callers do not have to duplicate guard logic.
/// </summary>
public static class PathValidation
{
    /// <summary>1 GiB is the largest evtx file we will read; the user's
    /// org caps collected logs at that size and going larger risks running
    /// out of address space when loading into in-memory lists.</summary>
    public const long MaxEvtxFileSizeBytes = 1L * 1024 * 1024 * 1024;

    private static readonly string[] ReservedWindowsNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Validates an EVTX input file: must exist, end in <c>.evtx</c> (case
    /// insensitive), be under <see cref="MaxEvtxFileSizeBytes"/>, and start
    /// with the binary-XML <c>ElfFile\0</c> magic header.
    /// </summary>
    public static void EnsureValidEvtxFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Event log file not found.", filePath);
        }

        if (!string.Equals(Path.GetExtension(filePath), ".evtx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"File '{Path.GetFileName(filePath)}' does not have a .evtx extension.");
        }

        var size = new FileInfo(filePath).Length;
        if (size > MaxEvtxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Event log file is {size:N0} bytes; the {MaxEvtxFileSizeBytes:N0}-byte safety limit prevents loading.");
        }

        // EVTX magic header: 'E','l','f','F','i','l','e',0x00.
        Span<byte> header = stackalloc byte[8];
        using (var stream = File.OpenRead(filePath))
        {
            var read = stream.Read(header);
            if (read < header.Length
                || header[0] != (byte)'E'
                || header[1] != (byte)'l'
                || header[2] != (byte)'f'
                || header[3] != (byte)'F'
                || header[4] != (byte)'i'
                || header[5] != (byte)'l'
                || header[6] != (byte)'e'
                || header[7] != 0x00)
            {
                throw new InvalidOperationException(
                    $"File '{Path.GetFileName(filePath)}' is not a valid EVTX file (missing 'ElfFile' magic header).");
            }
        }
    }

    /// <summary>
    /// Validates an export destination path: must have the expected
    /// extension and must not target a reserved Windows device name
    /// (CON/PRN/AUX/NUL/COM#/LPT#).
    /// </summary>
    public static void EnsureValidExportPath(string filePath, string expectedExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedExtension);

        if (!string.Equals(Path.GetExtension(filePath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Export path must end with '{expectedExtension}'.", nameof(filePath));
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        foreach (var reserved in ReservedWindowsNames)
        {
            if (string.Equals(nameWithoutExt, reserved, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Export file name '{nameWithoutExt}' is a reserved Windows device name.", nameof(filePath));
            }
        }
    }

    /// <summary>
    /// Validates that <paramref name="computerName"/> is a syntactically
    /// plausible DNS name or IP literal. Rejects empty input, anything
    /// longer than 253 characters (the DNS limit), and anything that
    /// <see cref="Uri.CheckHostName"/> classifies as unknown.
    /// </summary>
    public static string NormalizeAndValidateHost(string computerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(computerName);

        var trimmed = computerName.Trim();
        if (trimmed.Length > 253)
        {
            throw new ArgumentException("Computer name exceeds the 253-character DNS limit.", nameof(computerName));
        }

        var kind = Uri.CheckHostName(trimmed);
        if (kind == UriHostNameType.Unknown)
        {
            throw new ArgumentException(
                $"'{trimmed}' is not a valid DNS host name or IP address.", nameof(computerName));
        }

        return trimmed;
    }

    /// <summary>
    /// Combines <paramref name="root"/> with <paramref name="candidate"/>
    /// segments and verifies the resolved full path stays inside
    /// <paramref name="root"/>. Defends against directory-traversal
    /// payloads in user-supplied names.
    /// </summary>
    public static string CombineAndEnsureUnderRoot(string root, params string[] candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(candidate);

        var rootFull = Path.GetFullPath(root);
        var combined = Path.Combine(new[] { rootFull }.Concat(candidate).ToArray());
        var full = Path.GetFullPath(combined);

        var rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Resolved path escapes the expected output folder.");
        }

        return full;
    }
}
