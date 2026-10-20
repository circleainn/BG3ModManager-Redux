using DivinityModManager.Models;

namespace DivinityModManager.Util;

public enum VisualModListPane { Active, Inactive, Override }

/// <summary>Plans an explicit move into a section using the complete, unfiltered pane.</summary>
public static class VisualDividerMovePolicy
{
	public static VisualDividerMoveResult Plan(
		IReadOnlyList<DivinityModData> sequence,
		IEnumerable<ModListVisualDividerData> dividers,
		IEnumerable<DivinityModData> selected,
		string destinationId,
		bool activeList)
	{
		ArgumentNullException.ThrowIfNull(sequence);
		ArgumentNullException.ThrowIfNull(dividers);
		ArgumentNullException.ThrowIfNull(selected);
		var pane = dividers.Where(divider => divider != null && divider.IsActiveList == activeList).ToList();
		var target = pane.FirstOrDefault(divider => String.Equals(divider.Id, destinationId, StringComparison.OrdinalIgnoreCase));
		var requested = selected.ToList();
		// Identity is intentional: a context menu retained across Refresh must never
		// move a replacement instance or silently operate on only part of a selection.
		if (target == null || requested.Count == 0 || requested.Any(mod =>
			mod == null || mod.IsVisualDivider || String.IsNullOrWhiteSpace(mod.UUID)
			|| !sequence.Any(current => ReferenceEquals(current, mod)))) return null;
		var requestedSet = new HashSet<DivinityModData>(requested, ReferenceEqualityComparer.Instance);
		var moving = sequence.Where(item => requestedSet.Contains(item)).ToList();
		var movingIds = moving.Select(mod => mod.UUID).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var remaining = sequence.Where(item => !requestedSet.Contains(item)).ToList();
		var markerIndex = remaining.FindIndex(item => item.IsVisualDivider &&
			String.Equals(item.VisualDividerId, destinationId, StringComparison.OrdinalIgnoreCase));
		if (markerIndex < 0) return null;

		// Append to the destination's own rows, before its first child or the next
		// section. A closed section ends at its sealed members, before loose rows.
		var targetIds = (target.MemberModUuids ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var insertIndex = markerIndex + 1;
		while (insertIndex < remaining.Count && !remaining[insertIndex].IsVisualDivider &&
			(!target.IsCollapsed || targetIds.Contains(remaining[insertIndex].UUID))) insertIndex++;
		remaining.InsertRange(insertIndex, moving);

		var memberships = pane.Select(divider => new ModListVisualDividerData
		{
			Id = divider.Id, IsActiveList = activeList, IsCollapsed = divider.IsCollapsed,
			Position = divider.Position,
			MemberModUuids = (divider.MemberModUuids ?? []).Where(id => !movingIds.Contains(id)).ToList()
		}).ToList();
		var targetMembership = memberships.First(divider => String.Equals(divider.Id, destinationId, StringComparison.OrdinalIgnoreCase));
		targetMembership.MemberModUuids.AddRange(moving.Select(mod => mod.UUID));
		VisualDividerSectionPolicy.AssignMembersPreservingCollapsedSections(remaining, memberships, activeList);
		var byId = memberships.ToDictionary(divider => divider.Id,
			divider => (IReadOnlyList<string>)divider.MemberModUuids, StringComparer.OrdinalIgnoreCase);
		var changed = !sequence.SequenceEqual(remaining, ReferenceEqualityComparer.Instance) || pane.Any(divider =>
			!(divider.MemberModUuids ?? []).SequenceEqual(byId[divider.Id], StringComparer.OrdinalIgnoreCase));
		return new VisualDividerMoveResult(remaining, byId, changed);
	}
}

public sealed record VisualDividerMoveResult(
	IReadOnlyList<DivinityModData> Items,
	IReadOnlyDictionary<string, IReadOnlyList<string>> MembersByDividerId,
	bool Changed);
