using System.Windows;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;

namespace PoolTournamentManager.App;

/// <summary>
/// Modal dialog for creating or editing a single player. The owning window supplies a
/// <see cref="PlayerEditorViewModel"/> pre-loaded for the New/Edit case; on Save the dialog
/// validates and only closes with <c>DialogResult == true</c> when the input is valid, so the
/// caller can trust the editor's values and simply persist them.
/// </summary>
public partial class PlayerEditorWindow : Window
{
    private readonly PlayerEditorViewModel _viewModel;

    public PlayerEditorWindow(PlayerEditorViewModel viewModel, ThemeService themeService)
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
