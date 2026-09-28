using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

/// <summary>
/// Keeps manager-owned load-order saves separate from the game's modsettings file.
/// </summary>
public static class LoadOrderPersistencePolicy
{
	/// <summary>Keep the selected profile on refresh; on startup use the game's selected profile before falling back to Public.</summary>
	public static int FindPreferredProfileIndex(IReadOnlyList<DivinityProfileData> profiles, string? selectedProfileUuid)
	{
		if (profiles == null || profiles.Count == 0) return -1;
		if (!String.IsNullOrWhiteSpace(selectedProfileUuid))
		{
			for (var index = 0; index < profiles.Count; index++)
				if (String.Equals(profiles[index]?.UUID, selectedProfileUuid, StringComparison.OrdinalIgnoreCase))
					return index;
		}
		for (var index = 0; index < profiles.Count; index++)
			if (String.Equals(profiles[index]?.ProfileName, "Public", StringComparison.OrdinalIgnoreCase))
				return index;
		return 0;
	}

	public static DivinityLoadOrder FindGameBackedCurrentOrder(IEnumerable<DivinityLoadOrder> orders) =>
		orders?.FirstOrDefault(order => order?.IsModSettings == true);

	/// <summary>Record the game-export backup without selecting it or rewriting a named order.</summary>
	public static void RememberGameExportBackup(IList<DivinityLoadOrder> displayedOrders,
		IList<DivinityLoadOrder> savedOrders, DivinityLoadOrder backup)
	{
		if (displayedOrders == null || savedOrders == null || backup == null) return;
		var displayed = displayedOrders.FirstOrDefault(order =>
			String.Equals(order.FilePath, backup.FilePath, StringComparison.OrdinalIgnoreCase));
		var saved = savedOrders.FirstOrDefault(order =>
			String.Equals(order.FilePath, backup.FilePath, StringComparison.OrdinalIgnoreCase));
		var entries = backup.Order.Select(entry => entry.Clone()).ToArray();
		if (displayed != null && !ReferenceEquals(displayed, backup))
			displayed.SetOrder(entries.Select(entry => entry.Clone()));
		if (saved != null && !ReferenceEquals(saved, displayed) && !ReferenceEquals(saved, backup))
			saved.SetOrder(entries.Select(entry => entry.Clone()));
		if (displayed == null) displayedOrders.Add(saved ?? backup);
		if (saved == null) savedOrders.Add(displayed ?? backup);
	}

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
			OverrideModFiles = selectedOrder?.OverrideModFiles?.ToList(),
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
		.Where(divider => divider != null && divider.IsActiveList)
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
			ParentDividerId = divider.ParentDividerId,
			IsGlobal = divider.IsGlobal,
			MemberModUuids = divider.MemberModUuids?.ToList()
		}).ToList();

	/// <summary>
	/// Keeps a global separator's shared appearance while restoring the position and
	/// section membership recorded by the selected load order.
	/// </summary>
	public static ModListVisualDividerData MergeGlobalDividerPlacement(
		ModListVisualDividerData definition,
		ModListVisualDividerData savedPlacement)
	{
		if (definition == null) return null;
		var placement = savedPlacement != null &&
			String.Equals(definition.Id, savedPlacement.Id, StringComparison.OrdinalIgnoreCase)
			? savedPlacement
			: definition;
		return new ModListVisualDividerData
		{
			Id = definition.Id,
			Title = definition.Title,
			Color = definition.Color,
			IconId = definition.IconId,
			Description = definition.Description,
			IsActiveList = true,
			IsGlobal = true,
			HideLine = definition.HideLine,
			ParentDividerId = definition.ParentDividerId,
			Position = placement.Position,
			IsCollapsed = placement.IsCollapsed,
			MemberModUuids = placement.MemberModUuids?.ToList()
		};
	}

	/// <summary>
	/// A separator copied between orders before global scope existed keeps its ID in
	/// each order. Treat that older local copy as this order's placement of the new
	/// global definition, rather than rendering two separators with the same ID.
	/// </summary>
	public static List<ModListVisualDividerData> MergeGlobalAndSavedDividers(
		IEnumerable<ModListVisualDividerData> globalDefinitions,
		IEnumerable<ModListVisualDividerData> savedDividers)
	{
		var globalCandidates = CloneActiveVisualDividers(globalDefinitions)
			.Where(divider => divider.IsGlobal)
			.ToList();
		var saved = CloneActiveVisualDividers(savedDividers);
		var reservedIds = globalCandidates.Concat(saved)
			.Select(divider => divider.Id)
			.Where(id => !String.IsNullOrWhiteSpace(id))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var divider in globalCandidates.Where(divider => String.IsNullOrWhiteSpace(divider.Id)))
			divider.Id = NewUniqueDividerId(reservedIds);
		var globals = globalCandidates
			.GroupBy(divider => divider.Id, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();
		var savedById = saved.Where(divider => !String.IsNullOrWhiteSpace(divider.Id))
			.GroupBy(divider => divider.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key,
				group => group.FirstOrDefault(divider => !divider.IsGlobal) ?? group.First(),
				StringComparer.OrdinalIgnoreCase);
		var globalIds = globals.Select(divider => divider.Id)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var usedPlacements = new HashSet<ModListVisualDividerData>();
		var result = new List<ModListVisualDividerData>();
		foreach (var definition in globals)
		{
			var placement = savedById.GetValueOrDefault(definition.Id);
			if (placement != null) usedPlacements.Add(placement);
			result.Add(MergeGlobalDividerPlacement(definition, placement));
		}
		var resultIds = new HashSet<string>(globalIds, StringComparer.OrdinalIgnoreCase);
		foreach (var divider in saved)
		{
			if (!String.IsNullOrWhiteSpace(divider.Id) && globalIds.Contains(divider.Id))
			{
				if (usedPlacements.Contains(divider) || divider.IsGlobal) continue;
				// An additional local copy is another visible row, not another
				// placement of the same global definition.
				divider.Id = NewUniqueDividerId(reservedIds);
			}
			// Only a copied global definition represents the same separator. Two
			// local rows with the same legacy ID are still two rows; keep both.
			if (String.IsNullOrWhiteSpace(divider.Id) || !resultIds.Add(divider.Id))
			{
				divider.Id = NewUniqueDividerId(reservedIds);
				resultIds.Add(divider.Id);
			}
			result.Add(divider);
		}
		return result;
	}

	private static string NewUniqueDividerId(HashSet<string> reservedIds)
	{
		string id;
		do id = Guid.NewGuid().ToString("N");
		while (!reservedIds.Add(id));
		return id;
	}

	public static bool RequiresSaveAs(DivinityLoadOrder order)
	{
		return order?.IsModSettings == true
			|| String.Equals(Path.GetExtension(order?.FilePath), ".lsx", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Restores Redux's saved Current workspace without replacing the logical Current
	/// entry or redirecting it to a second selectable load order.
	/// </summary>
	public static bool RestoreSavedCurrentState(DivinityLoadOrder currentOrder, DivinityLoadOrder savedCurrentState,
		bool restoreOrder = true)
	{
		if (currentOrder == null || savedCurrentState == null) return false;
		if (restoreOrder)
		{
			currentOrder.SetOrder(savedCurrentState.Order.Select(entry => entry.Clone()));
			currentOrder.LastModifiedDate = savedCurrentState.LastModifiedDate;
		}
		currentOrder.VisualDividers = savedCurrentState.VisualDividers == null
			? null
			: CloneActiveVisualDividers(savedCurrentState.VisualDividers);
		currentOrder.OverrideModFiles = savedCurrentState.OverrideModFiles?.ToList();
		return true;
	}
}
