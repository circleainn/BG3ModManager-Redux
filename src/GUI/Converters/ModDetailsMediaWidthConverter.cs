using System.Globalization;
using System.Windows.Data;

namespace DivinityModManager.Converters;

public class ModDetailsMediaWidthConverter : IValueConverter
{
	private const double MinimumWidth = 250;
	private const double MaximumWidth = 620;
	private const double WidescreenAspectRatio = 16d / 9d;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not double cardHeight || Double.IsNaN(cardHeight) || Double.IsInfinity(cardHeight))
		{
			return MinimumWidth;
		}

		// The source badge floats over the artwork now instead of occupying its own
		// footer row, so the full card height is available to the 16:9 preview.
		return Math.Clamp(cardHeight * WidescreenAspectRatio, MinimumWidth, MaximumWidth);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
