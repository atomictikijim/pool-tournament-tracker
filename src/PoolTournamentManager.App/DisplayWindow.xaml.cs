using System.Windows;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

public partial class DisplayWindow : Window
{
    public DisplayWindow(DisplayWindowViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => themeService.ApplyTitleBar(this);
    }
}
