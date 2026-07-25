using DivinityModManager.Models;
using DivinityModManager.Models.Modio;

using Newtonsoft.Json;

using System.Net.Http;
using System.Text.RegularExpressions;

namespace DivinityModManager.Util;

/// <summary>
/// Minimal read-only mod.io metadata client. It never downloads or modifies
/// installed mods and only accepts records validated against PublishHandle.
/// </summary>
public static class ModioDataLoader
{
	private const string ApiBaseUrl = "https://api.mod.io/v1";
	private const string Bg3GameSlug = "baldursgate3";
	private static readonly HttpClient Client = new();
	private static long _bg3GameId;

	public static async Task<ModioModData> LoadModDataAsync(DivinityModData mod, string apiKey, CancellationToken cancellationToken)
	{
		if (mod == null || mod.PublishHandle == 0 || String.IsNullOrWhiteSpace(apiKey))
		{
			return null;
		}

		var gameId = await GetBg3GameIdAsync(apiKey, cancellationToken);
		if (gameId <= 0)
		{
			return null;
		}

		var modId = mod.PublishHandle;
		var apiKeyParameter = Uri.EscapeDataString(apiKey);
		var gameApiBaseUrl = $"https://g-{gameId}.modapi.io/v1";
		DivinityApp.Log($"Using mod.io game API for game {gameId}.");

		// BG3's PublishHandle commonly identifies the installed mod.io file/release,
		// rather than the parent mod record. Resolve that relationship first.
		var fileLookupUrl = $"{gameApiBaseUrl}/games/{gameId}/mods?api_key={apiKeyParameter}&modfile={modId}&_limit=10";
		using var fileLookupResponse = await Client.GetAsync(fileLookupUrl, cancellationToken);
		if (fileLookupResponse.IsSuccessStatusCode)
		{
			var fileLookupJson = await fileLookupResponse.Content.ReadAsStringAsync(cancellationToken);
			var fileMatches = JsonConvert.DeserializeObject<ModioListResponse<ModioModData>>(fileLookupJson)?.Data
				.Where(result => result.ModFile?.FileId == (long)modId && NamesMatch(mod, result))
				.ToList() ?? new List<ModioModData>();

			if (fileMatches.Count == 1)
			{
				fileMatches[0].UUID = mod.UUID;
				return fileMatches[0];
			}

			if (fileMatches.Count > 1)
			{
				DivinityApp.Log($"Rejected ambiguous mod.io file match for '{mod.DisplayName}' (PublishHandle {modId}).");
				return null;
			}
		}
		else
		{
			DivinityApp.Log($"mod.io file lookup failed for PublishHandle {modId}: HTTP {(int)fileLookupResponse.StatusCode}");
		}

		// Compatibility fallback for mods whose PublishHandle is the parent mod ID.
		var directLookupUrl = $"{gameApiBaseUrl}/games/{gameId}/mods/{modId}?api_key={apiKeyParameter}";
		using var response = await Client.GetAsync(directLookupUrl, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			DivinityApp.Log($"mod.io direct lookup failed for PublishHandle {modId}: HTTP {(int)response.StatusCode}");
			return null;
		}

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		var result = JsonConvert.DeserializeObject<ModioModData>(json);
		if (result == null || result.ModId != (long)modId || !NamesMatch(mod, result))
		{
			DivinityApp.Log($"Rejected unverified mod.io metadata match for '{mod.DisplayName}' (PublishHandle {modId}).");
			return null;
		}

		result.UUID = mod.UUID;
		return result;
	}

	public static async Task<ModioModData> LoadModDataByProjectIdAsync(
		DivinityModData mod,
		long projectId,
		string apiKey,
		CancellationToken cancellationToken)
	{
		if (mod == null || projectId <= 0 || String.IsNullOrWhiteSpace(apiKey))
		{
			return null;
		}

		var gameId = await GetBg3GameIdAsync(apiKey, cancellationToken);
		if (gameId <= 0)
		{
			return null;
		}

		var apiKeyParameter = Uri.EscapeDataString(apiKey);
		var gameApiBaseUrl = $"https://g-{gameId}.modapi.io/v1";
		var directLookupUrl = $"{gameApiBaseUrl}/games/{gameId}/mods/{projectId}?api_key={apiKeyParameter}";
		using var response = await Client.GetAsync(directLookupUrl, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			DivinityApp.Log($"mod.io creator-manifest lookup failed for project {projectId}: HTTP {(int)response.StatusCode}");
			return null;
		}

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		var result = JsonConvert.DeserializeObject<ModioModData>(json);
		if (result == null || result.ModId != projectId || (result.GameId > 0 && result.GameId != gameId))
		{
			DivinityApp.Log($"Rejected an invalid mod.io creator-manifest response for project {projectId}.");
			return null;
		}

		result.UUID = mod.UUID;
		return result;
	}

	private static async Task<long> GetBg3GameIdAsync(string apiKey, CancellationToken cancellationToken)
	{
		if (_bg3GameId > 0)
		{
			return _bg3GameId;
		}

		var url = $"{ApiBaseUrl}/games?api_key={Uri.EscapeDataString(apiKey)}&name_id={Bg3GameSlug}&_limit=1";
		using var response = await Client.GetAsync(url, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			DivinityApp.Log($"Unable to resolve the Baldur's Gate 3 mod.io game record: HTTP {(int)response.StatusCode}");
			return 0;
		}

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		var games = JsonConvert.DeserializeObject<ModioListResponse<ModioGameData>>(json);
		_bg3GameId = games?.Data?.FirstOrDefault(game => String.Equals(game.NameId, Bg3GameSlug, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
		return _bg3GameId;
	}

	private static bool NamesMatch(DivinityModData localMod, ModioModData onlineMod)
	{
		var onlineName = NormalizeName(onlineMod.Name);
		return onlineName.Length > 0
			&& (onlineName == NormalizeName(localMod.Name) || onlineName == NormalizeName(localMod.DisplayName));
	}

	private static string NormalizeName(string value)
	{
		return Regex.Replace(value ?? String.Empty, "[^a-z0-9]", String.Empty, RegexOptions.IgnoreCase).ToLowerInvariant();
	}
}

internal class ModioListResponse<T>
{
	[JsonProperty("data")]
	public List<T> Data { get; set; } = new();
}

internal class ModioGameData
{
	[JsonProperty("id")]
	public long Id { get; set; }

	[JsonProperty("name_id")]
	public string NameId { get; set; }
}
