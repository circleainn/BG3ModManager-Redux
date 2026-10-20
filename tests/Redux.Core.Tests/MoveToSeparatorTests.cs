using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using DynamicData.Binding;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Redux.Core.Tests;

public sealed class MoveToSeparatorTests
{
	public void MoveToSeparatorUsesFullOrderAndPreservesBulkSelectionOrder()
	{
		var first = Mod("first");
		var hidden = Mod("hidden");
		var second = Mod("second");
		var member = Mod("member");
		var later = Mod("later");
		var destination = Divider("destination", 3, member);
		var next = Divider("next", 5, later);
		var sequence = Sequence([first, hidden, second, member, later], [destination, next]);
		// A filter/sort can present the selection in a different order. The full pane
		// remains authoritative, including the hidden row between selected mods.
		var result = VisualDividerMovePolicy.Plan(sequence, [destination, next], [second, first], destination.Id, true)!;
		RegressionAssert.SequenceEqual(new[] { "hidden", "destination", "member", "first", "second", "next", "later" }, Names(result.Items));
		RegressionAssert.SequenceEqual(new[] { "member", "first", "second" }, result.MembersByDividerId[destination.Id]);
		RegressionAssert.SequenceEqual(new[] { "member" }, destination.MemberModUuids);
		RegressionAssert.SequenceEqual(new[] { "first", "hidden", "second", "destination", "member", "next", "later" }, Names(sequence));
	}

	public void MoveToCollapsedParentAppendsBeforeChildrenAndLeavesLooseRowsUnowned()
	{
		var moving = Mod("moving");
		var parentMember = Mod("parent-member");
		var childMember = Mod("child-member");
		var parent = Divider("parent", 1, parentMember);
		parent.IsCollapsed = true;
		var child = Divider("child", 3, childMember);
		child.ParentDividerId = parent.Id;
		var result = VisualDividerMovePolicy.Plan(Sequence([moving, parentMember, childMember], [parent, child]),
			[parent, child], [moving], parent.Id, true)!;
		RegressionAssert.SequenceEqual(new[] { "parent", "parent-member", "moving", "child", "child-member" }, Names(result.Items));
		RegressionAssert.SequenceEqual(new[] { "parent-member", "moving" }, result.MembersByDividerId[parent.Id]);
		RegressionAssert.SequenceEqual(new[] { "child-member" }, result.MembersByDividerId[child.Id]);
		RegressionAssert.True(parent.IsCollapsed);

		var loose = Mod("loose");
		var closed = Divider("closed", 1, parentMember);
		closed.IsCollapsed = true;
		result = VisualDividerMovePolicy.Plan(Sequence([moving, parentMember, loose], [closed]), [closed], [moving], closed.Id, true)!;
		RegressionAssert.SequenceEqual(new[] { "closed", "parent-member", "moving", "loose" }, Names(result.Items));
		RegressionAssert.False(result.MembersByDividerId[closed.Id].Contains(loose.UUID));
	}

	public void MoveToNestedClosedSeparatorTransfersOnlyChosenMembers()
	{
		var chosen = Mod("chosen");
		var sourceHidden = Mod("source-hidden");
		var targetHidden = Mod("target-hidden");
		var source = Divider("source", 0, chosen, sourceHidden);
		source.IsCollapsed = true;
		var parent = Divider("parent", 3);
		parent.IsCollapsed = true;
		var child = Divider("child", 4, targetHidden);
		child.IsCollapsed = true;
		child.ParentDividerId = parent.Id;
		var result = VisualDividerMovePolicy.Plan(Sequence([chosen, sourceHidden, targetHidden], [source, parent, child]),
			[source, parent, child], [chosen], child.Id, true)!;
		RegressionAssert.SequenceEqual(new[] { "source", "source-hidden", "parent", "child", "target-hidden", "chosen" }, Names(result.Items));
		RegressionAssert.SequenceEqual(new[] { "source-hidden" }, result.MembersByDividerId[source.Id]);
		RegressionAssert.SequenceEqual(new[] { "target-hidden", "chosen" }, result.MembersByDividerId[child.Id]);
		RegressionAssert.Equal("parent", child.ParentDividerId);
		RegressionAssert.True(child.IsCollapsed);
	}

	public void MoveToSeparatorRejectsStaleMixedPaneAndSeparatorSelections()
	{
		var chosen = Mod("chosen");
		var target = Divider("target", 1);
		var sequence = Sequence([chosen], [target]);
		RegressionAssert.True(VisualDividerMovePolicy.Plan(sequence, [target], [Mod("chosen")], target.Id, true) == null);
		RegressionAssert.True(VisualDividerMovePolicy.Plan(sequence, [target], [chosen, Mod("other-pane")], target.Id, true) == null);
		RegressionAssert.True(VisualDividerMovePolicy.Plan(sequence, [target], [sequence[1]], target.Id, true) == null);
		RegressionAssert.True(VisualDividerMovePolicy.Plan(sequence, [target], [chosen], "missing", true) == null);
		RegressionAssert.True(VisualDividerMovePolicy.Plan(sequence, [target], [chosen], target.Id, false) == null);
		RegressionAssert.SequenceEqual(new[] { "chosen", "target" }, Names(sequence));
	}

	public void MoveToSeparatorAlreadyAtSectionEndDoesNotCreateAnEdit()
	{
		var chosen = Mod("chosen");
		var target = Divider("target", 0, chosen);
		target.IsCollapsed = true;
		var result = VisualDividerMovePolicy.Plan(Sequence([chosen], [target]), [target], [chosen], target.Id, true)!;
		RegressionAssert.False(result.Changed);
	}

	public void MoveToSeparatorActiveEditRestoresOrderMembershipAndDirtyStateThroughHistory()
	{
		using var fixture = new ViewModelFixture();
		var first = Mod("first", true);
		var hidden = Mod("hidden", true);
		var last = Mod("last", true);
		var target = Divider("target", 2, last);
		target.IsCollapsed = true;
		fixture.Model.ActiveMods.AddRange([first, hidden, last]);
		fixture.Model.Settings.VisualModListDividers.Add(target);
		fixture.Model.ActiveModFilterText = "first";
		hidden.Visibility = System.Windows.Visibility.Collapsed;
		last.Visibility = System.Windows.Visibility.Collapsed;
		RegressionAssert.True(fixture.Model.MoveModsToSeparator([first], VisualModListPane.Active, target.Id));
		RegressionAssert.SequenceEqual(new[] { "hidden", "last", "first" }, Names(fixture.Model.ActiveMods));
		RegressionAssert.True(fixture.Model.HasUnsavedLoadOrderChanges);
		RegressionAssert.True(fixture.Model.CanUndoLoadOrderChange);
		RegressionAssert.SequenceEqual(new[] { "last", "first" }, fixture.Model.Settings.VisualModListDividers.Single().MemberModUuids);
		RegressionAssert.True(fixture.RestoreHistory(undo: true));
		RegressionAssert.SequenceEqual(new[] { "first", "hidden", "last" }, Names(fixture.Model.ActiveMods));
		RegressionAssert.False(fixture.Model.HasUnsavedLoadOrderChanges);
		RegressionAssert.SequenceEqual(new[] { "last" }, fixture.Model.Settings.VisualModListDividers.Single().MemberModUuids);
		RegressionAssert.True(fixture.RestoreHistory(undo: false));
		RegressionAssert.True(fixture.Model.HasUnsavedLoadOrderChanges);
		RegressionAssert.SequenceEqual(new[] { "hidden", "last", "first" }, Names(fixture.Model.ActiveMods));
		RegressionAssert.True(fixture.Model.ActiveMods.All(mod => mod.IsActive));
		var saved = JsonConvert.DeserializeObject<List<ModListVisualDividerData>>(
			JsonConvert.SerializeObject(fixture.Model.Settings.VisualModListDividers))!;
		RegressionAssert.SequenceEqual(new[] { "hidden", "target", "last", "first" }, Names(Sequence(fixture.Model.ActiveMods, saved)));
		RegressionAssert.True(saved.Single().IsCollapsed);
		fixture.Model.ActiveModFilterText = String.Empty;
		hidden.Visibility = System.Windows.Visibility.Visible;
		last.Visibility = System.Windows.Visibility.Visible;
		fixture.Model.RefreshVisualDividers();
		RegressionAssert.SequenceEqual(new[] { "hidden", "target" }, Names(fixture.Model.DisplayActiveMods));
	}

	public void MoveToSeparatorInactiveAndOverrideStayInPaneAndUndoWithoutActivation()
	{
		foreach (var pane in new[] { VisualModListPane.Inactive, VisualModListPane.Override })
		{
			using var fixture = new ViewModelFixture();
			var chosen = Mod("chosen");
			chosen.IsForceLoaded = true;
			chosen.IsHeldOverride = pane == VisualModListPane.Inactive;
			chosen.FilePath = "Z:\\nonexistent-readonly-fixture\\chosen.pak";
			var member = Mod("member");
			var active = Mod("active", true);
			fixture.Model.ActiveMods.Add(active);
			var target = Divider("target", 1, member);
			target.IsActiveList = false;
			if (pane == VisualModListPane.Inactive)
			{
				fixture.Model.InactiveMods.AddRange([chosen, member]);
				fixture.Model.Settings.VisualModListDividers.Add(target);
			}
			else
			{
				fixture.Overrides.Add(chosen);
				fixture.Overrides.Add(member);
				fixture.Model.Settings.OverrideVisualModListDividers.Add(target);
			}
			RegressionAssert.True(fixture.Model.MoveModsToSeparator([chosen], pane, target.Id));
			RegressionAssert.False(fixture.Model.HasUnsavedLoadOrderChanges);
			RegressionAssert.False(chosen.IsActive);
			RegressionAssert.Equal(pane == VisualModListPane.Inactive, chosen.IsHeldOverride);
			RegressionAssert.Equal("Z:\\nonexistent-readonly-fixture\\chosen.pak", chosen.FilePath);
			RegressionAssert.SequenceEqual(new[] { active }, fixture.Model.ActiveMods);
			RegressionAssert.SequenceEqual(new[] { "member", "chosen" }, pane == VisualModListPane.Inactive
				? Names(fixture.Model.InactiveMods) : fixture.Model.Settings.OverrideModOrder);
			RegressionAssert.True(fixture.RestoreHistory(undo: true));
			RegressionAssert.SequenceEqual(new[] { "chosen", "member" }, pane == VisualModListPane.Inactive
				? Names(fixture.Model.InactiveMods) : Names(fixture.Model.DisplayOverrideMods.Where(item => !item.IsVisualDivider)));
			RegressionAssert.True(fixture.RestoreHistory(undo: false));
			RegressionAssert.False(fixture.Model.HasUnsavedLoadOrderChanges);
		}
	}

	public void MoveToSeparatorBusyAndStaleRequestsLeaveHistoryAndStateUntouched()
	{
		using var fixture = new ViewModelFixture();
		var chosen = Mod("chosen", true);
		var target = Divider("target", 1);
		fixture.Model.ActiveMods.Add(chosen);
		fixture.Model.Settings.VisualModListDividers.Add(target);
		fixture.Model.MainProgressIsActive = true;
		RegressionAssert.False(fixture.Model.MoveModsToSeparator([chosen], VisualModListPane.Active, target.Id));
		fixture.Model.MainProgressIsActive = false;
		RegressionAssert.False(fixture.Model.MoveModsToSeparator([Mod("chosen")], VisualModListPane.Active, target.Id));
		RegressionAssert.False(fixture.Model.MoveModsToSeparator([chosen], VisualModListPane.Inactive, target.Id));
		RegressionAssert.False(fixture.Model.CanUndoLoadOrderChange);
		RegressionAssert.False(fixture.Model.HasUnsavedLoadOrderChanges);
		RegressionAssert.SequenceEqual(new[] { chosen }, fixture.Model.ActiveMods);
	}

	public void MoveToSeparatorHistoryRemainsBoundedAndNewMovesClearRedo()
	{
		using var fixture = new ViewModelFixture();
		var chosen = Mod("chosen");
		var first = Divider("first", 1);
		var second = Divider("second", 2);
		first.IsActiveList = second.IsActiveList = false;
		fixture.Model.InactiveMods.Add(chosen);
		fixture.Model.Settings.VisualModListDividers.AddRange([first, second]);
		for (var index = 0; index < 52; index++)
			RegressionAssert.True(fixture.Model.MoveModsToSeparator([chosen], VisualModListPane.Inactive,
				index % 2 == 0 ? first.Id : second.Id));
		for (var index = 0; index < 50; index++) RegressionAssert.True(fixture.RestoreHistory(undo: true));
		RegressionAssert.False(fixture.RestoreHistory(undo: true));
		RegressionAssert.True(fixture.Model.CanRedoLoadOrderChange);
		RegressionAssert.True(fixture.Model.MoveModsToSeparator([chosen], VisualModListPane.Inactive, first.Id));
		RegressionAssert.False(fixture.Model.CanRedoLoadOrderChange);
	}

	private static DivinityModData Mod(string id, bool active = false) => new RegressionModData { UUID = id, Name = id, IsActive = active };
	private static ModListVisualDividerData Divider(string id, int position, params DivinityModData[] members) => new()
	{
		Id = id, Title = id, Position = position, IsActiveList = true,
		MemberModUuids = members.Select(mod => mod.UUID).ToList()
	};
	private static IEnumerable<string> Names(IEnumerable<DivinityModData> items) => items.Select(item => item.IsVisualDivider ? item.VisualDividerId : item.UUID);
	private static IReadOnlyList<DivinityModData> Sequence(IEnumerable<DivinityModData> mods, IEnumerable<ModListVisualDividerData> dividers) =>
		VisualDividerSectionPolicy.BuildVisualSequence(mods, dividers, true, divider => new RegressionModData
		{
			UUID = "marker-" + divider.Id, IsVisualDivider = true, VisualDividerId = divider.Id,
			IsVisualDividerCollapsed = divider.IsCollapsed
		});

	private sealed class ViewModelFixture : IDisposable
	{
		private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
		private readonly IScheduler _previousScheduler = RxApp.MainThreadScheduler;
		private readonly List<IDisposable> _disposables = [];
		public MainWindowViewModel Model { get; }
		public ObservableCollection<DivinityModData> Overrides { get; } = [];
		public ViewModelFixture()
		{
			// Exercise the real edit/history paths without loading user profiles,
			// watching directories, or running deferred settings and diagnostics work.
			RxApp.MainThreadScheduler = new SuspendedScheduler();
			Model = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
			var settings = new DivinityModManagerSettings();
			Set("_settings", settings);
			Set("_activeMods", new ObservableCollectionExtended<DivinityModData>());
			Set("_inactiveMods", new ObservableCollectionExtended<DivinityModData>());
			Set("_forceLoadedMods", new ReadOnlyObservableCollection<DivinityModData>(Overrides));
			foreach (var name in new[] { "DisplayActiveMods", "DisplayInactiveMods", "DisplayOverrideMods" })
				Set($"<{name}>k__BackingField", new ObservableCollectionExtended<DivinityModData>());
			var modules = new ReduxModuleState(settings);
			Set("<Modules>k__BackingField", modules);
			_disposables.Add(modules);
			SetObservable("_isLocked", false);
			SetObservable("_selectedModOrder", new DivinityLoadOrder { Name = "Disposable test order" });
			var historyField = typeof(MainWindowViewModel).GetField("_loadOrderEditHistory", Private)!;
			historyField.SetValue(Model, Activator.CreateInstance(historyField.FieldType, new object[] { 50 }));
			typeof(MainWindowViewModel).GetProperty(nameof(Model.IsInitialized))!.SetValue(Model, true);
			Model.SelectedModCategory = MainWindowViewModel.AllModsCategory;
		}
		public bool RestoreHistory(bool undo)
		{
			var history = typeof(MainWindowViewModel).GetField("_loadOrderEditHistory", Private)!.GetValue(Model)!;
			object?[] arguments = [null];
			if (!(bool)history.GetType().GetMethod(undo ? "TryUndo" : "TryRedo")!.Invoke(history, arguments)!) return false;
			object?[] restore = [arguments[0], null];
			var result = (bool)typeof(MainWindowViewModel).GetMethod("TryRestoreLoadOrderHistoryState", Private)!.Invoke(Model, restore)!;
			typeof(MainWindowViewModel).GetMethod("UpdateLoadOrderHistoryAvailability", Private)!.Invoke(Model, null);
			return result;
		}
		private void SetObservable<T>(string field, T value)
		{
			var helper = new ObservableAsPropertyHelper<T>(Observable.Return(value), _ => { }, value, scheduler: ImmediateScheduler.Instance);
			Set(field, helper);
			_disposables.Add(helper);
		}
		private void Set(string field, object value) => typeof(MainWindowViewModel).GetField(field, Private)!.SetValue(Model, value);
		public void Dispose()
		{
			foreach (var disposable in _disposables) disposable.Dispose();
			RxApp.MainThreadScheduler = _previousScheduler;
		}
	}

	private sealed class SuspendedScheduler : IScheduler
	{
		public DateTimeOffset Now => DateTimeOffset.UtcNow;
		public IDisposable Schedule<T>(T state, Func<IScheduler, T, IDisposable> action) => Disposable.Empty;
		public IDisposable Schedule<T>(T state, TimeSpan dueTime, Func<IScheduler, T, IDisposable> action) => Disposable.Empty;
		public IDisposable Schedule<T>(T state, DateTimeOffset dueTime, Func<IScheduler, T, IDisposable> action) => Disposable.Empty;
	}
}
