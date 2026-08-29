using DynamicData.Binding;

namespace DivinityModManager.Util;

/// <summary>
/// Applies separator projection changes without making WPF process one complete
/// viewport update for every member in a populated section.
/// </summary>
public static class VisualDividerProjectionMutation
{
	public const int BulkChangeThreshold = 8;

	/// <summary>
	/// Keeps recycling ListView containers available across an animated section
	/// transition. A Reset clears WPF's generator and forces the next expansion to
	/// reconstruct every complex mod-row template.
	/// </summary>
	public static void InsertRangePreservingContainers<T>(
		ObservableCollectionExtended<T> target,
		IReadOnlyList<T> items,
		int index)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(items);
		if (items.Count == 0) return;

		target.InsertRange(items, index);
	}

	public static void InsertRange<T>(
		ObservableCollectionExtended<T> target,
		IReadOnlyList<T> items,
		int index)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(items);
		if (items.Count == 0) return;

		if (items.Count < BulkChangeThreshold)
		{
			target.InsertRange(items, index);
			return;
		}

		// ObservableCollectionExtended.InsertRange raises one Add notification for
		// every item. Collapse a populated section change to one Reset notification.
		using (target.SuspendNotifications())
			target.InsertRange(items, index);
	}

	public static void RemoveRange<T>(
		ObservableCollectionExtended<T> target,
		int index,
		int count)
	{
		ArgumentNullException.ThrowIfNull(target);
		if (count <= 0) return;

		if (count < BulkChangeThreshold)
		{
			target.RemoveRange(index, count);
			return;
		}

		// RemoveRange has the same per-item notification behavior as InsertRange.
		using (target.SuspendNotifications())
			target.RemoveRange(index, count);
	}

	public static void RemoveRangePreservingContainers<T>(
		ObservableCollectionExtended<T> target,
		int index,
		int count)
	{
		ArgumentNullException.ThrowIfNull(target);
		if (count <= 0) return;

		target.RemoveRange(index, count);
	}
}
