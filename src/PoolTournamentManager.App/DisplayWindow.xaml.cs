using System.Windows;
using System.Windows.Input;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

public partial class DisplayWindow : Window
{
    private readonly DisplayWindowViewModel _viewModel;

    private bool _isFullScreen;
    private WindowState _preFullScreenState;
    private WindowStyle _preFullScreenStyle;
    private ResizeMode _preFullScreenResizeMode;

    public DisplayWindow(DisplayWindowViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => themeService.ApplyTitleBar(this);
    }

    /// <summary>F11 toggles full screen; Esc leaves it (but never minimizes an already-windowed
    /// display). Handled at the window level so it works regardless of which control has focus.</summary>
    private void DisplayWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
        }
    }

    private void FullScreenButton_OnClick(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            ExitFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    /// <summary>Borderless, maximized true full screen (covers the taskbar). Remembers the prior
    /// window chrome/state so <see cref="ExitFullScreen"/> can restore it exactly. The
    /// Normal-then-Maximized bounce forces a re-maximize so a window that was already maximized
    /// still expands over the taskbar once the border is removed.</summary>
    private void EnterFullScreen()
    {
        _preFullScreenState = WindowState;
        _preFullScreenStyle = WindowStyle;
        _preFullScreenResizeMode = ResizeMode;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Normal;
        WindowState = WindowState.Maximized;
        _isFullScreen = true;
        FullScreenButton.Content = "Exit Full Screen (Esc)";
    }

    private void ExitFullScreen()
    {
        WindowStyle = _preFullScreenStyle;
        ResizeMode = _preFullScreenResizeMode;
        WindowState = _preFullScreenState;
        _isFullScreen = false;
        FullScreenButton.Content = "Full Screen (F11)";
    }

    /// <summary>Ctrl+MouseWheel over the bracket zooms it, mirroring the +/- buttons; a plain
    /// scroll is left alone so the ScrollViewer's normal panning still works.</summary>
    private void BracketScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => HandleZoomWheel(e);

    /// <summary>Ctrl+MouseWheel over the round-robin columns zooms them, same as the bracket.</summary>
    private void RoundRobinScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => HandleZoomWheel(e);

    private void HandleZoomWheel(MouseWheelEventArgs e)
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

    /// <summary>"Fit" zooms so the whole bracket/schedule - however large - fits inside the visible
    /// area, useful for eyeballing its overall shape/progress. The round-robin content has no
    /// precomputed layout size, so its (unscaled) extent is read off the rendered element.</summary>
    private void FitBracketButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ShowFlatRounds)
        {
            _viewModel.FitToViewport(
                RoundRobinContent.ActualWidth, RoundRobinContent.ActualHeight,
                RoundRobinScrollViewer.ViewportWidth, RoundRobinScrollViewer.ViewportHeight);
        }
        else
        {
            _viewModel.FitBracketToViewport(BracketScrollViewer.ViewportWidth, BracketScrollViewer.ViewportHeight);
        }
    }
}
