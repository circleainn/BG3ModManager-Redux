using System.Windows;
using System.Windows.Media;

namespace DivinityModManager.Util;

/// <summary>
/// Lets an individual menu row override the accent tint used by
/// <c>ReduxPopupMenuItemTemplate</c>'s hover surface.
/// </summary>
/// <remarks>
/// The category-assignment rows are built in code and want their own category colour on hover.
/// An attached property is used rather than repurposing <see cref="FrameworkElement.Tag"/>,
/// because sibling rows in the same menu already carry an unrelated marker in Tag
/// (<c>SourceLinkMenuTag</c>); binding the hover to Tag would leave those rows with a null
/// brush and no visible hover at all.
///
/// When unset the template falls back to <c>ReduxAccentSoftBrush</c> via a trigger rather than
/// a binding fallback, so the default keeps following theme changes.
/// </remarks>
public static class ReduxMenuItemExtension
{
	public static readonly DependencyProperty HoverBrushProperty =
		DependencyProperty.RegisterAttached(
			"HoverBrush",
			typeof(Brush),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(null));

	public static Brush GetHoverBrush(DependencyObject element) => (Brush)element.GetValue(HoverBrushProperty);

	public static void SetHoverBrush(DependencyObject element, Brush value) => element.SetValue(HoverBrushProperty, value);

	/// <summary>
	/// Colour of the row's leading rail, which the template paints as the hover surface's left
	/// border. Kept separate from <see cref="HoverBrushProperty"/> because the rail is drawn at
	/// full strength while the fill behind it is translucent.
	/// </summary>
	public static readonly DependencyProperty RailBrushProperty =
		DependencyProperty.RegisterAttached(
			"RailBrush",
			typeof(Brush),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(null));

	public static Brush GetRailBrush(DependencyObject element) => (Brush)element.GetValue(RailBrushProperty);

	public static void SetRailBrush(DependencyObject element, Brush value) => element.SetValue(RailBrushProperty, value);
}
