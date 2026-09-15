using System;
using System.Linq;
using Newtonsoft.Json;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class InactiveModOrderTests
{
	public void SavedInactiveOrderSurvivesRestartAndDiscoveryChanges()
	{
		var first = new DivinityModData { UUID = "first", Name = "Zebra" };
		var second = new DivinityModData { UUID = "second", Name = "Alpha" };
		var added = new DivinityModData { UUID = "new", Name = "New" };
		var settings = new DivinityModManagerSettings {
			InactiveModOrder = InactiveModOrderPolicy.Capture([first, second], ["temporarily-active"]),
			VisualModListDividers = [new ModListVisualDividerData { Id = "inactive-section", IsActiveList = false, Position = 0, MemberModUuids = [first.UUID, second.UUID] }]
		};
		var restored = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings))!;
		RegressionAssert.SequenceEqual([first, second, added], InactiveModOrderPolicy.Restore([second, added, first], restored.InactiveModOrder));
		RegressionAssert.SequenceEqual([second, added], InactiveModOrderPolicy.Restore([added, second], restored.InactiveModOrder));
		RegressionAssert.False(restored.VisualModListDividers.Single().IsActiveList);
		RegressionAssert.SequenceEqual(["first", "second"], restored.VisualModListDividers.Single().MemberModUuids);
		RegressionAssert.SequenceEqual([first, second], InactiveModOrderPolicy.Restore([second, first], ["FIRST", "first", "missing", "second"]));
	}

	public void InactiveBlockMoveDoesNotChangeActiveOrder()
	{
		var active = new DivinityModData { UUID = "active", IsActive = true };
		var divider = new DivinityModData { UUID = "divider", IsVisualDivider = true, VisualDividerId = "section", IsActive = false, IsVisualDividerCollapsed = true };
		var first = new DivinityModData { UUID = "first" };
		var second = new DivinityModData { UUID = "second" };
		var outside = new DivinityModData { UUID = "outside" };
		var section = new ModListVisualDividerData { Id = "section", IsActiveList = false, IsCollapsed = true, MemberModUuids = [first.UUID, second.UUID] };
		var payload = VisualDividerSectionPolicy.ResolveSectionBlockDragPayload([divider, first, second, outside], divider, section);
		var moved = VisualModListDropPolicy.Apply([active], [divider, first, second, outside], payload, false, 4);
		RegressionAssert.SequenceEqual([active], moved.ActiveItems);
		RegressionAssert.SequenceEqual([outside, divider, first, second], moved.InactiveItems);
		RegressionAssert.SequenceEqual(["outside", "first", "second"], InactiveModOrderPolicy.Capture(moved.InactiveItems, []));
	}

	public void InactiveControlsLoadAndColumnSortKeepsUnderlyingOrder()
	{
		var layout = new DivinityModManager.Views.HorizontalModLayout();
		var add = (System.Windows.Controls.Button)layout.FindName("AddInactiveSeparatorButton");
		var actions = (System.Windows.Controls.StackPanel)add.Parent;
		var collapseAll = (System.Windows.Controls.Button)layout.FindName("InactiveSeparatorBulkToggleButton");
		RegressionAssert.Equal(5, System.Windows.Controls.Grid.GetColumn(actions));
		RegressionAssert.True(add.Style != null);
		RegressionAssert.True(collapseAll.Style != null);
		RegressionAssert.True(ReferenceEquals(actions, collapseAll.Parent));
		var first = new DivinityModData { UUID = "first", Name = "Zebra", FilePath = "Zebra.pak" };
		var second = new DivinityModData { UUID = "second", Name = "Alpha", FilePath = "Alpha.pak" };
		var source = new System.Collections.ObjectModel.ObservableCollection<DivinityModData> { first, second };
		layout.InactiveModsView.ItemsSource = source;
		layout.Sort("UUID", System.ComponentModel.ListSortDirection.Descending, layout.InactiveModsView);
		RegressionAssert.SequenceEqual([first, second], source);
		RegressionAssert.SequenceEqual([second, first], layout.InactiveModsView.Items.Cast<DivinityModData>());
		layout.Sort("#", System.ComponentModel.ListSortDirection.Ascending, layout.InactiveModsView);
		RegressionAssert.SequenceEqual([first, second], layout.InactiveModsView.Items.Cast<DivinityModData>());
	}

	public void AdvisorIgnoresInactiveOrganization()
	{
		var active = new DivinityModData { UUID = "active", Name = "Active", IsActive = true };
		var inactive = new ModListVisualDividerData { Id = "inactive", Title = "My stored mods", IsActiveList = false, IsCollapsed = true, Position = 7, MemberModUuids = ["second", "first"] };
		var before = JsonConvert.SerializeObject(inactive);
		var plan = LoadOrderAdvisorOrganizer.CreatePlan([active], [inactive], LoadOrderAdvisorSeparatorPolicy.PreserveMySeparators);
		RegressionAssert.SequenceEqual([active], plan.OrderedMods);
		RegressionAssert.True(plan.Dividers.All(divider => divider.IsActiveList));
		RegressionAssert.Equal(before, JsonConvert.SerializeObject(inactive));
	}
}
