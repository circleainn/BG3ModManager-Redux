using DivinityModManager;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Metadata;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Util;

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
		RegressionAssert.True(mod.NexusModsData.HasMetadata);
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
}
