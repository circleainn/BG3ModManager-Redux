namespace DivinityModManager.Models.Github;

public class GithubModData : ReactiveObject
{
	[Reactive] public string Author { get; set; }
	[Reactive] public string Repository { get; set; }
	[Reactive] public Uri LatestRelease { get; set; }

	public void Update(GithubModData data)
	{
		Author = data.Author;
		Repository = data.Repository;
		LatestRelease = data.LatestRelease;
	}
}
