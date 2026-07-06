using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoolTournamentManager.Core.Interfaces;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _appScope;
    private Services.ThemeService? _themeService;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        _appScope = _serviceProvider.CreateScope();

        var dbContext = _appScope.ServiceProvider.GetRequiredService<PoolTournamentDbContext>();
        dbContext.Database.Migrate();

        base.OnStartup(e);

        _themeService = _appScope.ServiceProvider.GetRequiredService<Services.ThemeService>();
        _themeService.Start();

        var mainWindow = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        var databasePath = PoolTournamentDbContextFactory.GetDefaultDatabasePath();

        services.AddDbContext<PoolTournamentDbContext>(
            options => options.UseSqlite($"Data Source={databasePath}"));

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddSingleton<BracketGenerationService>();
        services.AddSingleton<Services.TournamentStateService>();
        services.AddSingleton<Services.ThemeService>();
        services.AddTransient<ViewModels.TournamentViewModel>();
        services.AddTransient<ViewModels.MainWindowViewModel>();
        services.AddTransient<ViewModels.DisplayWindowViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<DisplayWindow>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{GetDeepestMessage(e.Exception)}",
            "Pool Tournament Manager - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception is not null)
        {
            LogException(exception);
        }
        MessageBox.Show(
            $"A fatal error occurred:\n\n{(exception is null ? "Unknown error" : GetDeepestMessage(exception))}",
            "Pool Tournament Manager - Fatal Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string GetDeepestMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }
        return current.Message;
    }

    private static void LogException(Exception exception)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PoolTournamentManager", "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "error.log");
            File.AppendAllText(logPath, $"{DateTime.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never throw.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themeService?.Stop();
        _appScope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
