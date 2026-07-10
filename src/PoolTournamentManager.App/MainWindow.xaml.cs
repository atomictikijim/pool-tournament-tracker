using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PoolTournamentManager.App.Help;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;

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
        await _viewModel.LoadTeamsAsync();
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
            _viewModel.Tournament.ZoomBracketInCommand.Execute(null);
        }
        else
        {
            _viewModel.Tournament.ZoomBracketOutCommand.Execute(null);
        }
    }

    /// <summary>"Fit" zooms so the whole bracket - however large - fits inside the ScrollViewer's
    /// currently visible area, useful for eyeballing a big bracket's overall shape/progress.</summary>
    private void FitBracketButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Tournament.FitBracketToViewport(BracketScrollViewer.ViewportWidth, BracketScrollViewer.ViewportHeight);
    }

    // ----- Players tab: create/edit in a modal window, multi-select delete with confirmation -----

    private async void NewPlayerButton_OnClick(object sender, RoutedEventArgs e)
    {
        var editor = new PlayerEditorViewModel { Title = "New Player" };
        editor.Reset();
        if (ShowPlayerEditor(editor))
        {
            await _viewModel.CreatePlayerAsync(editor);
        }
    }

    private async void EditPlayerButton_OnClick(object sender, RoutedEventArgs e)
    {
        await EditSelectedPlayerAsync();
    }

    private async void PlayersGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await EditSelectedPlayerAsync();
    }

    private async Task EditSelectedPlayerAsync()
    {
        var target = _viewModel.SelectedPlayer;
        if (target is null)
        {
            _viewModel.StatusMessage = "Select a player to edit.";
            return;
        }

        var editor = new PlayerEditorViewModel { Title = "Edit Player" };
        editor.LoadFrom(target);
        if (ShowPlayerEditor(editor))
        {
            await _viewModel.UpdatePlayerAsync(target, editor);
        }
    }

    private bool ShowPlayerEditor(PlayerEditorViewModel editor)
    {
        var window = new PlayerEditorWindow(editor, _themeService) { Owner = this };
        return window.ShowDialog() == true;
    }

    private async void DeletePlayersButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = PlayersGrid.SelectedItems.Cast<Player>().ToList();
        if (selected.Count == 0)
        {
            _viewModel.StatusMessage = "Select one or more players to delete.";
            return;
        }

        if (Confirm(
                selected.Count == 1
                    ? $"Delete player \"{selected[0].FullName}\"?\n\nThis cannot be undone."
                    : $"Delete these {selected.Count} players?\n\n{string.Join("\n", selected.Select(p => p.FullName))}\n\nThis cannot be undone."))
        {
            await _viewModel.DeletePlayersAsync(selected);
        }
    }

    // ----- Teams tab: same pattern as Players -----

    private async void NewTeamButton_OnClick(object sender, RoutedEventArgs e)
    {
        var editor = new TeamEditorViewModel { Title = "New Team" };
        editor.Reset();
        if (ShowTeamEditor(editor))
        {
            await _viewModel.CreateTeamAsync(editor);
        }
    }

    private async void EditTeamButton_OnClick(object sender, RoutedEventArgs e)
    {
        await EditSelectedTeamAsync();
    }

    private async void TeamsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await EditSelectedTeamAsync();
    }

    private async Task EditSelectedTeamAsync()
    {
        var target = _viewModel.SelectedTeam;
        if (target is null)
        {
            _viewModel.StatusMessage = "Select a team to edit.";
            return;
        }

        var editor = new TeamEditorViewModel { Title = "Edit Team" };
        editor.LoadFrom(target);
        if (ShowTeamEditor(editor))
        {
            await _viewModel.UpdateTeamAsync(target, editor);
        }
    }

    private bool ShowTeamEditor(TeamEditorViewModel editor)
    {
        var window = new TeamEditorWindow(editor, _themeService) { Owner = this };
        return window.ShowDialog() == true;
    }

    private async void DeleteTeamsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = TeamsGrid.SelectedItems.Cast<Team>().ToList();
        if (selected.Count == 0)
        {
            _viewModel.StatusMessage = "Select one or more teams to delete.";
            return;
        }

        if (Confirm(
                selected.Count == 1
                    ? $"Delete team \"{selected[0].Name}\"?\n\nThis cannot be undone."
                    : $"Delete these {selected.Count} teams?\n\n{string.Join("\n", selected.Select(t => t.Name))}\n\nThis cannot be undone."))
        {
            await _viewModel.DeleteTeamsAsync(selected);
        }
    }

    // ----- Tournament tab: delete the selected tournament, with confirmation -----

    private async void DeleteTournamentButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.Tournament.SelectedTournamentSummary;
        if (selected is null)
        {
            return;
        }

        if (Confirm($"Permanently delete tournament \"{selected.Name}\"?\n\n" +
                    "This removes its bracket, matches, tables and entrant list. " +
                    "Players and teams are not deleted. This cannot be undone."))
        {
            await _viewModel.Tournament.DeleteTournamentAsync(selected);
        }
    }

    /// <summary>Reopens the selected NotStarted tournament on the Tournament Settings tab for
    /// editing - only visible while CanEditSelectedTournament is true.</summary>
    private void EditTournamentButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.Tournament.SelectedTournamentSummary;
        if (selected is null)
        {
            return;
        }

        _viewModel.Tournament.BeginEditTournament(selected);
        _viewModel.SelectedTabIndex = 3;
    }

    // ----- Contextual help: each tab's "?" button opens a modal guide for that tab -----

    /// <summary>Opens the contextual help modal for the tab whose "?" button was clicked. The topic
    /// is carried in the button's Tag (set in XAML to a HelpTopic name), keeping one handler for all
    /// four tabs.</summary>
    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } &&
            Enum.TryParse<HelpTopic>(tag, out var topic))
        {
            var window = new HelpWindow(topic, _themeService) { Owner = this };
            window.ShowDialog();
        }
    }

    /// <summary>Opens the app-wide About modal. The button lives in the header, so this is reachable
    /// from every tab.</summary>
    private void AboutButton_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow(_themeService) { Owner = this };
        window.ShowDialog();
    }

    private bool Confirm(string message) =>
        MessageBox.Show(this, message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
}
