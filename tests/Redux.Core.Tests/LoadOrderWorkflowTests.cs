using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Redux.Core.Tests;

internal sealed class LoadOrderWorkflowTests
{
	public void ExportKeepsExplicitCommunityFrameworkPlacementDespiteDependentMetadata()
	{
		var cleric = new DivinityModData { UUID = "cleric", Name = "Goon's Cleric Overhaul" };
		var framework = new DivinityModData { UUID = "framework", Name = "Community Framework" };
		cleric.Dependencies.AddOrUpdate(new ModuleShortDesc
		{
			UUID = framework.UUID,
			Name = framework.Name
		});
		var order = new[]
		{
			new DivinityLoadOrderEntry { UUID = cleric.UUID, Name = cleric.Name },
			new DivinityLoadOrderEntry { UUID = framework.UUID, Name = framework.Name }
		};

		var output = DivinityModDataLoader.BuildOutputList(order, [cleric, framework]);
		RegressionAssert.SequenceEqual(new[] { cleric.UUID, framework.UUID }, output.Select(mod => mod.UUID));
		var conflict = DivinityModDataLoader.FindReversedDependencies(output).Single();
		RegressionAssert.Equal(framework.UUID, conflict.Dependency.UUID);
		RegressionAssert.Equal(cleric.UUID, conflict.Dependent.UUID);
	}

	public void ExportAddsOnlyMissingDependenciesWithoutMovingSelectedMods()
	{
		var cleric = new DivinityModData { UUID = "cleric", Name = "Cleric" };
		var framework = new DivinityModData { UUID = "framework", Name = "Framework" };
		var missing = new DivinityModData { UUID = "missing", Name = "Missing Dependency" };
		cleric.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = framework.UUID, Name = framework.Name });
		cleric.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = missing.UUID, Name = missing.Name });
		var order = new[]
		{
			new DivinityLoadOrderEntry { UUID = cleric.UUID, Name = cleric.Name },
			new DivinityLoadOrderEntry { UUID = framework.UUID, Name = framework.Name }
		};

		var output = DivinityModDataLoader.BuildOutputList(order, [cleric, framework, missing]);
		RegressionAssert.SequenceEqual(new[] { missing.UUID, cleric.UUID, framework.UUID },
			output.Select(mod => mod.UUID));
	}

	public void CyclicMissingDependenciesDoNotRecurseForeverOrDuplicateMods()
	{
		var first = new DivinityModData { UUID = "first", Name = "First" };
		var second = new DivinityModData { UUID = "second", Name = "Second" };
		first.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = second.UUID, Name = second.Name });
		second.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = first.UUID, Name = first.Name });
		var output = DivinityModDataLoader.BuildOutputList(
			[new DivinityLoadOrderEntry { UUID = first.UUID, Name = first.Name }], [first, second]);
		RegressionAssert.SequenceEqual(new[] { second.UUID, first.UUID }, output.Select(mod => mod.UUID));
	}

	public void RefreshKeepsSelectedProfileInsteadOfForcingPublic()
	{
		var profiles = new[]
		{
			new DivinityProfileData("public-uuid", "public/modsettings.lsx") { ProfileName = "Public" },
			new DivinityProfileData("custom-uuid", "custom/modsettings.lsx") { ProfileName = "Custom" }
		};
		RegressionAssert.Equal(1, LoadOrderPersistencePolicy.FindPreferredProfileIndex(profiles, "custom-uuid"));
		RegressionAssert.Equal(0, LoadOrderPersistencePolicy.FindPreferredProfileIndex(profiles, "missing-uuid"));
		RegressionAssert.Equal(0, LoadOrderPersistencePolicy.FindPreferredProfileIndex(profiles, null));
		RegressionAssert.Equal(-1, LoadOrderPersistencePolicy.FindPreferredProfileIndex([], null));
	}

	public void EmptyGameOrderRecoveryTargetsCurrentEvenWhenNamedOrderComesFirst()
	{
		var named = new DivinityLoadOrder { Name = "My saved order" };
		var current = new DivinityLoadOrder { Name = "Current", IsModSettings = true };
		RegressionAssert.True(ReferenceEquals(current,
			LoadOrderPersistencePolicy.FindGameBackedCurrentOrder([named, current])));
		RegressionAssert.True(LoadOrderPersistencePolicy.FindGameBackedCurrentOrder([named]) == null);
	}

	public void FirstSyncBackupDoesNotReplaceOrSelectTheWorkingOrder()
	{
		var current = new DivinityLoadOrder { Name = "Current", IsModSettings = true, FilePath = "modsettings.lsx" };
		var named = new DivinityLoadOrder { Name = "My order", FilePath = "mine.json" };
		var displayed = new System.Collections.ObjectModel.ObservableCollection<DivinityLoadOrder> { current, named };
		var saved = new System.Collections.Generic.List<DivinityLoadOrder> { named };
		const int selectedIndex = 1;
		var backup = new DivinityLoadOrder
		{
			Name = "LastExported", FilePath = "last-exported.json",
			Order = [new DivinityLoadOrderEntry { UUID = "exported-mod", Name = "Exported" }]
		};
		LoadOrderPersistencePolicy.RememberGameExportBackup(displayed, saved, backup);
		RegressionAssert.True(ReferenceEquals(named, displayed[selectedIndex]));
		RegressionAssert.Equal("My order", saved[0].Name);
		RegressionAssert.Equal(1, saved.Count(order => order.Name == "LastExported"));
		RegressionAssert.Equal("exported-mod", displayed.Last().Order.Single().UUID);
		backup.Order[0].UUID = "updated-export";
		LoadOrderPersistencePolicy.RememberGameExportBackup(displayed, saved, backup);
		RegressionAssert.Equal(3, displayed.Count);
		RegressionAssert.True(ReferenceEquals(named, displayed[selectedIndex]));
		RegressionAssert.Equal("updated-export", displayed.Last().Order.Single().UUID);
	}

	public void StartupRestoresRememberedOrderWhileRefreshKeepsCurrentSelection()
	{
		RegressionAssert.Equal(
			"Remembered Order",
			LoadOrderPersistencePolicy.ResolveOrderNameForRefresh(String.Empty, "Remembered Order"));
		RegressionAssert.Equal(
			"Open Order",
			LoadOrderPersistencePolicy.ResolveOrderNameForRefresh("Open Order", "Remembered Order"));
		RegressionAssert.Equal(
			String.Empty,
			LoadOrderPersistencePolicy.ResolveOrderNameForRefresh(null, null));
	}

	public void SaveSwitchRenameAndRestartPreservesEachOrder()
	{
		WithTemporaryDirectory(directory =>
		{
			var first = CreateOrder(directory, "First", "first-mod");
			var second = CreateOrder(directory, "Second", "second-mod");
			DivinityModDataLoader.ExportLoadOrderToFile(first.FilePath, first);
			DivinityModDataLoader.ExportLoadOrderToFile(second.FilePath, second);

			var afterFirstStart = LoadOrders(directory);
			RegressionAssert.Equal("first-mod", afterFirstStart.Single(order => order.Name == "First").Order.Single().UUID);
			RegressionAssert.Equal("second-mod", afterFirstStart.Single(order => order.Name == "Second").Order.Single().UUID);

			var selected = afterFirstStart.Single(order => order.Name == "First");
			var rename = LoadOrderFileWorkflow.PlanRename(selected, "Renamed First");
			LoadOrderFileWorkflow.ApplyRename(selected, rename);

			var afterRestart = LoadOrders(directory);
			RegressionAssert.Equal(2, afterRestart.Count);
			RegressionAssert.False(afterRestart.Any(order => order.Name == "First"));
			RegressionAssert.Equal("first-mod", afterRestart.Single(order => order.Name == "Renamed First").Order.Single().UUID);
			RegressionAssert.Equal("second-mod", afterRestart.Single(order => order.Name == "Second").Order.Single().UUID);
		});
	}

	public void RenameRequiresConfirmationBeforeReplacingAnotherSavedOrder()
	{
		WithTemporaryDirectory(directory =>
		{
			var source = CreateOrder(directory, "Source", "source-mod");
			var destination = CreateOrder(directory, "Destination", "destination-mod");
			DivinityModDataLoader.ExportLoadOrderToFile(source.FilePath, source);
			DivinityModDataLoader.ExportLoadOrderToFile(destination.FilePath, destination);

			var rename = LoadOrderFileWorkflow.PlanRename(source, "Destination");
			RegressionAssert.True(rename.DestinationExists);
			try
			{
				LoadOrderFileWorkflow.ApplyRename(source, rename);
				throw new InvalidOperationException("Expected replacement without confirmation to fail.");
			}
			catch (IOException)
			{
				var unchanged = LoadOrders(directory);
				RegressionAssert.Equal("source-mod", unchanged.Single(order => order.Name == "Source").Order.Single().UUID);
				RegressionAssert.Equal("destination-mod", unchanged.Single(order => order.Name == "Destination").Order.Single().UUID);
			}

			LoadOrderFileWorkflow.ApplyRename(source, rename, replaceExisting: true);
			var replaced = LoadOrders(directory);
			RegressionAssert.Equal(1, replaced.Count);
			RegressionAssert.Equal("source-mod", replaced.Single(order => order.Name == "Destination").Order.Single().UUID);
		});
	}

	public void SaveCopyPreservesOverrideMembershipAndSeparatorPlacementAfterRestart()
	{
		WithTemporaryDirectory(directory =>
		{
			var original = CreateOrder(directory, "Original", "first-mod");
			original.OverrideModFiles = ["Texture.pak"];
			original.VisualDividers = [new ModListVisualDividerData
			{
				Id = "section", Title = "Textures", IsActiveList = true, IsGlobal = true,
				Position = 0, IsCollapsed = true, MemberModUuids = ["first-mod"]
			}];
			DivinityModDataLoader.ExportLoadOrderToFile(original.FilePath, original);
			var originalBytes = File.ReadAllBytes(original.FilePath);
			var copyPath = Path.Combine(directory, "Copy.json");
			var copy = LoadOrderFileWorkflow.SaveCopy(original, copyPath, [original]);
			var reopened = LoadOrders(directory).Single(order => order.Name == "Copy");
			RegressionAssert.Equal("Texture.pak", reopened.OverrideModFiles.Single());
			RegressionAssert.Equal("first-mod", reopened.Order.Single().UUID);
			RegressionAssert.True(reopened.VisualDividers.Single().IsCollapsed);
			RegressionAssert.True(reopened.VisualDividers.Single().IsGlobal);
			RegressionAssert.Equal("first-mod", reopened.VisualDividers.Single().MemberModUuids.Single());
			RegressionAssert.Equal("Original", original.Name);
			RegressionAssert.Equal(Path.Combine(directory, "Original.json"), original.FilePath);
			RegressionAssert.SequenceEqual(originalBytes, File.ReadAllBytes(original.FilePath));
			copy.Order[0].UUID = "changed";
			copy.OverrideModFiles.Clear();
			copy.VisualDividers[0].MemberModUuids.Clear();
			RegressionAssert.Equal("first-mod", original.Order.Single().UUID);
			RegressionAssert.Equal("Texture.pak", original.OverrideModFiles.Single());
			RegressionAssert.Equal("first-mod", original.VisualDividers.Single().MemberModUuids.Single());
		});
	}

	public void ReplacingSavedCopyRefreshesLoadedOrdersIncludingEmptyOverrideSelection()
	{
		WithTemporaryDirectory(directory =>
		{
			var displayed = CreateOrder(directory, "Destination", "old-mod");
			var savedListEntry = CreateOrder(directory, "Destination", "old-mod");
			var unrelated = CreateOrder(directory, "Unrelated", "keep-mod");
			var working = CreateOrder(directory, "Working", "new-mod");
			working.VisualDividers = [];
			foreach (var selection in new System.Collections.Generic.List<string>?[]
				{ ["NewTexture.pak"], [], null })
			{
				displayed.OverrideModFiles = ["Stale.pak"];
				savedListEntry.OverrideModFiles = ["Stale.pak"];
				displayed.VisualDividers = [new ModListVisualDividerData { Id = "old-section", IsActiveList = true }];
				working.OverrideModFiles = selection!;
				var copy = LoadOrderFileWorkflow.SaveCopy(working, displayed.FilePath,
					[displayed, savedListEntry, displayed, unrelated]);
				var reopened = LoadOrders(directory).Single(order => order.Name == "Destination");
				foreach (var order in new[] { displayed, savedListEntry, reopened })
				{
					RegressionAssert.Equal("new-mod", order.Order.Single().UUID);
					RegressionAssert.Equal(0, order.VisualDividers.Count);
					RegressionAssert.Equal(selection == null, order.OverrideModFiles == null);
					if (selection != null) RegressionAssert.SequenceEqual(selection, order.OverrideModFiles!);
				}
				RegressionAssert.Equal(copy.LastModifiedDate, displayed.LastModifiedDate);
				RegressionAssert.Equal(copy.LastModifiedDate, savedListEntry.LastModifiedDate);
				copy.Order[0].UUID = "copy-only-edit";
				copy.OverrideModFiles?.Clear();
				RegressionAssert.Equal("new-mod", displayed.Order.Single().UUID);
				RegressionAssert.Equal("new-mod", savedListEntry.Order.Single().UUID);
				if (selection?.Count > 0) RegressionAssert.Equal("NewTexture.pak", displayed.OverrideModFiles.Single());
			}
			RegressionAssert.Equal("keep-mod", unrelated.Order.Single().UUID);
		});
	}

	public void FailedCopyPreservesSavedFileAndLoadedOrderState()
	{
		WithTemporaryDirectory(directory =>
		{
			var destination = CreateOrder(directory, "Destination", "keep-mod");
			destination.OverrideModFiles = ["Keep.pak"];
			destination.VisualDividers = [new ModListVisualDividerData { Id = "keep-section", IsActiveList = true }];
			DivinityModDataLoader.ExportLoadOrderToFile(destination.FilePath, destination);
			var before = File.ReadAllBytes(destination.FilePath);
			var modified = destination.LastModifiedDate;
			var working = CreateOrder(directory, "Working", "new-mod");
			working.OverrideModFiles = [];
			working.VisualDividers = [];
			using (var held = new FileStream(destination.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				RegressionAssert.Throws<IOException>(() =>
					LoadOrderFileWorkflow.SaveCopy(working, destination.FilePath, [destination]));
			}
			RegressionAssert.SequenceEqual(before, File.ReadAllBytes(destination.FilePath));
			RegressionAssert.Equal("keep-mod", destination.Order.Single().UUID);
			RegressionAssert.Equal("Keep.pak", destination.OverrideModFiles.Single());
			RegressionAssert.Equal("keep-section", destination.VisualDividers.Single().Id);
			RegressionAssert.Equal(modified, destination.LastModifiedDate);
			RegressionAssert.Equal(0, Directory.GetFiles(directory, "*.tmp").Length);
		});
	}

	public void ReplacingMalformedLoadedCopyRepairsItsStateAfterCommit()
	{
		WithTemporaryDirectory(directory =>
		{
			var destinationPath = Path.Combine(directory, "Destination.json");
			File.WriteAllText(destinationPath, "{\"Order\":null,\"OverrideModFiles\":[\"Stale.pak\"]}");
			var loaded = LoadOrders(directory).Single();
			RegressionAssert.True(loaded.Order == null);
			var working = CreateOrder(directory, "Working", "new-mod");
			working.OverrideModFiles = [];
			LoadOrderFileWorkflow.SaveCopy(working, destinationPath, [loaded]);
			RegressionAssert.Equal("new-mod", loaded.Order!.Single().UUID);
			RegressionAssert.Equal(0, loaded.OverrideModFiles.Count);
			RegressionAssert.Equal("new-mod", LoadOrders(directory).Single().Order.Single().UUID);
		});
	}

	public void SavingOverAnotherListedOrderSelectsItsDestinationThroughTheGuard()
	{
		WithSavedCopySelection((model, copy) =>
		{
			var selections = new System.Collections.Generic.List<int>();
			using var observer = model.WhenAnyValue(item => item.SelectedModOrderIndex)
				.Skip(1).Subscribe(selections.Add);
			SelectSavedCopy(model, copy);
			RegressionAssert.SequenceEqual(new[] { 1 }, selections);
			RegressionAssert.Equal(1, model.SelectedModOrderIndex);
			// Loading and clearing the dirty flag belong to the guarded selection
			// handler, which this isolated fixture deliberately does not install.
			RegressionAssert.True(model.HasUnsavedLoadOrderChanges);
		});
	}

	public void RejectedSavedCopySelectionDoesNotForceLoadOrClearUnsavedChanges()
	{
		WithSavedCopySelection((model, copy) =>
		{
			var rejected = false;
			using var guard = model.WhenAnyValue(item => item.SelectedModOrderIndex)
				.Skip(1).Subscribe(index =>
				{
					if (index != 1) return;
					rejected = true;
					model.SelectedModOrderIndex = 0;
				});
			SelectSavedCopy(model, copy);
			RegressionAssert.True(rejected);
			RegressionAssert.Equal(0, model.SelectedModOrderIndex);
			RegressionAssert.True(model.HasUnsavedLoadOrderChanges);
			RegressionAssert.Equal("source-mod", model.ModOrderList[0].Order.Single().UUID);
		});
	}

	private static void WithSavedCopySelection(Action<MainWindowViewModel, DivinityLoadOrder> action)
	{
		var originalScheduler = RxApp.MainThreadScheduler;
		RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
		try
		{
			// Exercise the actual Save As selection handoff without profile discovery,
			// directory watchers, file dialogs or a forced mod-list load.
			var model = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
			var source = new DivinityLoadOrder { Name = "Source", FilePath = "source.json",
				Order = [new DivinityLoadOrderEntry { UUID = "source-mod" }] };
			var destination = new DivinityLoadOrder { Name = "Destination", FilePath = "destination.json" };
			model.ModOrderList = new ObservableCollectionExtended<DivinityLoadOrder> { source, destination };
			model.SelectedModOrderIndex = 0;
			typeof(MainWindowViewModel).GetProperty(nameof(model.HasUnsavedLoadOrderChanges))!.SetValue(model, true);
			action(model, new DivinityLoadOrder { Name = "Destination", FilePath = destination.FilePath });
		}
		finally { RxApp.MainThreadScheduler = originalScheduler; }
	}

	private static void SelectSavedCopy(MainWindowViewModel model, DivinityLoadOrder copy) =>
		typeof(MainWindowViewModel).GetMethod("SelectSavedLoadOrderCopy", BindingFlags.Instance | BindingFlags.NonPublic)!
			.Invoke(model, [copy]);

	private static DivinityLoadOrder CreateOrder(string directory, string name, string modUuid) => new()
	{
		Name = name,
		FilePath = Path.Combine(directory, name + ".json"),
		Order = [new DivinityLoadOrderEntry { Name = modUuid, UUID = modUuid }]
	};

	private static System.Collections.Generic.List<DivinityLoadOrder> LoadOrders(string directory) =>
		DivinityModDataLoader.FindLoadOrderFilesInDirectoryAsync(directory).GetAwaiter().GetResult();

	private static void WithTemporaryDirectory(Action<string> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxLoadOrderWorkflowTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			action(directory);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}
}
