using Newtonsoft.Json;

using System.Security.Cryptography;
using System.Text;

namespace DivinityModManager.Util;

/// <summary>
/// Stores provider credentials encrypted for the current Windows account so
/// ordinary Redux settings and their backups remain safe to share.
/// </summary>
public static class ProtectedCredentialStore
{
	private sealed class CredentialData
	{
		public string NexusModsApiKey { get; set; } = String.Empty;
		public string ModioApiKey { get; set; } = String.Empty;
	}

	private static readonly byte[] AdditionalEntropy = SHA256.HashData(
		Encoding.UTF8.GetBytes("BG3ModManager-Redux/provider-credentials/v1"));

	public static bool TryLoad(string path, out string nexusModsApiKey, out string modioApiKey)
	{
		nexusModsApiKey = String.Empty;
		modioApiKey = String.Empty;
		if (!File.Exists(path)) return false;

		try
		{
			var protectedBytes = File.ReadAllBytes(path);
			var clearBytes = ProtectedData.Unprotect(
				protectedBytes,
				AdditionalEntropy,
				DataProtectionScope.CurrentUser);
			try
			{
				var data = JsonConvert.DeserializeObject<CredentialData>(Encoding.UTF8.GetString(clearBytes));
				if (data == null) return false;
				nexusModsApiKey = data.NexusModsApiKey ?? String.Empty;
				modioApiKey = data.ModioApiKey ?? String.Empty;
				return true;
			}
			finally
			{
				CryptographicOperations.ZeroMemory(clearBytes);
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not load protected provider credentials: {ex.Message}");
			return false;
		}
	}

	public static void Save(string path, string nexusModsApiKey, string modioApiKey)
	{
		var json = JsonConvert.SerializeObject(new CredentialData
		{
			NexusModsApiKey = nexusModsApiKey ?? String.Empty,
			ModioApiKey = modioApiKey ?? String.Empty
		});
		var clearBytes = Encoding.UTF8.GetBytes(json);
		try
		{
			var protectedBytes = ProtectedData.Protect(
				clearBytes,
				AdditionalEntropy,
				DataProtectionScope.CurrentUser);
			AtomicFileWriter.WriteAllBytes(path, protectedBytes);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(clearBytes);
		}
	}
}
