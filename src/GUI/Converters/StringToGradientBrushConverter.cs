using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DivinityModManager.Converters;

/// <summary>
/// Horizontal (left-to-right) two-stop gradient from a hex colour string, matching every other
/// pill in Redux. Extends that same lit-object look to per-category pills, whose colour is
/// data-driven rather than a fixed theme resource, so it can't be a static LinearGradientBrush
/// resource the way the source pills' brushes are.
/// </summary>
public class StringToGradientBrushConverter : IValueConverter
{
	/// <summary>
	/// ConverterParameter as "topAlpha,bottomAlpha" hex bytes, e.g. "3E,1E". Defaults to the same
	/// split the Nexus/mod.io pill fills use.
	/// </summary>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not string str || String.IsNullOrWhiteSpace(str))
			return null;

		byte topAlpha = 0x3E;
		byte bottomAlpha = 0x1E;
		if (parameter is string alphaText)
		{
			var parts = alphaText.Split(',');
			if (parts.Length == 2)
			{
				Byte.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out topAlpha);
				Byte.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bottomAlpha);
			}
		}

		Color color;
		try
		{
			color = (Color)ColorConverter.ConvertFromString(str);
		}
		catch (FormatException)
		{
			return Binding.DoNothing;
		}
		var top = color;
		top.A = topAlpha;
		var bottom = color;
		bottom.A = bottomAlpha;

		var brush = new LinearGradientBrush(top, bottom, new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));
		if (brush.CanFreeze) brush.Freeze();
		return brush;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
