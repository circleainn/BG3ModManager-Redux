using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

/// <summary>
/// Keeps manager-owned load-order saves separate from the game's modsettings file.
/// </summary>
public static class LoadOrderPersistencePolicy
{
	/// <summary>
	/// Keeps the currently selected order during an in-app refresh and restores the
	/// remembered order during initial startup, before a selection exists.
	/// </summary>
	public static string ResolveOrderNameForRefresh(string currentOrderName, string rememberedOrderName) =>
		!String.IsNullOrWhiteSpace(currentOrderName)
			? currentOrderName
			: rememberedOrderName ?? String.Empty;

	/// <summary>
	/// Creates a detached working-order snapshot. Editing the active list must not
	/// mutate the selected saved order until the user explicitly saves it.
	/// </summary>
	public static DivinityLoadOrder CreateWorkingCopy(
		DivinityLoadOrder selectedOrder,
		IEnumerable<DivinityModData> activeMods,
		IEnumerable<ModListVisualDividerData> activeVisualDividers = null)
	{
		var workingCopy = new DivinityLoadOrder
		{
			Name = selectedOrder?.Name,
			FilePath = selectedOrder?.FilePath,
			LastModifiedDate = DateTime.Now,
			VisualDividers = CloneActiveVisualDividers(
				activeVisualDividers ?? selectedOrder?.VisualDividers)
		};
		workingCopy.AddRange(activeMods ?? Enumerable.Empty<DivinityModData>(), true);
		return workingCopy;
	}

	public static DivinityLoadOrder CreateBlankOrder(string name, string filePath)
	{
		return new DivinityLoadOrder
		{
			Name = name,
			FilePath = filePath,
			Order = [],
			VisualDividers = []
		};
	}

	public static List<ModListVisualDividerData> CloneActiveVisualDividers(
		IEnumerable<ModListVisualDividerData> dividers) =>
		(dividers ?? Enumerable.Empty<ModListVisualDividerData>())
		.Where(divider => divider != null && divider.IsActiveList && !divider.IsGlobal)
		.Select(divider => new ModListVisualDividerData
		{
			Id = divider.Id,
			Title = divider.Title,
			Color = divider.Color,
			IconId = divider.IconId,
			Description = divider.Description,
			IsActiveList = true,
			Position = divider.Position,
			IsCollapsed = divider.IsCollapsed,
			HideLine = divider.HideLine,
			IsGlobal = divider.IsGlobal,
			MemberModUuids = divider.MemberModUuids?.ToList()
		}).ToList();

	public static bool RequiresSaveAs(DivinityLoadOrder order)
	{
		return order?.IsModSettings == true
			|| String.Equals(Path.GetExtension(order?.FilePath), ".lsx", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Restores Redux's saved Current workspace without replacing the logical Current
	/// entry or redirecting it to a second selectable load order.
	/// </summary>
	public static bool RestoreSavedCurrentState(DivinityLoadOrder currentOrder, DivinityLoadOrder savedCurrentState)
	{
		if (currentOrder == null || savedCurrentState == null) return false;
		currentOrder.SetOrder(savedCurrentState.Order.Select(entry => entry.Clone()));
		currentOrder.VisualDividers = savedCurrentState.VisualDividers == null
			? null
			: CloneActiveVisualDividers(savedCurrentState.VisualDividers);
		currentOrder.LastModifiedDate = savedCurrentState.LastModifiedDate;
		return true;
	}
}
