using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class ReduxPreUpdateBackupTests
{
	public void BackupIncludesInstallModsAndOrdersWithoutSaveGames()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-pre-update-" + Guid.NewGuid().ToString("N"));
		var install = Path.Combine(root, "Install");
		var mods = Path.Combine(root, "Mods");
		var previous = Path.Combine(root, "Mods_Old_ModManager");
		var orders = Path.Combine(root, "Orders");
		var profiles = Path.Combine(root, "Profiles");
		var archive = Path.Combine(root, "BeforeUpdate.zip");
		try
		{
			Directory.CreateDirectory(install);
			Directory.CreateDirectory(mods);
			Directory.CreateDirectory(previous);
			Directory.CreateDirectory(orders);
			Directory.CreateDirectory(Path.Combine(profiles, "Public", "Savegames"));
			File.WriteAllText(Path.Combine(install, "Redux.exe"), "old app");
			File.WriteAllText(Path.Combine(install, "settings.json"), "old settings");
			File.WriteAllText(Path.Combine(mods, "Example.pak"), "old pak");
			File.WriteAllText(Path.Combine(previous, "Earlier.pak"), "previous pak");
			File.WriteAllText(Path.Combine(orders, "My Order.json"), "old order");
			File.WriteAllText(Path.Combine(profiles, "Public", "modsettings.lsx"), "game order");
			File.WriteAllText(Path.Combine(profiles, "Public", "Savegames", "save.lsv"), "unrelated save");
			var sources = new[]
			{
			new ReduxPreUpdateBackupService.Source("Redux", install),
				new ReduxPreUpdateBackupService.Source("BG3 Mods", mods),
				new ReduxPreUpdateBackupService.Source("Previous Mod Versions", previous),
			new ReduxPreUpdateBackupService.Source("Saved Orders", orders),
			new ReduxPreUpdateBackupService.Source("Profile Load Orders", profiles, true)
			};
			ReduxPreUpdateBackupService.CreateAsync(archive, sources).GetAwaiter().GetResult();
			using var zip = ZipFile.OpenRead(archive);
			var names = zip.Entries.Select(entry => entry.FullName).ToArray();
			RegressionAssert.True(names.Contains("Redux/Redux.exe"));
			RegressionAssert.True(names.Contains("Redux/settings.json"));
			RegressionAssert.True(names.Contains("BG3 Mods/Example.pak"));
			RegressionAssert.True(names.Contains("Previous Mod Versions/Earlier.pak"));
			RegressionAssert.True(names.Contains("Saved Orders/My Order.json"));
			RegressionAssert.True(names.Contains("Profile Load Orders/Public/modsettings.lsx"));
			RegressionAssert.False(names.Any(name => name.EndsWith("save.lsv", StringComparison.OrdinalIgnoreCase)));
			RegressionAssert.False(File.Exists(archive + ".partial"));
			RegressionAssert.True(File.Exists(Path.Combine(mods, "Example.pak")));
			RegressionAssert.Throws<IOException>(() => ReduxPreUpdateBackupService.CreateAsync(
				Path.Combine(install, "recursive.zip"), sources).GetAwaiter().GetResult());
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			var canceledArchive = Path.Combine(root, "Canceled.zip");
			RegressionAssert.Throws<OperationCanceledException>(() => ReduxPreUpdateBackupService.CreateAsync(
				canceledArchive, sources, cancellationToken: cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.False(File.Exists(canceledArchive));
			RegressionAssert.False(File.Exists(canceledArchive + ".partial"));
		}
		finally
		{
			if (Directory.Exists(root)) Directory.Delete(root, true);
		}
	}
}
