using System.Windows;
using PoolTournamentManager.App.Help;
using PoolTournamentManager.App.Services;

namespace PoolTournamentManager.App;

/// <summary>
/// Read-only modal that shows the contextual help for a single tab (see
/// <see cref="HelpContentProvider"/>). The owning tab supplies the <see cref="HelpTopic"/>; the
/// window resolves the matching <see cref="HelpDocument"/> as its DataContext and colors its title
/// bar to match the active theme, exactly like the other editor dialogs.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow(HelpTopic topic, ThemeService themeService)
    {
        InitializeComponent();
        DataContext = HelpContentProvider.For(topic);
        SourceInitialized += (_, _) => themeService.ApplyTitleBar(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
