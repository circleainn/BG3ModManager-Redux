using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager.Controls;

/// <summary>
/// Theme-aware vector icon surface used by Redux-owned UI.
/// Geometry resources are vendored from Ionicons under the MIT license.
/// </summary>
public sealed class ReduxIcon : Control
{
	public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
		nameof(Data),
		typeof(Geometry),
		typeof(ReduxIcon));

	public static readonly DependencyProperty StrokeDataProperty = DependencyProperty.Register(
		nameof(StrokeData),
		typeof(Geometry),
		typeof(ReduxIcon));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
		nameof(StrokeThickness),
		typeof(double),
		typeof(ReduxIcon),
		new FrameworkPropertyMetadata(32d));

	public Geometry Data
	{
		get => (Geometry)GetValue(DataProperty);
		set => SetValue(DataProperty, value);
	}

	public Geometry StrokeData
	{
		get => (Geometry)GetValue(StrokeDataProperty);
		set => SetValue(StrokeDataProperty, value);
	}

	public double StrokeThickness
	{
		get => (double)GetValue(StrokeThicknessProperty);
		set => SetValue(StrokeThicknessProperty, value);
	}
}
