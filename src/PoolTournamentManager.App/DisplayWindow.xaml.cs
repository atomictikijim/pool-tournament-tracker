using System.Windows;
using System.Windows.Input;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

public partial class DisplayWindow : Window
{
    private readonly DisplayWindowViewModel _viewModel;

    public DisplayWindow(DisplayWindowViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => themeService.ApplyTitleBar(this);
    }

    /// <summary>Ctrl+MouseWheel over the bracket zooms it, mirroring the +/- buttons; a plain
    /// scroll is left alone so the ScrollViewer's normal panning still works.</summary>
    private void BracketScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;
        if (e.Delta > 0)
        {
            _viewModel.ZoomBracketInCommand.Execute(null);
        }
        else
        {
            _viewModel.ZoomBracketOutCommand.Execute(null);
        }
    }

    /// <summary>"Fit" zooms so the whole bracket - however large - fits inside the ScrollViewer's
    /// currently visible area, useful for eyeballing a big bracket's overall shape/progress.</summary>
    private void FitBracketButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.FitBracketToViewport(BracketScrollViewer.ViewportWidth, BracketScrollViewer.ViewportHeight);
    }
}
