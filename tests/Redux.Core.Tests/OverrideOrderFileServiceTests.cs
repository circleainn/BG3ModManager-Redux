using System;
using System.IO;
using System.Linq;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using Newtonsoft.Json;

namespace Redux.Core.Tests;

public sealed class OverrideOrderFileServiceTests
{
	public void PureOverridesUseSelectionWhileMixedOverridesFollowActiveMods()
	{
		var pure = new DivinityModData { UUID = "pure", FilePath = "Pure.pak", IsForceLoaded = true };
		var mixed = new DivinityModData { UUID = "mixed", FilePath = "Mixed.pak",
			IsForceLoaded = true, IsForceLoadedMergedMod = true };
		var wanted = OverrideOrderMembershipPolicy.WantedFiles([pure, mixed], [], ["mixed"]);
		RegressionAssert.Equal("Mixed.pak", wanted.Single());
		wanted = OverrideOrderMembershipPolicy.WantedFiles([pure, mixed], ["Pure.pak"], []);
		RegressionAssert.Equal("Pure.pak", wanted.Single());
	}

	public void OptInOrderRoundTripsIncludingAnEmptySelection()
	{
		var order = new DivinityLoadOrder { Name = "A", OverrideModFiles = [] };
		var restored = JsonConvert.DeserializeObject<DivinityLoadOrder>(JsonConvert.SerializeObject(order));
		RegressionAssert.True(restored?.OverrideModFiles != null);
		RegressionAssert.Equal(0, restored.OverrideModFiles.Count);
		RegressionAssert.True(new DivinityLoadOrder { Name = "Legacy" }.OverrideModFiles == null);
		order.OverrideModFiles.Add("First.pak");
		var working = LoadOrderPersistencePolicy.CreateWorkingCopy(order, []);
		RegressionAssert.Equal("First.pak", working.OverrideModFiles.Single());
		working.OverrideModFiles.Clear();
		RegressionAssert.Equal(1, order.OverrideModFiles.Count);
	}

	public void ReviewedSwitchHoldsAndRestoresOnlySelectedPakFiles()
	{
		WithFolders((mods, holding) =>
		{
			var first = Path.Combine(mods, "First.pak");
			var second = Path.Combine(mods, "Second.pak");
			File.WriteAllText(first, "first");
			File.WriteAllText(second, "second");
			File.WriteAllText(Path.Combine(mods, "Unrelated.txt"), "keep");
			var service = new OverrideOrderFileService(mods, holding);
			var plan = service.Review([first, second], ["Second.pak"]);
			RegressionAssert.Equal(1, plan.Moves.Count);
			service.Apply(plan);
			RegressionAssert.False(File.Exists(first));
			RegressionAssert.True(File.Exists(Path.Combine(holding, "First.pak")));
			RegressionAssert.True(File.Exists(second));
			RegressionAssert.True(File.Exists(Path.Combine(mods, "Unrelated.txt")));
			service.Apply(service.Review([second], ["First.pak", "Second.pak"]));
			RegressionAssert.True(File.Exists(first));
			RegressionAssert.False(File.Exists(Path.Combine(holding, "First.pak")));
		});
	}

	public void ChangedFileOrOccupiedDestinationBlocksReviewedSwitch()
	{
		WithFolders((mods, holding) =>
		{
			var first = Path.Combine(mods, "First.pak");
			File.WriteAllText(first, "first");
			var service = new OverrideOrderFileService(mods, holding);
			var plan = service.Review([first], []);
			File.WriteAllText(first, "changed");
			try { service.Apply(plan); throw new InvalidOperationException("Changed file was moved."); }
			catch (IOException) { }
			RegressionAssert.True(File.Exists(first));
			RegressionAssert.False(File.Exists(Path.Combine(holding, "First.pak")));
		});
	}

	public void InterruptedSwitchRestoresAlreadyMovedPackages()
	{
		WithFolders((mods, holding) =>
		{
			var first = Path.Combine(mods, "First.pak");
			File.WriteAllText(first, "first");
			var service = new OverrideOrderFileService(mods, holding);
			var plan = service.Review([first], []);
			Directory.CreateDirectory(holding);
			var journal = Path.Combine(holding, "pending-switch.json");
			File.WriteAllText(journal, System.Text.Json.JsonSerializer.Serialize(plan.Moves));
			File.Move(first, Path.Combine(holding, "First.pak"));
			service.Recover();
			RegressionAssert.True(File.Exists(first));
			RegressionAssert.False(File.Exists(journal));
		});
	}

	private static void WithFolders(Action<string, string> run)
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxOverrideOrderTests", Guid.NewGuid().ToString("N"));
		var mods = Path.Combine(root, "Mods");
		var holding = Path.Combine(root, "OverrideOrderHolding");
		Directory.CreateDirectory(mods);
		try { run(mods, holding); }
		finally { Directory.Delete(root, true); }
	}
}
