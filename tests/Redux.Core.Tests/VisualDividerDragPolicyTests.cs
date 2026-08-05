using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.Collections.Generic;

namespace Redux.Core.Tests;

public sealed class VisualDividerDragPolicyTests
{
	public void NormalModDragNeverIncludesASelectedDivider()
	{
		var divider = CreateDivider("first", collapsed: false);
		var source = CreateMod("source", selected: true);
		var otherSelectedMod = CreateMod("other", selected: true);
		var nextDivider = CreateDivider("next", collapsed: false);
		divider.IsSelected = true;
		nextDivider.IsSelected = true;

		var payload = VisualDividerDragPolicy.ResolveDragItems(
			new[] { divider, source, otherSelectedMod, nextDivider },
			source,
			_ => true);

		RegressionAssert.SequenceEqual(new[] { source, otherSelectedMod }, payload);
	}

	public void ExpandedDividerDragMovesOnlyTheDivider()
	{
		var divider = CreateDivider("expanded", collapsed: false);
		var firstMod = CreateMod("first", selected: true);
		var secondMod = CreateMod("second", selected: true);

		var payload = VisualDividerDragPolicy.ResolveDragItems(
			new[] { divider, firstMod, secondMod },
			divider,
			_ => true);

		RegressionAssert.SequenceEqual(new[] { divider }, payload);
	}

	public void CollapsedDividerDragMovesItsWholeSectionToTheNextDivider()
	{
		var divider = CreateDivider("collapsed", collapsed: true);
		var firstMod = CreateMod("first");
		var secondMod = CreateMod("second");
		var nextDivider = CreateDivider("next", collapsed: false);
		var outsideSection = CreateMod("outside");

		var payload = VisualDividerDragPolicy.ResolveDragItems(
			new[] { divider, firstMod, secondMod, nextDivider, outsideSection },
			divider,
			_ => false);

		RegressionAssert.SequenceEqual(new[] { divider, firstMod, secondMod }, payload);
	}

	public void CollapsedFinalDividerDragIncludesEveryRemainingMod()
	{
		var divider = CreateDivider("final", collapsed: true);
		var firstMod = CreateMod("first");
		var secondMod = CreateMod("second");

		var payload = VisualDividerDragPolicy.ResolveDragItems(
			new[] { divider, firstMod, secondMod },
			divider,
			_ => true);

		RegressionAssert.SequenceEqual(new[] { divider, firstMod, secondMod }, payload);
	}

	public void DropAfterCollapsedSeparatorSkipsItsHiddenSection()
	{
		var firstDivider = CreateDivider("first", collapsed: true);
		var firstMod = CreateMod("first-mod");
		var targetDivider = CreateDivider("target", collapsed: true);
		var targetFirstMod = CreateMod("target-first");
		var targetSecondMod = CreateMod("target-second");
		var nextDivider = CreateDivider("next", collapsed: true);
		var items = new List<DivinityModData>
		{
			firstDivider, firstMod, targetDivider, targetFirstMod, targetSecondMod, nextDivider
		};

		var insertIndex = VisualModListDropPolicy.ResolveInsertionIndex(
			items,
			items.IndexOf(targetDivider),
			insertAfter: true);

		RegressionAssert.Equal(items.IndexOf(nextDivider), insertIndex);
	}

	public void CollapsedSectionMovedBetweenCollapsedSectionsKeepsEveryBlockIntact()
	{
		var movedDivider = CreateDivider("moved", collapsed: true);
		var movedFirstMod = CreateMod("moved-first");
		var movedSecondMod = CreateMod("moved-second");
		var targetDivider = CreateDivider("target", collapsed: true);
		var targetFirstMod = CreateMod("target-first");
		var targetSecondMod = CreateMod("target-second");
		var nextDivider = CreateDivider("next", collapsed: true);
		var nextMod = CreateMod("next-mod");
		var items = new List<DivinityModData>
		{
			movedDivider, movedFirstMod, movedSecondMod,
			targetDivider, targetFirstMod, targetSecondMod,
			nextDivider, nextMod
		};
		var payload = VisualDividerDragPolicy.ResolveDragItems(items, movedDivider, _ => true);
		var insertIndex = VisualModListDropPolicy.ResolveInsertionIndex(
			items,
			items.IndexOf(targetDivider),
			insertAfter: true);

		var result = VisualModListDropPolicy.Apply(items, Array.Empty<DivinityModData>(), payload, true, insertIndex);

		RegressionAssert.SequenceEqual(
			new[]
			{
				targetDivider, targetFirstMod, targetSecondMod,
				movedDivider, movedFirstMod, movedSecondMod,
				nextDivider, nextMod
			},
			result.ActiveItems);
	}

	public void CollapseAllChangesOnlyTheRequestedPaneAndOnlyOnce()
	{
		var firstActive = new ModListVisualDividerData { IsActiveList = true };
		var secondActive = new ModListVisualDividerData { IsActiveList = true, IsCollapsed = true };
		var inactive = new ModListVisualDividerData { IsActiveList = false };

		var changed = VisualDividerStatePolicy.SetAllCollapsed(
			new[] { firstActive, secondActive, inactive },
			activeList: true,
			collapsed: true);

		RegressionAssert.Equal(1, changed);
		RegressionAssert.True(firstActive.IsCollapsed);
		RegressionAssert.True(secondActive.IsCollapsed);
		RegressionAssert.False(inactive.IsCollapsed);
		RegressionAssert.Equal(0, VisualDividerStatePolicy.SetAllCollapsed(
			new[] { firstActive, secondActive, inactive }, true, true));
	}

	private static DivinityModData CreateDivider(string id, bool collapsed) => new()
	{
		UUID = $"divider-{id}",
		Name = id,
		IsVisualDivider = true,
		IsVisualDividerCollapsed = collapsed,
		CanDrag = true
	};

	private static DivinityModData CreateMod(string id, bool selected = false) => new()
	{
		UUID = id,
		Name = id,
		IsSelected = selected,
		CanDrag = true
	};
}
