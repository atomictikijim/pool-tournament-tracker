using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Keeps the app's color palette (see Themes/Palette.{Scheme}.Light.xaml and
/// Themes/Palette.{Scheme}.Dark.xaml) in sync with both the selected <see cref="ColorScheme"/>
/// (a user preference, persisted to disk) and the Windows "choose your color mode" setting (live-
/// tracked, not persisted - it's the OS's own setting). Every themed brush is a DynamicResource,
/// so changing either one repaints the whole app instantly with no restart needed. Also colors
/// each window's native title bar to match (WPF resources can't reach the title bar itself - see
/// TitleBarColorizer), reapplying to every open window on any change and exposing ApplyTitleBar
/// for a newly-created window to call once its HWND exists.
/// </summary>
public partial class ThemeService : ObservableObject
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private bool _started;
    private bool _isLight = true;

    [ObservableProperty]
    private AppColorScheme _colorScheme = AppColorScheme.Green;

    public IReadOnlyList<AppColorScheme> AvailableColorSchemes { get; } = Enum.GetValues<AppColorScheme>();

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        // Bypass the generated property setter (direct field assignment) so loading the
        // persisted preference doesn't itself trigger OnColorSchemeChanged's re-save - it just
        // needs to notify bound UI (e.g. the Settings tab's swatches) of the loaded value.
        _colorScheme = AppSettingsStore.LoadColorScheme();
        OnPropertyChanged(nameof(ColorScheme));

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

    [RelayCommand]
    private void SelectColorScheme(AppColorScheme scheme)
    {
        ColorScheme = scheme;
    }

    partial void OnColorSchemeChanged(AppColorScheme value)
    {
        AppSettingsStore.SaveColorScheme(value);
        ApplyTheme(_isLight);
    }

    /// <summary>Colors one window's title bar to match the currently-active theme. Call this once
    /// the window's HWND exists (e.g. on SourceInitialized) - needed because a window opened after
    /// startup (like the Display window) won't have been included in the last theme application.</summary>
    public void ApplyTitleBar(Window window)
    {
        if (_isLight)
        {
            TitleBarColorizer.Apply(window, captionColor: null, textColor: null);
        }
        else
        {
            var resources = Application.Current.Resources;
            var captionColor = ((SolidColorBrush)resources["AccentPrimaryBrush"]).Color;
            var textColor = ((SolidColorBrush)resources["AccentPrimaryTextBrush"]).Color;
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

        var targetUri = $"Themes/Palette.{ColorScheme}.{(light ? "Light" : "Dark")}.xaml";
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

/// <summary>Tiny disk-backed store for the one user preference the app has so far (color scheme).
/// Deliberately not a general-purpose settings system - add to this only if a second setting
/// actually shows up; a single JSON file with one field doesn't need more structure than this.</summary>
internal static class AppSettingsStore
{
    // Without this, System.Text.Json serializes enums as plain integers - readable in neither
    // the persisted file nor a hand-edited one, and an easy way to silently corrupt (any
    // mismatch throws, which LoadColorScheme swallows and falls back to Green).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoolTournamentManager", "settings.json");

    public static AppColorScheme LoadColorScheme()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return AppColorScheme.Green;
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<StoredSettings>(json, JsonOptions);
            return settings?.ColorScheme ?? AppColorScheme.Green;
        }
        catch
        {
            // A corrupt or unreadable settings file should never block startup.
            return AppColorScheme.Green;
        }
    }

    public static void SaveColorScheme(AppColorScheme scheme)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(new StoredSettings { ColorScheme = scheme }, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Failing to persist a preference shouldn't crash the app - it just won't stick
            // across restarts this time.
        }
    }

    private class StoredSettings
    {
        public AppColorScheme ColorScheme { get; set; }
    }
}
