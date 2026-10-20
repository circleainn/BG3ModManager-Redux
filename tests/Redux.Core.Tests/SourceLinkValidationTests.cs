using DivinityModManager.Util;

using System;

namespace Redux.Core.Tests;

public sealed class SourceLinkValidationTests
{
	public void NexusProjectReferencesAcceptBg3PagesAndPositiveIds()
	{
		foreach (var input in new[]
		{
			"125", " 125 ", "00125",
			"https://www.nexusmods.com/baldursgate3/mods/125",
			"https://nexusmods.com/baldursgate3/mods/125/",
			"http://www.nexusmods.com/baldursgate3/mods/125",
			"www.nexusmods.com/baldursgate3/mods/125",
			"nexusmods.com/baldursgate3/mods/125?tab=files&file_id=456#description",
			" \thttps://WWW.NEXUSMODS.COM/baldursgate3/mods/125/?tab=files&file_id=456#description\r\n"
		})
		{
			RegressionAssert.True(NexusModLinkParser.TryParseProjectId(input, out var id));
			RegressionAssert.Equal(125L, id);
		}
		RegressionAssert.True(NexusModLinkParser.TryParseProjectId("9223372036854775807", out var largest));
		RegressionAssert.Equal(Int64.MaxValue, largest);
	}

	public void NexusProjectReferencesRejectOtherHostsGamesAndMalformedIds()
	{
		foreach (var input in new string?[]
		{
			null, "", " ", "0", "-125", "+125", "1.25", "1e3", "125garbage", "garbage125", "9223372036854775808",
			"https://example.com/mods/125",
			"https://www.nexusmods.com/skyrimspecialedition/mods/125",
			"https://www.nexusmods.com.evil.example/baldursgate3/mods/125",
			"https://nexusmods.com@evil.example/baldursgate3/mods/125",
			"https://user:password@www.nexusmods.com/baldursgate3/mods/125",
			"https://user@www.nexusmods.com/baldursgate3/mods/125",
			"https://www.nexusmods.com:444/baldursgate3/mods/125",
			"https://www.nexusmods.com/mods/125",
			"https://www.nexusmods.com/baldursgate3/mods/0",
			"https://www.nexusmods.com/baldursgate3/mods/-125",
			"https://www.nexusmods.com/baldursgate3/mods/125extra",
			"https://www.nexusmods.com/baldursgate3/mods/9223372036854775808",
			"https://www.nexusmods.com/baldursgate3/mods/125/files/456",
			"https://www.nexusmods.com/baldursgate3//mods/125",
			"https://www.nexusmods.com/baldursgate3/mods/12 5",
			"https://www.nexusmods.com/baldursgate3/mods/12\n5",
			"ftp://www.nexusmods.com/baldursgate3/mods/125",
			"file:///baldursgate3/mods/125", "//www.nexusmods.com/baldursgate3/mods/125"
		})
		{
			RegressionAssert.False(NexusModLinkParser.TryParseProjectId(input!, out var id));
			RegressionAssert.Equal(0L, id);
		}
	}

	public void BrowserLaunchPreparationPreservesQueriesFragmentsAndEscapes()
	{
		foreach (var url in new[]
		{
			"https://github.com/circleainn/BG3ModManager-Redux/releases?version=16&channel=alpha#notes",
			"https://example.com/help?q=one%20two&literal=%26%22%25&next=a+b#section%202",
			"http://example.com/help?first=1&second=2"
		})
		{
			var start = ProcessHelper.CreateUrlStartInfo(url);
			RegressionAssert.True(start.UseShellExecute);
			RegressionAssert.Equal(url, start.FileName);
			RegressionAssert.Equal(String.Empty, start.Arguments);
			RegressionAssert.Equal(0, start.ArgumentList.Count);
		}
	}

	public void BrowserLaunchPreparationRetainsProtocolTargetsAndSeparateArguments()
	{
		const string url = "steam://run/1086940//?first=1&second=2";
		var start = ProcessHelper.CreateUrlStartInfo(url, "existing-handler-argument");
		RegressionAssert.True(start.UseShellExecute);
		RegressionAssert.Equal(url, start.FileName);
		RegressionAssert.Equal("existing-handler-argument", start.Arguments);
	}
}
