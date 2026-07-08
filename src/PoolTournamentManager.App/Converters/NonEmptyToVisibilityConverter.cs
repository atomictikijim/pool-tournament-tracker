using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PoolTournamentManager.App.Converters;

/// <summary>Visible when the bound string is non-null and non-whitespace, Collapsed otherwise -
/// used to show an inline validation-error line in the modal editors only when there is a
/// message to display.</summary>
public class NonEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
