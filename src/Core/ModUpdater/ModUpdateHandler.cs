using DivinityModManager.Models;
using DivinityModManager.ModUpdater.Cache;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace DivinityModManager.ModUpdater;

public class ModUpdateHandler : ReactiveObject
{
	private readonly NexusModsCacheHandler _nexus;
	public NexusModsCacheHandler Nexus => _nexus;
	private readonly ModioCacheHandler _modio;
	public ModioCacheHandler Modio => _modio;

	private readonly SteamWorkshopCacheHandler _workshop;
	public SteamWorkshopCacheHandler Workshop => _workshop;

	private readonly GithubModsCacheHandler _github;
	public GithubModsCacheHandler Github => _github;

	[Reactive] public bool IsRefreshing { get; set; }

	public static readonly JsonSerializerSettings DefaultSerializerSettings = new()
	{
		NullValueHandling = NullValueHandling.Ignore,
		Formatting = Formatting.None
	};

	public async Task<bool> UpdateAsync(IEnumerable<DivinityModData> mods, CancellationToken cts)
	{
		IsRefreshing = true;
		try
		{
			if (Workshop.IsEnabled)
			{
				await Workshop.Update(mods, cts);
			}
			if (Nexus.IsEnabled)
			{
				await Nexus.Update(mods, cts);
			}
			if (Modio.IsEnabled)
			{
				await Modio.Update(mods, cts);
			}
			if (Github.IsEnabled)
			{
				await Github.Update(mods, cts);
			}
		}
		finally
		{
			IsRefreshing = false;
		}
		return false;
	}

	public async Task<bool> LoadAsync(IEnumerable<DivinityModData> mods, string currentAppVersion, CancellationToken cts)
	{
		if (Workshop.IsEnabled)
		{
			if ((DateTimeOffset.Now.ToUnixTimeSeconds() - Workshop.CacheData.LastUpdated >= 3600))
			{
				await Workshop.LoadCacheAsync(currentAppVersion, cts);
			}
		}
		if (Nexus.IsEnabled)
		{
			var data = await Nexus.LoadCacheAsync(currentAppVersion, cts);
			foreach (var entry in data.Mods)
			{
				if (Nexus.CacheData.Mods.TryGetValue(entry.Key, out var existing))
				{
					if (existing.UpdatedTimestamp < entry.Value.UpdatedTimestamp || !existing.IsUpdated)
					{
						Nexus.CacheData.Mods[entry.Key] = entry.Value;
					}
				}
				else
				{
					Nexus.CacheData.Mods[entry.Key] = entry.Value;
				}
			}
		}
		if (Modio.IsEnabled)
		{
			var data = await Modio.LoadCacheAsync(currentAppVersion, cts);
			if (data != null)
			{
				Modio.CacheData = data;
			}
		}
		if (Github.IsEnabled)
		{
			await Github.LoadCacheAsync(currentAppVersion, cts);
		}

		await Observable.Start(() =>
		{
			foreach (var mod in mods)
			{
				if (Workshop.IsEnabled)
				{
					if (Workshop.CacheData.Mods.TryGetValue(mod.UUID, out var workshopData))
					{
						if (string.IsNullOrEmpty(mod.WorkshopData.ID) || mod.WorkshopData.ID == workshopData.WorkshopID)
						{
							mod.WorkshopData.ID = workshopData.WorkshopID;
							mod.WorkshopData.CreatedDate = DateUtils.UnixTimeStampToDateTime(workshopData.Created);
							mod.WorkshopData.UpdatedDate = DateUtils.UnixTimeStampToDateTime(workshopData.LastUpdated);
							mod.WorkshopData.Tags = workshopData.Tags;
							mod.AddTags(workshopData.Tags);
							if (workshopData.LastUpdated > 0)
							{
								mod.LastUpdated = mod.WorkshopData.UpdatedDate;
							}
						}
					}
				}
				if (Nexus.IsEnabled)
				{
					if (Nexus.CacheData.Mods.TryGetValue(mod.UUID, out var nexusData))
					{
						mod.NexusModsData.Update(nexusData);
					}
				}
				if (Modio.IsEnabled
					&& Modio.CacheData.Mods.TryGetValue(mod.UUID, out var modioData))
				{
					if (ModioCacheHandler.IsCachedAssociationCompatible(mod, modioData))
					{
						mod.ModioData.Update(modioData);
					}
					else
					{
						Modio.CacheData.Mods.Remove(mod.UUID);
					}
				}
				if (Github.IsEnabled)
				{
					if (Github.CacheData.Mods.TryGetValue(mod.UUID, out var githubData))
					{
						mod.GithubData.Update(githubData);
					}
				}
			}
			return Unit.Default;
		}, RxApp.MainThreadScheduler);

		return false;
	}

	public async Task<bool> SaveAsync(IEnumerable<DivinityModData> mods, string currentAppVersion, CancellationToken cts)
	{
		if (Workshop.IsEnabled)
		{
			await Workshop.SaveCacheAsync(true, currentAppVersion, cts);
		}
		if (Nexus.IsEnabled)
		{
			foreach (var mod in mods.Where(x => x.NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START).Select(x => x.NexusModsData))
			{
				Nexus.CacheData.Mods[mod.UUID] = mod;
			}
			await Nexus.SaveCacheAsync(true, currentAppVersion, cts);
		}
		if (Modio.IsEnabled)
		{
			foreach (var mod in mods.Where(mod => mod.ModioData.HasMetadata))
			{
				Modio.CacheData.Mods[mod.UUID] = mod.ModioData;
			}
			await Modio.SaveCacheAsync(true, currentAppVersion, cts);
		}
		if (Github.IsEnabled)
		{
			await Github.SaveCacheAsync(true, currentAppVersion, cts);
		}
		return false;
	}

	public bool DeleteCache()
	{
		return Nexus.DeleteCache() || Modio.DeleteCache() || Workshop.DeleteCache() || Github.DeleteCache();
	}

	public ModUpdateHandler()
	{
		_nexus = new NexusModsCacheHandler();
		_modio = new ModioCacheHandler();
		_workshop = new SteamWorkshopCacheHandler();
		_github = new GithubModsCacheHandler();
	}
}
