using DivinityModManager.Models;

namespace DivinityModManager.Util;

public static class VisualDividerStatePolicy
{
	public static bool SetCollapsed(ModListVisualDividerData divider, bool collapsed)
	{
		if (divider == null || divider.IsCollapsed == collapsed) return false;
		divider.IsCollapsed = collapsed;
		return true;
	}

	public static int SetAllCollapsed(
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList,
		bool collapsed)
	{
		if (dividers == null) return 0;
		var changed = 0;
		foreach (var divider in dividers.Where(item => item != null && item.IsActiveList == activeList))
		{
			if (SetCollapsed(divider, collapsed)) changed++;
		}
		return changed;
	}
}
