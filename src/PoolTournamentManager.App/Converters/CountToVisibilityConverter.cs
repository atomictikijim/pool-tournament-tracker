using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PoolTournamentManager.App.Converters;

/// <summary>Visible when the bound collection's Count is greater than zero, Collapsed otherwise -
/// used to hide a panel (e.g. round-robin standings) entirely for formats that never populate it.</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
