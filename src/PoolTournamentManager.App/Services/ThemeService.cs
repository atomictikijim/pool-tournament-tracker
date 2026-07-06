using System.Windows;
using Microsoft.Win32;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Keeps the app's color palette (see Themes/Palette.Light.xaml and Themes/Palette.Dark.xaml)
/// in sync with the Windows "choose your color mode" setting - both at startup and live, if the
/// user changes it while the app is running. Every themed brush is a DynamicResource, so
/// swapping the palette dictionary repaints the whole app instantly with no restart needed.
/// </summary>
public class ThemeService
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string LightPaletteUri = "Themes/Palette.Light.xaml";
    private const string DarkPaletteUri = "Themes/Palette.Dark.xaml";

    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        ApplyCurrentWindowsTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
        {
            ApplyCurrentWindowsTheme();
        }
    }

    private void ApplyCurrentWindowsTheme()
    {
        Application.Current.Dispatcher.Invoke(() => ApplyTheme(IsWindowsAppsLightTheme()));
    }

    private static bool IsWindowsAppsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        var value = key?.GetValue("AppsUseLightTheme");
        return value is int intValue ? intValue != 0 : true;
    }

    private static void ApplyTheme(bool light)
    {
        var targetUri = light ? LightPaletteUri : DarkPaletteUri;
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var current = dictionaries.Count > 0 ? dictionaries[0] : null;
        if (current?.Source?.OriginalString == targetUri)
        {
            return;
        }

        dictionaries[0] = new ResourceDictionary { Source = new Uri(targetUri, UriKind.Relative) };
    }
}
