namespace SecParser.UI.Services;

/// <summary>
/// Abstraction over user-facing dialog boxes (MessageBox + the remote-log dialog)
/// so that view-models do not depend on WPF dialog classes directly. Implementations
/// must marshal calls onto the UI thread.
/// </summary>
public interface IUserDialogService
{
    void ShowInfo(string message, string title);
    void ShowWarning(string message, string title);
    void ShowError(string message, string title);

    /// <summary>
    /// Displays the remote-log connection dialog and returns the user's input,
    /// or <c>null</c> if they cancelled.
    /// </summary>
    RemoteLogConnectionRequest? PromptRemoteLogConnection();
}
