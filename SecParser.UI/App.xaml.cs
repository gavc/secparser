using System.Windows;

namespace SecParser.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI-thread unhandled exceptions
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Non-UI-thread unhandled exceptions (e.g. background threads)
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(ex?.ToString() ?? args.ExceptionObject.ToString(), "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // Unobserved Task exceptions (fire-and-forget tasks)
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            args.SetObserved();
        };
    }
}

