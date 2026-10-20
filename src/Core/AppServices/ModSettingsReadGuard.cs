using DivinityModManager.Models.App;

using System.Security.Cryptography;

namespace DivinityModManager.AppServices;

/// <summary>Checks that a reviewed game-order file still has the same bytes before writing.</summary>
public static class ModSettingsReadGuard
{
	public static bool MatchesCurrentFile(ModSettingsParseResults read, string path)
	{
		if (read == null || read.Status == ModSettingsReadStatus.Failed
			|| read.Succeeded && String.IsNullOrWhiteSpace(read.SourceSha256)) return false;
		try
		{
			// File.Exists cannot distinguish a missing file from an inaccessible file.
			// Opening also prevents hashing an intermediate state while a writer owns it.
			using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return read.Succeeded && String.Equals(read.SourceSha256,
				Convert.ToHexString(SHA256.HashData(input)), StringComparison.OrdinalIgnoreCase);
		}
		catch (FileNotFoundException) { return read.Status == ModSettingsReadStatus.Missing; }
		catch (DirectoryNotFoundException) { return read.Status == ModSettingsReadStatus.Missing; }
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not recheck the reviewed game load order '{path}': {ex}");
			return false;
		}
	}
}
