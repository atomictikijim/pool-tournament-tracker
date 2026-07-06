using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoolTournamentManager.Core.Interfaces;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _appScope;

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

        var mainWindow = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        var databasePath = PoolTournamentDbContextFactory.GetDefaultDatabasePath();

        services.AddDbContext<PoolTournamentDbContext>(
            options => options.UseSqlite($"Data Source={databasePath}"));

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddTransient<ViewModels.MainWindowViewModel>();
        services.AddTransient<MainWindow>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred and the application needs to close:\n\n{e.Exception.Message}",
            "Pool Tournament Manager - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"A fatal error occurred:\n\n{exception?.Message}",
            "Pool Tournament Manager - Fatal Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appScope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
