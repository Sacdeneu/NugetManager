using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NugetManager.ViewModels;
using NugetManager.Views;
using NugetManager.Services;
using NugetManager.Settings;
using BeyondWPF.Common.Services;
using BeyondWPF.Core.Abstractions;
using BeyondWPF.Core.Enums;
using BeyondWPF.Core.Settings;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace NugetManager;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "Exception détaillée", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Core BeyondWPF Services
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<INotificationService, NativeNotificationService>();

                // Settings
                var settings = new AppSettings();
                settings.LoadSettings<AppSettings>();
                services.AddSingleton<ISetting>(settings);
                services.AddSingleton(settings);

                // App Services
                services.AddSingleton<PackageStorageService>();
                services.AddSingleton<NuGetServerService>();
                services.AddSingleton<NuGetApiService>();
                services.AddSingleton<ProjectService>();
                services.AddSingleton<TagService>();
                services.AddSingleton<PackagerService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<PackagesViewModel>();
                services.AddSingleton<ProjectsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<PackagerViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
                services.AddTransient<PackagesView>();
                services.AddSingleton<ProjectsView>();
                services.AddTransient<SettingsView>();
                services.AddTransient<PackagerView>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            Current.Dispatcher.Invoke(() =>
            {
                if (Current.MainWindow is { WindowState: WindowState.Minimized })
                    Current.MainWindow.WindowState = WindowState.Normal;
                Current.MainWindow?.Activate();
            });
        };

        await _host.StartAsync();

        try
        {
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            var settings = _host.Services.GetRequiredService<AppSettings>();
            themeService.ApplySystemAccent(settings.UseAccentColor);
            themeService.ApplyTheme(SystemTheme.Dark);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur initialisation thème : {ex.Message}");
        }

        // Initialize storage then start the NuGet server
        try
        {
            var storage = _host.Services.GetRequiredService<PackageStorageService>();
            await storage.InitializeAsync();

            var server = _host.Services.GetRequiredService<NuGetServerService>();
            await server.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur démarrage serveur NuGet : {ex.Message}");
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var server = _host.Services.GetService<NuGetServerService>();
        if (server != null)
            await server.StopAsync();

        using (_host)
        {
            await _host.StopAsync();
        }
        base.OnExit(e);
    }
}
