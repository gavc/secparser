using Microsoft.Win32;

namespace SecParser.UI.Services;

/// <summary>
/// Default <see cref="IFileDialogService"/> implementation backed by the
/// Win32 <see cref="OpenFileDialog"/> / <see cref="SaveFileDialog"/> shells.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    public string? PromptOpenFile(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PromptSaveFile(string filter, string defaultExtension, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExtension,
            FileName = suggestedFileName
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
