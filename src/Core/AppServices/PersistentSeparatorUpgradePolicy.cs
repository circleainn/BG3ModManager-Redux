using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

public static class PersistentSeparatorUpgradePolicy
{
	public static bool HasExistingActiveSeparators(IEnumerable<ModListVisualDividerData> dividers) =>
		(dividers ?? Enumerable.Empty<ModListVisualDividerData>())
		.Any(divider => divider?.IsActiveList == true);

	public static bool ShouldOfferUpgrade(
		bool hasResolvedUpgrade,
		IEnumerable<ModListVisualDividerData> dividers) =>
		!hasResolvedUpgrade &&
		(dividers ?? Enumerable.Empty<ModListVisualDividerData>())
		.Any(divider => divider?.IsActiveList == true && !divider.IsGlobal);

	public static int MakeAllActiveSeparatorsPersistent(IEnumerable<ModListVisualDividerData> dividers)
	{
		var changed = 0;
		foreach (var divider in dividers ?? Enumerable.Empty<ModListVisualDividerData>())
		{
			if (divider?.IsActiveList != true || divider.IsGlobal) continue;
			divider.IsGlobal = true;
			changed++;
		}
		return changed;
	}
}
