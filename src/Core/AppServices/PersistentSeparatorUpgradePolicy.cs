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
		.Any(divider => divider?.IsActiveList == true && (!divider.IsGlobal || !divider.HideLine));

	public static string OrderKey(DivinityLoadOrder order) =>
		(String.IsNullOrWhiteSpace(order?.FilePath) ? "name:" + order?.Name : order.FilePath)
		.Replace('/', '\\').ToUpperInvariant();

	public static Dictionary<string, List<string>> SnapshotExistingOrders(
		IEnumerable<DivinityLoadOrder> orders, DivinityLoadOrder selectedOrder,
		IEnumerable<ModListVisualDividerData> workingDividers)
	{
		var pending = new Dictionary<string, List<string>>();
		foreach (var order in (orders ?? []).Where(order => order != null))
		{
			var dividers = OrderKey(order) == OrderKey(selectedOrder) ? workingDividers : order.VisualDividers;
			var ids = (dividers ?? []).Where(divider => divider?.IsActiveList == true)
				.Select(divider => divider.Id).Where(id => !String.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			if (ids.Count > 0) pending[OrderKey(order)] = ids;
		}
		return pending;
	}

	public static List<ModListVisualDividerData> EligibleSeparators(
		IEnumerable<ModListVisualDividerData> dividers, IEnumerable<string> existingIds)
	{
		var ids = new HashSet<string>(existingIds ?? [], StringComparer.OrdinalIgnoreCase);
		return (dividers ?? []).Where(divider => divider?.IsActiveList == true && ids.Contains(divider.Id)).ToList();
	}

	public static int DisableExistingLines(IEnumerable<ModListVisualDividerData> dividers)
	{
		var changed = 0;
		foreach (var divider in dividers ?? [])
		{
			if (divider?.IsActiveList != true || divider.HideLine) continue;
			divider.HideLine = true;
			changed++;
		}
		return changed;
	}

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
