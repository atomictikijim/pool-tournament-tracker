using System.Globalization;
using System.Windows.Data;

namespace PoolTournamentManager.App.Converters;

/// <summary>Returns true when the bound enum value's name matches the ConverterParameter string -
/// used to highlight the currently-selected option in a fixed set of choices (e.g. which color
/// scheme swatch is active) without a ViewModel property per option.</summary>
public class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null && parameter is not null && value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
