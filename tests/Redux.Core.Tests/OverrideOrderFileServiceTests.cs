using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

	public void LinkedModsFolderSwitchesOverridesWithoutFollowingARetargetedLink()
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxOverrideOrderTests", Guid.NewGuid().ToString("N"));
		var target = Path.Combine(root, "ActualMods");
		var otherTarget = Path.Combine(root, "OtherMods");
		var link = Path.Combine(root, "ModsLink");
		var holding = Path.Combine(root, "OverrideOrderHolding");
		Directory.CreateDirectory(target);
		Directory.CreateDirectory(otherTarget);
		try
		{
			CreateJunction(link, target);
			var first = Path.Combine(link, "First.pak");
			File.WriteAllText(first, "first");
			var service = new OverrideOrderFileService(link, holding);
			var plan = service.Review([first], []);
			service.Apply(plan);
			RegressionAssert.False(File.Exists(first));
			RegressionAssert.True(File.Exists(Path.Combine(holding, "First.pak")));
			service.Apply(service.Review([], ["First.pak"]));
			RegressionAssert.True(File.Exists(first));

			plan = service.Review([first], []);
			Directory.Delete(link);
			CreateJunction(link, otherTarget);
			try { service.Apply(plan); throw new InvalidOperationException("A retargeted link was accepted."); }
			catch (IOException) { }
			RegressionAssert.True(File.Exists(Path.Combine(target, "First.pak")));
			RegressionAssert.False(File.Exists(Path.Combine(otherTarget, "First.pak")));

			Directory.Delete(link);
			CreateJunction(link, target);
			Directory.CreateDirectory(holding);
			var journal = Path.Combine(holding, "pending-switch.json");
			File.WriteAllText(journal, System.Text.Json.JsonSerializer.Serialize(new
			{
				ModsFolderTarget = target,
				Moves = plan.Moves
			}));
			File.Move(Path.Combine(target, "First.pak"), Path.Combine(holding, "First.pak"));
			Directory.Delete(link);
			CreateJunction(link, otherTarget);
			try { new OverrideOrderFileService(link, holding).Recover(); throw new InvalidOperationException("A retargeted journal was accepted."); }
			catch (IOException) { }
			RegressionAssert.True(File.Exists(journal));
			RegressionAssert.True(File.Exists(Path.Combine(holding, "First.pak")));
			Directory.Delete(link);
			CreateJunction(link, target);
			new OverrideOrderFileService(link, holding).Recover();
			RegressionAssert.True(File.Exists(first));
			RegressionAssert.False(File.Exists(journal));
		}
		finally
		{
			if (Directory.Exists(link)) Directory.Delete(link);
			Directory.Delete(root, true);
		}
	}

	public void HoldingFolderCannotBeInsideTheModsTargetEvenWhenItIsADriveRoot()
	{
		var root = Path.GetPathRoot(Path.GetTempPath())!;
		var holding = Path.Combine(Path.GetTempPath(), "ReduxOverrideOrderTests", "Holding");
		try { _ = new OverrideOrderFileService(root, holding); throw new InvalidOperationException("A nested holding folder was accepted."); }
		catch (IOException) { }
	}

	private static void CreateJunction(string link, string target)
	{
		const string script = "$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path $env:REDUX_TEST_LINK -Target $env:REDUX_TEST_TARGET | Out-Null";
		var start = new ProcessStartInfo("powershell.exe")
		{
			UseShellExecute = false, CreateNoWindow = true,
			RedirectStandardError = true, RedirectStandardOutput = true
		};
		start.ArgumentList.Add("-NoProfile");
		start.ArgumentList.Add("-NonInteractive");
		start.ArgumentList.Add("-EncodedCommand");
		start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
		start.Environment["REDUX_TEST_LINK"] = link;
		start.Environment["REDUX_TEST_TARGET"] = target;
		using var process = Process.Start(start) ?? throw new IOException("Could not create the test junction.");
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0) throw new IOException("Could not create the test junction: " + error);
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
