namespace DivinityModManager.Models.Extender;

public static class ExtenderVersionSelection
{
	// Refreshing the list is presentation only: never weaken or replace a saved pin.
	public static ScriptExtenderUpdateVersion Resolve(ScriptExtenderUpdateConfig config,
		IEnumerable<ScriptExtenderUpdateVersion> versions, ScriptExtenderUpdateVersion latest)
	{
		if (String.IsNullOrEmpty(config.TargetVersion) && String.IsNullOrEmpty(config.TargetResourceDigest)) return latest;
		return versions.FirstOrDefault(v =>
			(String.IsNullOrEmpty(config.TargetVersion) || v.Version == config.TargetVersion) &&
			(String.IsNullOrEmpty(config.TargetResourceDigest) || v.Digest == config.TargetResourceDigest))
			?? new ScriptExtenderUpdateVersion { Version = config.TargetVersion, Digest = config.TargetResourceDigest, IsUnavailablePin = true,
				Notice = "Saved pin is unavailable in the current version list." };
	}
}
