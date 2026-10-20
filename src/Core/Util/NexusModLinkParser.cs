using System.Globalization;

namespace DivinityModManager.Util;

/// <summary>Parses a BG3 Nexus project page or an explicitly entered project ID.</summary>
public static class NexusModLinkParser
{
	public static bool TryParseProjectId(string value, out long projectId)
	{
		projectId = 0;
		var input = value?.Trim();
		if (String.IsNullOrEmpty(input)) return false;
		if (TryParsePositiveId(input, out projectId)) return true;

		if (input.StartsWith("nexusmods.com/", StringComparison.OrdinalIgnoreCase)
			|| input.StartsWith("www.nexusmods.com/", StringComparison.OrdinalIgnoreCase))
			input = "https://" + input;

		if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
			|| !uri.IsWellFormedOriginalString()
			|| (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
			|| !String.IsNullOrEmpty(uri.UserInfo)
			|| !uri.IsDefaultPort
			|| !(String.Equals(uri.Host, "nexusmods.com", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(uri.Host, "www.nexusmods.com", StringComparison.OrdinalIgnoreCase)))
			return false;

		var segments = uri.AbsolutePath.TrimEnd('/').Split('/');
		return segments.Length == 4
			&& String.Equals(segments[1], "baldursgate3", StringComparison.OrdinalIgnoreCase)
			&& String.Equals(segments[2], "mods", StringComparison.OrdinalIgnoreCase)
			&& TryParsePositiveId(segments[3], out projectId);
	}

	private static bool TryParsePositiveId(string value, out long projectId)
	{
		if (Int64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
			&& parsed >= DivinityApp.NEXUSMODS_MOD_ID_START)
		{
			projectId = parsed;
			return true;
		}
		projectId = 0;
		return false;
	}
}
