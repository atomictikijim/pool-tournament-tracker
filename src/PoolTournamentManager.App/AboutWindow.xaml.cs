using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using PoolTournamentManager.App.About;
using PoolTournamentManager.App.Services;

namespace PoolTournamentManager.App;

/// <summary>
/// Standard "About" modal, launched from the header (visible on every tab). Shows product/version
/// metadata from <see cref="AboutInfo"/> and colors its title bar to match the active theme, like
/// the other dialogs. The repository link opens in the user's default browser.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow(ThemeService themeService)
    {
        InitializeComponent();
        DataContext = new AboutInfo();
        SourceInitialized += (_, _) => themeService.ApplyTitleBar(this);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // UseShellExecute launches the URL in whatever the user's default browser is.
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
