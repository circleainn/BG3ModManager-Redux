using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using DivinityModManager.Models.NexusMods;

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DivinityModManager.AppServices;

public enum ReduxGameDirectoryModKind
{
	NativeLoader,
	NativePlugin,
	ScriptExtender
}

public enum ReduxNativePluginDestination
{
	GameBin,
	YanmlPlugins
}

public static class ReduxAlternativeNativeLoader
{
	public static string PluginsDirectory(string localAppData) =>
		Path.Combine(localAppData, "Larian Studios", "Baldur's Gate 3", "Plugins");

	public static string CurrentPluginsDirectory =>
		PluginsDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

	public static bool HasYanmlConfiguration(string localAppData) =>
		File.Exists(Path.Combine(PluginsDirectory(localAppData), "config.toml"));

	public static bool IsPluginLoadingDisabled(string configurationPath)
	{
		var inCore = false;
		foreach (var rawLine in File.ReadLines(configurationPath))
		{
			var line = rawLine.Trim();
			if (line.StartsWith('['))
			{
				inCore = line.Equals("[core]", StringComparison.OrdinalIgnoreCase);
				continue;
			}
			if (!inCore || !line.StartsWith("enabled", StringComparison.OrdinalIgnoreCase)) continue;
			var separator = line.IndexOf('=');
			if (separator < 0 || !line[..separator].Trim().Equals("enabled", StringComparison.OrdinalIgnoreCase)) continue;
			var value = line[(separator + 1)..].Split('#', 2)[0].Trim();
			if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	public static string? ReadConfiguredInstallRoot(string configurationPath)
	{
		var inCore = false;
		foreach (var rawLine in File.ReadLines(configurationPath))
		{
			var line = rawLine.Trim();
			if (line.StartsWith('['))
			{
				inCore = line.Equals("[core]", StringComparison.OrdinalIgnoreCase);
				continue;
			}
			if (!inCore) continue;
			var separator = line.IndexOf('=');
			if (separator < 0 || !line[..separator].Trim().Equals("install_root", StringComparison.OrdinalIgnoreCase)) continue;
			var value = line[(separator + 1)..].Trim();
			if (value.StartsWith('\''))
			{
				var end = value.IndexOf('\'', 1);
				return end > 1 ? value[1..end] : null;
			}
			if (value.StartsWith('"'))
			{
				for (var index = 1; index < value.Length; index++)
				{
					if (value[index] != '"') continue;
					var backslashes = 0;
					for (var previous = index - 1; previous >= 0 && value[previous] == '\\'; previous--)
						backslashes++;
					if (backslashes % 2 != 0) continue;
					try { return JsonSerializer.Deserialize<string>(value[..(index + 1)]); }
					catch (JsonException) { return null; }
				}
			}
			return null;
		}
		return null;
	}

	public static string MissingLoaderMessage(string modName, ReduxNativeLoaderStatus loaderStatus, string localAppData)
	{
		if (loaderStatus.Description.Contains("YANML", StringComparison.OrdinalIgnoreCase))
			return $"{modName} cannot load through YANML. {loaderStatus.Description}";
		if (!HasYanmlConfiguration(localAppData))
			return $"{modName} requires Native Mod Loader. {loaderStatus.Description}";
		return $"{modName} cannot be installed by this game-directory workflow while Native Mod Loader is missing. "
			+ $"A Yet Another Native Mod Loader configuration was found at {PluginsDirectory(localAppData)}. "
			+ "YANML loads plugins from that Plugins folder, while this workflow targets BG3\\bin\\NativeMods. "
			+ "Use Install for YANML in the manager to review and manage this native plugin at the correct destination.";
	}
}

public sealed record ReduxGameDirectoryModLayout(
	string Name,
	IReadOnlyDictionary<string, string> ManagedEntries,
	IReadOnlyList<string> PackageEntries,
	IReadOnlyList<string> IgnoredEntries);

public sealed record ReduxGameDirectoryBinaryFingerprint(
	string RelativePath,
	long Length,
	string Sha256,
	string Version);

public sealed record ReduxGameDirectoryBinaryMatch(
	ReduxGameDirectoryModDefinition Definition,
	ReduxGameDirectoryBinaryFingerprint Fingerprint);

public sealed record ReduxGameDirectoryModDefinition(
	long NexusModId,
	string PackageId,
	string Name,
	ReduxGameDirectoryModKind Kind,
	bool RequiresLoader,
	IReadOnlyList<string> RelativeFiles,
	IReadOnlySet<string> PreserveExistingFiles,
	IReadOnlyList<ReduxGameDirectoryModLayout> Layouts,
	string SourceUrl,
	string Requirements)
{
	public bool SupportsGuardedInstall => true;
	public bool ReplacesExistingGameFiles { get; init; }
	public IReadOnlyDictionary<string, string> ReplacementOriginals { get; init; }
		= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyList<ReduxGameDirectoryBinaryFingerprint> BinaryFingerprints { get; init; }
		= Array.Empty<ReduxGameDirectoryBinaryFingerprint>();
}

public static class ReduxGameDirectoryModCatalog
{
	private static IReadOnlyDictionary<string, string> Map(params (string Archive, string Canonical)[] entries) =>
		new Dictionary<string, string>(entries.ToDictionary(item => item.Archive, item => item.Canonical), StringComparer.OrdinalIgnoreCase);
	private static IReadOnlyList<string> Files(params string[] entries) => Array.AsReadOnly(entries);
	private static IReadOnlySet<string> Preserve(params string[] entries) => new HashSet<string>(entries, StringComparer.OrdinalIgnoreCase);
	private static IReadOnlyList<ReduxGameDirectoryBinaryFingerprint> Fingerprints(
		params ReduxGameDirectoryBinaryFingerprint[] entries) => Array.AsReadOnly(entries);
	private static ReduxGameDirectoryBinaryFingerprint Fingerprint(string path, long length, string sha256, string version) =>
		new(path, length, sha256.ToLowerInvariant(), version);
	private static ReduxGameDirectoryModLayout Layout(string name, IReadOnlyDictionary<string, string> managed,
		IReadOnlyList<string>? packages = null, IReadOnlyList<string>? ignored = null) =>
		new(name, managed, packages ?? Array.Empty<string>(), ignored ?? Array.Empty<string>());

	public static IReadOnlyList<ReduxGameDirectoryModDefinition> All { get; } = Array.AsReadOnly(new[]
	{
		new ReduxGameDirectoryModDefinition(944, "native-mod-loader", "Native Mod Loader", ReduxGameDirectoryModKind.NativeLoader, false,
			Files("bin/bink2w64.dll", "bin/bink2w64_original.dll"), Preserve(),
			[Layout("Standard", Map(("bin/bink2w64.dll", "bin/bink2w64.dll"), ("bin/bink2w64_original.dll", "bin/bink2w64_original.dll")))],
			"https://www.nexusmods.com/baldursgate3/mods/944", "Replaces the game's bink2w64 loader and retains the packaged original.")
		{
			ReplacesExistingGameFiles = true,
			ReplacementOriginals = Map(("bin/bink2w64.dll", "bin/bink2w64_original.dll")),
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/bink2w64.dll", 232448, "6c2932d54e56dcb1a6e9e0d9cd13c2dbacb9bdf9d4175b0bcbb0e0cab0fc20fc", "1.0"),
				Fingerprint("bin/bink2w64_original.dll", 411136, "7c3ac825eb7fe769c3831540e108282dd90f6275b0f61dec8beeea0a6615b0c1", ""))
		},
		new ReduxGameDirectoryModDefinition(781, "bg3-wasd", "WASD Character Movement", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml"), Preserve("bin/NativeMods/BG3WASD.toml"),
			[Layout("Standard", Map(("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.dll"), ("bin/NativeMods/BG3WASD.toml", "bin/NativeMods/BG3WASD.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/781", "Requires Native Mod Loader.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BG3WASD.dll", 1080832, "b75aa02bbda0186287aca3946efc03b78b1c05bec7a152aebb809071b52c6e8c", "1.9.7"),
				Fingerprint("bin/NativeMods/BG3WASD.dll", 1080832, "05de4bfef58f13c142717e8b73b58885d9b8b259648d77f1ee1d3caf90601196", "1.9.8"),
				Fingerprint("bin/NativeMods/BG3WASD.dll", 1074688, "5d7106834dcd0938edf367324f5688f9c518a8faaae056cde77c9ced52616d74", "1.9.9"))
		},
		new ReduxGameDirectoryModDefinition(945, "native-camera-tweaks", "Native Camera Tweaks", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml"), Preserve("bin/NativeMods/BG3NativeCameraTweaks.toml"),
			[Layout("Standard", Map(("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.dll"), ("bin/NativeMods/BG3NativeCameraTweaks.toml", "bin/NativeMods/BG3NativeCameraTweaks.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/945", "Requires Native Mod Loader.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 782336, "3953996c3a08e63c7a56580463240d19cdcd4e4368b336d963a53c890fc904cb", "2.4.2"),
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 782336, "a866fb3bc1c9ffedfb038ae74d9dd8f1f359db7a77047d4b35a790252bfbe5a5", "2.4.3"),
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 782336, "bf9d45ecb5390a6b6bfa447911b4dcad834c40a81b54d640af67f39abe35ba09", "2.4.4"),
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 774656, "7d183b30892c69978534af5875fbf2b7ae510a2fd3408387b50a21f3612491ca", "2.4.5"))
		},
		new ReduxGameDirectoryModDefinition(22892, "native-camera-tweaks", "Native Camera Tweaks with GUI", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.toml"), Preserve("bin/NativeMods/BG3NativeCameraTweaks.toml"),
			[Layout("Standard", Map(("bin/NativeMods/BG3NativeCameraTweaks.dll", "bin/NativeMods/BG3NativeCameraTweaks.dll"), ("bin/NativeMods/BG3NativeCameraTweaks.toml", "bin/NativeMods/BG3NativeCameraTweaks.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/22892", "Requires Native Mod Loader; supersedes the earlier Native Camera Tweaks project.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 1387008, "6d033888ba0e3b6e5fe36196c2568d39b86384397d14f0d0720c551195058776", "2.5.0"),
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 1360384, "6b12004f021238878dca17b8700abb506a1b0e9054a8121d66f9b7555427f7ff", "2.5.1"),
				Fingerprint("bin/NativeMods/BG3NativeCameraTweaks.dll", 1361408, "e254d1195b45b7c94add56b3a16fc823e2d7589d7b6e3d8b6ad45fcece266543", "2.5.1"))
		},
		new ReduxGameDirectoryModDefinition(668, "achievement-enabler", "Achievement Enabler", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3AchievementEnabler.dll"), Preserve(),
			[
				Layout("Game root", Map(("bin/NativeMods/BG3AchievementEnabler.dll", "bin/NativeMods/BG3AchievementEnabler.dll"))),
				Layout("Bin contents", Map(("NativeMods/BG3AchievementEnabler.dll", "bin/NativeMods/BG3AchievementEnabler.dll")))
			],
			"https://www.nexusmods.com/baldursgate3/mods/668", "Requires Native Mod Loader.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BG3AchievementEnabler.dll", 376832, "6d473b79f535e76eee37ad3a9bdaeb08a398488964acfac504a82868b3c710e0", "RC2"),
				Fingerprint("bin/NativeMods/BG3AchievementEnabler.dll", 462848, "a423345bf1084edaa9d6a1b567cd9bfab0a04ef875c415ea48f1d607f7d1163b", "1.2"),
				Fingerprint("bin/NativeMods/BG3AchievementEnabler.dll", 469504, "15568bd0e0c1aea0b3d73e0f89944fabdd043a6ec447b7e210852718f9506aa8", "1.3"))
		},
		new ReduxGameDirectoryModDefinition(1326, "baldurs-priority", "Baldur's Priority", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/CpuOptimizer.dll", "bin/NativeMods/CpuOptimizer.ini"), Preserve("bin/NativeMods/CpuOptimizer.ini"),
			[Layout("Standard", Map(("bin/NativeMods/CpuOptimizer.dll", "bin/NativeMods/CpuOptimizer.dll"), ("bin/NativeMods/CpuOptimizer.ini", "bin/NativeMods/CpuOptimizer.ini")))],
			"https://www.nexusmods.com/baldursgate3/mods/1326", "Requires Native Mod Loader.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/CpuOptimizer.dll", 234496, "d239ba0e32691413b57192aaf76a0bd4bc8b281f223066681bd76c2d32aaace3", "1.0.0"))
		},
		new ReduxGameDirectoryModDefinition(742, "improved-camera", "Improved Camera", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BGIII_ImprovedCamera.dll", "bin/NativeMods/BGIII_ImprovedCamera.toml"), Preserve("bin/NativeMods/BGIII_ImprovedCamera.toml"),
			[Layout("Legacy wrapper", Map(("BGIII_ImprovedCamera - DLL/BGIII_ImprovedCamera.dll", "bin/NativeMods/BGIII_ImprovedCamera.dll"), ("BGIII_ImprovedCamera - DLL/BGIII_ImprovedCamera.toml", "bin/NativeMods/BGIII_ImprovedCamera.toml")))],
			"https://www.nexusmods.com/baldursgate3/mods/742", "Requires Native Mod Loader.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BGIII_ImprovedCamera.dll", 278528, "66b1c2b99cd6b91d3f18e4a948a00dab674b1c557e35c03ed39cc1dc2008419a", "2.0"))
		},
		new ReduxGameDirectoryModDefinition(23881, "best-of-hands", "Best of Hands", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BestofHands.dll"), Preserve(),
			[Layout("Native and PAK", Map(("bin/NativeMods/BestofHands.dll", "bin/NativeMods/BestofHands.dll")), Files("BestofHands.pak"), Files("info.json"))],
			"https://www.nexusmods.com/baldursgate3/mods/23881", "Requires Native Mod Loader; its PAK must be reviewed by Redux's normal package installer.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BestofHands.dll", 900608, "bd5f14955e61e3b032d8433550d5424df3f43d389125eea98f3a65503b454b0f", "2.2.0"))
		},
		new ReduxGameDirectoryModDefinition(23413, "bg3-wasd", "BG3WASD Camera Follow", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.toml"), Preserve("bin/NativeMods/BG3WASD.toml"),
			[Layout("Native and PAK", Map(("bin/NativeMods/BG3WASD.dll", "bin/NativeMods/BG3WASD.dll"), ("bin/NativeMods/BG3WASD.toml", "bin/NativeMods/BG3WASD.toml")), Files("Mods/BG3YawBridge.pak"), Files("CREDITS.txt", "LICENSE.txt", "README.txt"))],
			"https://www.nexusmods.com/baldursgate3/mods/23413", "Requires Native Mod Loader; its PAK must be reviewed by Redux's normal package installer.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/BG3WASD.dll", 1110016, "6b62ac4b2859c64ee73977ae451f515ea19eef7d2c66bb25e58b2cc90e406e66", "1.2"))
		},
		new ReduxGameDirectoryModDefinition(23959, "true-third-person-camera", "True Third-Person Camera", ReduxGameDirectoryModKind.NativePlugin, true,
			Files("bin/NativeMods/TrueThirdPersonCamera.dll"), Preserve(),
			[Layout("Guided wrapper", Map(("1 - Main Game Folder Files/bin/NativeMods/TrueThirdPersonCamera.dll", "bin/NativeMods/TrueThirdPersonCamera.dll")), Files("2 - BG3 Mod Manager File/TrueThirdPersonCamera.pak"), Files("README.txt"))],
			"https://www.nexusmods.com/baldursgate3/mods/23959", "Requires Native Mod Loader and removal of legacy camera files before first installation; its PAK must use Redux's normal package installer.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/TrueThirdPersonCamera.dll", 640512, "2baa24e55f87e395ccfd33068b7d5207b34fd97327273af0fbfe05d208e02249", "2.0"))
		},
		new ReduxGameDirectoryModDefinition(24804, "bg3fgvk", "bg3fgvk", ReduxGameDirectoryModKind.NativePlugin, true,
			Files(
				"bin/NativeMods/fgvk.dll",
				"bin/NativeMods/Streamline/NvLowLatencyVk.dll",
				"bin/NativeMods/Streamline/nvngx_dlssg.dll",
				"bin/NativeMods/Streamline/README-STREAMLINE.txt",
				"bin/NativeMods/Streamline/sl.common.dll",
				"bin/NativeMods/Streamline/sl.dlss_g.dll",
				"bin/NativeMods/Streamline/sl.interposer.dll",
				"bin/NativeMods/Streamline/sl.pcl.dll",
				"bin/NativeMods/Streamline/sl.reflex.dll",
				"bin/NativeMods/Streamline/STREAMLINE-LICENSE.txt"), Preserve(),
			[Layout("Bin contents", Map(
				("NativeMods/fgvk.dll", "bin/NativeMods/fgvk.dll"),
				("NativeMods/Streamline/NvLowLatencyVk.dll", "bin/NativeMods/Streamline/NvLowLatencyVk.dll"),
				("NativeMods/Streamline/nvngx_dlssg.dll", "bin/NativeMods/Streamline/nvngx_dlssg.dll"),
				("NativeMods/Streamline/README-STREAMLINE.txt", "bin/NativeMods/Streamline/README-STREAMLINE.txt"),
				("NativeMods/Streamline/sl.common.dll", "bin/NativeMods/Streamline/sl.common.dll"),
				("NativeMods/Streamline/sl.dlss_g.dll", "bin/NativeMods/Streamline/sl.dlss_g.dll"),
				("NativeMods/Streamline/sl.interposer.dll", "bin/NativeMods/Streamline/sl.interposer.dll"),
				("NativeMods/Streamline/sl.pcl.dll", "bin/NativeMods/Streamline/sl.pcl.dll"),
				("NativeMods/Streamline/sl.reflex.dll", "bin/NativeMods/Streamline/sl.reflex.dll"),
				("NativeMods/Streamline/STREAMLINE-LICENSE.txt", "bin/NativeMods/Streamline/STREAMLINE-LICENSE.txt")),
				ignored: Files("INSTALL.txt", "LICENSE.txt", "README.md"))],
			"https://www.nexusmods.com/baldursgate3/mods/24804", "Requires Native Mod Loader, Vulkan, supported NVIDIA hardware, and hardware-accelerated GPU scheduling.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/NativeMods/fgvk.dll", 76800, "30eaa46d415eca4f1fd2b207ec42eaffc178a9b1bea8ba376b946135fa03cc42", "0.1.0"),
				Fingerprint("bin/NativeMods/fgvk.dll", 78336, "bc5dfafb1a263bb7ddb69368f6002933055aefda18477b2f7016d041578c2399", "1.0"),
				Fingerprint("bin/NativeMods/Streamline/NvLowLatencyVk.dll", 57840, "2a77dc3e1c724b7eea5755be0ae7423752e79a2459fae72181a9f00e3507e5d6", ""),
				Fingerprint("bin/NativeMods/Streamline/nvngx_dlssg.dll", 7519856, "135eaf0733c1e37381a8c28abcf7a862404a54132b81787c04e35d09efc5e36f", ""),
				Fingerprint("bin/NativeMods/Streamline/sl.common.dll", 830080, "c57930ef5a8a3fe9be85efdf71a61d8107c1148e8a6aed456464547128f7f4ae", ""),
				Fingerprint("bin/NativeMods/Streamline/sl.dlss_g.dll", 612992, "1fec3f8fdfc59d78c4445c276c1a0fb798bf251985f348597dc2b44d0c995e52", ""),
				Fingerprint("bin/NativeMods/Streamline/sl.interposer.dll", 647808, "2a79db6857ae8c75bbd871a9489c48bc6a39f7fcc88b9b02afd53d0376cbec66", ""),
				Fingerprint("bin/NativeMods/Streamline/sl.pcl.dll", 359552, "699ab461e64e95189a7fe6a21c79ad237cf56b60ea748cb6c840cd5431ba91d1", ""),
				Fingerprint("bin/NativeMods/Streamline/sl.reflex.dll", 382080, "7e6e4ccc4b561bd449fb0da90709d9b96b08c3f6f4697362caaa359e72a58a67", ""))
		},
		new ReduxGameDirectoryModDefinition(2172, "script-extender", "Baldur's Gate 3 Script Extender", ReduxGameDirectoryModKind.ScriptExtender, false,
			Files("bin/DWrite.dll"), Preserve(),
			[Layout("Bin contents", Map(("DWrite.dll", "bin/DWrite.dll")))],
			"https://www.nexusmods.com/baldursgate3/mods/2172", "Installs DWrite.dll through Redux's guarded game-directory workflow.")
		{
			BinaryFingerprints = Fingerprints(
				Fingerprint("bin/DWrite.dll", 5837824, "25151fb060cdad69fc322bb338edbf822d77291ef75661958a72df186d1a2fcb", "31"),
				Fingerprint("bin/DWrite.dll", 5988352, "8f3c0782461cc280cab4adfc270979549211f6cac91ad851baa2b2716118ecb0", "32"),
				Fingerprint("bin/DWrite.dll", 5987840, "3d2496c8e2e88accc53ef50f5cca7967b437409c4e11b744861cc3ae2816c777", "32 hotfix 1"))
		}
	});

	public static ReduxGameDirectoryModDefinition? Find(long id) => All.FirstOrDefault(definition => definition.NexusModId == id);
	public static IReadOnlyList<ReduxGameDirectoryModDefinition> FindByPackageId(string packageId) => All
		.Where(definition => definition.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))
		.ToArray();

	public static ReduxGameDirectoryBinaryMatch? FindByBinaryFingerprint(string relativePath, long length, string sha256)
	{
		if (String.IsNullOrWhiteSpace(relativePath) || length <= 0 || String.IsNullOrWhiteSpace(sha256)) return null;
		var normalizedPath = relativePath.Replace('\\', '/');
		var matches = All.SelectMany(definition => definition.BinaryFingerprints
			.Where(fingerprint => fingerprint.Length == length
				&& fingerprint.RelativePath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)
				&& fingerprint.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase))
			.Select(fingerprint => new ReduxGameDirectoryBinaryMatch(definition, fingerprint)))
			.Take(2).ToArray();
		return matches.Length == 1 ? matches[0] : null;
	}
}

public sealed record ReduxNativeLoaderStatus(bool IsPresent, bool IsVerified, string Description,
	bool IsAlternativeLoader = false);

public sealed record ReduxGameDirectoryArchiveFile(
	string ArchivePath,
	string DestinationPath,
	bool PreserveExisting);

public sealed record ReduxGameDirectoryArchiveInspection(
	ReduxGameDirectoryModDefinition Definition,
	string LayoutName,
	string ArchivePath,
	string ArchiveHash,
	int FileCount,
	long ExpandedBytes,
	IReadOnlyList<ReduxGameDirectoryArchiveFile> ManagedFiles,
	IReadOnlyList<string> PackageEntries,
	IReadOnlyList<string> IgnoredEntries);

public enum ReduxGameDirectoryModStatus
{
	Managed,
	Changed,
	Missing,
	External,
	RecoveryRequired
}

public sealed record ReduxGameDirectoryModEntry(
	string PackageId,
	long NexusModId,
	string Name,
	string SourceUrl,
	string ArchiveName,
	ReduxGameDirectoryModStatus Status,
	string StatusText,
	IReadOnlyList<string> Files,
	bool CanRestore,
	string DetectedVersion = "",
	bool CanAdopt = false);

public sealed class ReduxGameDirectoryRecoveryException(string message, Exception? innerException = null) : IOException(message, innerException) { }
public sealed class ReduxUnsupportedGameDirectoryArchiveException(string message) : IOException(message) { }

/// <summary>
/// Installs only the explicitly reviewed native-mod archives. Archive structure and PE headers are
/// validated locally; this is not a provenance or universal game-compatibility assertion.
/// </summary>
public sealed class ReduxGameDirectoryInstallService
{
	private const int ManifestVersion = 2;
	private const long MaximumArchiveBytes = 128L * 1024 * 1024;
	private const long MaximumEntryBytes = 32L * 1024 * 1024;
	private const long MaximumExpandedBytes = 64L * 1024 * 1024;
	private const long MaximumManifestBytes = 256 * 1024;
	private const int MaximumArchiveEntries = 128;
	private const int MaximumCompressionRatio = 200;
	private const long CompressionRatioMinimumBytes = 1024 * 1024;
	private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".7z", ".7zip", ".rar", ".zip"
	};
	private static readonly Version MinimumPluginGameVersion = new(4, 1, 1, 6931813);
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		WriteIndented = false
	};

	private readonly string _gameBin;
	private readonly string? _yanmlPluginsDirectory;
	private readonly string _stateRoot;
	private readonly string _manifestPath;
	private readonly string _journalDirectory;
	private readonly string _backupDirectory;
	private readonly string _stagingDirectory;
	private readonly string _stateLockPath;
	private readonly string _gameBinIdentity;
	private readonly Version? _gameVersion;
	private readonly bool _enforceReviewedReplacementOriginals;
	private readonly SemaphoreSlim _operationGate = new(1, 1);
	public string GameBin => _gameBin;
	public ReduxNativePluginDestination NativePluginDestination { get; }
	public string NativePluginDirectory => _yanmlPluginsDirectory ?? Path.Combine(_gameBin, "NativeMods");
	public string GetManagedFilePath(string canonicalRelativePath) =>
		ResolveTargetPath(ToTargetRelative(canonicalRelativePath), createParent: false);
	public string RecoveryDirectory => _stateRoot;
	public static bool HasYanmlOwnershipRecord(string gameBin, string stateDirectory)
	{
		var identity = HashText(Path.GetFullPath(gameBin).ToUpperInvariant() + "|YANML");
		return File.Exists(Path.Combine(stateDirectory, "native-mods", identity, "manifest.json"));
	}

	public ReduxGameDirectoryInstallService(string gameBin, string stateDirectory, Version gameVersion)
		: this(gameBin, stateDirectory, gameVersion, true, ReduxNativePluginDestination.GameBin) { }

	public ReduxGameDirectoryInstallService(string gameBin, string stateDirectory, Version gameVersion,
		ReduxNativePluginDestination nativePluginDestination)
		: this(gameBin, stateDirectory, gameVersion, true, nativePluginDestination) { }

	internal ReduxGameDirectoryInstallService(string gameBin, string stateDirectory, Version gameVersion,
		bool enforceReviewedReplacementOriginals)
		: this(gameBin, stateDirectory, gameVersion, enforceReviewedReplacementOriginals, ReduxNativePluginDestination.GameBin) { }

	internal ReduxGameDirectoryInstallService(string gameBin, string stateDirectory, Version gameVersion,
		bool enforceReviewedReplacementOriginals, ReduxNativePluginDestination nativePluginDestination,
		string? yanmlLocalAppData = null)
	{
		_gameBin = NormalizeExistingDirectory(gameBin, nameof(gameBin));
		NativePluginDestination = nativePluginDestination;
		if (nativePluginDestination == ReduxNativePluginDestination.YanmlPlugins)
		{
			var localAppData = yanmlLocalAppData
				?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			_yanmlPluginsDirectory = ReduxAlternativeNativeLoader.PluginsDirectory(localAppData);
			if (Directory.Exists(_yanmlPluginsDirectory))
				EnsureSafeDirectoryTree(_yanmlPluginsDirectory, create: false);
			var configuration = Path.Combine(_yanmlPluginsDirectory, "config.toml");
			if (File.Exists(configuration)) EnsureSafeRegularFile(configuration);
		}
		ValidateGameBin();
		_gameVersion = gameVersion;
		_enforceReviewedReplacementOriginals = enforceReviewedReplacementOriginals;
		_gameBinIdentity = HashText(_gameBin.ToUpperInvariant()
			+ (nativePluginDestination == ReduxNativePluginDestination.YanmlPlugins ? "|YANML" : String.Empty));

		var root = NormalizeOrCreateDirectory(stateDirectory, nameof(stateDirectory));
		_stateRoot = Path.Combine(root, "native-mods", _gameBinIdentity);
		EnsureSafeDirectoryTree(_stateRoot, create: true);
		_manifestPath = Path.Combine(_stateRoot, "manifest.json");
		_journalDirectory = Path.Combine(_stateRoot, "journals");
		_backupDirectory = Path.Combine(_stateRoot, "backups");
		_stagingDirectory = Path.Combine(_stateRoot, "staging");
		_stateLockPath = Path.Combine(_stateRoot, "operation.lock");
		EnsureSafeDirectoryTree(_journalDirectory, create: true);
		EnsureSafeDirectoryTree(_backupDirectory, create: true);
		EnsureSafeDirectoryTree(_stagingDirectory, create: true);
	}

	private void EnsureDestinationSupports(ReduxGameDirectoryModDefinition definition)
	{
		if (NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
			&& definition.Kind != ReduxGameDirectoryModKind.NativePlugin)
			throw new InvalidOperationException("YANML's Plugins folder accepts native plugins only. Install loaders and Script Extender through their own workflows.");
	}

	public ReduxNativeLoaderStatus DetectLoader()
	{
		try
		{
			ValidateGameBin();
			return DetectLoader(ReadManifest());
		}
		catch (Exception ex) when (IsValidationException(ex))
		{
			return new ReduxNativeLoaderStatus(false, false,
				"Native Mod Loader status is unavailable because Redux ownership records or game files could not be safely verified.");
		}
	}

	public IReadOnlyList<ReduxGameDirectoryModEntry> GetInstalledMods()
	{
		ValidateGameBin();
		var manifest = ReadManifest();
		var results = new List<ReduxGameDirectoryModEntry>();
		if (manifest != null)
		{
			foreach (var installation in manifest.Installations)
			{
				var definition = ReduxGameDirectoryModCatalog.Find(installation.NexusModId)!;
				var snapshots = installation.Files.Select(file => (File: file, Current: CaptureDestination(file.RelativePath))).ToArray();
				var missing = snapshots.Any(item => !item.Current.Exists);
				var changed = !missing && snapshots.Any(item =>
					!String.Equals(item.File.InstalledHash, item.Current.Hash, StringComparison.Ordinal));
				var status = missing ? ReduxGameDirectoryModStatus.Missing
					: changed ? ReduxGameDirectoryModStatus.Changed : ReduxGameDirectoryModStatus.Managed;
				var statusText = missing ? "Managed files are missing"
					: changed ? "Changed outside Redux" : "Managed by Redux";
				results.Add(new ReduxGameDirectoryModEntry(
					installation.PackageId, installation.NexusModId, installation.Name, installation.SourceUrl,
					installation.ArchiveName,
					status, statusText, installation.Files.Select(file => file.RelativePath).ToArray(),
					status == ReduxGameDirectoryModStatus.Managed, installation.DetectedVersion));
			}
		}

		var managedPackages = results.Select(result => result.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var package in ReduxGameDirectoryModCatalog.All.Where(definition => definition.SupportsGuardedInstall
			&& (NativePluginDestination == ReduxNativePluginDestination.GameBin
				|| definition.Kind == ReduxGameDirectoryModKind.NativePlugin))
			.GroupBy(definition => definition.PackageId, StringComparer.OrdinalIgnoreCase))
		{
			if (managedPackages.Contains(package.Key)) continue;
			var existingSnapshots = package.SelectMany(definition => definition.RelativeFiles)
				.Select(ToTargetRelative).Distinct(StringComparer.Ordinal)
				.Select(CaptureDestination).Where(snapshot => snapshot.Exists).ToArray();
			if (!existingSnapshots.Any(snapshot => snapshot.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
				continue;

			var definitions = package.ToArray();
			if (definitions.Any(definition => definition.Kind == ReduxGameDirectoryModKind.NativeLoader)
				&& !HasExternalLoaderEvidence(CaptureDestination("bink2w64.dll"), CaptureDestination("bink2w64_original.dll")))
				continue;

			var exactMatches = existingSnapshots
				.Where(snapshot => snapshot.Hash != null)
				.Select(snapshot => ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
					$"bin/{snapshot.RelativePath}", snapshot.Length, snapshot.Hash!))
				.Where(match => match != null && definitions.Any(definition => definition.NexusModId == match.Definition.NexusModId))
				.Cast<ReduxGameDirectoryBinaryMatch>()
				.GroupBy(match => match.Definition.NexusModId)
				.Select(group => group.First()).ToArray();
			var exactMatch = exactMatches.Length == 1 ? exactMatches[0] : null;
			var representative = exactMatch?.Definition ?? (definitions.Length == 1 ? definitions[0] : null);
			var isUnverifiedVariant = exactMatch == null;
			var externalName = representative?.Name ?? package.Key switch
			{
				"native-camera-tweaks" => "Native Camera Tweaks (unverified variant)",
				"bg3-wasd" => "BG3WASD (unverified variant)",
				_ => "Unverified game-directory mod variant"
			};
			if (isUnverifiedVariant && representative?.Kind == ReduxGameDirectoryModKind.ScriptExtender)
				externalName = "Baldur's Gate 3 Script Extender (unverified variant)";
			var detectedVersion = exactMatch?.Fingerprint.Version ?? String.Empty;
			var statusText = isUnverifiedVariant ? "Not managed by this Redux installation · unverified variant"
				: String.IsNullOrWhiteSpace(detectedVersion) ? "Not managed by this Redux installation"
				: $"Not managed by this Redux installation · identified v{detectedVersion}";
			if (representative?.ReplacesExistingGameFiles == true)
				statusText = "Can't manage · this installation has no protected original backup";
			var canAdopt = (exactMatch?.Definition.Kind is ReduxGameDirectoryModKind.NativePlugin
				or ReduxGameDirectoryModKind.ScriptExtender)
				&& !exactMatch.Definition.ReplacesExistingGameFiles
				&& FindExactReviewedDllSetIdentity(exactMatch.Definition, existingSnapshots) != null;
			results.Add(new ReduxGameDirectoryModEntry(
				package.Key, representative?.NexusModId ?? -1,
				externalName,
				representative?.SourceUrl ?? String.Empty, String.Empty, ReduxGameDirectoryModStatus.External, statusText,
				existingSnapshots.Select(snapshot => snapshot.RelativePath).ToArray(), false, detectedVersion, canAdopt));
		}

		var nativeModsDirectory = NativePluginDirectory;
		if (Directory.Exists(nativeModsDirectory))
		{
			EnsureSafeDirectoryContents(nativeModsDirectory);
			var catalogPaths = ReduxGameDirectoryModCatalog.All.SelectMany(definition => definition.RelativeFiles)
				.Select(ToTargetRelative).ToHashSet(StringComparer.Ordinal);
			var otherDlls = Directory.EnumerateFiles(nativeModsDirectory, "*.dll", SearchOption.AllDirectories)
				.Select(path => "NativeMods/" + Path.GetRelativePath(nativeModsDirectory, path).Replace('\\', '/'))
				.Where(path => !catalogPaths.Contains(path))
				.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
				.Take(65).ToArray();
			if (otherDlls.Length > 0)
			{
				var displayed = otherDlls.Take(64).ToArray();
				results.Add(new ReduxGameDirectoryModEntry(
					"external-native-files", -1, "Other native files", String.Empty, String.Empty,
					ReduxGameDirectoryModStatus.External,
					otherDlls.Length > 64 ? "Not managed by this Redux installation · more than 64 DLLs" : "Not managed by this Redux installation",
					displayed, false));
			}
		}

		if (Directory.EnumerateFiles(_journalDirectory, "*.json", SearchOption.TopDirectoryOnly).Any())
		{
			results.Insert(0, new ReduxGameDirectoryModEntry(
				"redux-recovery", -1, "Incomplete game-directory change", String.Empty, String.Empty,
				ReduxGameDirectoryModStatus.RecoveryRequired,
				"Recovery review required before the game is launched", Array.Empty<string>(), false));
		}

		return results.OrderBy(entry => entry.Status == ReduxGameDirectoryModStatus.RecoveryRequired ? 0 : 1)
			.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
	}

	/// <summary>
	/// Records ownership of an already-installed reviewed add-only native component without changing game files.
	/// Only exact catalog fingerprints can be adopted; settings and companion content remain user-owned.
	/// </summary>
	public async Task AdoptExternalAsync(long projectId, CancellationToken cancellationToken = default)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This game-directory mod is not in Redux's reviewed catalog.");
		EnsureDestinationSupports(definition);
		if (definition.Kind is not (ReduxGameDirectoryModKind.NativePlugin or ReduxGameDirectoryModKind.ScriptExtender))
			throw new InvalidOperationException("Only reviewed add-only native components can be adopted by the game-directory manager.");
		if (definition.ReplacesExistingGameFiles)
			throw new InvalidOperationException($"Redux cannot adopt {definition.Name} because it did not preserve the original files before they were replaced.");

		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			cancellationToken.ThrowIfCancellationRequested();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			var manifest = ReadManifest();
			if (manifest != null && FindInstallation(manifest, definition.PackageId) != null)
				throw new InvalidOperationException($"{definition.Name} is already managed by Redux.");

			var dllPaths = definition.RelativeFiles
				.Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				.Select(ToTargetRelative).Distinct(StringComparer.Ordinal).ToArray();
			if (dllPaths.Length == 0)
				throw new InvalidDataException("This catalog entry has no native DLL to adopt.");
			var snapshots = dllPaths.Select(CaptureDestination).ToArray();
			if (snapshots.Any(snapshot => !snapshot.Exists || snapshot.Hash == null))
				throw new InvalidOperationException($"Redux cannot adopt {definition.Name} because its reviewed DLL set is incomplete.");

			var identity = FindExactReviewedDllSetIdentity(definition, snapshots);
			if (identity == null)
				throw new InvalidOperationException($"Redux cannot adopt {definition.Name} because the installed DLL does not exactly match a reviewed version.");
			foreach (var snapshot in snapshots)
				ValidateAmd64PeDll(ResolveTargetPath(snapshot.RelativePath, createParent: false));

			var installation = new NativeOwnedInstallation
			{
				PackageId = definition.PackageId,
				NexusModId = definition.NexusModId,
				Name = definition.Name,
				SourceUrl = definition.SourceUrl,
				ArchiveName = "Adopted external installation",
				ArchiveHash = HashText(String.Join("\n", snapshots.OrderBy(snapshot => snapshot.RelativePath, StringComparer.Ordinal)
					.Select(snapshot => $"{snapshot.RelativePath}:{snapshot.Hash}"))),
				DetectedVersion = identity.Fingerprint.Version,
				InstalledAtUtc = DateTimeOffset.UtcNow,
				Files = snapshots.Select(snapshot => new NativeOwnedFile
				{
					RelativePath = snapshot.RelativePath,
					InstalledHash = snapshot.Hash!,
					Created = true
				}).ToList()
			};

			cancellationToken.ThrowIfCancellationRequested();
			ThrowIfGameRunning();
			foreach (var snapshot in snapshots)
			{
				if (!SameSnapshot(snapshot, CaptureDestination(snapshot.RelativePath)))
					throw new InvalidOperationException("A native DLL changed while Redux was adopting the installation. Refresh and try again.");
			}
			var nextManifest = CreateOrCloneManifest(manifest);
			nextManifest.Installations.Add(installation);
			await WriteManifestAsync(nextManifest, cancellationToken);
		}
		finally
		{
			_operationGate.Release();
		}
	}

	/// <summary>
	/// Conservatively identifies a reviewed game-directory archive without changing either Redux state
	/// or the game. Layout alone is sufficient only when it identifies one catalog project; overlapping
	/// layouts additionally require a Nexus-generated filename carrying the matching project id.
	/// </summary>
	public static ReduxGameDirectoryArchiveInspection? TryInspectKnownArchive(string archivePath, bool computeArchiveHash = true)
	{
		var normalizedArchive = ValidateArchivePath(archivePath);
		using var source = new FileStream(normalizedArchive, FileMode.Open, FileAccess.Read, FileShare.Read,
			128000, FileOptions.SequentialScan);
		try
		{
			using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions());
			var entries = archive.Entries.ToArray();
			ValidateArchiveEntrySet(entries);
			var matching = new List<(ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)>();
			foreach (var definition in ReduxGameDirectoryModCatalog.All)
			{
				foreach (var layout in FindMatchingLayouts(definition, entries))
					matching.Add((definition, layout));
			}

			if (matching.Count == 0)
			{
				if (entries.Any(entry => !entry.IsDirectory
					&& NormalizeArchivePath(entry.Key).EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
				{
					throw new ReduxUnsupportedGameDirectoryArchiveException(
						"This archive contains native DLLs but does not match a reviewed Redux game-directory layout. No files were changed.");
				}
				return null;
			}
			var exactBinaryMatch = FindExactBinaryMatch(matching, entries);
			var nexusIdentity = NexusModFileVersionData.FromFilePath(normalizedArchive);
			var projectMatches = nexusIdentity.Success
				? matching.Where(match => match.Definition.NexusModId == nexusIdentity.ModId).ToArray()
				: Array.Empty<(ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)>();
			var selected = exactBinaryMatch ?? (projectMatches.Length == 1
				? projectMatches[0]
				: matching.Count == 1 ? matching[0] : default);
			if (selected.Definition == null)
				throw new ReduxUnsupportedGameDirectoryArchiveException(
					"This native archive matches more than one reviewed package. Keep its Nexus-generated filename so Redux can identify the intended project safely.");

			var files = entries.Where(entry => !entry.IsDirectory).ToArray();
			var managed = selected.Layout.ManagedEntries.Select(mapping => new ReduxGameDirectoryArchiveFile(
				NormalizeArchivePath(mapping.Key), mapping.Value,
				selected.Definition.PreserveExistingFiles.Contains(mapping.Value))).ToArray();
			return new ReduxGameDirectoryArchiveInspection(
				selected.Definition,
				selected.Layout.Name,
				normalizedArchive,
				computeArchiveHash ? HashFile(normalizedArchive) : String.Empty,
				files.Length,
				files.Sum(entry => entry.Size),
				managed,
				selected.Layout.PackageEntries.Select(NormalizeArchivePath).ToArray(),
				selected.Layout.IgnoredEntries.Select(NormalizeArchivePath).ToArray());
		}
		catch (Exception ex) when (ex is SharpCompressException or InvalidOperationException or OverflowException)
		{
			throw new InvalidDataException("Redux could not inspect this game-directory archive safely.", ex);
		}
	}

	public async Task InspectArchiveAsync(long projectId, string archivePath, CancellationToken cancellationToken = default)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not approved for native installation.");
		EnsureDestinationSupports(definition);
		if (!definition.SupportsGuardedInstall)
			throw new InvalidOperationException($"{definition.Name} uses an existing Redux installation workflow.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			cancellationToken.ThrowIfCancellationRequested();
			ValidateGameBin();
			EnsurePluginVersion(definition);
			var archive = ValidateArchivePath(archivePath);
			var stageDirectory = Path.Combine(_stagingDirectory, Guid.NewGuid().ToString("N"));
			EnsureStageDirectory(stageDirectory);
			try
			{
				await ExtractValidatedArchiveAsync(definition, archive, stageDirectory, cancellationToken);
			}
			finally
			{
				DeleteStageDirectory(stageDirectory);
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	public async Task<ReduxGameDirectoryInstallTransaction> StageAsync(long projectId, string archivePath,
		CancellationToken cancellationToken = default)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not approved for native installation.");
		EnsureDestinationSupports(definition);
		if (!definition.SupportsGuardedInstall)
			throw new InvalidOperationException($"{definition.Name} uses an existing Redux installation workflow.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			cancellationToken.ThrowIfCancellationRequested();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			EnsurePluginVersion(definition);

			var manifest = ReadManifest();
			EnsurePackageSpecificPrerequisites(definition);
			var loaderStatus = DetectLoader(manifest);
			if (definition.RequiresLoader && !loaderStatus.IsPresent)
				throw new InvalidOperationException(ReduxAlternativeNativeLoader.MissingLoaderMessage(
					definition.Name, loaderStatus, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));

			var archive = ValidateArchivePath(archivePath);
			var archiveFingerprint = CaptureFingerprint(archive);
			var transactionId = Guid.NewGuid().ToString("N");
			var stageDirectory = Path.Combine(_stagingDirectory, transactionId);
			EnsureStageDirectory(stageDirectory);
			try
			{
				var stagedFiles = await ExtractValidatedArchiveAsync(definition, archive, stageDirectory, cancellationToken);
				var plan = BuildInstallPlan(definition, manifest, FingerprintManifest(manifest), archive, archiveFingerprint, stageDirectory,
					transactionId, stagedFiles, loaderStatus);
				return new ReduxGameDirectoryInstallTransaction(this, plan, plan.ReviewText);
			}
			catch
			{
				DeleteStageDirectory(stageDirectory);
				throw;
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	public async Task RestoreAsync(long projectId, CancellationToken cancellationToken = default)
	{
		var definition = ReduxGameDirectoryModCatalog.Find(projectId)
			?? throw new InvalidDataException("This Nexus project is not managed by the native installer.");
		EnsureDestinationSupports(definition);
		if (!definition.SupportsGuardedInstall)
			throw new InvalidOperationException($"{definition.Name} uses an existing Redux installation workflow.");
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			var manifest = ReadManifest() ?? throw new InvalidOperationException("Redux has no native installation record for this game.");
			var installation = FindInstallation(manifest, definition.PackageId)
				?? throw new InvalidOperationException("Redux does not own this native installation.");

			if (definition.Kind == ReduxGameDirectoryModKind.NativeLoader) EnsureNoNativePluginDllsRemain();
			var writes = new List<PlannedWrite>();
			foreach (var ownedFile in installation.Files)
			{
				var snapshot = CaptureDestination(ownedFile.RelativePath);
				if (!snapshot.Exists || !String.Equals(snapshot.Hash, ownedFile.InstalledHash, StringComparison.Ordinal))
					throw new InvalidOperationException($"Redux will not restore {definition.Name} because '{ownedFile.RelativePath}' was changed outside Redux.");
				if (!ownedFile.Created)
				{
					var backupPath = GetBackupPath(ownedFile.OriginalBackupId!);
					EnsureSafeRegularFile(backupPath);
					if (!String.Equals(HashFile(backupPath), ownedFile.OriginalHash, StringComparison.Ordinal))
						throw new InvalidDataException("Redux's original native-file backup no longer matches its ownership record.");
				}
				writes.Add(new PlannedWrite(ownedFile.RelativePath, ownedFile.InstalledHash, snapshot,
					CloneOwnedFile(ownedFile)));
			}

			var transactionId = Guid.NewGuid().ToString("N");
			var transactionBackups = await CreateTransactionBackupsAsync(writes, transactionId, cancellationToken);
			var journalPath = GetJournalPath(transactionId);
			var journalWritten = false;
			var manifestSaved = false;
			var completedWrites = new List<PlannedWrite>();
			try
			{
				await WriteJournalAsync(journalPath, "restore", projectId, transactionId, writes, transactionBackups, cancellationToken);
				journalWritten = true;
				foreach (var write in writes)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ThrowIfGameRunning();
					var current = CaptureDestination(write.RelativePath);
					if (!SameSnapshot(write.Before, current))
						throw new InvalidOperationException("A native destination changed after restore review.");
					if (write.Ownership.Created)
					{
						File.Delete(ResolveTargetPath(write.RelativePath, createParent: false));
					}
					else
					{
						await CopyToTargetAtomicallyAsync(GetBackupPath(write.Ownership.OriginalBackupId!),
							ResolveTargetPath(write.RelativePath, createParent: true), write.Ownership.OriginalHash!, write.Before, cancellationToken);
					}
					completedWrites.Add(write);
				}

				manifest.Installations.RemoveAll(item => PackageIdsEqual(item.PackageId, definition.PackageId));
				await WriteManifestAsync(manifest, cancellationToken);
				manifestSaved = true;
				DeleteJournal(journalPath);
			}
			catch
			{
				if (manifestSaved) throw;
				var rollbackError = await RollbackRestoreAsync(completedWrites, transactionBackups);
				if (rollbackError == null && journalWritten) DeleteJournal(journalPath);
				if (rollbackError != null)
					throw new ReduxGameDirectoryRecoveryException("Game-directory restore failed and Redux could not safely roll back every changed file. Do not launch the game; inspect the recovery journal and backups.", rollbackError);
				throw;
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	internal async Task CommitAsync(NativeInstallPlan plan, CancellationToken cancellationToken)
	{
		await _operationGate.WaitAsync(cancellationToken);
		try
		{
			using var stateLock = AcquireStateLock();
			ValidateGameBin();
			ThrowIfGameRunning();
			EnsureNoPendingJournals();
			var currentManifest = ReadMatchingManifest(plan);
			EnsureCommitPrerequisites(plan.Definition, currentManifest);
			EnsureArchiveUnchanged(plan.ArchivePath, plan.ArchiveFingerprint);
			foreach (var guard in plan.Guards)
			{
				if (!SameSnapshot(guard, CaptureDestination(guard.RelativePath)))
					throw new InvalidOperationException("A native destination changed after the installation review.");
			}
			foreach (var write in plan.Writes)
			{
				var stagedPath = GetStagedPath(plan.StageDirectory, write.RelativePath);
				EnsureSafeRegularFile(stagedPath);
				if (!String.Equals(HashFile(stagedPath), write.StagedHash, StringComparison.Ordinal))
					throw new InvalidDataException("The staged native file changed after archive validation.");
			}

			var transactionBackups = await CreateTransactionBackupsAsync(plan.Writes, plan.TransactionId, cancellationToken);
			foreach (var write in plan.Writes)
			{
				if (write.Ownership.Created) continue;
				if (String.IsNullOrWhiteSpace(write.Ownership.OriginalBackupId))
				{
					var backup = transactionBackups.Single(item => item.RelativePath == write.RelativePath);
					write.Ownership.OriginalBackupId = backup.BackupId;
					write.Ownership.OriginalHash = write.Before.Hash;
				}
			}

			var journalPath = GetJournalPath(plan.TransactionId);
			var journalWritten = false;
			var manifestSaved = false;
			var completedWrites = new List<PlannedWrite>();
			try
			{
				await WriteJournalAsync(journalPath, "install", plan.Definition.NexusModId, plan.TransactionId,
					plan.Writes, transactionBackups, cancellationToken);
				journalWritten = true;
				currentManifest = ReadMatchingManifest(plan);
				EnsureCommitPrerequisites(plan.Definition, currentManifest);
				foreach (var guard in plan.Guards)
				{
					if (!SameSnapshot(guard, CaptureDestination(guard.RelativePath)))
						throw new InvalidOperationException("A native destination changed after the installation review.");
				}
				foreach (var write in plan.Writes)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ThrowIfGameRunning();
					var current = CaptureDestination(write.RelativePath);
					if (!SameSnapshot(write.Before, current))
						throw new InvalidOperationException("A native destination changed after the installation review.");
					await CopyToTargetAtomicallyAsync(GetStagedPath(plan.StageDirectory, write.RelativePath),
						ResolveTargetPath(write.RelativePath, createParent: true), write.StagedHash, write.Before, cancellationToken);
					completedWrites.Add(write);
				}

				var nextManifest = CreateOrCloneManifest(currentManifest);
				nextManifest.Installations.RemoveAll(item => PackageIdsEqual(item.PackageId, plan.Definition.PackageId));
				nextManifest.Installations.Add(CloneInstallation(plan.InstallationAfter));
				await WriteManifestAsync(nextManifest, cancellationToken);
				manifestSaved = true;
				DeleteJournal(journalPath);
			}
			catch
			{
				if (manifestSaved) throw;
				var rollbackError = await RollbackAsync(completedWrites, transactionBackups);
				if (rollbackError == null && journalWritten) DeleteJournal(journalPath);
				if (rollbackError != null)
					throw new ReduxGameDirectoryRecoveryException("Game-directory installation failed and Redux could not safely roll back every changed file. Do not launch the game; inspect the recovery journal and backups.", rollbackError);
				throw;
			}
			finally
			{
				DeleteStageDirectory(plan.StageDirectory);
			}
		}
		finally
		{
			_operationGate.Release();
		}
	}

	internal ValueTask DiscardStageAsync(NativeInstallPlan plan)
	{
		DeleteStageDirectory(plan.StageDirectory);
		return ValueTask.CompletedTask;
	}

	private NativeInstallPlan BuildInstallPlan(ReduxGameDirectoryModDefinition definition, NativeInstallManifest? manifest,
		string manifestFingerprint, string archivePath, FileFingerprint archiveFingerprint, string stageDirectory, string transactionId,
		IReadOnlyDictionary<string, string> stagedFiles, ReduxNativeLoaderStatus loaderStatus)
	{
		var existing = manifest == null ? null : FindInstallation(manifest, definition.PackageId);
		var snapshots = definition.RelativeFiles
			.Select(path => CaptureDestination(ToTargetRelative(path)))
			.ToDictionary(snapshot => snapshot.RelativePath, StringComparer.Ordinal);
		var guards = snapshots.Values.ToList();
		if (definition.RequiresLoader && NativePluginDestination == ReduxNativePluginDestination.GameBin)
		{
			foreach (var loaderFile in ReduxGameDirectoryModCatalog.Find(944)!.RelativeFiles)
				guards.Add(CaptureDestination(ToTargetRelative(loaderFile)));
		}
		var stagedHashes = stagedFiles.ToDictionary(pair => pair.Key, pair => HashFile(pair.Value), StringComparer.Ordinal);
		if (existing == null) EnsureReplacementOriginalsAreClean(definition, snapshots, stagedFiles, stagedHashes);
		var detectedVersion = definition.BinaryFingerprints.FirstOrDefault(fingerprint =>
		{
			var relativePath = ToTargetRelative(fingerprint.RelativePath);
			return stagedFiles.TryGetValue(relativePath, out var stagedPath)
				&& new FileInfo(stagedPath).Length == fingerprint.Length
				&& String.Equals(stagedHashes[relativePath], fingerprint.Sha256, StringComparison.OrdinalIgnoreCase);
		})?.Version ?? String.Empty;
		var installation = new NativeOwnedInstallation
		{
			PackageId = definition.PackageId,
			NexusModId = definition.NexusModId,
			Name = definition.Name,
			SourceUrl = definition.SourceUrl,
			ArchiveName = Path.GetFileName(archivePath),
			ArchiveHash = archiveFingerprint.Hash,
			DetectedVersion = detectedVersion,
			InstalledAtUtc = DateTimeOffset.UtcNow
		};
		var writes = new List<PlannedWrite>();

		if (definition.Kind == ReduxGameDirectoryModKind.NativeLoader)
		{
			BuildLoaderPlan(existing, snapshots, stagedHashes, installation, writes);
		}
		else
		{
			BuildPluginPlan(definition, existing, snapshots, stagedHashes, installation, writes);
		}

		var review = new StringBuilder();
		review.Append(definition.Name).Append(" is staged for explicit confirmation.\n\nDestination: ")
			.Append(NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins ? NativePluginDirectory : _gameBin)
			.Append("\n\nRedux will change: ")
			.Append(String.Join(", ", writes.Select(write => write.RelativePath))).Append('.');
		if (definition.RequiresLoader && !loaderStatus.IsVerified)
			review.Append(NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
				? " Redux cannot verify that the YANML watcher, injector, or autostart is running when BG3 starts."
				: " Native Mod Loader is external and unverified; review plugin compatibility before continuing.");
		review.Append(" Archive structure and AMD64 PE headers were checked locally; this does not prove publisher provenance or universal game compatibility.");

		return new NativeInstallPlan(definition, manifestFingerprint, archivePath, archiveFingerprint,
			stageDirectory, transactionId, guards, writes, installation, review.ToString());
	}

	private static void BuildLoaderPlan(NativeOwnedInstallation? existing,
		IReadOnlyDictionary<string, DestinationSnapshot> snapshots, IReadOnlyDictionary<string, string> stagedHashes,
		NativeOwnedInstallation installation, ICollection<PlannedWrite> writes)
	{
		const string loader = "bink2w64.dll";
		const string original = "bink2w64_original.dll";
		var loaderSnapshot = snapshots[loader];
		var originalSnapshot = snapshots[original];
		var loaderHash = stagedHashes[loader];
		var packagedOriginalHash = stagedHashes[original];

		if (existing == null)
		{
			if (originalSnapshot.Exists)
				throw new InvalidOperationException("Native Mod Loader cannot replace an unmanaged bink2w64_original.dll. Recover or review it manually first.");
			if (!loaderSnapshot.Exists || !String.Equals(loaderSnapshot.Hash, packagedOriginalHash, StringComparison.Ordinal))
				throw new InvalidOperationException("Native Mod Loader cannot establish that the current bink2w64.dll matches this archive's packaged original. Do not overwrite it.");
			installation.Files.Add(NewOwnership(loaderSnapshot, loaderHash));
			installation.Files.Add(NewOwnership(originalSnapshot, packagedOriginalHash));
			writes.Add(new PlannedWrite(loader, loaderHash, loaderSnapshot, installation.Files[0]));
			writes.Add(new PlannedWrite(original, packagedOriginalHash, originalSnapshot, installation.Files[1]));
			return;
		}

		var priorLoader = FindOwnedFile(existing, loader)
			?? throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		var priorOriginal = FindOwnedFile(existing, original)
			?? throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		EnsureOwnedFileUnchanged(priorLoader, loaderSnapshot);
		EnsureOwnedFileUnchanged(priorOriginal, originalSnapshot);
		if (!String.Equals(originalSnapshot.Hash, packagedOriginalHash, StringComparison.Ordinal))
			throw new InvalidOperationException("The loader archive's packaged original does not match the verified current game original. Do not apply an old loader archive after a game update.");

		var nextLoader = CloneOwnedFile(priorLoader);
		nextLoader.InstalledHash = loaderHash;
		installation.Files.Add(nextLoader);
		installation.Files.Add(CloneOwnedFile(priorOriginal));
		writes.Add(new PlannedWrite(loader, loaderHash, loaderSnapshot, nextLoader));
	}

	private static void BuildPluginPlan(ReduxGameDirectoryModDefinition definition, NativeOwnedInstallation? existing,
		IReadOnlyDictionary<string, DestinationSnapshot> snapshots, IReadOnlyDictionary<string, string> stagedHashes,
		NativeOwnedInstallation installation, ICollection<PlannedWrite> writes)
	{
		foreach (var canonicalPath in definition.RelativeFiles)
		{
			var relativePath = ToTargetRelative(canonicalPath);
			var snapshot = snapshots[relativePath];
			var prior = existing == null ? null : FindOwnedFile(existing, relativePath);
			if (definition.PreserveExistingFiles.Contains(canonicalPath))
			{
				// Configuration belongs to the player. Place a default only when none exists,
				// then leave it unowned so ordinary edits never block update or restore.
				if (!snapshot.Exists)
				{
					var defaultConfiguration = NewOwnership(snapshot, stagedHashes[relativePath]);
					writes.Add(new PlannedWrite(relativePath, stagedHashes[relativePath], snapshot, defaultConfiguration));
				}
				continue;
			}

			if (snapshot.Exists)
			{
				if (prior == null && !definition.ReplacementOriginals.ContainsKey(canonicalPath))
					throw new InvalidOperationException($"Redux will not replace the unmanaged native file '{relativePath}'.");
				if (prior != null && definition.ReplacesExistingGameFiles) EnsureOwnedFileUnchanged(prior, snapshot);
			}
			else if (prior != null && definition.ReplacesExistingGameFiles)
			{
				throw new InvalidOperationException($"Redux will not recreate the Redux-owned file '{relativePath}' after it was removed outside Redux.");
			}

			var next = prior == null ? NewOwnership(snapshot, stagedHashes[relativePath]) : CloneOwnedFile(prior);
			next.InstalledHash = stagedHashes[relativePath];
			installation.Files.Add(next);
			writes.Add(new PlannedWrite(relativePath, stagedHashes[relativePath], snapshot, next));
		}
	}

	private void EnsureReplacementOriginalsAreClean(ReduxGameDirectoryModDefinition definition,
		IReadOnlyDictionary<string, DestinationSnapshot> snapshots,
		IReadOnlyDictionary<string, string> stagedFiles,
		IReadOnlyDictionary<string, string> stagedHashes)
	{
		if (!definition.ReplacesExistingGameFiles || !_enforceReviewedReplacementOriginals) return;
		if (definition.ReplacementOriginals.Count == 0)
			throw new InvalidOperationException($"Redux has no reviewed clean-file proof for {definition.Name} and will not back up or replace existing game files.");

		foreach (var replacement in definition.ReplacementOriginals)
		{
			var targetPath = ToTargetRelative(replacement.Key);
			var originalPath = ToTargetRelative(replacement.Value);
			if (!snapshots.TryGetValue(targetPath, out var current) || !current.Exists || current.Hash == null
				|| !stagedFiles.TryGetValue(originalPath, out var stagedOriginal)
				|| !stagedHashes.TryGetValue(originalPath, out var stagedOriginalHash))
			{
				throw new InvalidOperationException($"Redux cannot prove that '{targetPath}' is a clean BG3 file, so it will not back it up or replace it.");
			}
			var originalInfo = new FileInfo(stagedOriginal);
			var reviewedOriginal = ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
				replacement.Value, originalInfo.Length, stagedOriginalHash);
			if (reviewedOriginal?.Definition.NexusModId != definition.NexusModId
				|| current.Length != originalInfo.Length
				|| !String.Equals(current.Hash, stagedOriginalHash, StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"Redux cannot verify that '{targetPath}' is an unmodified BG3 file. Verify the game files before installing this replacer through Redux.");
			}
		}
	}

	private async Task<IReadOnlyDictionary<string, string>> ExtractValidatedArchiveAsync(ReduxGameDirectoryModDefinition definition,
		string archivePath, string stageDirectory, CancellationToken cancellationToken)
	{
		try
		{
			using var source = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
				128000, FileOptions.SequentialScan);
			using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions());
			var entries = archive.Entries.ToArray();
			var layout = ValidateArchiveEntries(definition, entries);
			var filesByPath = entries.Where(entry => !entry.IsDirectory)
				.ToDictionary(entry => NormalizeArchivePath(entry.Key), StringComparer.OrdinalIgnoreCase);
			var stagedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var mapping in layout.ManagedEntries)
			{
				var targetRelative = ToTargetRelative(mapping.Value);
				var stagedPath = GetStagedPath(stageDirectory, targetRelative);
				await CopyArchiveEntryAsync(filesByPath[mapping.Key], stagedPath, cancellationToken);
				if (mapping.Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ValidateAmd64PeDll(stagedPath);
				stagedFiles.Add(targetRelative, stagedPath);
			}
			return stagedFiles;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex) when (ex is SharpCompressException or InvalidOperationException)
		{
			throw new InvalidDataException("Redux could not read this native archive safely.", ex);
		}
	}

	private static ReduxGameDirectoryModLayout ValidateArchiveEntries(ReduxGameDirectoryModDefinition definition,
		IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		ValidateArchiveEntrySet(archiveEntries);
		var matches = FindMatchingLayouts(definition, archiveEntries);
		if (matches.Count == 0)
			throw new InvalidDataException("The native archive does not match a reviewed package layout or contains unexpected files.");
		if (matches.Count > 1)
			throw new InvalidDataException("The native archive matches more than one package layout and cannot be selected safely.");
		return matches[0];
	}

	private static void ValidateArchiveEntrySet(IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		if (archiveEntries.Count(entry => !entry.IsDirectory) > MaximumArchiveEntries)
			throw new InvalidDataException("The native archive contains too many entries.");
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		long expandedBytes = 0;
		foreach (var entry in archiveEntries)
		{
			ValidateArchiveEntryPath(entry);
			var normalized = NormalizeArchivePath(entry.Key).TrimEnd('/');
			if (!seen.Add(normalized))
				throw new InvalidDataException("The native archive contains paths that differ only by case.");
			if (entry.IsDirectory) continue;
			files.Add(normalized);
			if (entry.IsEncrypted)
				throw new InvalidDataException("Encrypted native archives are not supported.");
			if (entry.Size <= 0 || entry.Size > MaximumEntryBytes)
				throw new InvalidDataException("The native archive contains an empty or oversized file.");
			// Solid 7z/RAR readers may not expose a meaningful compressed size for each
			// member. The absolute per-entry and total expansion ceilings still apply.
			if (entry.CompressedSize > 0 && entry.Size >= CompressionRatioMinimumBytes
				&& entry.Size / entry.CompressedSize > MaximumCompressionRatio)
				throw new InvalidDataException("The native archive exceeds safe expansion limits.");
			expandedBytes = checked(expandedBytes + entry.Size);
			if (expandedBytes > MaximumExpandedBytes)
				throw new InvalidDataException("The native archive expands beyond the allowed size.");
		}
	}

	private static IReadOnlyList<ReduxGameDirectoryModLayout> FindMatchingLayouts(ReduxGameDirectoryModDefinition definition,
		IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		var files = archiveEntries.Where(entry => !entry.IsDirectory)
			.Select(entry => NormalizeArchivePath(entry.Key))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return definition.Layouts.Where(layout =>
		{
			var allowed = layout.ManagedEntries.Keys
				.Concat(layout.PackageEntries)
				.Concat(layout.IgnoredEntries)
				.Select(NormalizeArchivePath)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			return files.SetEquals(allowed) && layout.ManagedEntries.Keys.All(path => files.Contains(NormalizeArchivePath(path)));
		}).ToArray();
	}

	private static (ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)? FindExactBinaryMatch(
		IReadOnlyList<(ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)> matching,
		IReadOnlyList<IArchiveEntry> archiveEntries)
	{
		var entryHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var exactMatches = new List<(ReduxGameDirectoryModDefinition Definition, ReduxGameDirectoryModLayout Layout)>();
		foreach (var candidate in matching)
		{
			foreach (var fingerprint in candidate.Definition.BinaryFingerprints)
			{
				var archivePath = candidate.Layout.ManagedEntries
					.FirstOrDefault(mapping => NormalizeArchivePath(mapping.Value)
						.Equals(fingerprint.RelativePath, StringComparison.OrdinalIgnoreCase)).Key;
				if (String.IsNullOrWhiteSpace(archivePath)) continue;
				var normalizedArchivePath = NormalizeArchivePath(archivePath);
				var entry = archiveEntries.FirstOrDefault(item => !item.IsDirectory
					&& NormalizeArchivePath(item.Key).Equals(normalizedArchivePath, StringComparison.OrdinalIgnoreCase));
				if (entry == null || entry.Size != fingerprint.Length) continue;
				if (!entryHashes.TryGetValue(normalizedArchivePath, out var hash))
				{
					using var stream = entry.OpenEntryStream();
					hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
					entryHashes[normalizedArchivePath] = hash;
				}
				if (fingerprint.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
					exactMatches.Add(candidate);
			}
		}

		var distinct = exactMatches
			.GroupBy(match => (match.Definition.NexusModId, match.Layout.Name))
			.Select(group => group.First()).Take(2).ToArray();
		return distinct.Length == 1 ? distinct[0] : null;
	}

	private static async Task CopyArchiveEntryAsync(IArchiveEntry entry, string destinationPath,
		CancellationToken cancellationToken)
	{
		EnsureSafeDirectoryTree(Path.GetDirectoryName(destinationPath)!, create: true);
		using var input = entry.OpenEntryStream();
		await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
		var buffer = new byte[128000];
		long written = 0;
		while (true)
		{
			var read = await input.ReadAsync(buffer, cancellationToken);
			if (read == 0) break;
			written = checked(written + read);
			if (written > entry.Size || written > MaximumEntryBytes)
				throw new InvalidDataException("The native archive entry expanded beyond its declared safe size.");
			await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}
		if (written != entry.Size) throw new InvalidDataException("The native archive entry was truncated while staging.");
		await output.FlushAsync(cancellationToken);
		output.Flush(true);
	}

	private async Task<IReadOnlyList<TransactionBackup>> CreateTransactionBackupsAsync(
		IReadOnlyCollection<PlannedWrite> writes, string transactionId, CancellationToken cancellationToken)
	{
		var backups = new List<TransactionBackup>();
		foreach (var write in writes.Where(write => write.Before.Exists))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var current = CaptureDestination(write.RelativePath);
			if (!SameSnapshot(write.Before, current))
				throw new InvalidOperationException("A native destination changed before Redux could create its transaction backup.");
			var backupId = Guid.NewGuid().ToString("N");
			var backupPath = GetBackupPath(backupId);
			await CopyFileAsync(ResolveTargetPath(write.RelativePath, createParent: false), backupPath, write.Before.Hash!, cancellationToken);
			backups.Add(new TransactionBackup(write.RelativePath, backupId, backupPath, write.Before));
		}
		return backups;
	}

	private async Task<Exception?> RollbackAsync(IReadOnlyList<PlannedWrite> completedWrites,
		IReadOnlyList<TransactionBackup> transactionBackups)
	{
		Exception? failure = null;
		foreach (var write in completedWrites.Reverse())
		{
			try
			{
				var current = CaptureDestination(write.RelativePath);
				if (!current.Exists || !String.Equals(current.Hash, write.StagedHash, StringComparison.Ordinal))
					throw new InvalidOperationException($"'{write.RelativePath}' changed before Redux could roll it back.");
				if (write.Before.Exists)
				{
					var backup = transactionBackups.Single(item => item.RelativePath == write.RelativePath);
					await CopyToTargetAtomicallyAsync(backup.BackupPath,
						ResolveTargetPath(write.RelativePath, createParent: true), backup.Before.Hash!, current, CancellationToken.None);
				}
				else
				{
					File.Delete(ResolveTargetPath(write.RelativePath, createParent: false));
				}
			}
			catch (Exception error)
			{
				failure ??= error;
			}
		}
		return failure;
	}

	private async Task<Exception?> RollbackRestoreAsync(IReadOnlyList<PlannedWrite> completedWrites,
		IReadOnlyList<TransactionBackup> transactionBackups)
	{
		Exception? failure = null;
		foreach (var write in completedWrites.Reverse())
		{
			try
			{
				var current = CaptureDestination(write.RelativePath);
				if (write.Ownership.Created)
				{
					if (current.Exists)
						throw new InvalidOperationException($"'{write.RelativePath}' changed before Redux could roll back its restore.");
				}
				else if (!current.Exists || !String.Equals(current.Hash, write.Ownership.OriginalHash, StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"'{write.RelativePath}' changed before Redux could roll back its restore.");
				}
				var backup = transactionBackups.Single(item => item.RelativePath == write.RelativePath);
				await CopyToTargetAtomicallyAsync(backup.BackupPath,
					ResolveTargetPath(write.RelativePath, createParent: true), backup.Before.Hash!, current, CancellationToken.None);
			}
			catch (Exception error)
			{
				failure ??= error;
			}
		}
		return failure;
	}

	private async Task CopyToTargetAtomicallyAsync(string sourcePath, string destinationPath,
		string expectedSourceHash, DestinationSnapshot expectedDestination, CancellationToken cancellationToken)
	{
		EnsureSafeRegularFile(sourcePath);
		if (!SameSnapshot(expectedDestination, CaptureDestination(expectedDestination.RelativePath)))
			throw new InvalidOperationException("A native destination changed immediately before replacement.");
		EnsureSafeDirectoryTree(Path.GetDirectoryName(destinationPath)!, create: true);
		var temporaryPath = destinationPath + ".redux-native-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			await CopyFileAsync(sourcePath, temporaryPath, expectedSourceHash, cancellationToken);
			if (!SameSnapshot(expectedDestination, CaptureDestination(expectedDestination.RelativePath)))
				throw new InvalidOperationException("A native destination changed during replacement preparation.");
			if (expectedDestination.Exists) File.Replace(temporaryPath, destinationPath, null, true);
			else File.Move(temporaryPath, destinationPath);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	private static async Task CopyFileAsync(string sourcePath, string destinationPath, string expectedHash,
		CancellationToken cancellationToken)
	{
		using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan);
		await using (var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			128000, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
		{
			await input.CopyToAsync(output, 128000, cancellationToken);
			await output.FlushAsync(cancellationToken);
			output.Flush(true);
		}
		if (!String.Equals(HashFile(destinationPath), expectedHash, StringComparison.Ordinal))
			throw new InvalidDataException("A native installer copy did not match its validated source hash.");
	}

	private async Task WriteManifestAsync(NativeInstallManifest manifest, CancellationToken cancellationToken)
	{
		ValidateManifest(manifest);
		await WriteJsonAtomicallyAsync(_manifestPath, manifest, cancellationToken);
	}

	private async Task WriteJournalAsync(string journalPath, string operation, long projectId, string transactionId,
		IReadOnlyCollection<PlannedWrite> writes, IReadOnlyList<TransactionBackup> backups,
		CancellationToken cancellationToken)
	{
		var journal = new NativeJournal
		{
			Version = ManifestVersion,
			GameBinIdentity = _gameBinIdentity,
			Operation = operation,
			ProjectId = projectId,
			TransactionId = transactionId,
			Files = writes.Select(write => new NativeJournalFile
			{
				RelativePath = write.RelativePath,
				BeforeExists = write.Before.Exists,
				BeforeHash = write.Before.Hash,
				StagedHash = write.StagedHash,
				BackupId = backups.FirstOrDefault(backup => backup.RelativePath == write.RelativePath)?.BackupId
			}).ToList()
		};
		ValidateJournal(journal);
		var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
		EnsureSafeFileOrMissing(journalPath);
		await using var stream = new FileStream(journalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
			4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
		await stream.WriteAsync(bytes, cancellationToken);
		await stream.FlushAsync(cancellationToken);
		stream.Flush(true);
	}

	private async Task WriteJsonAtomicallyAsync<T>(string path, T value, CancellationToken cancellationToken)
	{
		EnsureSafeFileOrMissing(path);
		var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
		try
		{
			await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
				4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				await stream.WriteAsync(bytes, cancellationToken);
				await stream.FlushAsync(cancellationToken);
				stream.Flush(true);
			}
			if (File.Exists(path)) File.Replace(temporaryPath, path, null, true);
			else File.Move(temporaryPath, path);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	private NativeInstallManifest? ReadManifest()
	{
		if (!PathEntryExists(_manifestPath)) return null;
		EnsureSafeRegularFile(_manifestPath);
		if (new FileInfo(_manifestPath).Length > MaximumManifestBytes)
			throw new InvalidDataException("Redux's native ownership manifest is too large.");
		var json = File.ReadAllText(_manifestPath, Encoding.UTF8);
		using var document = JsonDocument.Parse(json);
		EnsureNoDuplicateJsonProperties(document.RootElement);
		var manifest = JsonSerializer.Deserialize<NativeInstallManifest>(json, JsonOptions)
			?? throw new InvalidDataException("Redux's native ownership manifest is empty.");
		ValidateManifest(manifest);
		return manifest;
	}

	private void EnsureNoPendingJournals()
	{
		EnsureSafeDirectoryTree(_journalDirectory, create: false);
		var journals = Directory.EnumerateFiles(_journalDirectory, "*.json", SearchOption.TopDirectoryOnly).ToArray();
		if (journals.Length == 0) return;
		foreach (var path in journals)
		{
			EnsureSafeRegularFile(path);
			if (new FileInfo(path).Length > MaximumManifestBytes) throw new InvalidDataException("A native installer journal is too large.");
			var json = File.ReadAllText(path, Encoding.UTF8);
			using var document = JsonDocument.Parse(json);
			EnsureNoDuplicateJsonProperties(document.RootElement);
			var journal = JsonSerializer.Deserialize<NativeJournal>(json, JsonOptions)
				?? throw new InvalidDataException("A native installer journal is empty.");
			ValidateJournal(journal);
		}
		throw new ReduxGameDirectoryRecoveryException("A previous game-directory installation did not finish. Do not launch the game; inspect the recovery journal and immutable backups before continuing.");
	}

	private void DeleteJournal(string path)
	{
		EnsureSafeRegularFile(path);
		File.Delete(path);
	}

	private static bool HasExternalLoaderEvidence(DestinationSnapshot loader, DestinationSnapshot original)
	{
		if (!loader.Exists) return false;
		var fingerprints = ReduxGameDirectoryModCatalog.Find(944)!.BinaryFingerprints;
		bool Matches(string path) => fingerprints.Any(fingerprint => fingerprint.RelativePath == path
			&& fingerprint.Length == loader.Length
			&& String.Equals(fingerprint.Sha256, loader.Hash, StringComparison.OrdinalIgnoreCase));
		if (Matches("bin/bink2w64_original.dll")) return false;
		if (Matches("bin/bink2w64.dll")) return true;
		// The base game owns this filename too. A lone DLL or identical leftover backup
		// does not establish an external loader installation.
		return original.Exists && !String.Equals(loader.Hash, original.Hash, StringComparison.OrdinalIgnoreCase);
	}

	private ReduxNativeLoaderStatus DetectLoader(NativeInstallManifest? manifest)
	{
		if (NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins)
		{
			var configuration = Path.Combine(NativePluginDirectory, "config.toml");
			if (!File.Exists(configuration))
				return new ReduxNativeLoaderStatus(false, false, "YANML's Plugins configuration is missing.");
			EnsureSafeRegularFile(configuration);
			if (new FileInfo(configuration).Length > 1024 * 1024)
				return new ReduxNativeLoaderStatus(false, false, "YANML's config.toml is too large to verify.", IsAlternativeLoader: true);
			if (ReduxAlternativeNativeLoader.IsPluginLoadingDisabled(configuration))
				return new ReduxNativeLoaderStatus(false, false,
					"YANML's [core] enabled setting is false. Enable plugin loading in config.toml before installing plugins.",
					IsAlternativeLoader: true);
			var configuredRoot = ReduxAlternativeNativeLoader.ReadConfiguredInstallRoot(configuration);
			if (!String.IsNullOrWhiteSpace(configuredRoot))
			{
				string normalizedRoot;
				try { normalizedRoot = Path.GetFullPath(configuredRoot).TrimEnd(Path.DirectorySeparatorChar); }
				catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
				{
					return new ReduxNativeLoaderStatus(false, false, "YANML's install_root path is invalid.", IsAlternativeLoader: true);
				}
				var gameRoot = Path.GetFullPath(Path.Combine(_gameBin, ".."))
					.TrimEnd(Path.DirectorySeparatorChar);
				if (!normalizedRoot.Equals(gameRoot, StringComparison.OrdinalIgnoreCase))
					return new ReduxNativeLoaderStatus(false, false,
						"YANML's [core] install_root points to a different BG3 installation. Update config.toml before installing plugins.",
						IsAlternativeLoader: true);
			}
			return new ReduxNativeLoaderStatus(true, false,
				"YANML Plugins folder is configured. Redux cannot verify whether YANML's watcher, injector, or autostart is running when BG3 starts.",
				IsAlternativeLoader: true);
		}
		const string loader = "bink2w64.dll";
		const string original = "bink2w64_original.dll";
		var owned = manifest == null ? null : FindInstallation(manifest, ReduxGameDirectoryModCatalog.Find(944)!.PackageId);
		var loaderSnapshot = CaptureDestination(loader);
		var originalSnapshot = CaptureDestination(original);
		if (owned != null)
		{
			var ownedLoader = FindOwnedFile(owned, loader);
			var ownedOriginal = FindOwnedFile(owned, original);
			if (ownedLoader == null || ownedOriginal == null
				|| !loaderSnapshot.Exists || !originalSnapshot.Exists
				|| !String.Equals(ownedLoader.InstalledHash, loaderSnapshot.Hash, StringComparison.Ordinal)
				|| !String.Equals(ownedOriginal.InstalledHash, originalSnapshot.Hash, StringComparison.Ordinal))
			{
				return new ReduxNativeLoaderStatus(false, false,
					"Native Mod Loader changed since Redux installed it; dependent native plugins are blocked.");
			}
			return new ReduxNativeLoaderStatus(true, true, "Native Mod Loader is verified as installed by Redux.");
		}

		if (!HasExternalLoaderEvidence(loaderSnapshot, originalSnapshot))
			return new ReduxNativeLoaderStatus(false, false, "Native Mod Loader is missing.");
		if (!loaderSnapshot.Exists || !originalSnapshot.Exists
			|| !IsAmd64PeDll(ResolveTargetPath(loader, createParent: false))
			|| !IsAmd64PeDll(ResolveTargetPath(original, createParent: false)))
		{
			return new ReduxNativeLoaderStatus(false, false, "Native Mod Loader is incomplete or has invalid DLL files.");
		}
		return new ReduxNativeLoaderStatus(true, false,
			"Native Mod Loader is present but external and unverified. Review compatibility before installing a dependent plugin.");
	}

	private void EnsureNoNativePluginDllsRemain()
	{
		var root = Path.Combine(_gameBin, "NativeMods");
		if (!PathEntryExists(root)) return;
		var directories = new Stack<string>();
		directories.Push(root);
		var inspected = 0;
		while (directories.Count > 0)
		{
			var directory = directories.Pop();
			EnsureSafeDirectoryTree(directory, create: false);
			foreach (var path in Directory.EnumerateFileSystemEntries(directory))
			{
				if (++inspected > 4096 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
					throw new InvalidOperationException("NativeMods could not be fully inspected safely. Review it manually before restoring the loader.");
				if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException("A native plugin DLL remains in NativeMods, possibly installed by another tool. Restore or remove dependent plugins before restoring Native Mod Loader.");
				if (Directory.Exists(path)) directories.Push(path);
			}
		}
	}

	private void EnsurePluginVersion(ReduxGameDirectoryModDefinition definition)
	{
		if (!definition.RequiresLoader) return;
		if (_gameVersion == null)
			throw new InvalidOperationException($"{definition.Name} requires a known BG3 game version of {MinimumPluginGameVersion} or newer.");
		if (_gameVersion.CompareTo(MinimumPluginGameVersion) < 0)
			throw new InvalidOperationException($"{definition.Name} requires BG3 {MinimumPluginGameVersion} (Hotfix 34) or newer.");
	}

	private NativeInstallManifest? ReadMatchingManifest(NativeInstallPlan plan)
	{
		var manifest = ReadManifest();
		if (!String.Equals(plan.ManifestFingerprint, FingerprintManifest(manifest), StringComparison.Ordinal))
			throw new InvalidOperationException("Native ownership changed after the installation review. Review and stage the archive again.");
		return manifest;
	}

	private void EnsureCommitPrerequisites(ReduxGameDirectoryModDefinition definition, NativeInstallManifest? manifest)
	{
		EnsurePluginVersion(definition);
		EnsurePackageSpecificPrerequisites(definition);
		if (!definition.RequiresLoader) return;
		var loaderStatus = DetectLoader(manifest);
		if (!loaderStatus.IsPresent)
			throw new InvalidOperationException(ReduxAlternativeNativeLoader.MissingLoaderMessage(
				definition.Name, loaderStatus, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
	}

	private void EnsurePackageSpecificPrerequisites(ReduxGameDirectoryModDefinition definition)
	{
		if (definition.NexusModId != 23959) return;
		var nativeMods = NativePluginDirectory;
		if (!Directory.Exists(nativeMods)) return;
		EnsureSafeDirectoryTree(nativeMods, create: false);
		if (Directory.EnumerateFileSystemEntries(nativeMods, "BG3NativeCameraTweaks*", SearchOption.TopDirectoryOnly).Any())
		{
			throw new InvalidOperationException(
				"True Third-Person Camera requires all legacy BG3NativeCameraTweaks files to be removed first. Redux will not delete those files automatically.");
		}
	}

	private static string ValidateArchivePath(string archivePath)
	{
		if (String.IsNullOrWhiteSpace(archivePath)) throw new InvalidDataException("A native archive path is required.");
		var path = Path.GetFullPath(archivePath);
		if (!SupportedArchiveExtensions.Contains(Path.GetExtension(path))
			|| Path.GetFileName(path).Contains(':'))
			throw new InvalidDataException("Choose a reviewed ZIP, 7z, or RAR native archive.");
		EnsureSafeRegularFile(path);
		if (new FileInfo(path).Length <= 0 || new FileInfo(path).Length > MaximumArchiveBytes)
			throw new InvalidDataException("The native archive is empty or exceeds the safe size limit.");
		return path;
	}

	private void EnsureArchiveUnchanged(string archivePath, FileFingerprint expected)
	{
		EnsureSafeRegularFile(archivePath);
		if (!expected.Equals(CaptureFingerprint(archivePath)))
			throw new InvalidOperationException("The native archive changed after review. Download it again before continuing.");
	}

	private DestinationSnapshot CaptureDestination(string relativePath)
	{
		var path = ResolveTargetPath(relativePath, createParent: false);
		if (Directory.Exists(path)) throw new InvalidDataException($"Native target '{relativePath}' is a directory.");
		if (!PathEntryExists(path)) return new DestinationSnapshot(relativePath, false, null, 0, 0);
		if (!File.Exists(path)) throw new InvalidDataException($"Native target '{relativePath}' is not a safe regular file.");
		EnsureSafeRegularFile(path);
		var info = new FileInfo(path);
		return new DestinationSnapshot(relativePath, true, HashFile(path), info.Length, info.LastWriteTimeUtc.Ticks);
	}

	private static ReduxGameDirectoryBinaryMatch? FindExactReviewedDllSetIdentity(
		ReduxGameDirectoryModDefinition definition, IReadOnlyCollection<DestinationSnapshot> snapshots)
	{
		var matches = new List<ReduxGameDirectoryBinaryMatch>();
		foreach (var relativePath in definition.RelativeFiles
			.Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(ToTargetRelative))
		{
			var snapshot = snapshots.SingleOrDefault(item =>
				String.Equals(item.RelativePath, relativePath, StringComparison.Ordinal));
			if (snapshot is not { Exists: true, Hash: not null }) return null;
			var match = ReduxGameDirectoryModCatalog.FindByBinaryFingerprint(
				$"bin/{snapshot.RelativePath}", snapshot.Length, snapshot.Hash);
			if (match?.Definition.NexusModId != definition.NexusModId) return null;
			matches.Add(match);
		}
		return matches.FirstOrDefault(match => !String.IsNullOrWhiteSpace(match.Fingerprint.Version))
			?? matches.FirstOrDefault();
	}

	private string ResolveTargetPath(string relativePath, bool createParent)
	{
		if (!IsTargetRelativePath(relativePath)) throw new InvalidDataException("A native target path is invalid.");
		var parts = relativePath.Split('/');
		var isYanmlPlugin = NativePluginDestination == ReduxNativePluginDestination.YanmlPlugins
			&& parts.Length > 1 && parts[0].Equals("NativeMods", StringComparison.OrdinalIgnoreCase);
		var root = isYanmlPlugin ? NativePluginDirectory : _gameBin;
		var directory = root;
		for (var index = isYanmlPlugin ? 1 : 0; index < parts.Length - 1; index++)
		{
			directory = Path.Combine(directory, parts[index]);
			if (PathEntryExists(directory) && !Directory.Exists(directory))
				throw new InvalidDataException("A native target parent is not a safe directory.");
			if (PathEntryExists(directory)) EnsureSafeDirectoryTree(directory, create: false);
			else if (createParent) EnsureSafeDirectoryTree(directory, create: true);
		}
		var target = Path.GetFullPath(Path.Combine(directory, parts[^1]));
		if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("A native target escapes its managed directory.");
		return target;
	}

	private string GetStagedPath(string stageDirectory, string relativePath)
	{
		if (!IsTargetRelativePath(relativePath)) throw new InvalidDataException("A staged native path is invalid.");
		var path = Path.GetFullPath(Path.Combine(stageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		if (!path.StartsWith(stageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("A staged native path escapes its transaction directory.");
		return path;
	}

	private void EnsureStageDirectory(string path)
	{
		if (!path.StartsWith(_stagingDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| !Guid.TryParseExact(Path.GetFileName(path), "N", out _))
			throw new InvalidDataException("A native staging directory is invalid.");
		EnsureSafeDirectoryTree(path, create: true);
	}

	private void DeleteStageDirectory(string path)
	{
		if (String.IsNullOrWhiteSpace(path)
			|| !path.StartsWith(_stagingDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| !Guid.TryParseExact(Path.GetFileName(path), "N", out _)
			|| !Directory.Exists(path)) return;
		EnsureSafeDirectoryTree(path, create: false);
		EnsureSafeDirectoryContents(path);
		Directory.Delete(path, true);
	}

	private FileStream AcquireStateLock()
	{
		EnsureSafeFileOrMissing(_stateLockPath);
		return new FileStream(_stateLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
			1, FileOptions.WriteThrough);
	}

	private string GetBackupPath(string backupId)
	{
		if (!Guid.TryParseExact(backupId, "N", out _)) throw new InvalidDataException("A native backup identifier is invalid.");
		return Path.Combine(_backupDirectory, backupId + ".bin");
	}

	private string GetJournalPath(string transactionId)
	{
		if (!Guid.TryParseExact(transactionId, "N", out _)) throw new InvalidDataException("A native journal identifier is invalid.");
		return Path.Combine(_journalDirectory, transactionId + ".json");
	}

	private void ValidateGameBin()
	{
		EnsureSafeDirectoryTree(_gameBin, create: false);
		var executables = new[] { "bg3.exe", "bg3_dx11.exe" }
			.Select(name => Path.Combine(_gameBin, name))
			.Where(PathEntryExists)
			.ToArray();
		if (executables.Length == 0)
			throw new InvalidDataException("The selected game bin directory does not contain bg3.exe or bg3_dx11.exe.");
		foreach (var executable in executables) EnsureSafeRegularFile(executable);
	}

	private static void ThrowIfGameRunning()
	{
		foreach (var processName in new[] { "bg3", "bg3_dx11" })
		{
			foreach (var process in Process.GetProcessesByName(processName))
			{
				using (process)
				{
					try
					{
						if (!process.HasExited)
							throw new InvalidOperationException("Close Baldur's Gate 3 before changing native game files.");
					}
					catch (InvalidOperationException) { throw; }
					catch (Exception) { }
				}
			}
		}
	}

	private static void ValidateAmd64PeDll(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
		if (stream.Length < 0x9a) throw new InvalidDataException("A native DLL is too small to be a valid AMD64 PE file.");
		Span<byte> header = stackalloc byte[0x9a];
		stream.ReadExactly(header);
		if (header[0] != (byte)'M' || header[1] != (byte)'Z')
			throw new InvalidDataException("A native DLL does not have a valid DOS header.");
		var peOffset = BitConverter.ToInt32(header.Slice(0x3c, 4));
		if (peOffset < 0x40 || peOffset > stream.Length - 26)
			throw new InvalidDataException("A native DLL has an invalid PE header offset.");
		stream.Position = peOffset;
		Span<byte> peHeader = stackalloc byte[26];
		stream.ReadExactly(peHeader);
		if (peHeader[0] != (byte)'P' || peHeader[1] != (byte)'E' || peHeader[2] != 0 || peHeader[3] != 0)
			throw new InvalidDataException("A native DLL does not have a valid PE signature.");
		if (BitConverter.ToUInt16(peHeader.Slice(4, 2)) != 0x8664
			|| (BitConverter.ToUInt16(peHeader.Slice(22, 2)) & 0x2000) == 0
			|| BitConverter.ToUInt16(peHeader.Slice(24, 2)) != 0x20b)
		{
			throw new InvalidDataException("A native DLL is not an AMD64 PE DLL.");
		}
	}

	private static bool IsAmd64PeDll(string path)
	{
		try
		{
			ValidateAmd64PeDll(path);
			return true;
		}
		catch (Exception ex) when (IsValidationException(ex)) { return false; }
	}

	private static void ValidateArchiveEntryPath(IArchiveEntry entry)
	{
		var path = NormalizeArchivePath(entry.Key);
		if (String.IsNullOrWhiteSpace(path) || path.StartsWith("/", StringComparison.Ordinal)
			|| Path.IsPathRooted(path) || path.Contains(':') || !String.IsNullOrEmpty(entry.LinkTarget)
			|| (entry.Attrib is int attributes && (attributes & (int)FileAttributes.ReparsePoint) != 0))
			throw new InvalidDataException("The native archive contains an unsafe entry path or link.");
		var parts = path.TrimEnd('/').Split('/');
		if (parts.Length == 0 || parts.Any(part => String.IsNullOrWhiteSpace(part) || part is "." or ".."))
			throw new InvalidDataException("The native archive contains a traversal entry path.");
	}

	private static string NormalizeArchivePath(string path) => (path ?? String.Empty).Replace('\\', '/');

	private static string ToTargetRelative(string archiveRelativePath)
	{
		if (String.IsNullOrWhiteSpace(archiveRelativePath)
			|| !archiveRelativePath.StartsWith("bin/", StringComparison.Ordinal)
			|| !IsTargetRelativePath(archiveRelativePath[4..]))
		{
			throw new InvalidDataException("The native catalog has an invalid game-bin-relative path.");
		}
		return archiveRelativePath[4..];
	}

	private static bool IsTargetRelativePath(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith("/", StringComparison.Ordinal)
			|| path.Contains(':') || Path.IsPathRooted(path)) return false;
		var parts = path.Split('/');
		return parts.Length > 0 && parts.All(part => !String.IsNullOrWhiteSpace(part) && part is not "." and not "..");
	}

	private static NativeOwnedFile NewOwnership(DestinationSnapshot snapshot, string installedHash) => new()
	{
		RelativePath = snapshot.RelativePath,
		InstalledHash = installedHash,
		Created = !snapshot.Exists,
		OriginalHash = snapshot.Exists ? snapshot.Hash : null,
		OriginalBackupId = null
	};

	private static void EnsureOwnedFileUnchanged(NativeOwnedFile ownedFile, DestinationSnapshot snapshot)
	{
		if (!snapshot.Exists || !String.Equals(ownedFile.InstalledHash, snapshot.Hash, StringComparison.Ordinal))
			throw new InvalidOperationException($"Redux will not overwrite '{ownedFile.RelativePath}' because it changed outside Redux.");
	}

	private static bool SameSnapshot(DestinationSnapshot expected, DestinationSnapshot actual) =>
		expected.Exists == actual.Exists
		&& expected.Length == actual.Length
		&& expected.LastWriteTicks == actual.LastWriteTicks
		&& String.Equals(expected.Hash, actual.Hash, StringComparison.Ordinal);

	private static NativeOwnedInstallation? FindInstallation(NativeInstallManifest manifest, string packageId) =>
		manifest.Installations.SingleOrDefault(installation => PackageIdsEqual(installation.PackageId, packageId));

	private static bool PackageIdsEqual(string left, string right) =>
		String.Equals(left, right, StringComparison.OrdinalIgnoreCase);

	private static NativeOwnedFile? FindOwnedFile(NativeOwnedInstallation installation, string relativePath) =>
		installation.Files.SingleOrDefault(file => String.Equals(file.RelativePath, relativePath, StringComparison.Ordinal));

	private void ValidateManifest(NativeInstallManifest manifest)
	{
		if (manifest.Version != ManifestVersion || !String.Equals(manifest.GameBinIdentity, _gameBinIdentity, StringComparison.Ordinal)
			|| manifest.Installations == null || manifest.Installations.Count > ReduxGameDirectoryModCatalog.All.Count)
		{
			throw new InvalidDataException("Redux's native ownership manifest is foreign or invalid.");
		}
		var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var installation in manifest.Installations)
		{
			if (installation == null) throw new InvalidDataException("Redux's native ownership manifest has a null installation entry.");
			var definition = ReduxGameDirectoryModCatalog.Find(installation.NexusModId);
			if (definition == null || !PackageIdsEqual(definition.PackageId, installation.PackageId)
				|| !packageIds.Add(installation.PackageId) || String.IsNullOrWhiteSpace(installation.Name)
				|| !Uri.TryCreate(installation.SourceUrl, UriKind.Absolute, out var sourceUri)
				|| sourceUri.Scheme != Uri.UriSchemeHttps || !IsHash(installation.ArchiveHash)
				|| String.IsNullOrWhiteSpace(installation.ArchiveName) || installation.ArchiveName.Length > 260
				|| !String.Equals(installation.ArchiveName, Path.GetFileName(installation.ArchiveName), StringComparison.Ordinal)
				|| installation.DetectedVersion == null || installation.DetectedVersion.Length > 64
				|| installation.InstalledAtUtc == default || installation.Files == null || installation.Files.Count == 0)
				throw new InvalidDataException("Redux's native ownership manifest has an invalid installation entry.");
			var allowed = definition.RelativeFiles.Select(ToTargetRelative).ToHashSet(StringComparer.Ordinal);
			var paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (var file in installation.Files)
			{
				if (file == null || !allowed.Contains(file.RelativePath) || !paths.Add(file.RelativePath)
					|| !IsHash(file.InstalledHash) || (file.Created && (!String.IsNullOrEmpty(file.OriginalHash) || !String.IsNullOrEmpty(file.OriginalBackupId)))
					|| (!file.Created && (!IsHash(file.OriginalHash) || !IsIdentifier(file.OriginalBackupId))))
				{
					throw new InvalidDataException("Redux's native ownership manifest has an unsafe file record.");
				}
			}
			var dlls = definition.RelativeFiles.Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(ToTargetRelative);
			if (dlls.Any(path => !paths.Contains(path)))
				throw new InvalidDataException("Redux's native ownership manifest is missing a managed native DLL.");
			if (definition.Kind == ReduxGameDirectoryModKind.NativeLoader && paths.Count != allowed.Count)
				throw new InvalidDataException("Redux's Native Mod Loader ownership record is incomplete.");
		}
	}

	private void ValidateJournal(NativeJournal journal)
	{
		if (journal.Version != ManifestVersion || !String.Equals(journal.GameBinIdentity, _gameBinIdentity, StringComparison.Ordinal)
			|| journal.Operation is not ("install" or "restore") || ReduxGameDirectoryModCatalog.Find(journal.ProjectId) == null
			|| !IsIdentifier(journal.TransactionId) || journal.Files == null || journal.Files.Count == 0)
		{
			throw new InvalidDataException("A native installer journal is foreign or invalid.");
		}
		var definition = ReduxGameDirectoryModCatalog.Find(journal.ProjectId)!;
		var allowed = definition.RelativeFiles.Select(ToTargetRelative).ToHashSet(StringComparer.Ordinal);
		var paths = new HashSet<string>(StringComparer.Ordinal);
		foreach (var file in journal.Files)
		{
			if (file == null || !allowed.Contains(file.RelativePath) || !paths.Add(file.RelativePath) || !IsHash(file.StagedHash)
				|| (file.BeforeExists && (!IsHash(file.BeforeHash) || !IsIdentifier(file.BackupId)))
				|| (!file.BeforeExists && (!String.IsNullOrEmpty(file.BeforeHash) || !String.IsNullOrEmpty(file.BackupId))))
			{
				throw new InvalidDataException("A native installer journal contains an unsafe file record.");
			}
		}
	}

	private static NativeInstallManifest CloneManifest(NativeInstallManifest? manifest) => new()
	{
		Version = ManifestVersion,
		GameBinIdentity = manifest?.GameBinIdentity ?? String.Empty,
		Installations = manifest?.Installations.Select(CloneInstallation).ToList() ?? new List<NativeOwnedInstallation>()
	};

	private NativeInstallManifest CreateOrCloneManifest(NativeInstallManifest? manifest)
	{
		var clone = CloneManifest(manifest);
		clone.GameBinIdentity = _gameBinIdentity;
		return clone;
	}

	private static NativeOwnedInstallation CloneInstallation(NativeOwnedInstallation installation) => new()
	{
		PackageId = installation.PackageId,
		NexusModId = installation.NexusModId,
		Name = installation.Name,
		SourceUrl = installation.SourceUrl,
		ArchiveName = installation.ArchiveName,
		ArchiveHash = installation.ArchiveHash,
		DetectedVersion = installation.DetectedVersion,
		InstalledAtUtc = installation.InstalledAtUtc,
		Files = installation.Files.Select(CloneOwnedFile).ToList()
	};

	private static NativeOwnedFile CloneOwnedFile(NativeOwnedFile file) => new()
	{
		RelativePath = file.RelativePath,
		InstalledHash = file.InstalledHash,
		Created = file.Created,
		OriginalHash = file.OriginalHash,
		OriginalBackupId = file.OriginalBackupId
	};

	private static bool IsHash(string? value)
	{
		return value != null && value.Length == 64 && value.All(character =>
			(character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
	}

	private static bool IsIdentifier(string? value) => value != null && Guid.TryParseExact(value, "N", out _);

	private static void EnsureNoDuplicateJsonProperties(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (var property in element.EnumerateObject())
			{
				if (!names.Add(property.Name)) throw new InvalidDataException("A native installer JSON record has duplicate properties.");
				EnsureNoDuplicateJsonProperties(property.Value);
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in element.EnumerateArray()) EnsureNoDuplicateJsonProperties(item);
		}
	}

	private static FileFingerprint CaptureFingerprint(string path)
	{
		EnsureSafeRegularFile(path);
		var info = new FileInfo(path);
		return new FileFingerprint(info.Length, info.LastWriteTimeUtc.Ticks, HashFile(path));
	}

	private static string HashFile(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128000, FileOptions.SequentialScan);
		return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
	}

	private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

	private static string FingerprintManifest(NativeInstallManifest? manifest) => manifest == null
		? "absent"
		: HashText(JsonSerializer.Serialize(manifest, JsonOptions));

	private static string NormalizeExistingDirectory(string path, string parameterName)
	{
		if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A directory is required.", parameterName);
		var fullPath = Path.GetFullPath(path);
		EnsureSafeDirectoryTree(fullPath, create: false);
		return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static string NormalizeOrCreateDirectory(string path, string parameterName)
	{
		if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A directory is required.", parameterName);
		var fullPath = Path.GetFullPath(path);
		EnsureSafeDirectoryTree(fullPath, create: true);
		return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static void EnsureSafeDirectoryTree(string directory, bool create)
	{
		var fullPath = Path.GetFullPath(directory);
		if (create) Directory.CreateDirectory(fullPath);
		if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException("A required native installer directory does not exist.");
		for (var current = new DirectoryInfo(fullPath); current != null; current = current.Parent)
		{
			var attributes = File.GetAttributes(current.FullName);
			if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
				throw new InvalidDataException("Native installer paths cannot traverse symlinks, junctions, or non-directory ancestors.");
		}
	}

	private static void EnsureSafeDirectoryContents(string root)
	{
		var pending = new Stack<string>();
		pending.Push(root);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
			{
				var attributes = File.GetAttributes(entry);
				if ((attributes & FileAttributes.ReparsePoint) != 0)
					throw new InvalidDataException("Redux-managed directories cannot contain symlinks or junctions.");
				if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
				else EnsureSafeRegularFile(entry);
			}
		}
	}

	private static void EnsureSafeRegularFile(string path)
	{
		if (!PathEntryExists(path) || Directory.Exists(path) || !File.Exists(path)) throw new FileNotFoundException("A required native installer file is missing or unsafe.", path);
		var attributes = File.GetAttributes(path);
		if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
			throw new InvalidDataException("Native installer files cannot be symlinks, junctions, or directories.");
		EnsureSafeDirectoryTree(Path.GetDirectoryName(Path.GetFullPath(path))!, create: false);
	}

	private static void EnsureSafeFileOrMissing(string path)
	{
		if (PathEntryExists(path) && Directory.Exists(path))
			throw new InvalidDataException("A native installer file path is occupied by a directory.");
		if (PathEntryExists(path)) EnsureSafeRegularFile(path);
		else EnsureSafeDirectoryTree(Path.GetDirectoryName(Path.GetFullPath(path))!, create: false);
	}

	private static bool PathEntryExists(string path)
	{
		try
		{
			_ = File.GetAttributes(path);
			return true;
		}
		catch (FileNotFoundException) { return false; }
		catch (DirectoryNotFoundException) { return false; }
	}

	private static bool IsValidationException(Exception exception) => exception is
		IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or JsonException;

	internal sealed record FileFingerprint(long Length, long LastWriteTicks, string Hash);
	internal sealed record DestinationSnapshot(string RelativePath, bool Exists, string? Hash, long Length, long LastWriteTicks);
	internal sealed record TransactionBackup(string RelativePath, string BackupId, string BackupPath, DestinationSnapshot Before);

	internal sealed class NativeInstallPlan
	{
		public ReduxGameDirectoryModDefinition Definition { get; }
		public string ManifestFingerprint { get; }
		public string ArchivePath { get; }
		public FileFingerprint ArchiveFingerprint { get; }
		public string StageDirectory { get; }
		public string TransactionId { get; }
		public IReadOnlyList<DestinationSnapshot> Guards { get; }
		public IReadOnlyList<PlannedWrite> Writes { get; }
		public NativeOwnedInstallation InstallationAfter { get; }
		public string ReviewText { get; }

		public NativeInstallPlan(ReduxGameDirectoryModDefinition definition, string manifestFingerprint, string archivePath,
			FileFingerprint archiveFingerprint, string stageDirectory, string transactionId,
			IReadOnlyList<DestinationSnapshot> guards, IReadOnlyList<PlannedWrite> writes,
			NativeOwnedInstallation installationAfter, string reviewText)
		{
			Definition = definition;
			ManifestFingerprint = manifestFingerprint;
			ArchivePath = archivePath;
			ArchiveFingerprint = archiveFingerprint;
			StageDirectory = stageDirectory;
			TransactionId = transactionId;
			Guards = guards;
			Writes = writes;
			InstallationAfter = installationAfter;
			ReviewText = reviewText;
		}
	}

	internal sealed class PlannedWrite
	{
		public string RelativePath { get; }
		public string? StagedHash { get; }
		public DestinationSnapshot Before { get; }
		public NativeOwnedFile Ownership { get; }

		public PlannedWrite(string relativePath, string? stagedHash, DestinationSnapshot before, NativeOwnedFile ownership)
		{
			RelativePath = relativePath;
			StagedHash = stagedHash;
			Before = before;
			Ownership = ownership;
		}
	}

	internal sealed class NativeInstallManifest
	{
		public int Version { get; set; }
		public string GameBinIdentity { get; set; } = String.Empty;
		public List<NativeOwnedInstallation> Installations { get; set; } = new();
	}

	internal sealed class NativeOwnedInstallation
	{
		public string PackageId { get; set; } = String.Empty;
		public long NexusModId { get; set; }
		public string Name { get; set; } = String.Empty;
		public string SourceUrl { get; set; } = String.Empty;
		public string ArchiveName { get; set; } = String.Empty;
		public string ArchiveHash { get; set; } = String.Empty;
		public string DetectedVersion { get; set; } = String.Empty;
		public DateTimeOffset InstalledAtUtc { get; set; }
		public List<NativeOwnedFile> Files { get; set; } = new();
	}

	internal sealed class NativeOwnedFile
	{
		public string RelativePath { get; set; } = String.Empty;
		public string InstalledHash { get; set; } = String.Empty;
		public bool Created { get; set; }
		public string? OriginalHash { get; set; }
		public string? OriginalBackupId { get; set; }
	}

	private sealed class NativeJournal
	{
		public int Version { get; set; }
		public string GameBinIdentity { get; set; } = String.Empty;
		public string Operation { get; set; } = String.Empty;
		public long ProjectId { get; set; }
		public string TransactionId { get; set; } = String.Empty;
		public List<NativeJournalFile> Files { get; set; } = new();
	}

	private sealed class NativeJournalFile
	{
		public string RelativePath { get; set; } = String.Empty;
		public bool BeforeExists { get; set; }
		public string? BeforeHash { get; set; }
		public string? StagedHash { get; set; }
		public string? BackupId { get; set; }
	}
}

public sealed class ReduxGameDirectoryInstallTransaction : IAsyncDisposable
{
	private ReduxGameDirectoryInstallService? _installer;
	private ReduxGameDirectoryInstallService.NativeInstallPlan? _plan;
	private bool _committed;

	internal ReduxGameDirectoryInstallTransaction(ReduxGameDirectoryInstallService installer, ReduxGameDirectoryInstallService.NativeInstallPlan plan, string reviewText)
	{
		_installer = installer;
		_plan = plan;
		ReviewText = reviewText;
	}

	public string ReviewText { get; }

	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		if (_committed) throw new InvalidOperationException("This native installation transaction was already committed.");
		var installer = _installer ?? throw new ObjectDisposedException(nameof(ReduxGameDirectoryInstallTransaction));
		var plan = _plan ?? throw new ObjectDisposedException(nameof(ReduxGameDirectoryInstallTransaction));
		await installer.CommitAsync(plan, cancellationToken);
		_committed = true;
	}

	public async ValueTask DisposeAsync()
	{
		var installer = Interlocked.Exchange(ref _installer, null);
		var plan = Interlocked.Exchange(ref _plan, null);
		if (installer != null && plan != null) await installer.DiscardStageAsync(plan);
	}
}
