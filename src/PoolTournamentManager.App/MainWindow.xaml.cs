using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ThemeService _themeService;
    private DisplayWindow? _displayWindow;

    public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider, ThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _themeService = themeService;
        DataContext = _viewModel;
        SourceInitialized += (_, _) => _themeService.ApplyTitleBar(this);
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadPlayersAsync();
        await _viewModel.Tournament.InitializeAsync();
    }

    private void OpenDisplayWindowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_displayWindow is null || !_displayWindow.IsLoaded)
        {
            _displayWindow = _serviceProvider.GetRequiredService<DisplayWindow>();
            _displayWindow.Closed += (_, _) => _displayWindow = null;
            _displayWindow.Show();
        }
        else
        {
            _displayWindow.Activate();
        }
    }
}
