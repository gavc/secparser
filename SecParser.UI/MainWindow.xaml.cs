using System.Windows;
using System.Windows.Input;
using SecParser.UI.ViewModels;

namespace SecParser.UI;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        // DataContext is set by App.OnStartup (which injects the logger).
        // The parameterless fallback covers design-time / direct instantiation.
        if (DataContext is null)
        {
            DataContext = new MainViewModel();
        }
    }

    // Prevents the classic WPF re-open bug: when the popup is open and the user
    // clicks the ToggleButton to close it, the Popup's StaysOpen=False fires first
    // and sets IsUserFilterOpen=false, then the ToggleButton click would toggle it
    // back to true (reopening the popup).  Consuming the event here stops the toggle.
    private void UserFilterToggle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsUserFilterOpen)
        {
            ViewModel.IsUserFilterOpen = false;
            e.Handled = true;
        }
    }
}