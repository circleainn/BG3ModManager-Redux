using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Validates and resolves the single-level separator hierarchy. Parent links are
/// explicit and stable; positions remain local to each saved load order.
/// </summary>
public static class VisualDividerHierarchyPolicy
{
	public static bool Normalize(IEnumerable<ModListVisualDividerData> dividers)
	{
		ArgumentNullException.ThrowIfNull(dividers);
		var items = dividers.Where(divider => divider != null).ToList();
		var byId = items
			.Where(divider => !String.IsNullOrWhiteSpace(divider.Id))
			.GroupBy(divider => divider.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var originallyNestedIds = items
			.Where(divider => !String.IsNullOrWhiteSpace(divider.ParentDividerId))
			.Select(divider => divider.Id)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var changed = false;
		foreach (var divider in items)
		{
			if (String.IsNullOrWhiteSpace(divider.ParentDividerId))
			{
				if (divider.ParentDividerId != String.Empty)
				{
					divider.ParentDividerId = String.Empty;
					changed = true;
				}
				continue;
			}

			var valid = byId.TryGetValue(divider.ParentDividerId, out var parent)
				&& !ReferenceEquals(parent, divider)
				&& parent.IsActiveList == divider.IsActiveList
				&& !originallyNestedIds.Contains(parent.Id);
			if (valid)
			{
				if (divider.IsGlobal != parent.IsGlobal)
				{
					divider.IsGlobal = parent.IsGlobal;
					changed = true;
				}
				continue;
			}
			divider.ParentDividerId = String.Empty;
			changed = true;
		}
		return changed;
	}

	public static IReadOnlyList<ModListVisualDividerData> ChildrenOf(
		IEnumerable<ModListVisualDividerData> dividers,
		ModListVisualDividerData parent) =>
		(dividers ?? Enumerable.Empty<ModListVisualDividerData>())
			.Where(divider => divider != null && parent != null &&
				String.Equals(divider.ParentDividerId, parent.Id, StringComparison.OrdinalIgnoreCase))
			.OrderBy(divider => divider.Position)
			.ToList();

	public static bool HasChildren(
		IEnumerable<ModListVisualDividerData> dividers,
		ModListVisualDividerData parent) => ChildrenOf(dividers, parent).Count > 0;

	public static bool NormalizePlacement(
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList)
	{
		ArgumentNullException.ThrowIfNull(dividers);
		var pane = dividers.Where(divider => divider != null && divider.IsActiveList == activeList)
			.OrderBy(divider => divider.Position)
			.ToList();
		var byId = pane.ToDictionary(divider => divider.Id, StringComparer.OrdinalIgnoreCase);
		var changed = false;
		foreach (var child in pane.Where(divider => !String.IsNullOrWhiteSpace(divider.ParentDividerId)))
		{
			if (!byId.TryGetValue(child.ParentDividerId, out var parent) ||
				parent.Position >= child.Position || pane.Any(candidate =>
					String.IsNullOrWhiteSpace(candidate.ParentDividerId) &&
					candidate.Position > parent.Position && candidate.Position < child.Position))
			{
				child.ParentDividerId = String.Empty;
				changed = true;
			}
		}
		return changed;
	}

	public static int ResolveChildInsertionIndex(
		IReadOnlyList<DivinityModData> sequence,
		IEnumerable<ModListVisualDividerData> dividers,
		ModListVisualDividerData parent)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		if (parent == null) return sequence.Count;
		var pane = (dividers ?? Enumerable.Empty<ModListVisualDividerData>())
			.Where(divider => divider != null && divider.IsActiveList == parent.IsActiveList)
			.ToDictionary(divider => divider.Id, StringComparer.OrdinalIgnoreCase);
		var parentIndex = FindMarker(sequence, parent.Id);
		if (parentIndex < 0) return sequence.Count;
		for (var index = parentIndex + 1; index < sequence.Count; index++)
		{
			var item = sequence[index];
			if (!item.IsVisualDivider || !pane.TryGetValue(item.VisualDividerId ?? String.Empty, out var divider))
				continue;
			if (String.IsNullOrWhiteSpace(divider.ParentDividerId)) return index;
		}
		return sequence.Count;
	}

	/// <summary>Returns a parent marker and all of its child markers, without mods.</summary>
	public static IReadOnlyList<DivinityModData> ResolveExpandedMarkerPayload(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		ModListVisualDividerData moved)
	{
		var sequence = (visualItems ?? Enumerable.Empty<DivinityModData>()).Where(item => item != null).ToList();
		if (moved == null) return [];
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { moved.Id };
		if (String.IsNullOrWhiteSpace(moved.ParentDividerId))
			foreach (var child in ChildrenOf(dividers, moved)) ids.Add(child.Id);
		return sequence.Where(item => item.IsVisualDivider && ids.Contains(item.VisualDividerId ?? String.Empty)).ToList();
	}

	/// <summary>
	/// A closed parent owns its complete visible subtree up to the next top-level
	/// separator. A closed child continues to carry only its sealed member block.
	/// </summary>
	public static IReadOnlyList<DivinityModData> ResolveCollapsedPayload(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		ModListVisualDividerData moved)
	{
		var sequence = (visualItems ?? Enumerable.Empty<DivinityModData>()).Where(item => item != null).ToList();
		if (moved == null || !moved.IsCollapsed) return [];
		if (!String.IsNullOrWhiteSpace(moved.ParentDividerId))
		{
			var marker = sequence.FirstOrDefault(item => item.IsVisualDivider &&
				String.Equals(item.VisualDividerId, moved.Id, StringComparison.OrdinalIgnoreCase));
			return marker == null ? [] : VisualDividerSectionPolicy.ResolveCollapsedBlockDragPayload(sequence, marker, moved);
		}

		var byId = (dividers ?? Enumerable.Empty<ModListVisualDividerData>())
			.Where(divider => divider != null)
			.ToDictionary(divider => divider.Id, StringComparer.OrdinalIgnoreCase);
		var start = FindMarker(sequence, moved.Id);
		if (start < 0) return [];
		var end = sequence.Count;
		for (var index = start + 1; index < sequence.Count; index++)
		{
			var item = sequence[index];
			if (item.IsVisualDivider && byId.TryGetValue(item.VisualDividerId ?? String.Empty, out var divider) &&
				String.IsNullOrWhiteSpace(divider.ParentDividerId))
			{
				end = index;
				break;
			}
		}
		return sequence.GetRange(start, end - start);
	}

	public static VisualDividerHierarchyProjection ResolveProjection(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList)
	{
		var sequence = (visualItems ?? Enumerable.Empty<DivinityModData>()).Where(item => item != null).ToList();
		var pane = (dividers ?? Enumerable.Empty<ModListVisualDividerData>())
			.Where(divider => divider != null && divider.IsActiveList == activeList)
			.ToDictionary(divider => divider.Id, StringComparer.OrdinalIgnoreCase);
		var hiddenMods = VisualDividerSectionPolicy.GetCollapsedMemberIds(sequence, pane.Values, activeList)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var hiddenDividers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		ModListVisualDividerData collapsedParent = null;
		foreach (var item in sequence)
		{
			if (item.IsVisualDivider)
			{
				pane.TryGetValue(item.VisualDividerId ?? String.Empty, out var divider);
				if (collapsedParent != null && divider != null && String.IsNullOrWhiteSpace(divider.ParentDividerId))
					collapsedParent = null;
				if (collapsedParent != null)
				{
					hiddenDividers.Add(item.VisualDividerId ?? String.Empty);
					continue;
				}
				if (divider is { IsCollapsed: true } && String.IsNullOrWhiteSpace(divider.ParentDividerId) &&
					HasChildren(pane.Values, divider))
					collapsedParent = divider;
				continue;
			}

			if (collapsedParent != null && !String.IsNullOrWhiteSpace(item.UUID)) hiddenMods.Add(item.UUID);
		}
		return new VisualDividerHierarchyProjection(hiddenMods, hiddenDividers);
	}

	/// <summary>
	/// Returns ordinary rows whose owning separator is a child. The display layer uses
	/// this presentation-only result to indent the complete child section without
	/// changing mod order or separator membership.
	/// </summary>
	public static IReadOnlySet<string> ResolveIndentedModIds(
		IEnumerable<DivinityModData> visualItems,
		IEnumerable<ModListVisualDividerData> dividers,
		bool activeList)
	{
		var pane = (dividers ?? Enumerable.Empty<ModListVisualDividerData>())
			.Where(divider => divider != null && divider.IsActiveList == activeList)
			.ToDictionary(divider => divider.Id, StringComparer.OrdinalIgnoreCase);
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var insideChild = false;
		foreach (var item in visualItems ?? Enumerable.Empty<DivinityModData>())
		{
			if (item == null) continue;
			if (item.IsVisualDivider)
			{
				insideChild = pane.TryGetValue(item.VisualDividerId ?? String.Empty, out var divider) &&
					!String.IsNullOrWhiteSpace(divider.ParentDividerId);
				continue;
			}
			if (insideChild && !String.IsNullOrWhiteSpace(item.UUID)) result.Add(item.UUID);
		}
		return result;
	}

	private static int FindMarker(IReadOnlyList<DivinityModData> sequence, string dividerId)
	{
		for (var index = 0; index < sequence.Count; index++)
			if (sequence[index].IsVisualDivider && String.Equals(
				sequence[index].VisualDividerId, dividerId, StringComparison.OrdinalIgnoreCase)) return index;
		return -1;
	}
}

public sealed record VisualDividerHierarchyProjection(
	IReadOnlySet<string> HiddenModUuids,
	IReadOnlySet<string> HiddenDividerIds);
