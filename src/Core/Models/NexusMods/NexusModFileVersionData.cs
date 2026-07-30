using System.Text.RegularExpressions;

namespace DivinityModManager.Models.NexusMods;

public struct NexusModFileVersionData
{
	public long ModId { get; set; }
	public long FileId { get; set; }
	public bool Success { get; set; }

	// Nexus has used three identifier-bearing download-name families:
	//   name-modId-version-timestamp
	//   name modId version slug
	//   name modId version ISO-timestamp slug
	// Parse the space-delimited formats first so the date segments in current
	// names can never be mistaken for a legacy hyphen-delimited project ID.
	private static readonly Regex _currentFilePattern = new(
		@"(?:^|\s)(?<modId>[1-9]\d*)\s+\S+(?:\s+\d{4}-\d{2}-\d{2}T\d{2}-\d{2}(?:-\d{2})?Z)?\s+[A-Za-z0-9]{6,32}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex _legacyFilePattern = new(
		@"^.*?-(?<modId>[1-9]\d*)-\S+",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static NexusModFileVersionData FromFilePath(string path)
	{
		var name = Path.GetFileNameWithoutExtension(path);
		var currentMatch = _currentFilePattern.Match(name);
		if (currentMatch.Success
			&& Int64.TryParse(currentMatch.Groups["modId"].Value, out var currentModId))
		{
			return new NexusModFileVersionData
			{
				ModId = currentModId,
				FileId = -1,
				Success = true
			};
		}

		var match = _legacyFilePattern.Match(name);

		var parsedLegacyId = match.Success
			&& Int64.TryParse(match.Groups["modId"].Value, out var legacyModId)
				? legacyModId
				: -1;

		return new NexusModFileVersionData()
		{
			ModId = parsedLegacyId,
			// Nexus-generated archive names identify the project, not its numeric
			// file record. Legacy trailing numbers are upload timestamps.
			FileId = -1,
			Success = parsedLegacyId >= DivinityApp.NEXUSMODS_MOD_ID_START
		};
	}
}
