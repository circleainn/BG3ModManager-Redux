using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Models.Updates;
//using DivinityModManager.ModUpdater.NexusMods;

using NexusModsNET;
using NexusModsNET.DataModels;

namespace DivinityModManager.Util;

public class NexusModsRateLimitsUpdatedEventArgs : EventArgs
{
	public NexusApiLimits Limits { get; set; }

	public NexusModsRateLimitsUpdatedEventArgs(NexusApiLimits limits)
	{
		Limits = limits;
	}
}

public delegate void NexusModsRateLimitsUpdatedEventHandler(object sender, NexusModsRateLimitsUpdatedEventArgs e);

public static class NexusModsDataLoader
{
	private static INexusModsClient _client;
	private static bool _isActive = false;
	private static bool _pendingDispose = false;

	private static string _lastApiKey = "";

	public static INexusModsClient Client => _client;

	public static event NexusModsRateLimitsUpdatedEventHandler RateLimitsUpdated;

	public static void Init(string apiKey, string appName, string appVersion)
	{
		if (!String.IsNullOrEmpty(apiKey) && apiKey != _lastApiKey)
		{
			if (Dispose())
			{
				_lastApiKey = apiKey;
				_client = NexusModsClient.Create(apiKey, appName, appVersion);
				//_client = new NexusModsCustomClient(apiKey, appName, appVersion);
				//RateLimitsUpdated ?.Invoke(_client, new NexusModsRateLimitsUpdatedEventArgs(_client.RateLimitsManagement.APILimits));
			}
		}
	}

	public static void EmitLimitsChanged(NexusApiLimits limits)
	{
		RateLimitsUpdated?.Invoke(_client, new NexusModsRateLimitsUpdatedEventArgs(limits));
	}

	public static bool Dispose()
	{
		if (!_isActive)
		{
			_client?.Dispose();
			_pendingDispose = false;
			return true;
		}
		_pendingDispose = true;
		return false;
	}

	public static bool CanFetchData => _client != null && !_client.RateLimitsManagement.ApiDailyLimitExceeded() && !_client.RateLimitsManagement.ApiHourlyLimitExceeded();
	public static bool LimitExceeded => _client != null && (_client.RateLimitsManagement.ApiDailyLimitExceeded() || !_client.RateLimitsManagement.ApiHourlyLimitExceeded());
	public static bool IsInitialized => _client != null;

	private static bool LimitExceededCheck()
	{
		if (_client != null)
		{
			var daily = _client.RateLimitsManagement.ApiDailyLimitExceeded();
			var hourly = _client.RateLimitsManagement.ApiHourlyLimitExceeded();

			if (daily)
			{
				DivinityApp.Log($"Daily limit exceeded ({_client.RateLimitsManagement.APILimits.DailyLimit})");
				return true;
			}
			else if (hourly)
			{
				DivinityApp.Log($"Hourly limit exceeded ({_client.RateLimitsManagement.APILimits.HourlyLimit})");
				return true;
			}
		}
		return false;
	}

	public static bool CanDoTask(int apiCalls)
	{
		if (_client != null)
		{
			var currentLimit = Math.Min(_client.RateLimitsManagement.APILimits.HourlyRemaining, _client.RateLimitsManagement.APILimits.DailyRemaining);
			if (currentLimit > apiCalls)
			{
				return true;
			}
		}
		return false;
	}

	private static void OnTaskDone()
	{
		_isActive = false;
		if (_pendingDispose) Dispose();
	}

	public static async Task<List<NexusModsModDownloadLink>> GetLatestDownloadsForMods(List<DivinityModData> mods, CancellationToken t)
	{
		var links = new List<NexusModsModDownloadLink>();
		if (!CanFetchData || mods.Count <= 0) return links;
		_isActive = true;

		try
		{
			var apiCallAmount = mods.Count(x => x.NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START) & 2;
			if (!CanDoTask(apiCallAmount))
			{
				var apiAmounts = _client.RateLimitsManagement.APILimits;

				DivinityApp.Log($"Task would exceed hourly or daily API limits. ExpectedCalls({apiCallAmount}) HourlyRemaining({apiAmounts.HourlyRemaining}/{apiAmounts.HourlyLimit}) DailyRemaining({apiAmounts.DailyRemaining}/{apiAmounts.DailyLimit})");
				OnTaskDone();
				return links;
			}
			// InfosInquirer.Dispose also disposes the shared API client. The loader owns
			// that client lifetime, so keep the inquirer alive for this request only.
			var dataLoader = new InfosInquirer(_client);
			foreach (var mod in mods)
			{
				if (mod.NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START)
				{
					var result = await dataLoader.ModFiles.GetModFilesAsync(DivinityApp.NEXUSMODS_GAME_DOMAIN, mod.NexusModsData.ModId, t);
					if (result != null)
					{
						var file = result.ModFiles.FirstOrDefault(x => x.IsPrimary);
						if (file != null)
						{
							var fileId = file.FileId;
							var linkResult = await dataLoader.ModFiles.GetModFileDownloadLinksAsync(DivinityApp.NEXUSMODS_GAME_DOMAIN, mod.NexusModsData.ModId, fileId, t);
							if (linkResult != null && linkResult.Count() > 0)
							{
								var primaryLink = linkResult.FirstOrDefault();
								links.Add(new NexusModsModDownloadLink(mod, primaryLink));
							}
						}
					}
				}

				if (t.IsCancellationRequested) break;
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error fetching NexusMods data:\n{ex}");
		}

		OnTaskDone();

		return links;
	}

	public static async Task<UpdateResult> LoadAllModsDataAsync(IEnumerable<DivinityModData> mods, CancellationToken t)
	{
		var taskResult = new UpdateResult();
		if (!CanFetchData)
		{
			taskResult.Success = false;
			if (_client == null)
			{
				taskResult.FailureMessage = "API Client not initialized.";
			}
			else
			{
				var rateLimits = _client.RateLimitsManagement.APILimits;
				taskResult.FailureMessage = $"API limit exceeded. Hourly({rateLimits.HourlyRemaining}/{rateLimits.HourlyLimit}) Daily({rateLimits.DailyRemaining}/{rateLimits.DailyLimit})";
			}
			return taskResult;
		}
		var totalLoaded = 0;

		_isActive = true;

		try
		{
			var targetMods = mods.Where(mod => mod.NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START).ToList();
			var total = targetMods.Count;
			if (total == 0)
			{
				taskResult.Success = false;
				taskResult.FailureMessage = "Skipping. No mods to check (no NexusMods ID set in the loaded mods).";
				return taskResult;
			}

			var apiCallAmount = total; // 1 call for 1 mod
			if (!CanDoTask(total))
			{
				var apiAmounts = _client.RateLimitsManagement.APILimits;

				DivinityApp.Log($"Task would exceed hourly or daily API limits. ExpectedCalls({apiCallAmount}) HourlyRemaining({apiAmounts.HourlyRemaining}/{apiAmounts.HourlyLimit}) DailyRemaining({apiAmounts.DailyRemaining}/{apiAmounts.DailyLimit})");
				OnTaskDone();
				return taskResult;
			}

			DivinityApp.Log($"Using NexusMods API to update {total} mods");

			// InfosInquirer.Dispose also disposes the shared API client. The loader owns
			// that client lifetime, so a following changelog request can reuse it safely.
			var dataLoader = new InfosInquirer(_client);
			foreach (var mod in targetMods)
			{
				var result = await dataLoader.Mods.GetMod(DivinityApp.NEXUSMODS_GAME_DOMAIN, mod.NexusModsData.ModId, t);
				if (result != null)
				{
					var associationOrigin = mod.NexusModsData.MetadataOrigin;
					mod.NexusModsData.Update(result);
					// Live API data enriches the record, but the association origin still
					// explains how Redux connected this installed package to the project.
					mod.NexusModsData.MetadataOrigin = associationOrigin switch
					{
						NexusMetadataOrigin.Manual => NexusMetadataOrigin.Manual,
						NexusMetadataOrigin.BundledProvenance => NexusMetadataOrigin.BundledProvenance,
						_ => NexusMetadataOrigin.LiveApi
					};
					taskResult.UpdatedMods.Add(mod);
					totalLoaded++;
				}

				if (t.IsCancellationRequested) break;
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error fetching NexusMods data:\n{ex}");
		}

		OnTaskDone();

		return taskResult;
	}

	public static async Task<bool> LoadChangelogsAsync(IEnumerable<DivinityModData> mods, CancellationToken t)
	{
		if (!CanFetchData)
		{
			return false;
		}

		var targetMods = mods
			.Where(mod => mod.NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START
				&& !mod.NexusModsData.ChangelogsLoaded)
			.ToList();
		if (targetMods.Count == 0)
		{
			return false;
		}

		_isActive = true;
		var totalLoaded = 0;
		try
		{
			if (!CanDoTask(targetMods.Count))
			{
				var apiAmounts = _client.RateLimitsManagement.APILimits;
				DivinityApp.Log($"Changelog task would exceed hourly or daily Nexus Mods API limits. ExpectedCalls({targetMods.Count}) HourlyRemaining({apiAmounts.HourlyRemaining}/{apiAmounts.HourlyLimit}) DailyRemaining({apiAmounts.DailyRemaining}/{apiAmounts.DailyLimit})");
				return false;
			}

			DivinityApp.Log($"Using Nexus Mods API to load changelogs for {targetMods.Count} mod(s)");
			var dataLoader = new InfosInquirer(_client);
			foreach (var mod in targetMods)
			{
				if (t.IsCancellationRequested)
				{
					break;
				}

				try
				{
					var result = await dataLoader.Mods.GetModChangelogs(DivinityApp.NEXUSMODS_GAME_DOMAIN, mod.NexusModsData.ModId, t);
					mod.NexusModsData.SetChangelogs(result ?? new Dictionary<string, IEnumerable<string>>());
					totalLoaded++;
				}
				catch (Exception ex)
				{
					DivinityApp.Log($"Error fetching Nexus Mods changelog for mod ID {mod.NexusModsData.ModId}:\n{ex}");
				}
			}
		}
		finally
		{
			OnTaskDone();
		}

		return totalLoaded > 0;
	}
}
