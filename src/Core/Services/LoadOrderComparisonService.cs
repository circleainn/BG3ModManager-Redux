namespace DivinityModManager.Models;

/// <summary>
/// Compares load orders without changing either order. The longest common subsequence is used
/// for placement changes so merely activating or deactivating a mod does not make every later
/// mod appear to have moved.
/// </summary>
public static class LoadOrderComparisonService
{
	/// <summary>
	/// Compares two user-selected orders. Every entry present only in the compared order is an
	/// intentional addition for comparison purposes; automatic-dependency terminology is reserved
	/// for Review Export, where Redux can distinguish explicit selections from generated output.
	/// </summary>
	public static LoadOrderComparison CompareSavedOrders(
		IEnumerable<DivinityLoadOrderEntry> baselineOrder,
		IEnumerable<DivinityLoadOrderEntry> comparedOrder)
	{
		var comparedEntries = (comparedOrder ?? Enumerable.Empty<DivinityLoadOrderEntry>()).ToArray();
		return Compare(
			baselineOrder,
			comparedEntries,
			comparedEntries.Select(entry => entry?.UUID),
			true);
	}

	public static LoadOrderComparison Compare(
		IEnumerable<DivinityLoadOrderEntry> previousOrder,
		IEnumerable<DivinityLoadOrderEntry> proposedOrder,
		IEnumerable<string> explicitlySelectedUuids = null,
		bool hasPreviousOrder = true)
	{
		var previous = Normalize(previousOrder);
		var proposed = Normalize(proposedOrder);
		var previousByUuid = previous.ToDictionary(item => item.UUID, StringComparer.OrdinalIgnoreCase);
		var proposedByUuid = proposed.ToDictionary(item => item.UUID, StringComparer.OrdinalIgnoreCase);
		var explicitSelection = (explicitlySelectedUuids ?? Enumerable.Empty<string>())
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var changes = new List<LoadOrderChange>();

		for (var index = 0; index < proposed.Count; index++)
		{
			var item = proposed[index];
			if (previousByUuid.ContainsKey(item.UUID))
			{
				continue;
			}

			changes.Add(new LoadOrderChange(
				explicitSelection.Contains(item.UUID)
					? LoadOrderChangeKind.Activated
					: LoadOrderChangeKind.AutomaticallyAdded,
				item.UUID,
				item.Name,
				null,
				index + 1));
		}

		for (var index = 0; index < previous.Count; index++)
		{
			var item = previous[index];
			if (proposedByUuid.ContainsKey(item.UUID))
			{
				continue;
			}

			changes.Add(new LoadOrderChange(
				LoadOrderChangeKind.Deactivated,
				item.UUID,
				item.Name,
				index + 1,
				null));
		}

		var previousCommon = previous
			.Where(item => proposedByUuid.ContainsKey(item.UUID))
			.Select(item => item.UUID)
			.ToArray();
		var proposedCommon = proposed
			.Where(item => previousByUuid.ContainsKey(item.UUID))
			.Select(item => item.UUID)
			.ToArray();
		var unchangedPlacement = FindLongestCommonSubsequence(previousCommon, proposedCommon);

		foreach (var uuid in proposedCommon.Where(uuid => !unchangedPlacement.Contains(uuid)))
		{
			var previousIndex = previous.FindIndex(item =>
				String.Equals(item.UUID, uuid, StringComparison.OrdinalIgnoreCase));
			var proposedIndex = proposed.FindIndex(item =>
				String.Equals(item.UUID, uuid, StringComparison.OrdinalIgnoreCase));
			var proposedItem = proposed[proposedIndex];

			changes.Add(new LoadOrderChange(
				LoadOrderChangeKind.Repositioned,
				uuid,
				proposedItem.Name,
				previousIndex + 1,
				proposedIndex + 1));
		}

		var kindOrder = new Dictionary<LoadOrderChangeKind, int>
		{
			[LoadOrderChangeKind.Activated] = 0,
			[LoadOrderChangeKind.AutomaticallyAdded] = 1,
			[LoadOrderChangeKind.Deactivated] = 2,
			[LoadOrderChangeKind.Repositioned] = 3
		};
		var orderedChanges = changes
			.OrderBy(change => kindOrder[change.Kind])
			.ThenBy(change => change.NextPosition ?? change.PreviousPosition ?? Int32.MaxValue)
			.ThenBy(change => change.Name, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		return new LoadOrderComparison(hasPreviousOrder, orderedChanges, proposed.Count);
	}

	private static List<DivinityLoadOrderEntry> Normalize(IEnumerable<DivinityLoadOrderEntry> order)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var normalized = new List<DivinityLoadOrderEntry>();
		foreach (var item in order ?? Enumerable.Empty<DivinityLoadOrderEntry>())
		{
			if (item == null || String.IsNullOrWhiteSpace(item.UUID) || !seen.Add(item.UUID))
			{
				continue;
			}

			normalized.Add(new DivinityLoadOrderEntry
			{
				UUID = item.UUID,
				Name = String.IsNullOrWhiteSpace(item.Name) ? item.UUID : item.Name
			});
		}
		return normalized;
	}

	private static HashSet<string> FindLongestCommonSubsequence(
		IReadOnlyList<string> previous,
		IReadOnlyList<string> proposed)
	{
		var lengths = new int[previous.Count + 1, proposed.Count + 1];
		for (var previousIndex = previous.Count - 1; previousIndex >= 0; previousIndex--)
		{
			for (var proposedIndex = proposed.Count - 1; proposedIndex >= 0; proposedIndex--)
			{
				lengths[previousIndex, proposedIndex] =
					String.Equals(previous[previousIndex], proposed[proposedIndex], StringComparison.OrdinalIgnoreCase)
						? lengths[previousIndex + 1, proposedIndex + 1] + 1
						: Math.Max(
							lengths[previousIndex + 1, proposedIndex],
							lengths[previousIndex, proposedIndex + 1]);
			}
		}

		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var left = 0;
		var right = 0;
		while (left < previous.Count && right < proposed.Count)
		{
			if (String.Equals(previous[left], proposed[right], StringComparison.OrdinalIgnoreCase))
			{
				result.Add(previous[left]);
				left++;
				right++;
			}
			else if (lengths[left + 1, right] >= lengths[left, right + 1])
			{
				left++;
			}
			else
			{
				right++;
			}
		}
		return result;
	}
}
