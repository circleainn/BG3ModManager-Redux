using System.Globalization;
using System.Text.RegularExpressions;

using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.Util;

public static partial class NexusModManagerLinkParser
{
	public const int MaximumLength = 8192;
	private const int MaximumQueryValueLength = 2048;

	[GeneratedRegex("^/mods/([1-9][0-9]*)/files/([1-9][0-9]*)$", RegexOptions.CultureInvariant)]
	private static partial Regex Bg3PathPattern();

	public static bool TryReadGame(string value, out string game)
	{
		game = null;
		if (!TryCreateNxmUri(value, out var uri, out _)) return false;
		game = uri.IdnHost.ToLowerInvariant();
		return !String.IsNullOrWhiteSpace(game);
	}

	public static bool TryParseBg3(string value, DateTimeOffset now, out NexusModManagerLink link, out string error)
	{
		link = null;
		if (!TryCreateNxmUri(value, out var uri, out error)) return false;
		if (!uri.IdnHost.Equals(DivinityApp.NEXUSMODS_GAME_DOMAIN, StringComparison.OrdinalIgnoreCase))
		{
			error = "This Nexus link is for a different game.";
			return false;
		}

		var pathMatch = Bg3PathPattern().Match(GetRawPath(value));
		if (!pathMatch.Success ||
			!Int64.TryParse(pathMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var modId) ||
			!Int64.TryParse(pathMatch.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var fileId))
		{
			error = "The Nexus link does not identify a valid mod file.";
			return false;
		}

		var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (!String.IsNullOrEmpty(uri.Query))
		{
			foreach (var pair in uri.Query[1..].Split('&'))
			{
				var equalsIndex = pair.IndexOf('=');
				if (equalsIndex <= 0 || equalsIndex == pair.Length - 1 ||
					!TryDecode(pair[..equalsIndex], out var name) ||
					!TryDecode(pair[(equalsIndex + 1)..], out var decodedValue) ||
					decodedValue.Length > MaximumQueryValueLength ||
					name is not ("key" or "expires" or "user_id") ||
					!query.TryAdd(name, decodedValue))
				{
					error = "The Nexus link contains malformed or unexpected authorization data.";
					return false;
				}
			}
		}

		query.TryGetValue("key", out var downloadKey);
		long? expires = null;
		if (query.TryGetValue("expires", out var expiresValue))
		{
			if (!Int64.TryParse(expiresValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedExpires) || parsedExpires <= 0)
			{
				error = "The Nexus link contains an invalid expiry time.";
				return false;
			}
			expires = parsedExpires;
		}
		if ((downloadKey == null) != (expires == null))
		{
			error = "The Nexus link has incomplete download authorization.";
			return false;
		}
		if (expires <= now.ToUnixTimeSeconds())
		{
			error = "The Nexus download authorization has expired.";
			return false;
		}

		long? userId = null;
		if (query.TryGetValue("user_id", out var userIdValue))
		{
			if (!Int64.TryParse(userIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedUserId) || parsedUserId <= 0)
			{
				error = "The Nexus link contains an invalid user identifier.";
				return false;
			}
			userId = parsedUserId;
		}

		link = new NexusModManagerLink(modId, fileId, downloadKey, expires, userId);
		error = null;
		return true;
	}

	public static string Redact(string value)
	{
		if (!TryCreateNxmUri(value, out var uri, out _)) return "nxm://invalid";
		var match = Bg3PathPattern().Match(GetRawPath(value));
		if (!match.Success) return $"nxm://{uri.IdnHost}/invalid";
		var suffix = String.IsNullOrEmpty(uri.Query) ? String.Empty : "?authorization=redacted";
		return $"nxm://{uri.IdnHost.ToLowerInvariant()}/mods/{match.Groups[1].Value}/files/{match.Groups[2].Value}{suffix}";
	}

	private static bool TryCreateNxmUri(string value, out Uri uri, out string error)
	{
		uri = null;
		if (String.IsNullOrWhiteSpace(value))
		{
			error = "The Nexus link is empty.";
			return false;
		}
		if (value.Length > MaximumLength)
		{
			error = "The Nexus link is too long.";
			return false;
		}
		if (!HasValidPercentEncoding(value) ||
			!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
			!uri.Scheme.Equals("nxm", StringComparison.OrdinalIgnoreCase) ||
			String.IsNullOrWhiteSpace(uri.Host) ||
			!String.IsNullOrEmpty(uri.UserInfo) ||
			uri.Port != -1 ||
			!String.IsNullOrEmpty(uri.Fragment))
		{
			error = "Enter a valid nxm:// download link. A Nexus API key belongs in Preferences, not in this link field.";
			return false;
		}

		error = null;
		return true;
	}

	private static string GetRawPath(string value)
	{
		var authorityStart = value.IndexOf("://", StringComparison.Ordinal) + 3;
		var pathStart = value.IndexOf('/', authorityStart);
		if (pathStart < 0) return "/";
		var queryStart = value.IndexOf('?', pathStart);
		var fragmentStart = value.IndexOf('#', pathStart);
		var pathEnd = queryStart < 0 ? value.Length : queryStart;
		if (fragmentStart >= 0 && fragmentStart < pathEnd) pathEnd = fragmentStart;
		return value[pathStart..pathEnd];
	}

	private static bool HasValidPercentEncoding(string value)
	{
		for (var index = 0; index < value.Length; index++)
		{
			if (value[index] != '%') continue;
			if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2])) return false;
			index += 2;
		}
		return true;
	}

	private static bool TryDecode(string value, out string decoded)
	{
		decoded = null;
		for (var index = 0; index < value.Length; index++)
		{
			if (value[index] != '%') continue;
			if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2])) return false;
			index += 2;
		}

		try
		{
			decoded = Uri.UnescapeDataString(value);
			return true;
		}
		catch (UriFormatException)
		{
			return false;
		}
	}

	private static bool IsHex(char value) =>
		value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
