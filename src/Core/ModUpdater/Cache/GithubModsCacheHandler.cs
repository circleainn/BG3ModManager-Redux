using DivinityModManager.Models;
using DivinityModManager.Models.Cache;

using Newtonsoft.Json;

namespace DivinityModManager.ModUpdater.Cache;

public class GithubModsCacheHandler : IExternalModCacheHandler<GithubModsCachedData>
{
	public ModSourceType SourceType => ModSourceType.GITHUB;
	public string FileName => "githubdata.json";

	//Format Github data so people can more easily edit/add mods manually.
	public JsonSerializerSettings SerializerSettings => new()
	{
		NullValueHandling = NullValueHandling.Ignore,
		Formatting = Formatting.Indented,
	};

	public bool IsEnabled { get; set; }
	public GithubModsCachedData CacheData { get; set; }

	public GithubModsCacheHandler() : base()
	{
		CacheData = new GithubModsCachedData();
	}

	public Task<bool> Update(IEnumerable<DivinityModData> mods, CancellationToken cts) => Task.FromResult(false);
}
