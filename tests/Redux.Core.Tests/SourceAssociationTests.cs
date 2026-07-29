using System;

using DivinityModManager;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Cache;
using DivinityModManager.Models.Metadata;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ModUpdater;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

public sealed class SourceAssociationTests
{
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
