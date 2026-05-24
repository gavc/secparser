namespace SecParser.UI.Services;

/// <summary>
/// Abstraction over Win32 Open/Save file dialogs so command handlers can be
/// unit-tested without touching the real shell.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Prompts the user for a file to open. Returns the full path, or
    /// <c>null</c> if the user cancelled.
    /// </summary>
    string? PromptOpenFile(string filter, string title);

    /// <summary>
    /// Prompts the user for a save location. Returns the full path, or
    /// <c>null</c> if the user cancelled.
    /// </summary>
    string? PromptSaveFile(string filter, string defaultExtension, string suggestedFileName);
}
