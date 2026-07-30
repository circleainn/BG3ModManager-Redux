using DivinityModManager.Models;

using System;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class LoadOrderComparisonTests
{
	public void SavedOrderComparisonTreatsRightOnlyModsAsIntentionalAdditions()
	{
		var shared = new DivinityLoadOrderEntry { UUID = "shared", Name = "Shared" };
		var added = new DivinityLoadOrderEntry { UUID = "added", Name = "Added" };
		var comparison = LoadOrderComparisonService.CompareSavedOrders(
			[shared],
			[shared, added]);

		RegressionAssert.Equal(1, comparison.Activated.Count);
		RegressionAssert.Equal("Added", comparison.Activated[0].Name);
		RegressionAssert.Equal(0, comparison.AutomaticallyAdded.Count);
	}

	public void ReportsActivationDeactivationAndAutomaticDependencies()
	{
		var previous = Order(("a", "Alpha"), ("b", "Beta"));
		var proposed = Order(("b", "Beta"), ("c", "Charlie"), ("d", "Dependency"));

		var result = LoadOrderComparisonService.Compare(previous, proposed, ["b", "c"]);

		RegressionAssert.Equal(1, result.Activated.Count);
		RegressionAssert.Equal("c", result.Activated[0].UUID);
		RegressionAssert.Equal(1, result.AutomaticallyAdded.Count);
		RegressionAssert.Equal("d", result.AutomaticallyAdded[0].UUID);
		RegressionAssert.Equal(1, result.Deactivated.Count);
		RegressionAssert.Equal("a", result.Deactivated[0].UUID);
		RegressionAssert.Equal(0, result.Repositioned.Count);
	}

	public void AddedOrRemovedModsDoNotCreateFalsePositionChanges()
	{
		var previous = Order(("a", "Alpha"), ("b", "Beta"), ("c", "Charlie"));
		var proposed = Order(("x", "Extra"), ("a", "Alpha"), ("c", "Charlie"));

		var result = LoadOrderComparisonService.Compare(previous, proposed, ["x", "a", "c"]);

		RegressionAssert.Equal(0, result.Repositioned.Count);
	}

	public void ReportsTheSmallestPlacementChangeForASingleMove()
	{
		var previous = Order(("a", "Alpha"), ("b", "Beta"), ("c", "Charlie"), ("d", "Delta"));
		var proposed = Order(("c", "Charlie"), ("a", "Alpha"), ("b", "Beta"), ("d", "Delta"));

		var result = LoadOrderComparisonService.Compare(previous, proposed, ["a", "b", "c", "d"]);

		RegressionAssert.Equal(1, result.Repositioned.Count);
		RegressionAssert.Equal("c", result.Repositioned[0].UUID);
		RegressionAssert.Equal(3, result.Repositioned[0].PreviousPosition);
		RegressionAssert.Equal(1, result.Repositioned[0].NextPosition);
	}

	public void IgnoresDuplicateAndBlankEntriesButRetainsMissingBaselineMods()
	{
		var previous = new[]
		{
			new DivinityLoadOrderEntry { UUID = "a", Name = "Alpha" },
			new DivinityLoadOrderEntry { UUID = "A", Name = "Duplicate" },
			new DivinityLoadOrderEntry { UUID = "missing", Name = "Missing", Missing = true },
			new DivinityLoadOrderEntry { UUID = " ", Name = "Blank" }
		};

		var result = LoadOrderComparisonService.Compare(previous, Order(("a", "Alpha")), ["a"]);

		RegressionAssert.Equal(1, result.Deactivated.Count);
		RegressionAssert.Equal("missing", result.Deactivated[0].UUID);
		RegressionAssert.Equal(1, result.ProposedModCount);
	}

	public void PreservesFirstExportState()
	{
		var result = LoadOrderComparisonService.Compare(
			Array.Empty<DivinityLoadOrderEntry>(),
			Order(("a", "Alpha")),
			["a"],
			hasPreviousOrder: false);

		RegressionAssert.False(result.HasPreviousOrder);
		RegressionAssert.Equal(1, result.Activated.Count);
	}

	private static DivinityLoadOrderEntry[] Order(params (string UUID, string Name)[] items) =>
		items.Select(item => new DivinityLoadOrderEntry
		{
			UUID = item.UUID,
			Name = item.Name
		}).ToArray();
}
