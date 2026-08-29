using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Util;

using DynamicData.Binding;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class InteractionPerformanceTests
{
	public void ReorderingOneRowEmitsOneMoveInsteadOfACollectionReset()
	{
		var first = new object();
		var second = new object();
		var third = new object();
		var rows = new ObservableCollection<object> { first, second, third };
		var changes = new List<NotifyCollectionChangedAction>();
		rows.CollectionChanged += (_, args) => changes.Add(args.Action);

		ObservableCollectionSynchronizer.Synchronize(
			rows,
			new[] { first, third, second },
			ReferenceEquals);

		RegressionAssert.SequenceEqual(new[] { first, third, second }, rows);
		RegressionAssert.SequenceEqual(new[] { NotifyCollectionChangedAction.Move }, changes);
	}

	public void RemovingOneRowDoesNotMoveOrResetTheRemainingRows()
	{
		var first = new object();
		var removed = new object();
		var last = new object();
		var rows = new ObservableCollection<object> { first, removed, last };
		var changes = new List<NotifyCollectionChangedAction>();
		rows.CollectionChanged += (_, args) => changes.Add(args.Action);

		ObservableCollectionSynchronizer.Synchronize(
			rows,
			new[] { first, last },
			ReferenceEquals);

		RegressionAssert.SequenceEqual(new[] { first, last }, rows);
		RegressionAssert.SequenceEqual(new[] { NotifyCollectionChangedAction.Remove }, changes);
	}

	public void UnchangedLargeCollectionUsesLinearComparisonWork()
	{
		const int itemCount = 2000;
		var desired = Enumerable.Range(0, itemCount).Select(_ => new object()).ToList();
		var rows = new ObservableCollection<object>(desired);
		var comparisons = 0;

		ObservableCollectionSynchronizer.Synchronize(
			rows,
			desired,
			(first, second) =>
			{
				comparisons++;
				return ReferenceEquals(first, second);
			});

		RegressionAssert.SequenceEqual(desired, rows);
		RegressionAssert.True(comparisons <= itemCount);
	}

	public void LargeSeparatorProjectionUsesOneCollectionReset()
	{
		var rows = new ObservableCollectionExtended<int>();
		rows.AddRange(new[] { 0, 1, 2, 3 });
		var inserted = Enumerable.Range(10, 24).ToList();
		var changes = new List<NotifyCollectionChangedAction>();
		rows.CollectionChanged += (_, args) => changes.Add(args.Action);

		VisualDividerProjectionMutation.InsertRange(rows, inserted, 2);

		RegressionAssert.SequenceEqual(
			new[] { 0, 1 }.Concat(inserted).Concat(new[] { 2, 3 }),
			rows);
		RegressionAssert.SequenceEqual(new[] { NotifyCollectionChangedAction.Reset }, changes);

		changes.Clear();
		VisualDividerProjectionMutation.RemoveRange(rows, 2, inserted.Count);

		RegressionAssert.SequenceEqual(new[] { 0, 1, 2, 3 }, rows);
		RegressionAssert.SequenceEqual(new[] { NotifyCollectionChangedAction.Reset }, changes);
	}

	public void SmallSeparatorProjectionKeepsIncrementalNotifications()
	{
		var rows = new ObservableCollectionExtended<int>();
		rows.AddRange(new[] { 0, 3 });
		var inserted = new[] { 1, 2 };
		var changes = new List<NotifyCollectionChangedAction>();
		rows.CollectionChanged += (_, args) => changes.Add(args.Action);

		VisualDividerProjectionMutation.InsertRange(rows, inserted, 1);

		RegressionAssert.SequenceEqual(new[] { 0, 1, 2, 3 }, rows);
		RegressionAssert.SequenceEqual(
			new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add },
			changes);
	}

	public void AnimatedSeparatorProjectionPreservesRecyclableContainers()
	{
		var rows = new ObservableCollectionExtended<int>();
		rows.AddRange(new[] { 0, 100 });
		var members = Enumerable.Range(1, 24).ToList();
		var changes = new List<NotifyCollectionChangedAction>();
		rows.CollectionChanged += (_, args) => changes.Add(args.Action);

		VisualDividerProjectionMutation.InsertRangePreservingContainers(rows, members, 1);
		RegressionAssert.Equal(members.Count, changes.Count);
		RegressionAssert.True(changes.All(change => change == NotifyCollectionChangedAction.Add));

		changes.Clear();
		VisualDividerProjectionMutation.RemoveRangePreservingContainers(rows, 1, members.Count);
		RegressionAssert.SequenceEqual(new[] { 0, 100 }, rows);
		RegressionAssert.Equal(members.Count, changes.Count);
		RegressionAssert.True(changes.All(change => change == NotifyCollectionChangedAction.Remove));
	}

	public void ImportProgressIsSharedAcrossFilesAndNeverExceedsOne()
	{
		const int fileCount = 37;
		const int phasesPerFile = 4;
		var step = ProgressMath.CalculatePhaseStep(fileCount, phasesPerFile);
		var progress = 0d;

		for (var index = 0; index < fileCount * phasesPerFile; index++)
		{
			progress = ProgressMath.AddClamped(progress, step);
		}

		RegressionAssert.True(Math.Abs(progress - 1d) < 0.000001d);
		RegressionAssert.Equal(1d, ProgressMath.AddClamped(progress, step));
	}

	public void EquivalentCategoryAndHealthDataCanReuseExistingRowBindings()
	{
		var firstCategory = new ModCategoryDisplayData(
			"Gameplay", "#E0AA35", "swords", "Gameplay changes", true, false, true);
		var equivalentCategory = new ModCategoryDisplayData(
			"gameplay", "#e0aa35", "SWORDS", "Gameplay changes", true, false, true);
		RegressionAssert.True(firstCategory.Equals(equivalentCategory));

		var mod = new DivinityModData { UUID = "performance-test", Name = "Performance Test" };
		var firstSnapshot = new ModHealthSnapshot(mod, new[]
		{
			new ModHealthFinding(
				ModHealthFindingCode.InvalidUuid,
				ModHealthSeverity.Error,
				"Invalid UUID",
				"Use a valid UUID.",
				new[] { "related-mod" })
		});
		var equivalentSnapshot = new ModHealthSnapshot(mod, new[]
		{
			new ModHealthFinding(
				ModHealthFindingCode.InvalidUuid,
				ModHealthSeverity.Error,
				"Invalid UUID",
				"Use a valid UUID.",
				new[] { "RELATED-MOD" })
		});
		var changedSnapshot = new ModHealthSnapshot(mod, new[]
		{
			new ModHealthFinding(
				ModHealthFindingCode.InvalidUuid,
				ModHealthSeverity.Error,
				"Invalid UUID",
				"Use a different valid UUID.")
		});

		RegressionAssert.True(firstSnapshot.HasEquivalentFindings(equivalentSnapshot));
		RegressionAssert.False(firstSnapshot.HasEquivalentFindings(changedSnapshot));

		mod.Index = 1;
		var firstAdvisorSnapshot = new ModHealthSnapshot(mod, new[]
		{
			new ModHealthFinding(
				ModHealthFindingCode.DependencyLoadsLater,
				ModHealthSeverity.Warning,
				"Dependency placement",
				"Move this package after its dependency.")
		});
		mod.Index = 2;
		var movedAdvisorSnapshot = new ModHealthSnapshot(mod, firstAdvisorSnapshot.Findings);
		RegressionAssert.False(firstAdvisorSnapshot.HasEquivalentFindings(movedAdvisorSnapshot));
	}
}
