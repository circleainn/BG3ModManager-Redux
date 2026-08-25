using DivinityModManager.Models;

using System.Windows;

namespace DivinityModManager.Util;

public static class VisualModSelectionPolicy
{
	public static IReadOnlyList<DivinityModData> ResolveSelectAllItems(
		IEnumerable<DivinityModData> visualItems)
	{
		ArgumentNullException.ThrowIfNull(visualItems);
		return visualItems
			.Where(item => item != null &&
				!item.IsVisualDivider &&
				item.Visibility == Visibility.Visible)
			.ToList();
	}
}
