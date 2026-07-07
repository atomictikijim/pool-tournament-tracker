using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Keeps the app's color palette (Themes/Palette.Light.xaml and Themes/Palette.Dark.xaml) in sync
/// with the Windows "choose your color mode" setting. The app has no color schemes of its own - it
/// simply inherits whatever light/dark mode Windows is in, live-tracked (not persisted, since it's
/// the OS's own setting). Every themed brush is a DynamicResource, so a change repaints the whole
/// app instantly with no restart. Also colors each window's native title bar to match (WPF
/// resources can't reach the title bar itself - see TitleBarColorizer), reapplying to every open
/// window on any change and exposing ApplyTitleBar for a newly-created window to call once its
/// HWND exists.
/// </summary>
public partial class ThemeService : ObservableObject
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private bool _started;
    private bool _isLight = true;

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

    /// <summary>Colors one window's title bar to match the currently-active theme. Call this once
    /// the window's HWND exists (e.g. on SourceInitialized) - needed because a window opened after
    /// startup (like the Display window) won't have been included in the last theme application.</summary>
    public void ApplyTitleBar(Window window)
    {
        if (_isLight)
        {
            // Let the system draw the default light caption - it already matches a light app.
            TitleBarColorizer.Apply(window, captionColor: null, textColor: null);
        }
        else
        {
            var resources = Application.Current.Resources;
            var captionColor = ((SolidColorBrush)resources["WindowChromeBrush"]).Color;
            var textColor = ((SolidColorBrush)resources["TextPrimaryBrush"]).Color;
            TitleBarColorizer.Apply(window, captionColor, textColor);
        }
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

    private void ApplyTheme(bool light)
    {
        _isLight = light;

        var targetUri = $"Themes/Palette.{(light ? "Light" : "Dark")}.xaml";
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var current = dictionaries.Count > 0 ? dictionaries[0] : null;
        if (current?.Source?.OriginalString != targetUri)
        {
            dictionaries[0] = new ResourceDictionary { Source = new Uri(targetUri, UriKind.Relative) };
        }

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }
    }
}
