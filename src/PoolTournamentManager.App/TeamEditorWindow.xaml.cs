using System.Windows;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

/// <summary>
/// Modal dialog for creating or editing a single team. Mirrors <see cref="PlayerEditorWindow"/>:
/// the owner supplies a pre-loaded <see cref="TeamEditorViewModel"/>, and Save only closes with
/// <c>DialogResult == true</c> once the input validates.
/// </summary>
public partial class TeamEditorWindow : Window
{
    private readonly TeamEditorViewModel _viewModel;

    public TeamEditorWindow(TeamEditorViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => themeService.ApplyTitleBar(this);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryValidate())
        {
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
