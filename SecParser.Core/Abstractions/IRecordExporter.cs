using System.Collections.Generic;
using System.Threading.Tasks;
using SecParser.Core.Models;

namespace SecParser.Core.Abstractions;

/// <summary>
/// Exports a set of <see cref="SecurityEventRecord"/> instances to an external file
/// (Excel, PDF, etc.). Implementations are registered with DI and surfaced by the UI
/// via their <see cref="DisplayName"/>, <see cref="FilterMask"/>, and
/// <see cref="DefaultExtension"/> metadata.
/// </summary>
public interface IRecordExporter
{
    /// <summary>Human-readable name used in menu items, e.g. "Excel Workbook".</summary>
    string DisplayName { get; }

    /// <summary>WPF file-dialog filter string, e.g. "Excel Workbook (*.xlsx)|*.xlsx".</summary>
    string FilterMask { get; }

    /// <summary>Default file extension including the leading dot, e.g. ".xlsx".</summary>
    string DefaultExtension { get; }

    /// <summary>Writes the supplied records to <paramref name="filePath"/>.</summary>
    Task ExportAsync(IEnumerable<SecurityEventRecord> records, string filePath);
}
