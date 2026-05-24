using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecParser.Core.Abstractions;
using SecParser.Core.Diagnostics;
using SecParser.Core.Exporters;
using SecParser.Core.Parsers;
using SecParser.UI.Configuration;
using SecParser.UI.Services;
using SecParser.UI.ViewModels;

namespace SecParser.UI;

public partial class App : Application
{
    private const string LogCategory = nameof(App);

    private IAppLogger _logger = NullAppLogger.Instance;
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = CreateLogger();
        _logger.Information(LogCategory, $"SecParser starting (version {GetAppVersion()}).");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = BuildHost(_logger);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Information(LogCategory, $"SecParser exiting (code {e.ApplicationExitCode}).");

        if (_host is not null)
        {
            try
            {
                _host.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                _logger.Warning(LogCategory, "Host disposal raised an exception.", ex);
            }
            _host = null;
        }

        base.OnExit(e);
    }

    private static IHost BuildHost(IAppLogger logger)
    {
        var builder = Host.CreateApplicationBuilder();

        // Options (Phase 3 — tunables; Phase 5 will hydrate from settings)
        builder.Services.AddSingleton(new SecParserOptions());

        // Core diagnostics
        builder.Services.AddSingleton(logger);

        // Core services
        builder.Services.AddSingleton<IEvtxLogParser, EvtxLogParser>();
        builder.Services.AddSingleton<IRemoteLogCollector, RemoteLogCollector>();
        builder.Services.AddSingleton<IRecordExporter, ExcelExporter>();
        builder.Services.AddSingleton<IRecordExporter, PdfExporter>();

        // UI services
        builder.Services.AddSingleton<IFileDialogService, FileDialogService>();
        builder.Services.AddSingleton<IUserDialogService, UserDialogService>();
        builder.Services.AddSingleton<ILogLoadingService, LogLoadingService>();
        builder.Services.AddSingleton<IExportCoordinator, ExportCoordinator>();

        // View-models and windows
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>(sp =>
        {
            var window = new MainWindow
            {
                DataContext = sp.GetRequiredService<MainViewModel>()
            };
            return window;
        });

        return builder.Build();
    }

    private static IAppLogger CreateLogger()
    {
        try
        {
            return new FileAppLogger(FileAppLogger.GetDefaultLogFolder());
        }
        catch (Exception ex) when (ex is System.IO.IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException)
        {
            // Logger setup must never crash the app; fall back to no-op.
            return NullAppLogger.Instance;
        }
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(App).Assembly;
        var name = assembly.GetName();
        return name.Version?.ToString() ?? "unknown";
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        var correlationId = Guid.NewGuid();
        _logger.Error(LogCategory, "Unhandled UI-thread exception.", args.Exception, correlationId);
        ShowFatal("An unexpected error occurred on the UI thread.", args.Exception, correlationId, "Unhandled Error");
        args.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        var correlationId = Guid.NewGuid();
        var ex = args.ExceptionObject as Exception;
        _logger.Error(LogCategory, $"Unhandled AppDomain exception (terminating={args.IsTerminating}).", ex, correlationId);
        ShowFatal("A fatal background error occurred.", ex, correlationId, "Fatal Error");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        var correlationId = Guid.NewGuid();
        _logger.Error(LogCategory, "Unobserved task exception.", args.Exception, correlationId);
        args.SetObserved();
    }

    private static void ShowFatal(string headline, Exception? ex, Guid correlationId, string title)
    {
        var message = $"{headline}\n\n{ex?.Message ?? "(no message)"}\n\nCorrelation ID: {correlationId:N}";
        try
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (InvalidOperationException)
        {
            // Dispatcher might be torn down — best effort only.
        }
    }
}
