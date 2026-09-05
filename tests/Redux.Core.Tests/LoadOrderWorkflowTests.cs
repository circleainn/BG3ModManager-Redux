using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System;
using System.IO;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class LoadOrderWorkflowTests
{
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
