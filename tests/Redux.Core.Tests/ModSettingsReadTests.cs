using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Security.Cryptography;
using System.Threading.Tasks;

using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Util;

using ReactiveUI;

namespace Redux.Core.Tests;

public sealed class ModSettingsReadTests
{
	private const string FirstUuid = "11111111-1111-1111-1111-111111111111";
	private const string SecondUuid = "22222222-2222-2222-2222-222222222222";
	private const string Header = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><save><version major=\"4\" minor=\"8\" revision=\"0\" build=\"100\"/>";

	public void MissingModSettingsIsDistinctFromASuccessfulEmptyOrder()
	{
		WithFolder(folder =>
		{
			var missing = Read(Path.Combine(folder, "missing.lsx"));
			RegressionAssert.Equal(ModSettingsReadStatus.Missing, missing.Status);
			RegressionAssert.False(missing.Succeeded);
			RegressionAssert.Equal(0, missing.ActiveMods.Count);
			RegressionAssert.True(!String.IsNullOrWhiteSpace(missing.ErrorMessage));
			RegressionAssert.True(missing.SourceSha256 == null);

			var absentFolder = Read(Path.Combine(folder, "absent", "modsettings.lsx"));
			RegressionAssert.Equal(ModSettingsReadStatus.Missing, absentFolder.Status);
		});
	}

	public void FailedModSettingsNeverExposesAnEmptyOrPartialSuccessfulOrder()
	{
		WithFolder(folder =>
		{
			var invalidFiles = new[]
			{
				String.Empty,
				Header + "<region",
				Document(String.Empty).Replace("ModuleSettings", "OtherRegion"),
				Header + "<region id=\"ModuleSettings\"><node id=\"root\"/></region></save>",
				Document(String.Empty).Replace("<node id=\"Mods\"><children></children></node>", "<node id=\"Mods\"/><node id=\"Mods\"/>"),
				Document("<node id=\"UnexpectedMod\"/>"),
				Document(Mod(FirstUuid, "First") + "<node id=\"ModuleShortDesc\"/>"),
				Document(Mod(FirstUuid, "First") + "<node id=\"ModuleShortDesc\"><attribute id=\"UUID\" type=\"LSString\" value=\"not-a-uuid\"/></node>"),
				Document(Mod(FirstUuid, "First"))[..^7]
			};
			for (var index = 0; index < invalidFiles.Length; index++)
			{
				var path = Path.Combine(folder, $"invalid-{index}.lsx");
				File.WriteAllText(path, invalidFiles[index]);
				var result = Read(path);
				RegressionAssert.Equal(ModSettingsReadStatus.Failed, result.Status);
				RegressionAssert.False(result.Succeeded);
				RegressionAssert.Equal(0, result.ActiveMods.Count);
				RegressionAssert.True(!String.IsNullOrWhiteSpace(result.ErrorMessage));
				RegressionAssert.True(result.SourceSha256 == null);
			}
		});
	}

	public void GenuineEmptyModSettingsReadsSuccessfully()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			foreach (var text in new[] { Document(String.Empty), Document(String.Empty).Replace("<children></children>", String.Empty) })
			{
				File.WriteAllText(path, text);
				var result = Read(path);
				RegressionAssert.True(result.Succeeded);
				RegressionAssert.Equal(ModSettingsReadStatus.Success, result.Status);
				RegressionAssert.Equal(0, result.CountActive());
				RegressionAssert.Equal(String.Empty, result.ErrorMessage);
				RegressionAssert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), result.SourceSha256);
			}
		});
	}

	public void SuccessfulModSettingsPreservesTheCompleteOrder()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			File.WriteAllText(path, Document(Mod(SecondUuid, "Second") + Mod(FirstUuid, "First")));
			var result = Read(path);
			RegressionAssert.True(result.Succeeded);
			RegressionAssert.Equal(2, result.ActiveMods.Count);
			RegressionAssert.Equal(SecondUuid, result.ActiveMods[0].UUID);
			RegressionAssert.Equal("Second", result.ActiveMods[0].Name);
			RegressionAssert.Equal(FirstUuid, result.ActiveMods[1].UUID);
		});
	}

	public void LockedModSettingsCanBeReadAfterTheWriterCloses()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			File.WriteAllText(path, Document(Mod(FirstUuid, "First")));
			// Even a writer that permits readers must not provide a potentially
			// intermediate order to the read path while it still owns write access.
			using (var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
			{
				var locked = Read(path);
				RegressionAssert.Equal(ModSettingsReadStatus.Failed, locked.Status);
				RegressionAssert.Equal(0, locked.ActiveMods.Count);
			}
			var recovered = Read(path);
			RegressionAssert.True(recovered.Succeeded);
			RegressionAssert.Equal(FirstUuid, recovered.ActiveMods.Single().UUID);
		});
	}

	public void ProfileDiscoveryPreservesModSettingsReadOutcomes()
	{
		WithFolder(folder =>
		{
			var previousScheduler = RxApp.MainThreadScheduler;
			try
			{
				RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
				var profileFolder = Path.Combine(folder, "Public");
				Directory.CreateDirectory(profileFolder);
				var path = Path.Combine(profileFolder, "modsettings.lsx");
				var missing = Task.Run(() => DivinityModDataLoader.LoadProfileDataAsync(folder)).GetAwaiter().GetResult().Single();
				RegressionAssert.Equal(ModSettingsReadStatus.Missing, missing.ModSettingsReadResult.Status);
				RegressionAssert.Equal(0, missing.ActiveMods.Count);

				File.WriteAllText(path, Header + "<region");
				var failed = Task.Run(() => DivinityModDataLoader.LoadProfileDataAsync(folder)).GetAwaiter().GetResult().Single();
				RegressionAssert.Equal(ModSettingsReadStatus.Failed, failed.ModSettingsReadResult.Status);
				RegressionAssert.Equal(0, failed.ActiveMods.Count);

				File.WriteAllText(path, Document(Mod(FirstUuid, "First")));
				var restored = Task.Run(() => DivinityModDataLoader.LoadProfileDataAsync(folder)).GetAwaiter().GetResult().Single();
				RegressionAssert.True(restored.ModSettingsReadResult.Succeeded);
				RegressionAssert.Equal(FirstUuid, restored.ActiveMods.Single().UUID);
			}
			finally { RxApp.MainThreadScheduler = previousScheduler; }
		});
	}

	public void OnlyAVerifiedNewerGameOrderSupersedesTheSavedWorkspace()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			File.WriteAllText(path, Header + "<region");
			var failed = Read(path);
			var workspaceTime = DateTime.UtcNow.AddMinutes(-1);
			var gameTime = DateTime.UtcNow;
			var saved = new DivinityLoadOrder { Order = [new DivinityLoadOrderEntry { UUID = FirstUuid }] };
			var current = new DivinityLoadOrder { IsModSettings = true };
			var supersedes = LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(failed, gameTime, workspaceTime);
			RegressionAssert.False(supersedes);
			LoadOrderPersistencePolicy.RestoreSavedCurrentState(current, saved, restoreOrder: !supersedes);
			RegressionAssert.Equal(FirstUuid, current.Order.Single().UUID);
			RegressionAssert.False(LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(
				ModSettingsParseResults.Missing(), gameTime, workspaceTime));

			File.WriteAllText(path, Document(String.Empty));
			var empty = Read(path);
			RegressionAssert.True(LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(empty, gameTime, workspaceTime));
			RegressionAssert.True(LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(empty, workspaceTime, workspaceTime));
			RegressionAssert.False(LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(empty, workspaceTime, gameTime));
			RegressionAssert.False(LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(empty, null, workspaceTime));
			RegressionAssert.False(LoadOrderPersistencePolicy.GameOrderSupersedesSavedCurrent(empty, gameTime, null));
		});
	}

	public void ReadFingerprintDetectsRewrittenBytesEvenWhenTheOrderAndTimestampMatch()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			File.WriteAllText(path, Document(Mod(FirstUuid, "First")));
			var original = Read(path);
			var modified = File.GetLastWriteTimeUtc(path);
			RegressionAssert.True(original.Succeeded);
			RegressionAssert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), original.SourceSha256);

			// The same game order with changed metadata is still a different reviewed file.
			File.WriteAllText(path, Document(Mod(FirstUuid, "Other")));
			File.SetLastWriteTimeUtc(path, modified);
			var replacement = Read(path);
			RegressionAssert.True(replacement.Succeeded);
			RegressionAssert.Equal(original.ActiveMods.Single().UUID, replacement.ActiveMods.Single().UUID);
			RegressionAssert.Equal(modified, File.GetLastWriteTimeUtc(path));
			RegressionAssert.True(original.SourceSha256 != replacement.SourceSha256);
			RegressionAssert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), replacement.SourceSha256);
			RegressionAssert.True(ModSettingsParseResults.Success(original.ActiveMods).SourceSha256 == null);
		});
	}

	public void FingerprintGuardRejectsChangedDeletedAndUnverifiedOrders()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			File.WriteAllText(path, Document(Mod(FirstUuid, "First")));
			var reviewed = Read(path);
			RegressionAssert.True(ModSettingsReadGuard.MatchesCurrentFile(reviewed, path));
			RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(
				ModSettingsParseResults.Success(reviewed.ActiveMods), path));
			RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(
				ModSettingsParseResults.Failed("An earlier read failed."), path));
			var originalTime = File.GetLastWriteTimeUtc(path);
			File.WriteAllText(path, Document(Mod(FirstUuid, "Other")));
			File.SetLastWriteTimeUtc(path, originalTime);
			RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(reviewed, path));
			File.Delete(path);
			RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(reviewed, path));
		});
	}

	public void FingerprintGuardOnlyAcceptsMissingWhenTheFileIsStillMissing()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			var missing = Read(path);
			RegressionAssert.True(ModSettingsReadGuard.MatchesCurrentFile(missing, path));
			File.WriteAllText(path, Document(String.Empty));
			RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(missing, path));
			using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(missing, path));
		});
	}

	public void FingerprintGuardBlocksALockedFileUntilTheWriterCloses()
	{
		WithFolder(folder =>
		{
			var path = Path.Combine(folder, "modsettings.lsx");
			File.WriteAllText(path, Document(Mod(FirstUuid, "First")));
			var reviewed = Read(path);
			using (var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
				RegressionAssert.False(ModSettingsReadGuard.MatchesCurrentFile(reviewed, path));
			RegressionAssert.True(ModSettingsReadGuard.MatchesCurrentFile(reviewed, path));
		});
	}

	private static ModSettingsParseResults Read(string path) =>
		DivinityModDataLoader.LoadModSettingsFileAsync(path).GetAwaiter().GetResult();

	private static string Document(string mods) => Header
		+ "<region id=\"ModuleSettings\"><node id=\"root\"><children><node id=\"Mods\"><children>"
		+ mods + "</children></node></children></node></region></save>";

	private static string Mod(string uuid, string name) =>
		"<node id=\"ModuleShortDesc\"><attribute id=\"Folder\" type=\"LSString\" value=\"AuditMod\"/>"
		+ $"<attribute id=\"Name\" type=\"LSString\" value=\"{name}\"/><attribute id=\"UUID\" type=\"guid\" value=\"{uuid}\"/>"
		+ "<attribute id=\"MD5\" type=\"LSString\" value=\"\"/><attribute id=\"Version64\" type=\"int64\" value=\"1\"/>"
		+ "<attribute id=\"PublishHandle\" type=\"uint64\" value=\"0\"/></node>";

	private static void WithFolder(Action<string> run)
	{
		var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ReduxModSettingsReadTests"));
		var folder = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
		Directory.CreateDirectory(folder);
		try { run(folder); }
		finally
		{
			if (!folder.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("The test fixture is outside its temporary folder.");
			Directory.Delete(folder, true);
		}
	}
}
