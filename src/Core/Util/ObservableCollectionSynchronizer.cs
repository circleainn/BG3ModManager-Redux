using System.Collections.ObjectModel;

namespace DivinityModManager.Util;

/// <summary>
/// Reconciles an observable collection without issuing a Reset notification.
/// Preserving unchanged items prevents WPF from rebuilding every realized row
/// when only one item was inserted, removed, or moved.
/// </summary>
public static class ObservableCollectionSynchronizer
{
	public static void Synchronize<T>(
		ObservableCollection<T> target,
		IReadOnlyList<T> desired,
		Func<T, T, bool> itemsMatch,
		Action<T, T> updateMatchedItem = null)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(desired);
		ArgumentNullException.ThrowIfNull(itemsMatch);

		// Most refreshes leave the projected list unchanged. Avoid the quadratic
		// membership scan in that common path while preserving the minimal Remove
		// notifications used when the sequence really changes.
		var sequenceMatches = target.Count == desired.Count;
		if (sequenceMatches)
		{
			for (var index = 0; index < desired.Count; index++)
			{
				if (itemsMatch(target[index], desired[index])) continue;
				sequenceMatches = false;
				break;
			}
		}

		if (sequenceMatches)
		{
			if (updateMatchedItem != null)
			{
				for (var index = 0; index < desired.Count; index++)
				{
					updateMatchedItem(target[index], desired[index]);
				}
			}

			return;
		}

		for (var targetIndex = target.Count - 1; targetIndex >= 0; targetIndex--)
		{
			if (desired.Any(desiredItem => itemsMatch(target[targetIndex], desiredItem))) continue;
			target.RemoveAt(targetIndex);
		}

		for (var desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
		{
			var desiredItem = desired[desiredIndex];
			if (desiredIndex < target.Count && itemsMatch(target[desiredIndex], desiredItem))
			{
				updateMatchedItem?.Invoke(target[desiredIndex], desiredItem);
				continue;
			}

			var existingIndex = -1;
			for (var candidateIndex = desiredIndex + 1; candidateIndex < target.Count; candidateIndex++)
			{
				if (!itemsMatch(target[candidateIndex], desiredItem)) continue;
				existingIndex = candidateIndex;
				break;
			}

			if (existingIndex >= 0)
			{
				target.Move(existingIndex, desiredIndex);
				updateMatchedItem?.Invoke(target[desiredIndex], desiredItem);
			}
			else
			{
				target.Insert(desiredIndex, desiredItem);
			}
		}

		while (target.Count > desired.Count)
		{
			target.RemoveAt(target.Count - 1);
		}
	}
}
