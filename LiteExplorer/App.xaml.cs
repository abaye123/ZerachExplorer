using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.MVVM.Views.Windows;
using LiteExplorer.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;

namespace LiteExplorer;

public partial class App
{
    private const int StartupFailureExitCode = 1;

    private bool hasStarted;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLogger.LogException(args.ExceptionObject as Exception, $"AppDomain.UnhandledException (IsTerminating: {args.IsTerminating})");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogger.LogException(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 3 && e.Args[0] == ProtectedConfigurationStore.ApplyArgument)
        {
            Shutdown(ProtectedConfigurationStore.RunElevatedSave(e.Args[1], e.Args[2]));
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var configuration = provider.GetRequiredService<IConfigurationService>();
        configuration.ApplyToPolicy(provider.GetRequiredService<IPathPolicy>());
        KioskController.Instance.Configure(configuration.Configuration);
        LogLimits.Apply(configuration.Configuration);

        provider.GetRequiredService<MainWindow>().Show();
        hasStarted = true;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLogger.LogException(e.Exception, "DispatcherUnhandledException");
        e.Handled = true;

        string message = $"אירעה שגיאה בלתי צפויה במערכת:\n{e.Exception.Message}\n\nפרטי השגיאה נרשמו לקובץ יומן:\n{CrashLogger.LogFilePath}";
        if (hasStarted)
        {
            MessageDialog.Show(MainWindow, message, "שגיאת מערכת", MessageKind.Error);
            return;
        }

        // Nothing of the application can be relied on yet, and without a main window only an invisible process would remain.
        MessageBox.Show(message, "שגיאת מערכת", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        Shutdown(StartupFailureExitCode);
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService>(_ => new ConfigurationService());
        // Constructed explicitly: the container would inject an empty extension list.
        services.AddSingleton<IPathPolicy>(_ => new PathPolicy());
        services.AddSingleton<IAuditService>(_ => AuditService.Instance);
        services.AddSingleton<IDeviceNotificationService>(_ => DeviceNotificationService.Instance);
        services.AddSingleton<ISessionService>(_ => SessionService.Instance);
        services.AddSingleton<IFileTransferService, FileTransferService>();
        services.AddSingleton<IFileDeletionService, FileDeletionService>();
        services.AddSingleton<IFileRenameService, FileRenameService>();
        services.AddSingleton<IFolderCreationService, FolderCreationService>();
        services.AddSingleton<OperationsViewModel>();
        services.AddSingleton<ViewSettings>(_ => new ViewSettings());
        services.AddSingleton<IDriveEjectService, DriveEjectService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IDirectoryLoader, DirectoryLoader>();
        services.AddSingleton<ExplorerServices>();

        services.AddTransient<TabContentViewModel>();
        services.AddSingleton<MainWindowViewModel>(provider => new(provider.GetRequiredService<TabContentViewModel>));
        services.AddSingleton<MainWindow>(provider =>
            new(provider.GetRequiredService<IConfigurationService>(), provider.GetRequiredService<IPathPolicy>(),
                provider.GetRequiredService<OperationsViewModel>(), provider.GetRequiredService<IDialogService>())
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>()
            });
    }
}
