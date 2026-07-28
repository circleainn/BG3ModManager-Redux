using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Cache;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace DivinityModManager.ModUpdater.Cache;

public class NexusModsCacheHandler : IExternalModCacheHandler<NexusModsCachedData>
{
	public ModSourceType SourceType => ModSourceType.NEXUSMODS;
	public string FileName => "nexusmodsdata.json";
	public JsonSerializerSettings SerializerSettings => ModUpdateHandler.DefaultSerializerSettings;
	public bool IsEnabled { get; set; } = false;
	public NexusModsCachedData CacheData { get; set; }

	public string APIKey { get; set; }
	public string AppName { get; set; }
	public string AppVersion { get; set; }

	public NexusModsCacheHandler() : base()
	{
		CacheData = new NexusModsCachedData();
	}

	public static bool IsCachedAssociationCompatible(
		DivinityModData mod,
		NexusModsModData data)
	{
		if (mod == null
			|| data == null
			|| data.MetadataOrigin != NexusMetadataOrigin.CreatorManifest)
		{
			return true;
		}

		if (mod.NexusModsData.MetadataOrigin is NexusMetadataOrigin.Manual
			or NexusMetadataOrigin.ManualUnlinked
			|| mod.ModioData?.HasMetadata == true)
		{
			return false;
		}

		var source = mod.CreatorManifest?.IsValid == true
			? mod.CreatorManifest.Sources.FirstOrDefault(candidate =>
				candidate.Service == ReduxCreatorManifestService.NexusSourceService)
			: null;
		return source != null && source.ProjectId == data.ModId;
	}

	public async Task<bool> Update(IEnumerable<DivinityModData> mods, CancellationToken cts)
	{
		if (!IsEnabled)
		{
			DivinityApp.Log("Nexus Mods metadata lookup skipped because the provider is disabled.");
			return false;
		}

		if (!NexusModsDataLoader.IsInitialized && !string.IsNullOrEmpty(APIKey))
		{
			NexusModsDataLoader.Init(APIKey, AppName, AppVersion);
		}

		if (NexusModsDataLoader.CanFetchData)
		{
			var result = await NexusModsDataLoader.LoadAllModsDataAsync(mods, cts);

			if (result.Success)
			{
				DivinityApp.Log($"Fetched NexusMods mod info for {result.UpdatedMods.Count} mod(s).");

				foreach (var mod in mods.Where(x => x.NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START).Select(x => x.NexusModsData))
				{
					CacheData.Mods[mod.UUID] = mod;
				}

				return true;
			}
			else
			{
				DivinityApp.Log($"Failed to update NexusMods mod info:\n{result.FailureMessage}");
			}
		}
		else
		{
			DivinityApp.Log("NexusModsAPIKey not set, or daily/hourly limit reached. Skipping.");
		}
		return false;
	}
}
