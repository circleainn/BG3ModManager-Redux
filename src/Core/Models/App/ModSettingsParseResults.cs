namespace DivinityModManager.Models.App;

public enum ModSettingsReadStatus
{
	Success,
	Missing,
	Failed
}

/// <summary>A failed read never represents an empty game load order.</summary>
public sealed class ModSettingsParseResults
{
	public ModSettingsReadStatus Status { get; }
	public bool Succeeded => Status == ModSettingsReadStatus.Success;
	public string ErrorMessage { get; }
	public IReadOnlyList<DivinityProfileActiveModData> ActiveMods { get; }
	public string? SourceSha256 { get; }

	private ModSettingsParseResults(ModSettingsReadStatus status,
		IReadOnlyList<DivinityProfileActiveModData> activeMods, string errorMessage, string? sourceSha256 = null)
	{
		Status = status;
		ActiveMods = activeMods;
		ErrorMessage = errorMessage;
		SourceSha256 = sourceSha256;
	}

	public static ModSettingsParseResults Success(IEnumerable<DivinityProfileActiveModData> activeMods, string? sourceSha256 = null) =>
		new(ModSettingsReadStatus.Success, activeMods.ToArray(), String.Empty, sourceSha256);

	public static ModSettingsParseResults Missing() =>
		new(ModSettingsReadStatus.Missing, [], "The game load-order file is missing.");

	public static ModSettingsParseResults Failed(string errorMessage) =>
		new(ModSettingsReadStatus.Failed, [], errorMessage);

	public int CountActive(bool includeIgnoredMods = false)
	{
		var i = 0;
		foreach (var mod in ActiveMods)
		{
			if (includeIgnoredMods || !DivinityApp.IgnoredMods.Lookup( mod.UUID).HasValue)
			{
				i++;
			}
		}
		return i;
	}
}
