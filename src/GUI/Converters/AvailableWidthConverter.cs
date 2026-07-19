using System.Globalization;
using System.Windows.Data;

namespace DivinityModManager.Converters;

/// <summary>
/// Constrains content to its arranged host width while allowing a natural first measure.
/// This is useful for items hosted by panels such as WrapPanel, which otherwise measure
/// horizontal children with infinite width and prevent text trimming from activating.
/// </summary>
public sealed class AvailableWidthConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not double width || width <= 0 || Double.IsNaN(width) || Double.IsInfinity(width))
		{
			return Double.PositiveInfinity;
		}

		var inset = parameter is string text && Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: 0;
		return Math.Max(0, width - inset);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
