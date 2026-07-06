namespace PoolTournamentManager.App.Services;

/// <summary>
/// A selectable color scheme - each has a Themes/Palette.{Scheme}.Light.xaml and
/// Themes/Palette.{Scheme}.Dark.xaml resource dictionary with identical brush keys.
/// Green is the out-of-box default.
/// </summary>
public enum AppColorScheme
{
    Green,
    Red,
    Blue,
    Grey
}
