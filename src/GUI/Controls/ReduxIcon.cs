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
	/// <summary>
	/// Creates a consistently styled menu icon from an application geometry resource.
	/// </summary>
	public static ReduxIcon FromResource(string resourceKey, bool useStroke = false, string foregroundResourceKey = null)
	{
		var icon = new ReduxIcon();
		var geometry = Application.Current?.MainWindow?.TryFindResource(resourceKey) as Geometry
			?? Application.Current?.TryFindResource(resourceKey) as Geometry;
		if (geometry != null)
		{
			if (useStroke)
			{
				icon.StrokeData = geometry;
			}
			else
			{
				icon.Data = geometry;
			}
		}
		if (!String.IsNullOrWhiteSpace(foregroundResourceKey))
		{
			icon.SetResourceReference(ForegroundProperty, foregroundResourceKey);
		}
		return icon;
	}

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
