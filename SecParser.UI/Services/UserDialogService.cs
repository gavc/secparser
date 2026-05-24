using System.Windows;

namespace SecParser.UI.Services;

/// <summary>
/// WPF-backed implementation of <see cref="IUserDialogService"/>.
/// </summary>
public sealed class UserDialogService : IUserDialogService
{
    public void ShowInfo(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public RemoteLogConnectionRequest? PromptRemoteLogConnection()
    {
        var dialog = new RemoteLogDialog
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ComputerName))
        {
            return null;
        }

        return new RemoteLogConnectionRequest(
            dialog.ComputerName!,
            dialog.Domain,
            dialog.Username,
            dialog.Password);
    }
}
