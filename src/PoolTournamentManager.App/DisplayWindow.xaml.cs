using System.Windows;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

public partial class DisplayWindow : Window
{
    public DisplayWindow(DisplayWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
