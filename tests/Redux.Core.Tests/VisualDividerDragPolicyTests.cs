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

	public void ExpandedDividerDragStillCarriesItsSection()
	{
		var divider = CreateDivider("expanded", collapsed: false);
		var firstMod = CreateMod("first", selected: true);
		var secondMod = CreateMod("second", selected: true);
		var nextDivider = CreateDivider("next", collapsed: false);
		var outsideSection = CreateMod("outside");

		var payload = VisualDividerDragPolicy.ResolveDragItems(
			new[] { divider, firstMod, secondMod, nextDivider, outsideSection },
			divider,
			_ => true);

		// Moving the bare marker would hand these mods to whichever separator ends up above them.
		RegressionAssert.SequenceEqual(new[] { divider, firstMod, secondMod }, payload);
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

	public void DropAfterCollapsedSeparatorLandsPastItsHiddenSection()
	{
		var firstDivider = CreateDivider("first", collapsed: true);
		var firstMod = CreateMod("first-mod");
		var targetDivider = CreateDivider("target", collapsed: true);
		var targetFirstMod = CreateMod("target-first");
		var targetSecondMod = CreateMod("target-second");
		var nextDivider = CreateDivider("next", collapsed: true);
		var sequence = new List<DivinityModData>
		{
			firstDivider, firstMod, targetDivider, targetFirstMod, targetSecondMod, nextDivider
		};
		// Collapsed sections are withheld from the list view, so only separators are visible.
		var visible = new List<DivinityModData> { firstDivider, targetDivider, nextDivider };

		var visibleIndex = VisualModListDropPolicy.ResolveInsertionIndex(
			visible,
			visible.IndexOf(targetDivider),
			insertAfter: true);
		var insertIndex = VisualModListDropPolicy.ResolveSequenceInsertIndex(sequence, visible, visibleIndex);

		RegressionAssert.Equal(sequence.IndexOf(nextDivider), insertIndex);
	}

	public void DroppingPastTheLastVisibleRowLandsAfterEveryHiddenRow()
	{
		var divider = CreateDivider("only", collapsed: true);
		var firstMod = CreateMod("first");
		var secondMod = CreateMod("second");
		var sequence = new List<DivinityModData> { divider, firstMod, secondMod };
		var visible = new List<DivinityModData> { divider };

		var visibleIndex = VisualModListDropPolicy.ResolveInsertionIndex(
			visible,
			visible.IndexOf(divider),
			insertAfter: true);
		var insertIndex = VisualModListDropPolicy.ResolveSequenceInsertIndex(sequence, visible, visibleIndex);

		RegressionAssert.Equal(sequence.Count, insertIndex);
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
		var visible = new List<DivinityModData> { movedDivider, targetDivider, nextDivider };
		var payload = VisualDividerDragPolicy.ResolveDragItems(items, movedDivider, _ => true);
		var visibleIndex = VisualModListDropPolicy.ResolveInsertionIndex(
			visible,
			visible.IndexOf(targetDivider),
			insertAfter: true);
		var insertIndex = VisualModListDropPolicy.ResolveSequenceInsertIndex(items, visible, visibleIndex);

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

	public void CollapsedSectionDroppedInsideAnotherSectionNeverAbsorbsItsMods()
	{
		var movedDivider = CreateDivider("moved", collapsed: true);
		var movedMod = CreateMod("moved-mod");
		var hostDivider = CreateDivider("host", collapsed: false);
		var hostFirstMod = CreateMod("host-first");
		var hostSecondMod = CreateMod("host-second");
		var items = new List<DivinityModData>
		{
			movedDivider, movedMod, hostDivider, hostFirstMod, hostSecondMod
		};
		var payload = VisualDividerDragPolicy.ResolveDragItems(items, movedDivider, _ => true);

		// Aimed between the host section's two mods, which would otherwise fall under
		// the moved separator once it is expanded again.
		var insertIndex = VisualModListDropPolicy.SnapToSectionBoundary(
			items,
			items.IndexOf(hostSecondMod));
		var result = VisualModListDropPolicy.Apply(items, Array.Empty<DivinityModData>(), payload, true, insertIndex);

		RegressionAssert.SequenceEqual(
			new[] { hostDivider, hostFirstMod, hostSecondMod, movedDivider, movedMod },
			result.ActiveItems);
	}

	public void CollapsedSectionSnapsBackwardWhenTheNearestBoundaryIsAbove()
	{
		var hostDivider = CreateDivider("host", collapsed: false);
		var hostFirstMod = CreateMod("host-first");
		var hostSecondMod = CreateMod("host-second");
		var hostThirdMod = CreateMod("host-third");
		var items = new List<DivinityModData> { hostDivider, hostFirstMod, hostSecondMod, hostThirdMod };

		RegressionAssert.Equal(0, VisualModListDropPolicy.SnapToSectionBoundary(items, 1));
		RegressionAssert.Equal(items.Count, VisualModListDropPolicy.SnapToSectionBoundary(items, 3));
	}

	public void CollapsedSectionDroppedAboveUnsectionedModsLandsBelowThem()
	{
		var looseFirstMod = CreateMod("loose-first");
		var looseSecondMod = CreateMod("loose-second");
		var divider = CreateDivider("section", collapsed: true);
		var sectionMod = CreateMod("section-mod");
		var items = new List<DivinityModData> { looseFirstMod, looseSecondMod, divider, sectionMod };

		// Index 0 would put the loose mods under the separator, so the only safe
		// boundary at or near the top of the list is the separator itself.
		RegressionAssert.Equal(2, VisualModListDropPolicy.SnapToSectionBoundary(items, 0));
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
