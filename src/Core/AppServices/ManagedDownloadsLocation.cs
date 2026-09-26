namespace DivinityModManager.AppServices;

/// <summary>Validates a user-selected download workspace without touching its existing contents.</summary>
public static class ManagedDownloadsLocation
{
	public static bool TryPrepare(string configuredPath, string defaultPath, out string directory, out string error)
	{
		directory = String.Empty;
		error = String.Empty;
		try
		{
			var requested = String.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath.Trim();
			if (String.IsNullOrWhiteSpace(requested)) throw new ArgumentException("A Downloads folder is required.");
			if (!Path.IsPathFullyQualified(requested))
				throw new ArgumentException("Choose an absolute Downloads folder path.");
			directory = Path.GetFullPath(requested);
			Directory.CreateDirectory(directory);
			var probe = Path.Combine(directory, $".redux-write-check-{Guid.NewGuid():N}.tmp");
			try
			{
				using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
					1, FileOptions.DeleteOnClose);
			}
			finally
			{
				if (File.Exists(probe)) File.Delete(probe);
			}
			return true;
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
		{
			directory = String.Empty;
			error = $"Redux cannot use that Downloads folder: {ex.Message}";
			return false;
		}
	}
}
