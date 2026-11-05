using System.Globalization;
using System.Windows.Data;

namespace LCWaikiki.POS.WpfClient.Converters;

/// <summary>
/// Decimal degerleri "1.234,56 TL" formatinda gosterir.
/// </summary>
public class DecimalToCurrencyConverter : IValueConverter
{
    private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is decimal d ? $"{d.ToString("N2", TrCulture)} TL" : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}