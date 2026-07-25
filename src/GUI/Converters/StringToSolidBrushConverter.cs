using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DivinityModManager.Converters;

public class StringToSolidBrushConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string str)
		{
			var color = (Color)ColorConverter.ConvertFromString(str);
			if (parameter is string alphaText &&
				Byte.TryParse(alphaText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var alpha))
			{
				color.A = alpha;
			}
			return new SolidColorBrush(color);
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
