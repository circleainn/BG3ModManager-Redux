using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.IO;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class LoadOrderWorkflowTests
{
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
