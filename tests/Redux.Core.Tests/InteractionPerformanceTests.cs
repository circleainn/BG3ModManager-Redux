using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Util;

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
