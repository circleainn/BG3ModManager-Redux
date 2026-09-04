using DivinityModManager.Models;
using DivinityModManager.AppServices;
using DivinityModManager.Models.Cache;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace DivinityModManager.ModUpdater.Cache;

public class ModioCacheHandler : IExternalModCacheHandler<ModioCachedData>
{
	public ModSourceType SourceType => ModSourceType.MODIO;
	public string FileName => "modiodata.json";
	public JsonSerializerSettings SerializerSettings => ModUpdateHandler.DefaultSerializerSettings;
	public bool IsEnabled { get; set; }
	public ModioCachedData CacheData { get; set; } = new();
	public string APIKey { get; set; }

	public async Task<bool> Update(IEnumerable<DivinityModData> mods, CancellationToken cancellationToken)
	{
		if (!IsEnabled || String.IsNullOrWhiteSpace(APIKey))
		{
			DivinityApp.Log("mod.io metadata lookup skipped because the provider is disabled or no API key is configured.");
			return false;
		}

		var candidates = mods
			.Where(mod => !mod.ModioData.HasMetadata
				&& mod.NexusModsData.MetadataOrigin != NexusMetadataOrigin.Manual
				&& mod.NexusModsData.MetadataOrigin != NexusMetadataOrigin.ReduxBundleImport
				&& (mod.PublishHandle > 0
					|| mod.NexusModsData.MetadataOrigin != NexusMetadataOrigin.ManualUnlinked
					&& mod.CreatorManifest?.IsValid == true
						&& mod.CreatorManifest.Sources.Any(source =>
							source.Service == ReduxCreatorManifestService.ModioSourceService)))
			.ToList();
		DivinityApp.Log($"mod.io metadata lookup found {candidates.Count} candidate mod(s).");

		var changed = false;
		foreach (var mod in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				var creatorSource = mod.CreatorManifest?.IsValid == true
					? mod.CreatorManifest.Sources.FirstOrDefault(source =>
						source.Service == ReduxCreatorManifestService.ModioSourceService)
					: null;
				ModioModData data;
				if (mod.PublishHandle > 0)
				{
					DivinityApp.Log($"Requesting mod.io metadata for '{mod.DisplayName}' using PublishHandle {mod.PublishHandle}.");
					data = await ModioDataLoader.LoadModDataAsync(mod, APIKey, cancellationToken);
				}
				else if (creatorSource != null)
				{
					DivinityApp.Log($"Requesting mod.io metadata for '{mod.DisplayName}' using its embedded project identity.");
					data = await ModioDataLoader.LoadModDataByProjectIdAsync(
						mod,
						creatorSource.ProjectId,
						APIKey,
						cancellationToken);
				}
				else continue;
				if (data != null)
				{
					data.MetadataOrigin = mod.PublishHandle > 0
						? ModioMetadataOrigin.NativePackage
						: ModioMetadataOrigin.CreatorManifest;
					mod.ModioData.Update(data);
					CacheData.Mods[mod.UUID] = data;
					changed = true;
					DivinityApp.Log($"Linked mod.io metadata for '{mod.DisplayName}' to mod {data.ModId}.");
				}
			}
			catch (Exception ex)
			{
				DivinityApp.Log($"Error loading mod.io metadata for '{mod.DisplayName}':\n{ex}");
			}
		}

		return changed;
	}

	public static bool IsCachedAssociationCompatible(DivinityModData mod, ModioModData data)
	{
		if (mod?.NexusModsData?.MetadataOrigin == NexusMetadataOrigin.ReduxBundleImport)
		{
			return false;
		}

		if (mod == null || data == null || data.MetadataOrigin != ModioMetadataOrigin.CreatorManifest)
		{
			return true;
		}

		if (mod.NexusModsData.MetadataOrigin is NexusMetadataOrigin.Manual
			or NexusMetadataOrigin.ManualUnlinked)
		{
			return false;
		}

		var source = mod.CreatorManifest?.IsValid == true
			? mod.CreatorManifest.Sources.FirstOrDefault(candidate =>
				candidate.Service == ReduxCreatorManifestService.ModioSourceService)
			: null;
		return source != null && source.ProjectId == data.ModId;
	}
}
