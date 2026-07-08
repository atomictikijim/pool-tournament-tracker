using System.Globalization;
using System.Windows.Data;

namespace PoolTournamentManager.App.Converters;

/// <summary>True when the bound value is non-null, false otherwise - used to enable a control
/// (e.g. the Delete Tournament button) only while something is selected.</summary>
public class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
