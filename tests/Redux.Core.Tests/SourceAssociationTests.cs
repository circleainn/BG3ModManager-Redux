using System;
using System.Windows;

using DivinityModManager;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Cache;
using DivinityModManager.Models.Health;
using DivinityModManager.Models.Metadata;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ModUpdater;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

public sealed class SourceAssociationTests
{
	private const string ReviewedModuleUuid = "069e5871-efe8-44bb-b02a-fe957df5ae0e";
	private const string CommunityModuleUuid = "26922ba9-6018-5252-075d-7ff2ba6ed879";

	public void ReviewedModuleUuidResolvesItsProject()
	{
		var match = ReduxModDatabaseService.TryResolveModuleUuid(ReviewedModuleUuid);

		RegressionAssert.True(match != null);
		RegressionAssert.Equal(3902L, match!.ModId);
		RegressionAssert.True(ReduxModDatabaseService.TryResolveModuleUuid(String.Empty) == null);
		RegressionAssert.True(ReduxModDatabaseService.TryResolveModuleUuid("11111111-1111-1111-1111-111111111111") == null);
	}

	public void CommunityModuleUuidResolvesItsDependencySource()
	{
		var match = ReduxModDatabaseService.TryResolveModuleUuid(CommunityModuleUuid);

		RegressionAssert.True(match != null);
		RegressionAssert.Equal(366L, match!.ModId);
		RegressionAssert.Equal(ReduxOfflineMatchKind.CommunityIdentity, match.Kind);
	}

	public void CommunityIdentityRequiresTheInstalledPackageNameToAgree()
	{
		var mod = CreateMod();
		mod.UUID = CommunityModuleUuid;
		mod.Name = "ImpUI (ImprovedUI)";
		mod.Folder = "ImpUI_P8_Fork_26922ba9-6018-5252-075d-7ff2ba6ed879";

		var match = ReduxModDatabaseService.TryResolveIdentity(mod);

		RegressionAssert.True(match != null);
		RegressionAssert.Equal(366L, match!.ModId);
		RegressionAssert.Equal(ReduxOfflineMatchKind.CommunityIdentity, match.Kind);
	}

	public void CommunityUuidDoesNotRelabelAnUnrelatedLocalPackage()
	{
		var mod = CreateMod();
		mod.UUID = CommunityModuleUuid;
		mod.Name = "Unrelated Local Package";
		mod.Folder = "UnrelatedLocalPackage";

		RegressionAssert.True(ReduxModDatabaseService.TryResolveIdentity(mod) == null);
	}

	public void CommunityProjectNameAndAuthorDoNotBypassUuidCorroboration()
	{
		var mod = CreateMod();
		mod.UUID = "11111111-1111-1111-1111-111111111111";
		mod.Name = "HairUnlocked";
		mod.Author = "ShaneH";
		mod.Folder = "HairUnlocked";

		RegressionAssert.True(ReduxModDatabaseService.TryResolveIdentity(mod) == null);
	}

	public void MissingDependencyOffersReviewedSourceOnlyWhenIntegrationsAreEnabled()
	{
		var affectedMod = CreateMod();
		var finding = new ModHealthFinding(
			ModHealthFindingCode.MissingDependency,
			ModHealthSeverity.Error,
			"Missing dependency",
			"A reviewed dependency is not installed.",
			new[] { ReviewedModuleUuid });
		var snapshot = new ModHealthSnapshot(affectedMod, new[] { finding });

		var enabled = new ModDiagnosticFindingGroupViewModel(
			finding,
			new[] { snapshot },
			Array.Empty<DivinityModData>(),
			sourceIntegrationsEnabled: true);
		RegressionAssert.True(enabled.CanOpenRelatedDependencySource);
		RegressionAssert.Contains(enabled.PrimaryRelatedSourceUrl, "/baldursgate3/mods/3902");

		var localOnly = new ModDiagnosticFindingGroupViewModel(
			finding,
			new[] { snapshot },
			Array.Empty<DivinityModData>(),
			sourceIntegrationsEnabled: false);
		RegressionAssert.False(localOnly.CanOpenRelatedDependencySource);

		var unknownFinding = new ModHealthFinding(
			ModHealthFindingCode.MissingDependency,
			ModHealthSeverity.Error,
			"Missing dependency",
			"An unknown dependency is not installed.",
			new[] { "11111111-1111-1111-1111-111111111111" });
		var unknown = new ModDiagnosticFindingGroupViewModel(
			unknownFinding,
			new[] { new ModHealthSnapshot(affectedMod, new[] { unknownFinding }) },
			Array.Empty<DivinityModData>(),
			sourceIntegrationsEnabled: true);
		RegressionAssert.False(unknown.CanOpenRelatedDependencySource);
		RegressionAssert.True(unknown.CanCopyRelatedDependencyUuid);
	}

	public void CurrentNexusArchiveNamesResolveTheirProject()
	{
		var camera = NexusModFileVersionData.FromFilePath(
			"True Third-Person Camera - V1.1 23959 1.1 2026-07-21T07-13Z 5eYi6PhcB.zip");
		var shirts = NexusModFileVersionData.FromFilePath(
			"Shirts, Lots Of Shirts - StandardBTs 23751 1.0.0.27 2026-07-06T22-22Z D2O3Y0aeq.zip");

		RegressionAssert.Equal(true, camera.Success);
		RegressionAssert.Equal(23959L, camera.ModId);
		RegressionAssert.Equal(-1L, camera.FileId);
		RegressionAssert.Equal(true, shirts.Success);
		RegressionAssert.Equal(23751L, shirts.ModId);
		RegressionAssert.Equal(-1L, shirts.FileId);
	}

	public void TransitionalNexusArchiveNamesResolveTheirProject()
	{
		var result = NexusModFileVersionData.FromFilePath(
			"KiiiNo CS Preset For Azurite III CS 184183 1 Z3mWyIk3g.rar");

		RegressionAssert.Equal(true, result.Success);
		RegressionAssert.Equal(184183L, result.ModId);
		RegressionAssert.Equal(-1L, result.FileId);
	}

	public void LegacyNexusArchiveNamesResolveTheirProjectWithoutInventingAFileId()
	{
		var result = NexusModFileVersionData.FromFilePath(
			"CET 1.37.1 - Scripting fixes-107-1-37-1-1759193708.zip");

		RegressionAssert.Equal(true, result.Success);
		RegressionAssert.Equal(107L, result.ModId);
		RegressionAssert.Equal(-1L, result.FileId);
	}

	public void UnrelatedNumberedArchiveNamesRemainUnmatched()
	{
		var result = NexusModFileVersionData.FromFilePath("Personal Backup 23959 version 1.zip");

		RegressionAssert.Equal(false, result.Success);
		RegressionAssert.Equal(-1L, result.ModId);
	}

	public void MatchingNexusCreatorAndUploaderUseOneLinkedCreatorLabel()
	{
		var mod = CreateMod();
		mod.NexusModsEnabled = true;
		mod.NexusModsData.Update(new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 23751,
			Name = "Shirts - Lots Of Shirts",
			Author = "BerrySemifreddo",
			UploadedBy = "BerrySemifreddo",
			IsUpdated = true,
			MetadataOrigin = NexusMetadataOrigin.LiveApi
		});

		RegressionAssert.Equal("Created by BerrySemifreddo", mod.Metadata.AuthorActionLabel);
		RegressionAssert.Equal(Visibility.Collapsed, mod.Metadata.LocalAuthorVisibility);
		RegressionAssert.Equal(Visibility.Visible, mod.Metadata.AuthorPageVisibility);
	}

	public void ManualNexusAssociationWinsOverCachedModioMetadata()
	{
		var mod = CreateMod();
		mod.NexusModsData.ModId = 12345;
		mod.NexusModsData.Name = "Manual Nexus project";
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;
		mod.ModioData.ModId = 67890;
		mod.ModioData.Name = "Stale mod.io project";

		RegressionAssert.Equal(ModSourceType.NEXUSMODS, mod.Metadata.SourceType);
		RegressionAssert.Equal("Manual Nexus project", mod.Metadata.Title);
	}

	public void CachedModioMetadataWinsOverAutomaticNexusMetadata()
	{
		var mod = CreateMod();
		mod.NexusModsData.ModId = 12345;
		mod.NexusModsData.Name = "Automatic Nexus project";
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.CreatorManifest;
		mod.ModioData.ModId = 67890;
		mod.ModioData.Name = "Native mod.io project";
		mod.ModioData.MetadataOrigin = ModioMetadataOrigin.NativePackage;

		RegressionAssert.Equal(ModSourceType.MODIO, mod.Metadata.SourceType);
		RegressionAssert.Equal("Native mod.io project", mod.Metadata.Title);
	}

	public void NexusArchiveImportWinsOverNativeModioMetadata()
	{
		var mod = CreateMod();
		mod.NexusModsEnabled = true;
		mod.NexusModsData.Update(new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 23751,
			Name = "Shirts - Lots Of Shirts",
			IsUpdated = true,
			MetadataOrigin = NexusMetadataOrigin.NexusArchiveImport
		});
		mod.ModioData.Update(new ModioModData
		{
			UUID = mod.UUID,
			ModId = 6197684,
			Name = "Shirts, Lots Of Shirts",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		});

		RegressionAssert.Equal(ModSourceType.NEXUSMODS, mod.Metadata.SourceType);
		RegressionAssert.Equal("Shirts - Lots Of Shirts", mod.Metadata.Title);
		RegressionAssert.True(mod.ModioData.HasMetadata);
	}

	public void NexusArchiveCacheBlocksNativeModioDiscoveryAfterRestart()
	{
		var mod = CreateMod();
		var nexusCache = new NexusModsCachedData();
		nexusCache.Mods[mod.UUID] = new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 23751,
			Name = "Persisted Nexus archive project",
			IsUpdated = true,
			MetadataOrigin = NexusMetadataOrigin.NexusArchiveImport
		};
		var modioCache = new ModioCachedData();
		modioCache.Mods[mod.UUID] = new ModioModData
		{
			UUID = mod.UUID,
			ModId = 6197684,
			Name = "Weaker native mod.io match",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		};

		var reloadedNexus = RoundTrip(nexusCache).Mods[mod.UUID];
		var reloadedModio = RoundTrip(modioCache).Mods[mod.UUID];
		mod.NexusModsData.Update(reloadedNexus);

		RegressionAssert.Equal(NexusMetadataOrigin.NexusArchiveImport, mod.NexusModsData.MetadataOrigin);
		RegressionAssert.False(ModioCacheHandler.IsCachedAssociationCompatible(mod, reloadedModio));
		RegressionAssert.Equal(ModSourceType.NEXUSMODS, mod.Metadata.SourceType);
	}

	public void ReduxBundleNexusLinkOverridesOnlyWhenExplicitlyApplied()
	{
		var mod = CreateMod();
		mod.NexusModsEnabled = true;
		mod.ModioData.Update(new ModioModData
		{
			UUID = mod.UUID,
			ModId = 6197684,
			Name = "Native mod.io project",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		});
		var link = new ReduxLoadOrderSourceLink
		{
			ModUuid = mod.UUID,
			Provider = ReduxLoadOrderSourceLink.NexusProvider,
			ProjectId = 23751,
			FileId = 555,
			Name = "Portable Nexus project"
		};

		RegressionAssert.Equal(ModSourceType.MODIO, mod.Metadata.SourceType);
		ReduxLoadOrderSourceService.ApplyToInstalledMod(mod, link);

		RegressionAssert.Equal(ModSourceType.NEXUSMODS, mod.Metadata.SourceType);
		RegressionAssert.Equal(NexusMetadataOrigin.ReduxBundleImport, mod.NexusModsData.MetadataOrigin);
		RegressionAssert.Equal(23751L, mod.NexusModsData.ModId);
		RegressionAssert.False(mod.ModioData.HasMetadata);
	}

	public void PortableSourceLinkUsesTheDisplayedProviderWithoutPrivateUrlData()
	{
		var mod = CreateMod();
		mod.NexusModsData.Update(new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 23751,
			MetadataOrigin = NexusMetadataOrigin.BundledProvenance
		});
		mod.ModioData.Update(new ModioModData
		{
			UUID = mod.UUID,
			ModId = 6197684,
			Name = "Native mod.io project",
			ProfileUrl = "https://mod.io/g/baldursgate3/m/example?token=private#account",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		});

		var link = ReduxLoadOrderSourceService.CreatePortableLink(mod);

		RegressionAssert.Equal(ReduxLoadOrderSourceLink.ModioProvider, link.Provider);
		RegressionAssert.Equal(6197684L, link.ProjectId);
		RegressionAssert.Equal("https://mod.io/g/baldursgate3/m/example", link.PageUrl);
	}

	public void DeletingAnInstalledModRetiresItsRememberedSourceAssociations()
	{
		var handler = new ModUpdateHandler();
		var removedUuid = "52809d6a-2f2f-79d9-843e-4aad7396eac0";
		var retainedUuid = "54ebec6c-00ce-48e1-8b75-53b6b72ecc3a";
		handler.Nexus.CacheData.Mods[removedUuid] = new NexusModsModData
		{
			UUID = removedUuid,
			ModId = 23751,
			MetadataOrigin = NexusMetadataOrigin.NexusArchiveImport
		};
		handler.Modio.CacheData.Mods[removedUuid] = new ModioModData
		{
			UUID = removedUuid,
			ModId = 6197684,
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		};
		handler.Nexus.CacheData.Mods[retainedUuid] = new NexusModsModData
		{
			UUID = retainedUuid,
			ModId = 1420
		};

		var removed = handler.RemoveSourceAssociations([removedUuid]);

		RegressionAssert.Equal(1, removed);
		RegressionAssert.False(handler.Nexus.CacheData.Mods.ContainsKey(removedUuid));
		RegressionAssert.False(handler.Modio.CacheData.Mods.ContainsKey(removedUuid));
		RegressionAssert.True(handler.Nexus.CacheData.Mods.ContainsKey(retainedUuid));
	}

	public void LocalOnlyPresentationHidesProvidersWithoutDeletingCachedMetadata()
	{
		var mod = CreateMod();
		mod.NexusModsData.ModId = 12345;
		mod.NexusModsData.Name = "Cached Nexus project";
		mod.NexusModsData.IsUpdated = true;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;

		mod.OnlineMetadataEnabled = false;

		RegressionAssert.Equal(ModSourceType.NONE, mod.Metadata.SourceType);
		RegressionAssert.Equal("Local", mod.Metadata.SourceLabel);
		RegressionAssert.Equal("Local package metadata", mod.Metadata.LinkStatus);
		RegressionAssert.True(mod.NexusModsData.HasMetadata);
	}

	public void LocalMetadataUsesExplicitUnavailableFallbacks()
	{
		var mod = CreateMod();
		mod.Author = String.Empty;
		mod.Description = String.Empty;
		mod.HasMetadata = false;

		RegressionAssert.Equal("Author unavailable", mod.Metadata.AuthorLabel);
		RegressionAssert.Equal("Version unavailable", mod.Metadata.VersionLabel);
		RegressionAssert.Equal("No local description is available.", mod.Metadata.Summary);
		RegressionAssert.Equal("No local description is available for this mod.", mod.Metadata.Description);
		RegressionAssert.Equal(System.Windows.Visibility.Visible, mod.Metadata.LocalAuthorVisibility);
	}

	public void CreatorManifestModioCacheRequiresTheCurrentProjectClaim()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithModioProject(67890);
		var cached = new ModioModData
		{
			ModId = 67890,
			MetadataOrigin = ModioMetadataOrigin.CreatorManifest
		};

		RegressionAssert.True(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.CreatorManifest = ValidManifestWithModioProject(11111);
		RegressionAssert.False(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.CreatorManifest = ReduxCreatorManifestData.NotPresent;
		RegressionAssert.False(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void ManualSourceChoicesBlockCreatorManifestModioCache()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithModioProject(67890);
		var cached = new ModioModData
		{
			ModId = 67890,
			MetadataOrigin = ModioMetadataOrigin.CreatorManifest
		};

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;
		RegressionAssert.False(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.ManualUnlinked;
		RegressionAssert.False(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void NativeModioCacheDoesNotDependOnCreatorManifest()
	{
		var mod = CreateMod();
		var cached = new ModioModData
		{
			ModId = 67890,
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		};

		RegressionAssert.True(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.ManualUnlinked;
		RegressionAssert.True(ModioCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void CreatorManifestNexusCacheRequiresTheCurrentProjectClaim()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithNexusProject(12345);
		var cached = new NexusModsModData
		{
			ModId = 12345,
			IsUpdated = true,
			MetadataOrigin = NexusMetadataOrigin.CreatorManifest
		};

		RegressionAssert.True(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.CreatorManifest = ValidManifestWithNexusProject(11111);
		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.CreatorManifest = ReduxCreatorManifestData.NotPresent;
		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void ManualAndNativeSourceChoicesBlockCreatorManifestNexusCache()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithNexusProject(12345);
		var cached = new NexusModsModData
		{
			ModId = 12345,
			IsUpdated = true,
			MetadataOrigin = NexusMetadataOrigin.CreatorManifest
		};

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Manual;
		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.ManualUnlinked;
		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.Unknown;
		mod.ModioData.ModId = 67890;
		mod.ModioData.MetadataOrigin = ModioMetadataOrigin.NativePackage;
		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void NonManifestNexusCacheDoesNotDependOnCreatorManifest()
	{
		var mod = CreateMod();
		var cached = new NexusModsModData
		{
			ModId = 12345,
			IsUpdated = true,
			MetadataOrigin = NexusMetadataOrigin.BundledProvenance
		};

		RegressionAssert.True(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.ManualUnlinked;
		mod.ModioData.ModId = 67890;
		RegressionAssert.True(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void ValidCreatorManifestNexusCacheSurvivesRestart()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithNexusProject(12345);
		var cache = new NexusModsCachedData();
		cache.Mods[mod.UUID] = new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 12345,
			Name = "Manifest-linked Nexus project",
			MetadataOrigin = NexusMetadataOrigin.CreatorManifest
		};

		var reloaded = RoundTrip(cache);
		var cached = reloaded.Mods[mod.UUID];

		RegressionAssert.Equal(NexusMetadataOrigin.CreatorManifest, cached.MetadataOrigin);
		RegressionAssert.True(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));

		mod.NexusModsData.Update(cached);
		RegressionAssert.True(mod.NexusModsData.HasMetadata);
		RegressionAssert.Equal(12345L, mod.NexusModsData.ModId);
		RegressionAssert.Equal(NexusMetadataOrigin.CreatorManifest, mod.NexusModsData.MetadataOrigin);
	}

	public void ChangedCreatorManifestInvalidatesReloadedNexusCache()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithNexusProject(12345);
		var cache = new NexusModsCachedData();
		cache.Mods[mod.UUID] = new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 12345,
			MetadataOrigin = NexusMetadataOrigin.CreatorManifest
		};

		var cached = RoundTrip(cache).Mods[mod.UUID];
		mod.CreatorManifest = ValidManifestWithNexusProject(54321);

		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, cached));
	}

	public void NativeModioCacheWinsOverCreatorManifestNexusAfterRestart()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithNexusProject(12345);

		var modioCache = new ModioCachedData();
		modioCache.Mods[mod.UUID] = new ModioModData
		{
			UUID = mod.UUID,
			ModId = 67890,
			Name = "Native mod.io project",
			MetadataOrigin = ModioMetadataOrigin.NativePackage
		};
		var nexusCache = new NexusModsCachedData();
		nexusCache.Mods[mod.UUID] = new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 12345,
			MetadataOrigin = NexusMetadataOrigin.CreatorManifest
		};

		var reloadedModio = RoundTrip(modioCache).Mods[mod.UUID];
		var reloadedNexus = RoundTrip(nexusCache).Mods[mod.UUID];
		mod.ModioData.Update(reloadedModio);

		RegressionAssert.Equal(ModioMetadataOrigin.NativePackage, mod.ModioData.MetadataOrigin);
		RegressionAssert.False(NexusModsCacheHandler.IsCachedAssociationCompatible(mod, reloadedNexus));
		RegressionAssert.Equal(ModSourceType.MODIO, mod.Metadata.SourceType);
	}

	public void ManualNexusCacheBlocksCreatorManifestModioAfterRestart()
	{
		var mod = CreateMod();
		mod.CreatorManifest = ValidManifestWithModioProject(67890);

		var nexusCache = new NexusModsCachedData();
		nexusCache.Mods[mod.UUID] = new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 12345,
			Name = "Manually linked Nexus project",
			MetadataOrigin = NexusMetadataOrigin.Manual
		};
		var modioCache = new ModioCachedData();
		modioCache.Mods[mod.UUID] = new ModioModData
		{
			UUID = mod.UUID,
			ModId = 67890,
			MetadataOrigin = ModioMetadataOrigin.CreatorManifest
		};

		var reloadedNexus = RoundTrip(nexusCache).Mods[mod.UUID];
		var reloadedModio = RoundTrip(modioCache).Mods[mod.UUID];
		mod.NexusModsData.Update(reloadedNexus);

		RegressionAssert.Equal(NexusMetadataOrigin.Manual, mod.NexusModsData.MetadataOrigin);
		RegressionAssert.False(ModioCacheHandler.IsCachedAssociationCompatible(mod, reloadedModio));
		RegressionAssert.Equal(ModSourceType.NEXUSMODS, mod.Metadata.SourceType);
	}

	private static T RoundTrip<T>(T value)
	{
		var json = JsonConvert.SerializeObject(value, ModUpdateHandler.DefaultSerializerSettings);
		var result = JsonConvert.DeserializeObject<T>(json, ModUpdateHandler.DefaultSerializerSettings);
		if (result == null)
		{
			throw new InvalidOperationException("Cache round-trip returned null.");
		}
		return result;
	}

	private static DivinityModData CreateMod() => new RegressionModData
	{
		UUID = "7a1731b4-1cc9-4495-9f4f-4e47c3eaf2ef",
		Name = "Local module",
		Author = "Local author",
		Folder = "LocalModule",
		HasMetadata = true,
		OnlineMetadataEnabled = true
	};

	private static ReduxCreatorManifestData ValidManifestWithModioProject(long projectId) => new()
	{
		State = ReduxCreatorManifestState.Valid,
		Sources = new[]
		{
			new ReduxCreatorSourceClaim(ReduxCreatorManifestService.ModioSourceService, projectId, null)
		}
	};

	private static ReduxCreatorManifestData ValidManifestWithNexusProject(long projectId) => new()
	{
		State = ReduxCreatorManifestState.Valid,
		Sources = new[]
		{
			new ReduxCreatorSourceClaim(ReduxCreatorManifestService.NexusSourceService, projectId, null)
		}
	};
}
