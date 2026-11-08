using LiteExplorer.MVVM.ViewModels;
using LiteExplorer.MVVM.Views.Windows;
using LiteExplorer.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LiteExplorer;

public partial class App
{
    public static IServiceProvider Container { get; private set; }

    public App()
    {
        SetupExceptionHandling();
        RegisterServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Container.GetService<MainWindow>()?.Show();
    }

    private void SetupExceptionHandling()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                CrashLogger.LogException(ex, $"AppDomain.UnhandledException (IsTerminating: {args.IsTerminating})");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            CrashLogger.LogException(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLogger.LogException(e.Exception, "DispatcherUnhandledException");

        try
        {
            string message = $"אירעה שגיאה בלתי צפויה במערכת:\n{e.Exception.Message}\n\nפרטי השגיאה נרשמו לקובץ יומן:\n{CrashLogger.LogFilePath}";
            MessageBox.Show(message, "שגיאת מערכת", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        catch
        {
        }

        e.Handled = true;
    }

    private static void RegisterServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPathPolicy, PathPolicy>();
        services.AddSingleton<IDriveEjectService, DriveEjectService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>(provider => new()
        {
            DataContext = provider.GetService<MainWindowViewModel>()
        });

        services.AddTransient<TabContentViewModel>();

        Container = services.BuildServiceProvider();
    }
}
