using DivinityModManager.Models;

using System.Windows;

namespace DivinityModManager.Util;

/// <summary>
/// Keeps filtered rows out of the virtualized ItemsSource. Leaving them in the
/// source with collapsed containers can trap WPF's recycling panel in repeated
/// measure passes, especially while a multi-selection is being reconciled.
/// </summary>
public static class VisualModFilterProjectionPolicy
{
	public static IReadOnlyList<DivinityModData> ResolveVisibleMods(
		IEnumerable<DivinityModData> mods)
	{
		ArgumentNullException.ThrowIfNull(mods);
		return mods
			.Where(mod => mod != null && mod.Visibility == Visibility.Visible)
			.ToList();
	}
}
