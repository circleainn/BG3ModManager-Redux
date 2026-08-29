using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.Collections.Generic;
using System.Linq;

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

	public void CollapsedDividerCannotStartDrag()
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

		RegressionAssert.Equal(0, payload.Count);
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

	public void CollapsedSeparatorOwnsEveryInsertionSlotUntilTheNextSeparator()
	{
		var looseBefore = CreateMod("loose-before");
		var collapsed = CreateDivider("collapsed", collapsed: true);
		var hidden = CreateMod("hidden");
		var next = CreateDivider("next", collapsed: false);
		var nextMember = CreateMod("next-member");
		var items = new[] { looseBefore, collapsed, hidden, next, nextMember };

		RegressionAssert.Equal(
			collapsed,
			VisualModListDropPolicy.ResolveCollapsedOwner(items, 2));
		RegressionAssert.Equal(
			collapsed,
			VisualModListDropPolicy.ResolveCollapsedOwner(items, 3));
		RegressionAssert.Equal(
			null,
			VisualModListDropPolicy.ResolveCollapsedOwner(items, 0));
		RegressionAssert.Equal(
			null,
			VisualModListDropPolicy.ResolveCollapsedOwner(items, 4));
	}

	public void CollapsedDropPreviewUsesOnlyTheAdjacentVisibleRow()
	{
		var collapsed = CreateDivider("collapsed", collapsed: true);
		var next = CreateDivider("next", collapsed: false);
		var nextMember = CreateMod("next-member");
		var visibleItems = new VisibleRowProbe(collapsed, next, nextMember);

		RegressionAssert.Equal(
			collapsed,
			VisualModListDropPolicy.ResolveVisibleCollapsedOwner(visibleItems, 1));
		RegressionAssert.Equal(1, visibleItems.IndexerReads);
		RegressionAssert.Equal(
			null,
			VisualModListDropPolicy.ResolveVisibleCollapsedOwner(new[] { collapsed, next, nextMember }, 2));
		RegressionAssert.Equal(
			null,
			VisualModListDropPolicy.ResolveVisibleCollapsedOwner(new[] { collapsed, next, nextMember }, 3));
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
		var divider = new ModListVisualDividerData
		{
			Id = "moved", IsActiveList = true,
			MemberModUuids = new List<string> { firstMember.UUID, secondMember.UUID }
		};

		RegressionAssert.Equal(1,
			VisualDividerSectionPolicy.ResolveExpansionInsertionIndex(
				new[] { marker, destinationSuffix }, marker, divider));
		RegressionAssert.Equal(2,
			VisualDividerSectionPolicy.ResolveExpansionInsertionIndex(
				new[] { marker, firstMember, destinationSuffix }, marker, divider));
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

	private sealed class VisibleRowProbe : IReadOnlyList<DivinityModData>
	{
		private readonly DivinityModData[] _items;

		public int Count => _items.Length;
		public int IndexerReads { get; private set; }
		public DivinityModData this[int index]
		{
			get
			{
				IndexerReads++;
				return _items[index];
			}
		}

		public VisibleRowProbe(params DivinityModData[] items) => _items = items;

		public IEnumerator<DivinityModData> GetEnumerator() =>
			throw new InvalidOperationException("The drag preview must not enumerate the mod list.");

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
