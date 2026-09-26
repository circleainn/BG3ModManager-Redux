using DivinityModManager.AppServices;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace Redux.Core.Tests;

public sealed class ReduxGameDirectoryInstallServiceTests
{
	public void YanmlPluginInstallUsesLocalFolderAndIndependentOwnership()
	{
		using var fixture = new NativeFixture();
		var localAppData = fixture.NewArchivePath("LocalAppData");
		var plugins = ReduxAlternativeNativeLoader.PluginsDirectory(localAppData);
		Directory.CreateDirectory(plugins);
		File.WriteAllText(Path.Combine(plugins, "config.toml"), "[core]\nenabled = true\n");
		fixture.CreateWasdArchive(fixture.Pe("YANML WASD plugin"), "[input]\nforward = \"W\"\n");
		var installer = new ReduxGameDirectoryInstallService(fixture.GameBin, fixture.StateDirectory,
			NativeFixture.SupportedVersion, false, ReduxNativePluginDestination.YanmlPlugins, localAppData);
		RegressionAssert.True(installer.DetectLoader().IsAlternativeLoader);
		RegressionAssert.True(installer.DetectLoader().IsPresent);
		File.WriteAllText(Path.Combine(plugins, "config.toml"), "[core]\nenabled = false # paused\n");
		RegressionAssert.False(installer.DetectLoader().IsPresent);
		RegressionAssert.Throws<InvalidOperationException>(() => installer.StageAsync(781, fixture.WasdArchivePath)
			.GetAwaiter().GetResult());
		File.WriteAllText(Path.Combine(plugins, "config.toml"), "[core]\nenabled = true\ninstall_root = 'C:\\Unrelated BG3'\n");
		RegressionAssert.False(installer.DetectLoader().IsPresent);
		RegressionAssert.Contains(installer.DetectLoader().Description, "different BG3 installation");
		var gameRoot = Path.GetDirectoryName(fixture.GameBin)!;
		File.WriteAllText(Path.Combine(plugins, "config.toml"),
			$"[core]\nenabled = true\ninstall_root = '{gameRoot}'\n");
		RegressionAssert.True(installer.DetectLoader().IsPresent);
		var escapedRoot = gameRoot.Replace("\\", "\\\\");
		File.WriteAllText(Path.Combine(plugins, "config.toml"),
			$"[core]\nenabled = true\ninstall_root = \"{escapedRoot}\"\n");
		RegressionAssert.True(installer.DetectLoader().IsPresent);
		File.WriteAllText(Path.Combine(plugins, "config.toml"), "[core]\nenabled = true\n");
		var transaction = installer.StageAsync(781, fixture.WasdArchivePath).GetAwaiter().GetResult();
		try { transaction.CommitAsync().GetAwaiter().GetResult(); }
		finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
		RegressionAssert.True(File.Exists(Path.Combine(plugins, "BG3WASD.dll")));
		RegressionAssert.True(File.Exists(Path.Combine(plugins, "BG3WASD.toml")));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.True(installer.GetInstalledMods().Any(mod => mod.NexusModId == 781
			&& mod.Status == ReduxGameDirectoryModStatus.Managed));
		RegressionAssert.False(fixture.Installer().GetInstalledMods().Any(mod => mod.NexusModId == 781));
		RegressionAssert.True(ReduxGameDirectoryInstallService.HasYanmlOwnershipRecord(fixture.GameBin, fixture.StateDirectory));
		File.Delete(Path.Combine(plugins, "config.toml"));
		var reopened = new ReduxGameDirectoryInstallService(fixture.GameBin, fixture.StateDirectory,
			NativeFixture.SupportedVersion, false, ReduxNativePluginDestination.YanmlPlugins, localAppData);
		RegressionAssert.False(reopened.DetectLoader().IsPresent);
		RegressionAssert.True(reopened.GetInstalledMods().Any(mod => mod.NexusModId == 781 && mod.CanRestore));
		File.WriteAllText(Path.Combine(plugins, "config.toml"), "[core]\nenabled = true\n");
		installer.RestoreAsync(781).GetAwaiter().GetResult();
		RegressionAssert.False(File.Exists(Path.Combine(plugins, "BG3WASD.dll")));
		RegressionAssert.True(File.Exists(Path.Combine(plugins, "BG3WASD.toml")));
		var unmanagedDll = fixture.Pe("external replacement");
		File.WriteAllBytes(Path.Combine(plugins, "BG3WASD.dll"), unmanagedDll);
		RegressionAssert.Throws<InvalidOperationException>(() => installer.StageAsync(781, fixture.WasdArchivePath)
			.GetAwaiter().GetResult());
		RegressionAssert.SequenceEqual(unmanagedDll, File.ReadAllBytes(Path.Combine(plugins, "BG3WASD.dll")));
	}

	public void YanmlConfigurationExplainsNativePluginDestination()
	{
		var directory = Directory.CreateTempSubdirectory("redux-yanml-");
		try
		{
			var loader = new ReduxNativeLoaderStatus(false, false, "Native Mod Loader is missing.");
			var ordinary = ReduxAlternativeNativeLoader.MissingLoaderMessage("WASD", loader, directory.FullName);
			RegressionAssert.Contains(ordinary, "requires Native Mod Loader");
			RegressionAssert.False(ordinary.Contains("YANML", StringComparison.Ordinal));

			var plugins = ReduxAlternativeNativeLoader.PluginsDirectory(directory.FullName);
			Directory.CreateDirectory(plugins);
			File.WriteAllText(Path.Combine(plugins, "config.toml"), "[core]\n");
			var alternative = ReduxAlternativeNativeLoader.MissingLoaderMessage("WASD", loader, directory.FullName);
			RegressionAssert.Contains(alternative, plugins);
			RegressionAssert.Contains(alternative, "BG3\\bin\\NativeMods");
			RegressionAssert.Contains(alternative, "Install for YANML");
		}
		finally
		{
			var tempRoot = Path.GetFullPath(Path.GetTempPath());
			var target = Path.GetFullPath(directory.FullName);
			if (!target.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("Unexpected test cleanup path.");
			Directory.Delete(target, recursive: true);
		}
	}

	public void VanillaBinkIsNotAnExternalNativeLoader()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla game bink");
		fixture.WriteVanillaLoader(vanilla);
		RegressionAssert.False(fixture.Installer().GetInstalledMods().Any(mod => mod.NexusModId == 944));
		RegressionAssert.False(fixture.Installer().DetectLoader().IsPresent);
		File.WriteAllBytes(fixture.LoaderOriginalPath, vanilla);
		RegressionAssert.False(fixture.Installer().GetInstalledMods().Any(mod => mod.NexusModId == 944));
		RegressionAssert.False(fixture.Installer().DetectLoader().IsPresent);
		fixture.WriteVanillaLoader(fixture.Pe("distinct external loader"));
		RegressionAssert.True(fixture.Installer().GetInstalledMods().Any(mod => mod.NexusModId == 944));
		RegressionAssert.True(fixture.Installer().DetectLoader().IsPresent);
	}

	public void AtomicReplacementRejectsSourceChangedSinceItsReviewedHash()
	{
		using var fixture = new NativeFixture();
		var original = fixture.Pe("original destination");
		fixture.WriteVanillaLoader(original);
		var reviewed = fixture.Pe("reviewed source");
		var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(reviewed)).ToLowerInvariant();
		File.WriteAllBytes(fixture.OtherArchivePath, fixture.Pe("changed after review"));
		var installer = fixture.Installer();
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var snapshot = typeof(ReduxGameDirectoryInstallService).GetMethod("CaptureDestination", flags)!.Invoke(installer, ["bink2w64.dll"]);
		var copy = typeof(ReduxGameDirectoryInstallService).GetMethod("CopyToTargetAtomicallyAsync", flags)!;
		RegressionAssert.Throws<InvalidDataException>(() => ((System.Threading.Tasks.Task)copy.Invoke(installer,
			[fixture.OtherArchivePath, fixture.LoaderPath, expectedHash, snapshot, CancellationToken.None])!).GetAwaiter().GetResult());
		RegressionAssert.SequenceEqual(original, File.ReadAllBytes(fixture.LoaderPath));
		File.WriteAllBytes(fixture.OtherArchivePath, reviewed);
		((System.Threading.Tasks.Task)copy.Invoke(installer,
			[fixture.OtherArchivePath, fixture.LoaderPath, expectedHash, snapshot, CancellationToken.None])!).GetAwaiter().GetResult();
		RegressionAssert.SequenceEqual(reviewed, File.ReadAllBytes(fixture.LoaderPath));
	}

	public void CatalogContainsReviewedNativeProjectsAndGuardedWorkflows()
	{
		RegressionAssert.Equal(12, ReduxGameDirectoryModCatalog.All.Count);
		RegressionAssert.Equal("Native Mod Loader", ReduxGameDirectoryModCatalog.Find(944)!.Name);
		RegressionAssert.True(ReduxGameDirectoryModCatalog.Find(944)!.ReplacesExistingGameFiles);
		RegressionAssert.False(ReduxGameDirectoryModCatalog.Find(781)!.ReplacesExistingGameFiles);
		RegressionAssert.Equal("WASD Character Movement", ReduxGameDirectoryModCatalog.Find(781)!.Name);
		RegressionAssert.Equal("Native Camera Tweaks", ReduxGameDirectoryModCatalog.Find(945)!.Name);
		RegressionAssert.Equal("native-camera-tweaks", ReduxGameDirectoryModCatalog.Find(22892)!.PackageId);
		RegressionAssert.Equal("native-camera-tweaks", ReduxGameDirectoryModCatalog.Find(945)!.PackageId);
		RegressionAssert.True(ReduxGameDirectoryModCatalog.Find(2172)!.SupportsGuardedInstall);
		RegressionAssert.Equal(null, ReduxGameDirectoryModCatalog.Find(1));
		RegressionAssert.SequenceEqual(
			new[] { "bin/bink2w64.dll", "bin/bink2w64_original.dll" },
			ReduxGameDirectoryModCatalog.Find(944)!.RelativeFiles);
		RegressionAssert.SequenceEqual(
			new[] { "bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml" },
			ReduxGameDirectoryModCatalog.Find(781)!.RelativeFiles);
		RegressionAssert.SequenceEqual(
			new[] { "bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml" },
			ReduxGameDirectoryModCatalog.Find(945)!.RelativeFiles);
	}

	public void ReviewedCameraFingerprintsDistinguishLegacyAndGuiProjects()
	{
		var legacy = ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
			"bin/NativeMods/BG3NativeCameraTweaks.dll", 774656,
			"7D183B30892C69978534AF5875FBF2B7AE510A2FD3408387B50A21F3612491CA");
		var gui = ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
			"bin/NativeMods/BG3NativeCameraTweaks.dll", 1361408,
			"E254D1195B45B7C94ADD56B3A16FC823E2D7589D7B6E3D8B6AD45FCECE266543");

		RegressionAssert.Equal(945L, legacy!.Definition.NexusModId);
		RegressionAssert.Equal("2.4.5", legacy.Fingerprint.Version);
		RegressionAssert.Equal(22892L, gui!.Definition.NexusModId);
		RegressionAssert.Equal("2.5.1", gui.Fingerprint.Version);
		RegressionAssert.Equal(null, ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
			"bin/NativeMods/BG3NativeCameraTweaks.dll", 774656, new string('0', 64)));
	}

	public void ReviewedCatalogFingerprintsCoverEveryKnownDllProject()
	{
		var expectedVersions = new (long ProjectId, string Version)[]
		{
			(944, "1.0"), (781, "1.9.9"), (945, "2.4.5"), (22892, "2.5.1"),
			(668, "1.3"), (1326, "1.0.0"), (742, "2.0"), (23881, "2.2.0"),
			(23413, "1.2"), (23959, "2.0"), (24804, "1.0"), (2172, "32 hotfix 1")
		};
		foreach (var expected in expectedVersions)
		{
			var definition = ReduxGameDirectoryModCatalog.Find(expected.ProjectId)!;
			RegressionAssert.True(definition.BinaryFingerprints.Any(fingerprint => fingerprint.Version == expected.Version));
			RegressionAssert.True(definition.BinaryFingerprints.All(fingerprint =>
				definition.RelativeFiles.Contains(fingerprint.RelativePath, StringComparer.OrdinalIgnoreCase)));
		}

		var wasd = ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
			"BIN\\NativeMods\\BG3WASD.dll", 1074688,
			"5D7106834DCD0938EDF367324F5688F9C518A8FAAAE056CDE77C9CED52616D74");
		var follow = ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
			"bin/NativeMods/BG3WASD.dll", 1110016,
			"6B62AC4B2859C64EE73977AE451F515EA19EEF7D2C66BB25E58B2CC90E406E66");
		RegressionAssert.Equal(781L, wasd!.Definition.NexusModId);
		RegressionAssert.Equal(23413L, follow!.Definition.NexusModId);
	}

	public void UnknownSharedCameraBinaryRemainsAnUnverifiedVariant()
	{
		using var fixture = new NativeFixture();
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		File.WriteAllBytes(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.dll"), fixture.Pe("unknown camera variant"));
		File.WriteAllText(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.toml"), "[camera]\nfov = 75\n");

		var entry = fixture.Installer().GetInstalledMods().Single();

		RegressionAssert.Equal(-1L, entry.NexusModId);
		RegressionAssert.Contains(entry.Name, "unverified variant");
		RegressionAssert.Contains(entry.StatusText, "unverified variant");
		RegressionAssert.Equal(String.Empty, entry.SourceUrl);
	}

	public void ArchiveRecognitionUsesReviewedLayoutAndCorroboratesOverlappingProjects()
	{
		using var fixture = new NativeFixture();
		fixture.CreateLoaderArchive(fixture.Pe("loader"), fixture.Pe("original"));
		var loader = ReduxGameDirectoryInstallService.TryInspectKnownArchive(fixture.LoaderArchivePath);
		RegressionAssert.Equal(944L, loader!.Definition.NexusModId);
		RegressionAssert.Equal(2, loader.ManagedFiles.Count);
		RegressionAssert.Equal(0, loader.PackageEntries.Count);

		fixture.CreateCameraArchive(fixture.Pe("camera"), "[camera]\nfov = 75\n");
		RegressionAssert.Throws<ReduxUnsupportedGameDirectoryArchiveException>(() =>
			ReduxGameDirectoryInstallService.TryInspectKnownArchive(fixture.CameraArchivePath));

		var namedCamera = Path.Combine(Path.GetDirectoryName(fixture.CameraArchivePath)!,
			"NativeCameraTweaksGUI 22892 2.5.1 abcDEF12.zip");
		fixture.CreateArchive(namedCamera,
			("bin/NativeMods/BG3NativeCameraTweaks.dll", fixture.Pe("camera")),
			("bin/NativeMods/BG3NativeCameraTweaks.toml", Encoding.UTF8.GetBytes("[camera]\nfov = 75\n")));
		var camera = ReduxGameDirectoryInstallService.TryInspectKnownArchive(namedCamera);
		RegressionAssert.Equal(22892L, camera!.Definition.NexusModId);
		RegressionAssert.Equal("native-camera-tweaks", camera.Definition.PackageId);
	}

	public void ArchiveRecognitionRoutesScriptExtenderToGuardedGameDirectoryWorkflow()
	{
		using var fixture = new NativeFixture();
		var scriptExtender = Path.Combine(Path.GetDirectoryName(fixture.LoaderArchivePath)!, "ScriptExtender.zip");
		fixture.CreateArchive(scriptExtender, ("DWrite.dll", fixture.Pe("script extender")));
		var inspection = ReduxGameDirectoryInstallService.TryInspectKnownArchive(scriptExtender);
		RegressionAssert.Equal(2172L, inspection!.Definition.NexusModId);
		RegressionAssert.True(inspection.Definition.SupportsGuardedInstall);
	}

	public void ScriptExtenderUsesTheSameStagedCommitAndOwnershipRecordAsOtherGameDirectoryMods()
	{
		using var fixture = new NativeFixture();
		var scriptExtender = Path.Combine(Path.GetDirectoryName(fixture.LoaderArchivePath)!, "ScriptExtender.zip");
		var dll = fixture.Pe("script extender guarded install");
		fixture.CreateArchive(scriptExtender, ("DWrite.dll", dll));
		var transaction = fixture.Installer().StageAsync(2172, scriptExtender, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			RegressionAssert.False(File.Exists(Path.Combine(fixture.GameBin, "DWrite.dll")));
			transaction.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.SequenceEqual(dll, File.ReadAllBytes(Path.Combine(fixture.GameBin, "DWrite.dll")));
		var managed = fixture.Installer().GetInstalledMods().Single();
		RegressionAssert.Equal("script-extender", managed.PackageId);
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Managed, managed.Status);
	}

	public void UnreviewedDllArchiveIsNeverTreatedAsAnOrdinaryModArchive()
	{
		using var fixture = new NativeFixture();
		var archivePath = fixture.NewArchivePath("UnknownNative.zip");
		fixture.CreateArchive(archivePath, ("bin/NativeMods/Unknown.dll", fixture.Pe("unknown")));
		RegressionAssert.Throws<ReduxUnsupportedGameDirectoryArchiveException>(() =>
			ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath));
	}

	public void EveryReviewedCatalogLayoutHasARecognizableFixture()
	{
		using var fixture = new NativeFixture();
		var checkedLayouts = 0;
		foreach (var definition in ReduxGameDirectoryModCatalog.All)
		{
			foreach (var layout in definition.Layouts)
			{
				var archivePath = fixture.NewArchivePath($"Fixture {definition.NexusModId} 1.0 abcDEF12.zip");
				var entries = layout.ManagedEntries.Keys.Select(path => (
					Name: path,
					Bytes: path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
						? fixture.Pe($"{definition.PackageId}-{layout.Name}-{path}")
						: Encoding.UTF8.GetBytes($"configuration for {definition.PackageId}")))
					.Concat(layout.PackageEntries.Select(path => (path, Encoding.UTF8.GetBytes("companion pak"))))
					.Concat(layout.IgnoredEntries.Select(path => (path, Encoding.UTF8.GetBytes("documentation"))))
					.ToArray();
				fixture.CreateArchive(archivePath, entries);

				var inspection = ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath);
				RegressionAssert.Equal(definition.NexusModId, inspection!.Definition.NexusModId);
				RegressionAssert.Equal(layout.Name, inspection.LayoutName);
				RegressionAssert.Equal(layout.PackageEntries.Count, inspection.PackageEntries.Count);
				checkedLayouts++;
			}
		}
		RegressionAssert.Equal(ReduxGameDirectoryModCatalog.All.Sum(definition => definition.Layouts.Count), checkedLayouts);
	}

	public void ZipValidationRejectsOtherFormatsTraversalAndUnexpectedFilesWithoutChangingGameFiles()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(vanilla);
		File.WriteAllText(fixture.OtherArchivePath, "not a zip");

		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.OtherArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		fixture.CreateArchive(fixture.LoaderArchivePath,
			("bin/bink2w64.dll", fixture.Pe("loader")),
			("bin/bink2w64_original.dll", vanilla),
			("../outside.dll", fixture.Pe("outside")));
		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		fixture.CreateArchive(fixture.LoaderArchivePath,
			("bin/bink2w64.dll", fixture.Pe("loader")),
			("bin/bink2w64_original.dll", vanilla),
			("readme.txt", Encoding.UTF8.GetBytes("unexpected")));
		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(fixture.LoaderOriginalPath));
	}

	public void OversizedZipEntriesAreRejectedBeforeTheyCanBeStaged()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(vanilla);
		fixture.CreateOversizedLoaderArchive(vanilla);

		RegressionAssert.Throws<InvalidDataException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());

		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
	}

	public void NativePluginsRequireAKnownSupportedGameVersionAndLoader()
	{
		using var fixture = new NativeFixture();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(null).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(new Version(4, 1, 1, 6931812)).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void InspectArchiveAllowsDeferredLoaderPrerequisiteButStillChecksGameVersion()
	{
		using var fixture = new NativeFixture();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");

		fixture.Installer(NativeFixture.SupportedVersion).InspectArchiveAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.False(Directory.Exists(fixture.NativeModsDirectory));
		RegressionAssert.Equal(0, fixture.StagingDirectoryCount());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().InspectArchiveAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void LoaderInstallStagesWithoutChangingGameFilesThenCommitsAsReduxVerified()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		var loader = fixture.Pe("loader");
		fixture.WriteVanillaLoader(vanilla);
		fixture.CreateLoaderArchive(loader, vanilla);

		var transaction = fixture.Installer().StageAsync(944, fixture.LoaderArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
			RegressionAssert.False(File.Exists(fixture.LoaderOriginalPath));
			RegressionAssert.Contains(transaction.ReviewText, "Native Mod Loader");
			RegressionAssert.Contains(transaction.ReviewText, fixture.GameBin);
			transaction.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.SequenceEqual(loader, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderOriginalPath));
		var status = fixture.Installer().DetectLoader();
		RegressionAssert.True(status.IsPresent);
		RegressionAssert.True(status.IsVerified);
		RegressionAssert.Contains(status.Description, "verified");
	}

	public void ReduxInstalledLoaderKeepsAProtectedCopyOfTheUsersOriginalDll()
	{
		using var fixture = new NativeFixture();
		var original = fixture.Pe("user game dll");
		fixture.WriteVanillaLoader(original);
		fixture.CreateLoaderArchive(fixture.Pe("reviewed loader"), original);
		fixture.Install(944, fixture.LoaderArchivePath, null);

		using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fixture.ManifestPath));
		var loader = manifest.RootElement.GetProperty("Installations")[0].GetProperty("Files")
			.EnumerateArray().Single(file => file.GetProperty("RelativePath").GetString() == "bink2w64.dll");
		RegressionAssert.False(loader.GetProperty("Created").GetBoolean());
		var backupId = loader.GetProperty("OriginalBackupId").GetString();
		RegressionAssert.True(!String.IsNullOrWhiteSpace(backupId));
		var backupPath = Path.Combine(Path.GetDirectoryName(fixture.ManifestPath)!, "backups", backupId + ".bin");
		RegressionAssert.SequenceEqual(original, File.ReadAllBytes(backupPath));
	}

	public void ReplacerInstallNeverBacksUpAnUnreviewedOrModdedDll()
	{
		using var fixture = new NativeFixture();
		var modded = fixture.Pe("already modded game dll");
		fixture.WriteVanillaLoader(modded);
		fixture.CreateLoaderArchive(fixture.Pe("unreviewed loader"), modded);

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.StrictInstaller().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.SequenceEqual(modded, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(fixture.ManifestPath));
	}

	public void ExternalLoaderPairIsPresentButUnverifiedAndUnmanagedOriginalConflicts()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(fixture.Pe("external loader"));
		File.WriteAllBytes(fixture.LoaderOriginalPath, vanilla);

		var status = fixture.Installer().DetectLoader();
		RegressionAssert.True(status.IsPresent);
		RegressionAssert.False(status.IsVerified);
		RegressionAssert.Contains(status.Description, "unverified");

		fixture.CreateLoaderArchive(fixture.Pe("approved loader"), vanilla);
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().StageAsync(
			944, fixture.LoaderArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void ChangedReduxOwnedLoaderBlocksDependentPluginInstallation()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		File.WriteAllBytes(fixture.LoaderPath, fixture.Pe("user replacement"));
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");

		var status = fixture.Installer(NativeFixture.SupportedVersion).DetectLoader();
		RegressionAssert.False(status.IsPresent);
		RegressionAssert.False(status.IsVerified);
		RegressionAssert.Contains(status.Description, "changed");
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult());
	}

	public void ManagerStatusDistinguishesManagedChangedAndExternalFiles()
	{
		using var managedFixture = new NativeFixture();
		managedFixture.InstallLoader();
		var managed = managedFixture.Installer().GetInstalledMods().Single();
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Managed, managed.Status);
		RegressionAssert.True(managed.CanRestore);

		File.WriteAllBytes(managedFixture.LoaderPath, managedFixture.Pe("changed outside Redux"));
		var changed = managedFixture.Installer().GetInstalledMods().Single();
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Changed, changed.Status);
		RegressionAssert.False(changed.CanRestore);

		using var externalFixture = new NativeFixture();
		externalFixture.WriteVanillaLoader(externalFixture.Pe("external loader"));
		File.WriteAllBytes(externalFixture.LoaderOriginalPath, externalFixture.Pe("external original"));
		var external = externalFixture.Installer().GetInstalledMods().Single();
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.External, external.Status);
		RegressionAssert.False(external.CanRestore);
		RegressionAssert.False(external.CanAdopt);
		RegressionAssert.Contains(external.StatusText, "no protected original backup");
	}

	public void ReviewedAddOnlyModCanRepairItsChangedOrMissingOwnedDll()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		var reviewedDll = fixture.Pe("reviewed wasd");
		fixture.CreateWasdArchive(reviewedDll, "[input]\nforward = \"W\"\n");
		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);
		var dllPath = Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll");

		File.WriteAllBytes(dllPath, fixture.Pe("changed outside Redux"));
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Changed,
			fixture.Installer().GetInstalledMods().Single(entry => entry.NexusModId == 781).Status);
		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);
		RegressionAssert.SequenceEqual(reviewedDll, File.ReadAllBytes(dllPath));

		File.Delete(dllPath);
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Missing,
			fixture.Installer().GetInstalledMods().Single(entry => entry.NexusModId == 781).Status);
		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);
		RegressionAssert.SequenceEqual(reviewedDll, File.ReadAllBytes(dllPath));
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Managed,
			fixture.Installer().GetInstalledMods().Single(entry => entry.NexusModId == 781).Status);
	}

	public void ManagerSurfacesUnknownNativeDllsWithoutClaimingOwnership()
	{
		using var fixture = new NativeFixture();
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		File.WriteAllBytes(Path.Combine(fixture.NativeModsDirectory, "Uncatalogued.dll"), fixture.Pe("external"));

		var external = fixture.Installer().GetInstalledMods().Single();
		RegressionAssert.Equal("Other native files", external.Name);
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.External, external.Status);
		RegressionAssert.False(external.CanRestore);
		RegressionAssert.SequenceEqual(new[] { "NativeMods/Uncatalogued.dll" }, external.Files);
	}

	public void UnknownExternalDllCannotBeAdoptedOrCreateOwnershipState()
	{
		using var fixture = new NativeFixture();
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		var dllPath = Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll");
		File.WriteAllBytes(dllPath, fixture.Pe("unreviewed wasd build"));

		var detected = fixture.Installer().GetInstalledMods().Single();
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.External, detected.Status);
		RegressionAssert.False(detected.CanAdopt);
		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().AdoptExternalAsync(
			781, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.ManifestPath));
		RegressionAssert.True(File.Exists(dllPath));
	}

	public void LeftoverExternalConfigurationIsNotReportedAsAnInstalledDllMod()
	{
		using var fixture = new NativeFixture();
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		File.WriteAllText(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml"), "[input]\nforward = \"W\"\n");

		RegressionAssert.Equal(0, fixture.Installer().GetInstalledMods().Count);
	}

	public void CommitRejectsArchiveAndDestinationChangesAfterReview()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.Pe("vanilla");
		fixture.WriteVanillaLoader(vanilla);
		fixture.CreateLoaderArchive(fixture.Pe("loader one"), vanilla);
		var archiveChanged = fixture.Installer().StageAsync(944, fixture.LoaderArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			fixture.CreateLoaderArchive(fixture.Pe("loader two"), vanilla);
			RegressionAssert.Throws<InvalidOperationException>(() => archiveChanged.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			archiveChanged.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}
		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));

		fixture.CreateLoaderArchive(fixture.Pe("loader one"), vanilla);
		var destinationChanged = fixture.Installer().StageAsync(944, fixture.LoaderArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			var replacement = fixture.Pe("replacement");
			File.WriteAllBytes(fixture.LoaderPath, replacement);
			RegressionAssert.Throws<InvalidOperationException>(() => destinationChanged.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
			RegressionAssert.SequenceEqual(replacement, File.ReadAllBytes(fixture.LoaderPath));
		}
		finally
		{
			destinationChanged.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}
	}

	public void PluginCommitRejectsRemovedLoaderAfterReview()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		var transaction = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			File.Delete(fixture.LoaderPath);
			RegressionAssert.Throws<InvalidOperationException>(() => transaction.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
	}

	public void PluginCommitRejectsChangedLoaderAfterReview()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		var transaction = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			File.WriteAllBytes(fixture.LoaderPath, fixture.Pe("replacement"));
			RegressionAssert.Throws<InvalidOperationException>(() => transaction.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
	}

	public void CommitRejectsStaleOwnershipManifestFromAnotherTransaction()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		fixture.CreateCameraArchive(fixture.Pe("camera"), "[camera]\nfov = 75\n");
		var wasd = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			781, fixture.WasdArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		var camera = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(
			945, fixture.CameraArchivePath, CancellationToken.None).GetAwaiter().GetResult();
		try
		{
			camera.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
			RegressionAssert.Throws<InvalidOperationException>(() => wasd.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			camera.DisposeAsync().AsTask().GetAwaiter().GetResult();
			wasd.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.True(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.dll")));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
	}

	public void CommitFailureRollsBackEveryWrittenTarget()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		var loader = File.ReadAllBytes(fixture.LoaderPath);
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		var transaction = fixture.Installer(NativeFixture.SupportedVersion).StageAsync(781, fixture.WasdArchivePath, CancellationToken.None)
			.GetAwaiter().GetResult();
		try
		{
			// Reads and the review fingerprint still succeed, but publishing ownership
			// fails after both plugin files have been copied.
			using var manifestLock = File.Open(fixture.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			RegressionAssert.Throws<IOException>(() => transaction.CommitAsync(CancellationToken.None)
				.GetAwaiter().GetResult());
		}
		finally
		{
			transaction.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		RegressionAssert.SequenceEqual(loader, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml")));
		RegressionAssert.True(fixture.Installer().DetectLoader().IsVerified);
	}

	public void PluginInstallPreservesExistingTomlConfiguration()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		const string userConfiguration = "[input]\nforward = \"Up\"\n";
		File.WriteAllText(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml"), userConfiguration);

		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);

		RegressionAssert.True(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.Equal(userConfiguration, File.ReadAllText(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml")));
	}

	public void UserConfigurationEditsNeverBlockNativePluginRestore()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);
		var configuration = Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml");
		const string userConfiguration = "[input]\nforward = \"Up\"\n";
		File.WriteAllText(configuration, userConfiguration);

		var entry = fixture.Installer(NativeFixture.SupportedVersion).GetInstalledMods()
			.Single(item => item.NexusModId == 781);
		RegressionAssert.Equal(ReduxGameDirectoryModStatus.Managed, entry.Status);
		fixture.Installer(NativeFixture.SupportedVersion).RestoreAsync(781, CancellationToken.None)
			.GetAwaiter().GetResult();
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.Equal(userConfiguration, File.ReadAllText(configuration));
	}

	public void RelatedProjectUpdateKeepsOneReduxOwnershipRecord()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		fixture.CreateCameraArchive(fixture.Pe("original camera"), "[camera]\nfov = 75\n");
		fixture.Install(945, fixture.CameraArchivePath, NativeFixture.SupportedVersion);
		var namedCamera = Path.Combine(Path.GetDirectoryName(fixture.CameraArchivePath)!,
			"NativeCameraTweaksGUI 22892 2.5.1 abcDEF12.zip");
		fixture.CreateArchive(namedCamera,
			("bin/NativeMods/BG3NativeCameraTweaks.dll", fixture.Pe("gui camera")),
			("bin/NativeMods/BG3NativeCameraTweaks.toml", Encoding.UTF8.GetBytes("[camera]\nfov = 90\n")));

		fixture.Install(22892, namedCamera, NativeFixture.SupportedVersion);

		var manifest = File.ReadAllText(fixture.ManifestPath);
		RegressionAssert.Contains(manifest, "\"NexusModId\":22892");
		RegressionAssert.False(manifest.Contains("\"NexusModId\":945", StringComparison.Ordinal));
		RegressionAssert.Contains(manifest, "\"PackageId\":\"native-camera-tweaks\"");
		RegressionAssert.Contains(Encoding.UTF8.GetString(
			File.ReadAllBytes(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.dll"))), "gui camera");
		RegressionAssert.Equal("[camera]\nfov = 75\n",
			File.ReadAllText(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.toml")));
	}

	public void MixedPackageStagesOnlyNativeFilesAndReportsItsCompanionPak()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		var archivePath = fixture.NewArchivePath("Best of Hands 23881 2.2.0 abcDEF12.zip");
		fixture.CreateArchive(archivePath,
			("bin/NativeMods/BestofHands.dll", fixture.Pe("best of hands")),
			("BestofHands.pak", Encoding.UTF8.GetBytes("companion pak")),
			("info.json", Encoding.UTF8.GetBytes("{}")));

		var inspection = ReduxGameDirectoryInstallService.TryInspectKnownArchive(archivePath);
		RegressionAssert.Equal(1, inspection!.PackageEntries.Count);
		fixture.Install(23881, archivePath, NativeFixture.SupportedVersion);
		RegressionAssert.True(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BestofHands.dll")));
		RegressionAssert.False(Directory.EnumerateFiles(fixture.GameBin, "*.pak", SearchOption.AllDirectories).Any());
	}

	public void TrueThirdPersonCameraRefusesLegacyCameraFiles()
	{
		using var fixture = new NativeFixture();
		fixture.InstallLoader();
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		File.WriteAllBytes(Path.Combine(fixture.NativeModsDirectory, "BG3NativeCameraTweaks.dll"), fixture.Pe("legacy camera"));
		var archivePath = fixture.NewArchivePath("True Third-Person Camera 23959 2.0 abcDEF12.zip");
		fixture.CreateArchive(archivePath,
			("1 - Main Game Folder Files/bin/NativeMods/TrueThirdPersonCamera.dll", fixture.Pe("true third person")),
			("2 - BG3 Mod Manager File/TrueThirdPersonCamera.pak", Encoding.UTF8.GetBytes("companion pak")),
			("README.txt", Encoding.UTF8.GetBytes("instructions")));

		var error = RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer(NativeFixture.SupportedVersion)
			.StageAsync(23959, archivePath, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.Contains(error.Message, "legacy BG3NativeCameraTweaks");
		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "TrueThirdPersonCamera.dll")));
	}

	public void RestoreRefusesActivePluginsThenRestoresOnlyOwnedFiles()
	{
		using var fixture = new NativeFixture();
		var vanilla = fixture.InstallLoader();
		fixture.CreateWasdArchive(fixture.Pe("wasd"), "[input]\nforward = \"W\"\n");
		fixture.Install(781, fixture.WasdArchivePath, NativeFixture.SupportedVersion);
		Directory.CreateDirectory(fixture.NativeModsDirectory);
		var unrelated = Path.Combine(fixture.NativeModsDirectory, "Unrelated.dll");
		File.WriteAllBytes(unrelated, fixture.Pe("unrelated"));

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().RestoreAsync(944, CancellationToken.None)
			.GetAwaiter().GetResult());
		fixture.Installer(NativeFixture.SupportedVersion).RestoreAsync(781, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.False(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.dll")));
		RegressionAssert.True(File.Exists(Path.Combine(fixture.NativeModsDirectory, "BG3WASD.toml")));
		RegressionAssert.True(File.Exists(unrelated));
		RegressionAssert.True(Directory.Exists(fixture.NativeModsDirectory));

		RegressionAssert.Throws<InvalidOperationException>(() => fixture.Installer().RestoreAsync(944, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.True(File.Exists(unrelated));
		File.Delete(unrelated);
		fixture.Installer().RestoreAsync(944, CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.SequenceEqual(vanilla, File.ReadAllBytes(fixture.LoaderPath));
		RegressionAssert.False(File.Exists(fixture.LoaderOriginalPath));
	}

	private sealed class NativeFixture : IDisposable
	{
		public static readonly Version SupportedVersion = new(4, 1, 1, 6931813);
		private readonly string _root = Path.Combine(Path.GetTempPath(), "ReduxNativeInstallerTests", Guid.NewGuid().ToString("N"));
		public string GameBin { get; }
		public string StateDirectory { get; }
		public string LoaderArchivePath { get; }
		public string WasdArchivePath { get; }
		public string CameraArchivePath { get; }
		public string OtherArchivePath { get; }
		public string ManifestPath => Path.Combine(Directory.GetDirectories(Path.Combine(StateDirectory, "native-mods")).Single(), "manifest.json");
		public string LoaderPath => Path.Combine(GameBin, "bink2w64.dll");
		public string LoaderOriginalPath => Path.Combine(GameBin, "bink2w64_original.dll");
		public string NativeModsDirectory => Path.Combine(GameBin, "NativeMods");
		public string NewArchivePath(string name) => Path.Combine(_root, name);

		public NativeFixture()
		{
			GameBin = Path.Combine(_root, "Game", "bin");
			StateDirectory = Path.Combine(_root, "State");
			LoaderArchivePath = Path.Combine(_root, "NativeModLoader.zip");
			WasdArchivePath = Path.Combine(_root, "WASD.zip");
			CameraArchivePath = Path.Combine(_root, "Camera.zip");
			OtherArchivePath = Path.Combine(_root, "Other.7z");
			Directory.CreateDirectory(GameBin);
			File.WriteAllBytes(Path.Combine(GameBin, "bg3.exe"), new byte[] { 1 });
		}

		public ReduxGameDirectoryInstallService Installer(Version? version = null) =>
			new(GameBin, StateDirectory, version!, false);

		public ReduxGameDirectoryInstallService StrictInstaller(Version? version = null) =>
			new(GameBin, StateDirectory, version!);

		public byte[] InstallLoader()
		{
			var vanilla = Pe("vanilla");
			WriteVanillaLoader(vanilla);
			CreateLoaderArchive(Pe("redux loader"), vanilla);
			Install(944, LoaderArchivePath, null);
			return vanilla;
		}

		public void Install(long projectId, string archivePath, Version? version)
		{
			var transaction = Installer(version).StageAsync(projectId, archivePath, CancellationToken.None)
				.GetAwaiter().GetResult();
			try { transaction.CommitAsync(CancellationToken.None).GetAwaiter().GetResult(); }
			finally { transaction.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
		}

		public void WriteVanillaLoader(byte[] vanilla) => File.WriteAllBytes(LoaderPath, vanilla);

		public void CreateLoaderArchive(byte[] loader, byte[] original) => CreateArchive(LoaderArchivePath,
			("bin/bink2w64.dll", loader),
			("bin/bink2w64_original.dll", original));

		public void CreateWasdArchive(byte[] dll, string toml) => CreateArchive(WasdArchivePath,
			("bin/NativeMods/BG3WASD.dll", dll),
			("bin/NativeMods/BG3WASD.toml", Encoding.UTF8.GetBytes(toml)));

		public void CreateCameraArchive(byte[] dll, string toml) => CreateArchive(CameraArchivePath,
			("bin/NativeMods/BG3NativeCameraTweaks.dll", dll),
			("bin/NativeMods/BG3NativeCameraTweaks.toml", Encoding.UTF8.GetBytes(toml)));

		public int StagingDirectoryCount() => Directory.GetDirectories(Path.Combine(
			Directory.GetDirectories(Path.Combine(StateDirectory, "native-mods")).Single(), "staging")).Length;

		public void CreateArchive(string path, params (string Name, byte[] Bytes)[] files)
		{
			if (File.Exists(path)) File.Delete(path);
			using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
			foreach (var file in files)
			{
				var entry = archive.CreateEntry(file.Name, CompressionLevel.Optimal);
				using var stream = entry.Open();
				stream.Write(file.Bytes, 0, file.Bytes.Length);
			}
		}

		public void CreateOversizedLoaderArchive(byte[] original)
		{
			if (File.Exists(LoaderArchivePath)) File.Delete(LoaderArchivePath);
			using var archive = ZipFile.Open(LoaderArchivePath, ZipArchiveMode.Create);
			var loader = archive.CreateEntry("bin/bink2w64.dll", CompressionLevel.Optimal);
			using (var stream = loader.Open())
			{
				var zeros = new byte[64 * 1024];
				for (var index = 0; index < 528; index++) stream.Write(zeros, 0, zeros.Length);
			}
			var originalEntry = archive.CreateEntry("bin/bink2w64_original.dll", CompressionLevel.Optimal);
			using var originalStream = originalEntry.Open();
			originalStream.Write(original, 0, original.Length);
		}

		public byte[] Pe(string marker)
		{
			var bytes = new byte[1024];
			bytes[0] = (byte)'M';
			bytes[1] = (byte)'Z';
			bytes[0x3c] = 0x80;
			bytes[0x80] = (byte)'P';
			bytes[0x81] = (byte)'E';
			bytes[0x84] = 0x64;
			bytes[0x85] = 0x86;
			bytes[0x94] = 0xf0;
			bytes[0x96] = 0x00;
			bytes[0x97] = 0x20;
			bytes[0x98] = 0x0b;
			bytes[0x99] = 0x02;
			Encoding.UTF8.GetBytes(marker).CopyTo(bytes, 0x200);
			return bytes;
		}

		public void Dispose()
		{
			if (Directory.Exists(_root)) Directory.Delete(_root, true);
		}
	}
}
