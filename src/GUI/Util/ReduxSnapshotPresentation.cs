using DivinityModManager.AppServices;
using System.Windows;
using System.Windows.Media;

namespace DivinityModManager.Util;

internal static class ReduxSnapshotPresentation
{
	public static ReduxSnapshotAppearance Capture(FrameworkElement source)
	{
		var appearance = new ReduxSnapshotAppearance { ShowIcons = DivinityApp.ShowInterfaceIcons };
		foreach (var key in ReduxSnapshotAppearance.DefaultColors.Keys)
			if (source.TryFindResource(key) is SolidColorBrush brush) appearance.Colors[key] = brush.Color.ToString();
		if (source.TryFindResource("Redux.FontSize.12") is double size) appearance.TextScale = size / 12;
		if (source.TryFindResource("Redux.FontFamily.UI") is FontFamily family)
			appearance.FontFamily = family.Source.Split('#').Last();
		return appearance.Normalize();
	}
}
