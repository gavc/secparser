namespace SecParser.UI.Configuration;

/// <summary>
/// Tunable values used by the UI layer. Held as a single options object so
/// the previously hard-coded magic numbers can be swapped out (and later
/// driven from settings persistence in Phase 5).
/// </summary>
public sealed class SecParserOptions
{
    /// <summary>
    /// Number of records buffered on the background thread before they are
    /// flushed to the UI collections.
    /// </summary>
    public int LoadBatchSize { get; init; } = 1000;

    /// <summary>
    /// Number of records displayed per page in the events grid.
    /// </summary>
    public int PageSize { get; init; } = 1000;

    /// <summary>
    /// Maximum length of the selected-users summary string shown in the
    /// filter toggle before an ellipsis is appended.
    /// </summary>
    public int UserSummaryEllipsisLength { get; init; } = 40;
}
