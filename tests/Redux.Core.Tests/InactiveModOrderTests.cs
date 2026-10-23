using System;
using System.Linq;
using Newtonsoft.Json;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class InactiveModOrderTests
{
	public void RestartRestoresInactiveAndOverrideOrganizationIntoLiveSettings()
	{
		var first = new DivinityModData { UUID = "first", Name = "Zebra" };
		var second = new DivinityModData { UUID = "second", Name = "Alpha" };
		var overrideFirst = new DivinityModData { UUID = "override-first", IsForceLoaded = true };
		var overrideSecond = new DivinityModData { UUID = "override-second", IsForceLoaded = true };
		var saved = new DivinityModManagerSettings {
			InactiveModOrder = [first.UUID, second.UUID],
			OverrideModOrder = [overrideFirst.UUID, overrideSecond.UUID],
			VisualModListDividers = [new ModListVisualDividerData {
				Id = "inactive-section", IsActiveList = false, Position = 0,
				MemberModUuids = [first.UUID, second.UUID]
			}],
			OverrideVisualModListDividers = [new ModListVisualDividerData {
				Id = "override-section", IsActiveList = false, Position = 0, IsCollapsed = true,
				MemberModUuids = [overrideFirst.UUID, overrideSecond.UUID]
			}]
		};
		var deserialized = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(saved))!;
		var live = new DivinityModManagerSettings();
		// LoadSettings copies persisted values into the existing reactive settings
		// instance; a JSON roundtrip alone does not exercise this startup boundary.
		live.RestorePersistedSettings(deserialized);

		var inactive = InactiveModOrderPolicy.Restore([second, first], live.InactiveModOrder);
		var overrides = InactiveModOrderPolicy.Restore([overrideSecond, overrideFirst], live.OverrideModOrder);
		RegressionAssert.SequenceEqual([first, second], inactive);
		RegressionAssert.SequenceEqual([overrideFirst, overrideSecond], overrides);
		RegressionAssert.SequenceEqual([first.UUID, second.UUID], live.VisualModListDividers.Single().MemberModUuids);
		RegressionAssert.SequenceEqual([overrideFirst.UUID, overrideSecond.UUID], live.OverrideVisualModListDividers.Single().MemberModUuids);
		RegressionAssert.True(live.OverrideVisualModListDividers.Single().IsCollapsed);

		// Startup immediately saves the live instance again, before mod discovery.
		var savedAgain = JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(live))!;
		RegressionAssert.SequenceEqual(saved.InactiveModOrder, savedAgain.InactiveModOrder);
		RegressionAssert.SequenceEqual(saved.OverrideModOrder, savedAgain.OverrideModOrder);
	}

	public void HeldOverridesKeepInterleavedInactiveOrderWhenOtherModsAreTemporarilyActive()
	{
		var first = new DivinityModData { UUID = "first" };
		var second = new DivinityModData { UUID = "second" };
		var held = new DivinityModData { UUID = "held", IsForceLoaded = true, IsHeldOverride = true };
		var temporarilyActive = new DivinityModData { UUID = "temporarily-active", IsActive = true };
		var added = new DivinityModData { UUID = "new" };
		var savedOrder = InactiveModOrderPolicy.Capture([first, held, temporarilyActive, second], []);

		// Discovery supplies ordinary mods before held Override packages. Restoring
		// the whole inactive pane places the held package back between its neighbors.
		RegressionAssert.SequenceEqual([first, held, second, added],
			InactiveModOrderPolicy.Restore([second, first, added, held], savedOrder));
		RegressionAssert.True(temporarilyActive.IsActive);

		temporarilyActive.IsActive = false;
		RegressionAssert.SequenceEqual([first, held, temporarilyActive, second, added],
			InactiveModOrderPolicy.Restore([second, temporarilyActive, first, added, held], savedOrder));
		RegressionAssert.SequenceEqual(["first", "held", "temporarily-active", "second"], savedOrder);
	}

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
		var payload = VisualDividerSectionPolicy.ResolveCollapsedBlockDragPayload([divider, first, second, outside], divider, section);
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
		RegressionAssert.Equal(6, System.Windows.Controls.Grid.GetColumn(actions));
		var inactiveSearch = (System.Windows.FrameworkElement)layout.FindName("InactiveModsFilterTextBox");
		RegressionAssert.Equal(5, System.Windows.Controls.Grid.GetColumn(inactiveSearch));
		var activeActions = (System.Windows.Controls.StackPanel)((System.Windows.Controls.Button)layout.FindName("ActiveSeparatorBulkToggleButton")).Parent;
		var activeSearch = (System.Windows.FrameworkElement)layout.FindName("ActiveModsFilterTextBox");
		RegressionAssert.Equal(6, System.Windows.Controls.Grid.GetColumn(activeActions));
		RegressionAssert.Equal(5, System.Windows.Controls.Grid.GetColumn(activeSearch));
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

	public void EveryModPaneFilterRetainsItsWorkingClearMenu()
	{
		var application = System.Windows.Application.Current;
		var originalResources = application.Resources;
		try
		{
			var resources = new System.Windows.ResourceDictionary();
			foreach (var file in new[] { "Typography", "Light", "Dark" })
				resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
					{ Source = new Uri($"pack://application:,,,/Redux;component/Themes/{file}.xaml") });
			resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
				{ Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml") });
			application.Resources = resources;
			// The standalone layout also resolves its unrelated mod-row image menu.
			resources[typeof(System.Windows.Controls.Image)] = new System.Windows.Style(typeof(System.Windows.Controls.Image));
			VerifyModPaneClearMenus();
		}
		finally { application.Resources = originalResources; }
	}

	private static void VerifyModPaneClearMenus()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var layout = new DivinityModManager.Views.HorizontalModLayout();
			ReduxThemeService.Apply(layout.Resources, theme);
			var first = new DivinityModData { UUID = "1", Name = "Matching Zebra", CustomAlias = "Matching Zebra", HasCustomAlias = true };
			var second = new DivinityModData { UUID = "2", Name = "Unrelated", CustomAlias = "Unrelated", HasCustomAlias = true };
			var third = new DivinityModData { UUID = "3", Name = "Matching Alpha", CustomAlias = "Matching Alpha", HasCustomAlias = true };
			var source = new System.Collections.ObjectModel.ObservableCollection<DivinityModData> { first, second, third };
			layout.ForceLoadedModsView.ItemsSource = source;
			layout.Sort("Name", System.ComponentModel.ListSortDirection.Ascending, layout.ForceLoadedModsView);
			foreach (var name in new[] { "ActiveModsFilterTextBox", "InactiveModsFilterTextBox", "OverrideModsFilterTextBox" })
			{
				var filter = (System.Windows.Controls.TextBox)layout.FindName(name);
				filter.Text = "Matching";
				var menu = filter.ContextMenu;
				RegressionAssert.True(menu != null);
				menu!.PlacementTarget = filter;
				menu.ApplyTemplate();
				menu.Measure(new System.Windows.Size(300, 300));
				menu.Arrange(new System.Windows.Rect(0, 0, 300, 300));
				menu.UpdateLayout();
				var clear = menu.Items.OfType<System.Windows.Controls.MenuItem>()
					.Single(item => ReferenceEquals(item.Command, DivinityModManager.AppCommands.Clear));
				clear.Command.Execute(clear);
				RegressionAssert.Equal(String.Empty, filter.Text);
				clear.Command.Execute(clear);
				RegressionAssert.Equal(String.Empty, filter.Text);
				RegressionAssert.SequenceEqual([first, second, third], source);
			}
			RegressionAssert.SequenceEqual([third, first, second], layout.ForceLoadedModsView.Items.Cast<DivinityModData>());
		}
	}
}
