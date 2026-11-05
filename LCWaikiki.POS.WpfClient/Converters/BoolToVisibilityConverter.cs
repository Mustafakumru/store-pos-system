using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LCWaikiki.POS.WpfClient.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is true ? Visibility.Visible : Visibility.Collapsed;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is Visibility.Visible;
}

public class StringToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> string.IsNullOrEmpty(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
/// <summary>
/// Sayi, ConverterParameter'daki esikten (varsayilan 0) BUYUKSE Visible.
/// Orn. "MissingReasonCount > 1" icin ConverterParameter=1.
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		var threshold = parameter is string s && int.TryParse(s, out var t) ? t : 0;
		return value is int n && n > threshold ? Visibility.Visible : Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}

/// <summary>Bool degerin tersi (IsEnabled gibi baglamalar icin).</summary>
public class InverseBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is not true;

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is not true;
}
