using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class VisualDividerDragPolicyTests
{
	public void PureOverrideModsCanReorderOnlyWithinTheirOwnPane()
	{
		var pureOverride = CreateMod("override");
		pureOverride.CanDrag = false;

		RegressionAssert.True(VisualDividerDragPolicy.CanStartDrag(pureOverride, withinOverridePane: true));
		RegressionAssert.False(VisualDividerDragPolicy.CanStartDrag(pureOverride, withinOverridePane: false));
	}

	public void EstablishedSectionsFollowTheirMembersAfterMultiModChanges()
	{
		var addedFirst = CreateMod("added-first");
		var addedSecond = CreateMod("added-second");
		var firstMember = CreateMod("first-member");
		var secondMember = CreateMod("second-member");
		var divider = new ModListVisualDividerData
		{
			Id = "section", IsActiveList = true, Position = 0,
			MemberModUuids = [firstMember.UUID, secondMember.UUID]
		};

		var changed = VisualDividerSectionPolicy.ReanchorPositionsToMembers(
			new[] { addedFirst, addedSecond, firstMember, secondMember },
			new[] { divider },
			activeList: true);

		RegressionAssert.True(changed);
		RegressionAssert.Equal(2, divider.Position);
		var projected = VisualDividerSectionPolicy.BuildVisualSequence(
			new[] { addedFirst, addedSecond, firstMember, secondMember },
			new[] { divider },
			activeList: true,
			_ => CreateDivider("section", collapsed: false));
		RegressionAssert.SequenceEqual(
			new[] { addedFirst, addedSecond, projected[2], firstMember, secondMember },
			projected);
	}

	public void InactivePaneAcceptsSeparatorsAndKeepsClosedBlocksInTheirPane()
	{
		var divider = CreateDivider("section", collapsed: false);
		var mod = CreateMod("ordinary-mod");

		RegressionAssert.True(VisualDividerDragPolicy.ContainsVisualDivider(new[] { divider }));
		RegressionAssert.False(VisualDividerDragPolicy.ContainsVisualDivider(new[] { mod }));
		RegressionAssert.True(VisualDividerDragPolicy.CanDropOnPane(new[] { divider }, destinationActive: false));
		divider.IsVisualDividerCollapsed = true;
		divider.IsActive = false;
		RegressionAssert.True(VisualDividerDragPolicy.CanDropOnPane(new[] { divider }, destinationActive: false));
		RegressionAssert.False(VisualDividerDragPolicy.CanDropOnPane(new[] { divider }, destinationActive: true));
		divider.IsVisualDividerCollapsed = false;
		RegressionAssert.True(VisualDividerDragPolicy.CanDropOnPane(new[] { divider }, destinationActive: true));
		RegressionAssert.True(VisualDividerDragPolicy.CanDropOnPane(new[] { mod }, destinationActive: false));
	}

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

	public void ExpandedDividerDragContainsOnlyItsMarker()
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

	public void CollapsedDividerDragStartsWithLightweightMarker()
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

		RegressionAssert.SequenceEqual(new[] { divider }, payload);
	}

	public void CollapsedSeparatorPayloadCarriesOnlyItsSealedMembers()
	{
		var visibleMarker = CreateDivider("closed", collapsed: true);
		var canonicalMarker = CreateDivider("closed", collapsed: true);
		var firstMember = CreateMod("first-member");
		var secondMember = CreateMod("second-member");
		var looseMod = CreateMod("loose");
		var nextMarker = CreateDivider("next", collapsed: false);
		var divider = new ModListVisualDividerData
		{
			Id = "closed", IsActiveList = true, IsCollapsed = true,
			MemberModUuids = new List<string> { firstMember.UUID, secondMember.UUID }
		};

		var payload = VisualDividerSectionPolicy.ResolveCollapsedBlockDragPayload(
			new[] { canonicalMarker, firstMember, secondMember, looseMod, nextMarker },
			visibleMarker,
			divider);

		RegressionAssert.SequenceEqual(
			new[] { canonicalMarker, firstMember, secondMember },
			payload);
		RegressionAssert.False(payload.Contains(looseMod));
	}

	public void CollapsedSeparatorMovesAroundAnotherClosedBlockWithoutAbsorption()
	{
		var movedMarker = CreateDivider("moved", collapsed: true);
		var movedFirst = CreateMod("moved-first");
		var movedSecond = CreateMod("moved-second");
		var looseBefore = CreateMod("loose-before");
		var targetMarker = CreateDivider("target", collapsed: true);
		var targetFirst = CreateMod("target-first");
		var targetSecond = CreateMod("target-second");
		var looseAfter = CreateMod("loose-after");
		var sequence = new[]
		{
			movedMarker, movedFirst, movedSecond, looseBefore,
			targetMarker, targetFirst, targetSecond, looseAfter
		};
		var movedDivider = new ModListVisualDividerData
		{
			Id = "moved", IsActiveList = true, IsCollapsed = true,
			MemberModUuids = new List<string> { movedFirst.UUID, movedSecond.UUID }
		};
		var targetDivider = new ModListVisualDividerData
		{
			Id = "target", IsActiveList = true, IsCollapsed = true,
			MemberModUuids = new List<string> { targetFirst.UUID, targetSecond.UUID }
		};
		var payload = VisualDividerSectionPolicy.ResolveCollapsedBlockDragPayload(
			sequence, movedMarker, movedDivider);
		var visible = new[] { movedMarker, looseBefore, targetMarker, looseAfter };
		var insertIndex = VisualModListDropPolicy.MapVisibleInsertionIndex(
			visible,
			sequence,
			visibleInsertionIndex: 3);

		var result = VisualModListDropPolicy.Apply(
			sequence,
			Array.Empty<DivinityModData>(),
			payload,
			true,
			insertIndex);
		VisualDividerSectionPolicy.AssignMembersPreservingCollapsedSections(
			result.ActiveItems, new[] { movedDivider, targetDivider }, true);

		RegressionAssert.SequenceEqual(
			new[]
			{
				looseBefore, targetMarker, targetFirst, targetSecond,
				movedMarker, movedFirst, movedSecond, looseAfter
			},
			result.ActiveItems);
		RegressionAssert.SequenceEqual(
			new[] { movedFirst.UUID, movedSecond.UUID },
			movedDivider.MemberModUuids);
		RegressionAssert.SequenceEqual(
			new[] { targetFirst.UUID, targetSecond.UUID },
			targetDivider.MemberModUuids);
		var hidden = VisualDividerSectionPolicy.GetCollapsedMemberIds(
			result.ActiveItems, new[] { movedDivider, targetDivider }, true);
		RegressionAssert.False(hidden.Contains(looseBefore.UUID));
		RegressionAssert.False(hidden.Contains(looseAfter.UUID));

		var movedAgainPayload = VisualDividerSectionPolicy.ResolveCollapsedBlockDragPayload(
			result.ActiveItems, movedMarker, movedDivider);
		var movedAgainVisible = new[] { looseBefore, targetMarker, movedMarker, looseAfter };
		var movedAgainInsertIndex = VisualModListDropPolicy.MapVisibleInsertionIndex(
			movedAgainVisible,
			result.ActiveItems,
			visibleInsertionIndex: 1);
		var movedAgain = VisualModListDropPolicy.Apply(
			result.ActiveItems,
			Array.Empty<DivinityModData>(),
			movedAgainPayload,
			true,
			movedAgainInsertIndex);
		VisualDividerSectionPolicy.AssignMembersPreservingCollapsedSections(
			movedAgain.ActiveItems, new[] { movedDivider, targetDivider }, true);

		RegressionAssert.SequenceEqual(
			new[]
			{
				looseBefore, movedMarker, movedFirst, movedSecond,
				targetMarker, targetFirst, targetSecond, looseAfter
			},
			movedAgain.ActiveItems);
		RegressionAssert.SequenceEqual(
			new[] { movedFirst.UUID, movedSecond.UUID },
			movedDivider.MemberModUuids);
		RegressionAssert.SequenceEqual(
			new[] { targetFirst.UUID, targetSecond.UUID },
			targetDivider.MemberModUuids);
	}

	public void ExpandedSeparatorMoveLeavesEveryModInPlace()
	{
		var movedDivider = CreateDivider("moved", collapsed: false);
		var movedFirst = CreateMod("moved-first");
		var movedSecond = CreateMod("moved-second");
		var targetDivider = CreateDivider("target", collapsed: false);
		var targetFirst = CreateMod("target-first");
		var targetSecond = CreateMod("target-second");
		var nextDivider = CreateDivider("next", collapsed: false);
		var sequence = new[]
		{
			movedDivider, movedFirst, movedSecond,
			targetDivider, targetFirst, targetSecond,
			nextDivider
		};
		var descriptors = new[]
		{
			new ModListVisualDividerData
			{
				Id = "moved", IsActiveList = true,
				MemberModUuids = new List<string> { movedFirst.UUID, movedSecond.UUID }
			},
			new ModListVisualDividerData
			{
				Id = "target", IsActiveList = true,
				MemberModUuids = new List<string> { targetFirst.UUID, targetSecond.UUID }
			},
			new ModListVisualDividerData
			{
				Id = "next", IsActiveList = true, MemberModUuids = new List<string>()
			}
		};

		var sourcePayload = VisualDividerDragPolicy.ResolveDragItems(
			sequence, movedDivider, _ => true);
		var payload = VisualDividerSectionPolicy.ResolveMarkerOnlyDragPayload(
			sequence, sourcePayload);
		var result = VisualModListDropPolicy.Apply(
			sequence,
			Array.Empty<DivinityModData>(),
			payload,
			true,
			Array.IndexOf(sequence, targetSecond));
		VisualDividerSectionPolicy.AssignMembersByCurrentBoundaries(
			result.ActiveItems, descriptors, true);

		RegressionAssert.SequenceEqual(new[] { movedDivider }, payload);
		RegressionAssert.SequenceEqual(
			new[] { movedFirst, movedSecond, targetDivider, targetFirst, movedDivider, targetSecond, nextDivider },
			result.ActiveItems);
		RegressionAssert.SequenceEqual(
			new[] { movedFirst, movedSecond, targetFirst, targetSecond },
			result.ActiveItems.Where(item => !item.IsVisualDivider));
		RegressionAssert.SequenceEqual(new[] { targetSecond.UUID }, descriptors[0].MemberModUuids);
		RegressionAssert.SequenceEqual(new[] { targetFirst.UUID }, descriptors[1].MemberModUuids);
		RegressionAssert.Equal(0, descriptors[2].MemberModUuids.Count);
	}

	public void RecreatedExpandedSeparatorResolvesToCanonicalMarkerOnly()
	{
		var canonicalDivider = CreateDivider("section", collapsed: false);
		var visibleDivider = CreateDivider("section", collapsed: false);
		var member = CreateMod("member");

		var payload = VisualDividerSectionPolicy.ResolveMarkerOnlyDragPayload(
			new[] { canonicalDivider, member },
			new[] { visibleDivider });

		RegressionAssert.SequenceEqual(new[] { canonicalDivider }, payload);
		RegressionAssert.True(payload.All(item => !ReferenceEquals(item, visibleDivider)));
	}

	public void DropAfterCollapsedSeparatorSkipsItsHiddenSection()
	{
		var firstDivider = CreateDivider("first", collapsed: true);
		var firstMod = CreateMod("first-mod");
		var targetDivider = CreateDivider("target", collapsed: true);
		var targetFirstMod = CreateMod("target-first");
		var targetSecondMod = CreateMod("target-second");
		var looseMod = CreateMod("loose");
		var nextDivider = CreateDivider("next", collapsed: true);
		var items = new List<DivinityModData>
		{
			firstDivider, firstMod, targetDivider, targetFirstMod, targetSecondMod, looseMod, nextDivider
		};

		var insertIndex = VisualModListDropPolicy.ResolveInsertionIndex(
			items,
			items.IndexOf(targetDivider),
			insertAfter: true,
			new[] { targetFirstMod.UUID, targetSecondMod.UUID });

		RegressionAssert.Equal(items.IndexOf(looseMod), insertIndex);
	}

	public void CollapsedSeparatorDoesNotAdoptAModDroppedBelowItsClosedBlock()
	{
		var collapsedMarker = CreateDivider("closed", collapsed: true);
		var firstMember = CreateMod("closed-first");
		var secondMember = CreateMod("closed-second");
		var sourceMarker = CreateDivider("source", collapsed: false);
		var movedMod = CreateMod("moved");
		var sequence = new[]
		{
			collapsedMarker, firstMember, secondMember, sourceMarker, movedMod
		};
		var descriptors = new[]
		{
			new ModListVisualDividerData
			{
				Id = "closed", IsActiveList = true, IsCollapsed = true,
				MemberModUuids = new List<string> { firstMember.UUID, secondMember.UUID }
			},
			new ModListVisualDividerData
			{
				Id = "source", IsActiveList = true,
				MemberModUuids = new List<string> { movedMod.UUID }
			}
		};

		var result = VisualModListDropPolicy.Apply(
			sequence,
			Array.Empty<DivinityModData>(),
			new[] { movedMod },
			true,
			Array.IndexOf(sequence, sourceMarker));
		VisualDividerSectionPolicy.AssignMembersPreservingCollapsedSections(
			result.ActiveItems, descriptors, true);

		RegressionAssert.SequenceEqual(
			new[] { collapsedMarker, firstMember, secondMember, movedMod, sourceMarker },
			result.ActiveItems);
		RegressionAssert.SequenceEqual(
			new[] { firstMember.UUID, secondMember.UUID },
			descriptors[0].MemberModUuids);
		RegressionAssert.Equal(0, descriptors[1].MemberModUuids.Count);
		var hidden = VisualDividerSectionPolicy.GetCollapsedMemberIds(
			result.ActiveItems, descriptors, true);
		RegressionAssert.True(hidden.Contains(firstMember.UUID));
		RegressionAssert.True(hidden.Contains(secondMember.UUID));
		RegressionAssert.False(hidden.Contains(movedMod.UUID));
	}

	public void MovingASeparatorAboveAClosedSectionCannotChangeItsContents()
	{
		var collapsedMarker = CreateDivider("closed", collapsed: true);
		var firstMember = CreateMod("closed-first");
		var secondMember = CreateMod("closed-second");
		var movedMarker = CreateDivider("moved", collapsed: false);
		var leftBehindMod = CreateMod("left-behind");
		var nextMarker = CreateDivider("next", collapsed: false);
		var sequence = new[]
		{
			collapsedMarker, firstMember, secondMember, movedMarker, leftBehindMod, nextMarker
		};
		var descriptors = new[]
		{
			new ModListVisualDividerData
			{
				Id = "closed", IsActiveList = true, IsCollapsed = true,
				MemberModUuids = new List<string> { firstMember.UUID, secondMember.UUID }
			},
			new ModListVisualDividerData
			{
				Id = "moved", IsActiveList = true,
				MemberModUuids = new List<string> { leftBehindMod.UUID }
			},
			new ModListVisualDividerData
			{
				Id = "next", IsActiveList = true, MemberModUuids = new List<string>()
			}
		};

		var result = VisualModListDropPolicy.Apply(
			sequence,
			Array.Empty<DivinityModData>(),
			new[] { movedMarker },
			true,
			0);
		VisualDividerSectionPolicy.AssignMembersPreservingCollapsedSections(
			result.ActiveItems, descriptors, true);

		RegressionAssert.SequenceEqual(
			new[] { movedMarker, collapsedMarker, firstMember, secondMember, leftBehindMod, nextMarker },
			result.ActiveItems);
		RegressionAssert.Equal(0, descriptors[1].MemberModUuids.Count);
		RegressionAssert.SequenceEqual(
			new[] { firstMember.UUID, secondMember.UUID },
			descriptors[0].MemberModUuids);
		RegressionAssert.False(VisualDividerSectionPolicy.GetCollapsedMemberIds(
			result.ActiveItems, descriptors, true).Contains(leftBehindMod.UUID));

		VisualDividerSectionPolicy.AssignMembersPreservingCollapsedSections(
			result.ActiveItems,
			descriptors,
			true,
			expandingDividerId: descriptors[0].Id);
		RegressionAssert.SequenceEqual(
			new[] { firstMember.UUID, secondMember.UUID, leftBehindMod.UUID },
			descriptors[0].MemberModUuids);
	}

	public void VisibleDropSlotMapsPastOmittedCollapsedMembers()
	{
		var divider = CreateDivider("collapsed", collapsed: true);
		var firstHidden = CreateMod("first-hidden");
		var secondHidden = CreateMod("second-hidden");
		var loose = CreateMod("loose");
		var nextDivider = CreateDivider("next", collapsed: false);
		var full = new[] { divider, firstHidden, secondHidden, loose, nextDivider };
		var visible = new[] { divider, loose, nextDivider };

		var mapped = VisualModListDropPolicy.MapVisibleInsertionIndex(
			visible,
			full,
			visibleInsertionIndex: 1);

		RegressionAssert.Equal(3, mapped);
	}

	public void FilteredReorderUsesVisibleRowsAsCanonicalAnchors()
	{
		var full = Enumerable.Range(1, 15)
			.Select(index => CreateMod(index.ToString()))
			.ToArray();
		var visible = new[] { full[0], full[2], full[4], full[9], full[14] };
		var moved = full[9];
		var insertIndex = VisualModListDropPolicy.MapVisibleInsertionIndex(
			visible,
			full,
			visibleInsertionIndex: 2);

		var result = VisualModListDropPolicy.Apply(
			full,
			Array.Empty<DivinityModData>(),
			new[] { moved },
			true,
			insertIndex);

		RegressionAssert.SequenceEqual(
			new[] { "1", "2", "3", "4", "10", "5", "6", "7", "8", "9", "11", "12", "13", "14", "15" },
			result.ActiveItems.Select(item => item.UUID));
	}

	public void VisibleDropSlotMatchesRecreatedDividerByIdentity()
	{
		var fullDivider = CreateDivider("section", collapsed: true);
		var visibleDivider = CreateDivider("section", collapsed: true);
		var looseBefore = CreateMod("loose-before");
		var hidden = CreateMod("hidden");
		var next = CreateMod("next");

		var mapped = VisualModListDropPolicy.MapVisibleInsertionIndex(
			new[] { looseBefore, visibleDivider, next },
			new[] { looseBefore, fullDivider, hidden, next },
			visibleInsertionIndex: 1);

		RegressionAssert.Equal(1, mapped);
		RegressionAssert.Equal(4, VisualModListDropPolicy.MapVisibleInsertionIndex(
			new[] { looseBefore, visibleDivider, next },
			new[] { looseBefore, fullDivider, hidden, next },
			visibleInsertionIndex: 3));
	}

	public void ProgressiveExpansionInsertsBeforeUnownedDestinationSuffix()
	{
		var marker = CreateDivider("moved", collapsed: false);
		var firstMember = CreateMod("first-member");
		var secondMember = CreateMod("second-member");
		var destinationSuffix = CreateMod("destination-suffix");

		RegressionAssert.Equal(1,
			VisualDividerSectionPolicy.ResolveExpansionInsertionIndex(
				new[] { marker, destinationSuffix }, marker, new[] { firstMember, secondMember }));
		RegressionAssert.Equal(2,
			VisualDividerSectionPolicy.ResolveExpansionInsertionIndex(
				new[] { marker, firstMember, destinationSuffix }, marker, new[] { firstMember, secondMember }));
	}

	public void ExpansionRestoresClosedMembersBeforeNewlyAdoptedRows()
	{
		var marker = CreateDivider("moved", collapsed: false);
		var carriedFirst = CreateMod("carried-first");
		var carriedSecond = CreateMod("carried-second");
		var newlyAdoptedFirst = CreateMod("new-first");
		var newlyAdoptedSecond = CreateMod("new-second");

		var insertIndex = VisualDividerSectionPolicy.ResolveExpansionInsertionIndex(
			new[] { marker, newlyAdoptedFirst, newlyAdoptedSecond },
			marker,
			new[] { carriedFirst, carriedSecond });

		RegressionAssert.Equal(1, insertIndex);
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

	public void BulkSeparatorToggleClosesMixedPanesBeforeReopeningThem()
	{
		var expanded = new ModListVisualDividerData { IsActiveList = true };
		var collapsed = new ModListVisualDividerData { IsActiveList = true, IsCollapsed = true };
		var inactive = new ModListVisualDividerData { IsActiveList = false };

		RegressionAssert.Equal(true, VisualDividerStatePolicy.ResolveToggleTarget(
			new[] { expanded, collapsed, inactive }, activeList: true));
		expanded.IsCollapsed = true;
		RegressionAssert.Equal(false, VisualDividerStatePolicy.ResolveToggleTarget(
			new[] { expanded, collapsed, inactive }, activeList: true));
		RegressionAssert.True(VisualDividerStatePolicy.ResolveToggleTarget(
			Array.Empty<ModListVisualDividerData>(), activeList: true) == null);
	}

	public void LegacyPositionsMigrateToDurableSectionMembership()
	{
		var first = CreateMod("first");
		var second = CreateMod("second");
		var third = CreateMod("third");
		var firstDivider = new ModListVisualDividerData { Id = "section-a", IsActiveList = true, Position = 0 };
		var secondDivider = new ModListVisualDividerData { Id = "section-b", IsActiveList = true, Position = 3 };

		RegressionAssert.True(VisualDividerSectionPolicy.MigrateLegacyMembership(
			new[] { first, second, third }, new[] { firstDivider, secondDivider }, true));
		RegressionAssert.SequenceEqual(new[] { "first", "second" }, firstDivider.MemberModUuids);
		RegressionAssert.SequenceEqual(new[] { "third" }, secondDivider.MemberModUuids);
		RegressionAssert.False(VisualDividerSectionPolicy.MigrateLegacyMembership(
			new[] { first, second, third }, new[] { firstDivider, secondDivider }, true));
	}

	public void LegacyMembershipWaitsForCompletedListLoading()
	{
		var first = CreateMod("first");
		var divider = new ModListVisualDividerData
		{
			Id = "section", IsActiveList = true, Position = 0
		};

		RegressionAssert.False(VisualDividerSectionPolicy.MigrateLegacyMembership(
			new[] { first }, new[] { divider }, true, migrationReady: false));
		RegressionAssert.True(divider.MemberModUuids == null);
		RegressionAssert.True(VisualDividerSectionPolicy.MigrateLegacyMembership(
			new[] { first }, new[] { divider }, true, migrationReady: true));
		RegressionAssert.SequenceEqual(new[] { first.UUID }, divider.MemberModUuids!);
	}

	public void VisualSequencePreservesAuthoritativeModOrder()
	{
		var first = CreateMod("first");
		var second = CreateMod("second");
		var loose = CreateMod("loose");
		var divider = new ModListVisualDividerData
		{
			Id = "section",
			IsActiveList = true,
			Position = 1,
			MemberModUuids = new List<string> { first.UUID, second.UUID }
		};

		var sequence = VisualDividerSectionPolicy.BuildVisualSequence(
			new[] { first, second, loose },
			new[] { divider },
			true,
			CreateDividerItem);

		RegressionAssert.SequenceEqual(
			new[] { first, sequence[1], second, loose },
			sequence);
		RegressionAssert.True(sequence[1].IsVisualDivider);
	}

	public void DuplicateOwnershipKeepsFirstDividerAndMissingIds()
	{
		var first = new ModListVisualDividerData
		{
			Id = "first", IsActiveList = true, Position = 0,
			MemberModUuids = new List<string> { "missing", "shared" }
		};
		var second = new ModListVisualDividerData
		{
			Id = "second", IsActiveList = true, Position = 3,
			MemberModUuids = new List<string> { "SHARED", "other" }
		};

		RegressionAssert.True(VisualDividerSectionPolicy.NormalizeOwnership(new[] { first, second }, true));
		RegressionAssert.SequenceEqual(new[] { "missing", "shared" }, first.MemberModUuids);
		RegressionAssert.SequenceEqual(new[] { "other" }, second.MemberModUuids);
	}

	public void CollapsedVisibilityUsesExplicitMembershipOnly()
	{
		var divider = new ModListVisualDividerData
		{
			Id = "section", IsActiveList = true, IsCollapsed = true,
			MemberModUuids = new List<string> { "member", "missing" }
		};

		var hidden = VisualDividerSectionPolicy.GetCollapsedMemberIds(new[] { divider }, true);

		RegressionAssert.True(hidden.Contains("member"));
		RegressionAssert.True(hidden.Contains("missing"));
		RegressionAssert.False(hidden.Contains("loose"));
	}

	public void CollapsedVisibilityStopsAtTheNextSeparator()
	{
		var firstDivider = CreateDivider("first", collapsed: true);
		var first = CreateMod("first-member");
		var secondDivider = CreateDivider("second", collapsed: false);
		var second = CreateMod("second-member");
		var thirdDivider = CreateDivider("third", collapsed: true);
		var third = CreateMod("third-member");

		var hidden = VisualDividerSectionPolicy.GetCollapsedMemberIds(new[]
		{
			firstDivider, first,
			secondDivider, second,
			thirdDivider, third
		});

		RegressionAssert.True(hidden.Contains(first.UUID));
		RegressionAssert.False(hidden.Contains(second.UUID));
		RegressionAssert.True(hidden.Contains(third.UUID));
	}

	public void ExpandedParentDragCarriesChildMarkersButLeavesMods()
	{
		var parent = new ModListVisualDividerData { Id = "parent", IsActiveList = true, Position = 0 };
		var child = new ModListVisualDividerData
		{
			Id = "child", ParentDividerId = parent.Id, IsActiveList = true, Position = 2
		};
		var parentMarker = CreateDivider("parent", false);
		var first = CreateMod("first");
		var childMarker = CreateDivider("child", false);
		var second = CreateMod("second");

		var payload = VisualDividerHierarchyPolicy.ResolveExpandedMarkerPayload(
			[parentMarker, first, childMarker, second], [parent, child], parent);

		RegressionAssert.SequenceEqual([parentMarker, childMarker], payload);
	}

	public void CollapsedParentDragCarriesItsCompleteNestedBlock()
	{
		var parent = new ModListVisualDividerData { Id = "parent", IsActiveList = true, IsCollapsed = true, Position = 0 };
		var child = new ModListVisualDividerData
		{
			Id = "child", ParentDividerId = parent.Id, IsActiveList = true, Position = 2
		};
		var next = new ModListVisualDividerData { Id = "next", IsActiveList = true, Position = 4 };
		var parentMarker = CreateDivider("parent", true);
		var first = CreateMod("first");
		var childMarker = CreateDivider("child", false);
		var second = CreateMod("second");
		var nextMarker = CreateDivider("next", false);
		var outside = CreateMod("outside");

		var payload = VisualDividerHierarchyPolicy.ResolveCollapsedPayload(
			[parentMarker, first, childMarker, second, nextMarker, outside],
			[parent, child, next], parent);

		RegressionAssert.SequenceEqual([parentMarker, first, childMarker, second], payload);
	}

	public void CollapsedParentProjectionHidesChildrenAndAllNestedMods()
	{
		var parent = new ModListVisualDividerData { Id = "parent", IsActiveList = true, IsCollapsed = true, Position = 0 };
		var child = new ModListVisualDividerData
		{
			Id = "child", ParentDividerId = parent.Id, IsActiveList = true, Position = 2
		};
		var next = new ModListVisualDividerData { Id = "next", IsActiveList = true, Position = 4 };
		var first = CreateMod("first");
		var second = CreateMod("second");
		var outside = CreateMod("outside");
		var projection = VisualDividerHierarchyPolicy.ResolveProjection(
			[CreateDivider("parent", true), first, CreateDivider("child", false), second,
				CreateDivider("next", false), outside],
			[parent, child, next], true);

		RegressionAssert.True(projection.HiddenDividerIds.Contains("child"));
		RegressionAssert.True(projection.HiddenModUuids.Contains(first.UUID));
		RegressionAssert.True(projection.HiddenModUuids.Contains(second.UUID));
		RegressionAssert.False(projection.HiddenModUuids.Contains(outside.UUID));
	}

	public void ChildSectionIndentationFollowsOnlyItsOwnedRows()
	{
		var parent = new ModListVisualDividerData { Id = "parent", IsActiveList = true, Position = 0 };
		var child = new ModListVisualDividerData
		{
			Id = "child", ParentDividerId = parent.Id, IsActiveList = true, Position = 2
		};
		var lastChild = new ModListVisualDividerData
		{
			Id = "last-child", ParentDividerId = parent.Id, IsActiveList = true, Position = 4
		};
		var next = new ModListVisualDividerData { Id = "next", IsActiveList = true, Position = 6 };
		var parentMod = CreateMod("parent-mod");
		var firstChildMod = CreateMod("child-first");
		var secondChildMod = CreateMod("child-second");
		var outside = CreateMod("outside");

		var indented = VisualDividerHierarchyPolicy.ResolveIndentedModIds(
			[CreateDivider("parent", false), parentMod, CreateDivider("child", false),
				firstChildMod, CreateDivider("last-child", false), secondChildMod,
				CreateDivider("next", false), outside],
			[parent, child, lastChild, next], true);

		RegressionAssert.False(indented.Contains(parentMod.UUID));
		RegressionAssert.True(indented.Contains(firstChildMod.UUID));
		RegressionAssert.True(indented.Contains(secondChildMod.UUID));
		RegressionAssert.False(indented.Contains(outside.UUID));

	}

	public void HierarchyValidationPreventsNestingAndMatchesParentPersistence()
	{
		var localParent = new ModListVisualDividerData { Id = "local", IsActiveList = true };
		var invalidGlobalChild = new ModListVisualDividerData
		{
			Id = "global-child", ParentDividerId = localParent.Id, IsActiveList = true, IsGlobal = true
		};
		var nested = new ModListVisualDividerData
		{
			Id = "nested", ParentDividerId = invalidGlobalChild.Id, IsActiveList = true
		};
		var globalParent = new ModListVisualDividerData { Id = "global", IsActiveList = true, IsGlobal = true };
		var invalidLocalChild = new ModListVisualDividerData
		{
			Id = "local-child", ParentDividerId = globalParent.Id, IsActiveList = true
		};

		RegressionAssert.True(VisualDividerHierarchyPolicy.Normalize(
			[localParent, invalidGlobalChild, nested, globalParent, invalidLocalChild]));
		RegressionAssert.Equal(localParent.Id, invalidGlobalChild.ParentDividerId);
		RegressionAssert.False(invalidGlobalChild.IsGlobal);
		RegressionAssert.Equal(String.Empty, nested.ParentDividerId);
		RegressionAssert.Equal(globalParent.Id, invalidLocalChild.ParentDividerId);
		RegressionAssert.True(invalidLocalChild.IsGlobal);
	}

	public void ChildMovedPastAnotherParentIsPromotedToTopLevel()
	{
		var parent = new ModListVisualDividerData { Id = "parent", IsActiveList = true, Position = 0 };
		var next = new ModListVisualDividerData { Id = "next", IsActiveList = true, Position = 2 };
		var child = new ModListVisualDividerData
		{
			Id = "child", ParentDividerId = parent.Id, IsActiveList = true, Position = 3
		};

		RegressionAssert.True(VisualDividerHierarchyPolicy.NormalizePlacement([parent, next, child], true));
		RegressionAssert.Equal(String.Empty, child.ParentDividerId);
	}

	private static DivinityModData CreateDividerItem(ModListVisualDividerData divider) => new()
	{
		UUID = $"divider-{divider.Id}",
		VisualDividerId = divider.Id,
		IsVisualDivider = true,
		CanDrag = true
	};

	private static DivinityModData CreateDivider(string id, bool collapsed) => new()
	{
		UUID = $"divider-{id}",
		Name = id,
		VisualDividerId = id,
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
