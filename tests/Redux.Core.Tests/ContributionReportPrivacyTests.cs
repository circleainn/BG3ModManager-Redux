using System;
using System.IO;

using DivinityModManager.AppServices;
using DivinityModManager.Models.NexusMods;

using Newtonsoft.Json;

namespace Redux.Core.Tests;

public sealed class ContributionReportPrivacyTests
{
	public void ContributionReportsIncludeOnlyUniqueInstalledUserMods()
	{
		var sharedUuid = Guid.NewGuid().ToString();
		var included = new RegressionModData
		{
			UUID = sharedUuid,
			Name = "Included mod",
			Folder = "IncludedMod",
			FilePath = "IncludedMod.pak",
			IsUserMod = true
		};
		var duplicate = new RegressionModData
		{
			UUID = sharedUuid,
			Name = "Duplicate scan result",
			Folder = "IncludedMod",
			FilePath = "IncludedMod.pak",
			IsUserMod = true
		};
		var baseGame = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = "Base game entry",
			Folder = "BaseGame",
			FilePath = "BaseGame.pak",
			IsUserMod = false
		};
		var divider = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = "Visual divider",
			Folder = "VisualDivider",
			FilePath = "VisualDivider.pak",
			IsUserMod = true,
			IsVisualDivider = true
		};

		var report = ReduxDatabaseContributionService.CreateAsync(
				new[] { included, duplicate, baseGame, divider })
			.GetAwaiter()
			.GetResult()
			.Report;

		RegressionAssert.Equal(1, report.Mods.Count);
		RegressionAssert.Equal("Included mod", report.Mods[0].Name);
		RegressionAssert.Equal("IncludedMod.pak", report.Mods[0].FileName);
	}

	public void ContributionReportsStripPrivatePathsAndOrderingData()
	{
		var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var mod = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = $"Private package at {Path.Combine(userProfile, "Mods", "PrivateMod")}",
			Folder = Path.Combine(userProfile, "Mods", "PrivateMod"),
			FilePath = Path.Combine(userProfile, "Mods", "VisiblePackageName.pak"),
			Author = @"%USERPROFILE%\PrivateAuthor",
			IsUserMod = true,
			HasMetadata = true,
			IsForceLoaded = true
		};
		mod.NexusModsData.ModId = 12345;
		mod.NexusModsData.Name = "Public Nexus project";
		mod.NexusModsData.Author = @"$HOME/private-author";
		mod.NexusModsData.PictureUrl = new Uri("https://static.example.test/image.png?token=private#profile");
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.LiveApi;

		var result = ReduxDatabaseContributionService.CreateAsync(new[] { mod })
			.GetAwaiter()
			.GetResult();
		var record = result.Report.Mods[0];

		RegressionAssert.Equal(1, result.Report.Mods.Count);
		RegressionAssert.Equal<string?>(null, record.Name);
		RegressionAssert.Equal<string?>(null, record.DisplayName);
		RegressionAssert.Equal<string?>(null, record.Folder);
		RegressionAssert.Equal("VisiblePackageName.pak", record.FileName);
		RegressionAssert.Equal<string?>(null, record.Author);
		RegressionAssert.Equal("Public Nexus project", record.Nexus?.Name);
		RegressionAssert.Equal<string?>(null, record.Nexus?.Author);
		RegressionAssert.Equal("https://static.example.test/image.png", record.Nexus?.PictureUrl);
		RegressionAssert.Equal("unavailable", record.FingerprintStatus);
		RegressionAssert.Equal(1, result.UnavailableFingerprintCount);
		RegressionAssert.False(result.Report.Privacy.ContainsAbsolutePaths);
		RegressionAssert.False(result.Report.Privacy.ContainsLoadOrder);
		RegressionAssert.False(result.Report.Privacy.ContainsProfileNames);
		RegressionAssert.False(result.Report.Privacy.ContainsSettings);
		RegressionAssert.False(result.Report.Privacy.ContainsCredentials);

		var json = JsonConvert.SerializeObject(result.Report);
		RegressionAssert.False(json.Contains(userProfile, StringComparison.OrdinalIgnoreCase));
		RegressionAssert.False(json.Contains("%USERPROFILE%", StringComparison.OrdinalIgnoreCase));
		RegressionAssert.False(json.Contains("\"loadOrderIndex\"", StringComparison.OrdinalIgnoreCase));
		RegressionAssert.False(json.Contains("\"isActive\"", StringComparison.OrdinalIgnoreCase));
	}

	public void ContributionReportsPreserveKnownNexusIdentifiers()
	{
		var mod = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = "Known Nexus source",
			FilePath = "KnownNexusSource.pak",
			IsUserMod = true
		};
		mod.NexusModsData.ModId = 12345;
		mod.NexusModsData.LastFileId = 67890;
		mod.NexusModsData.MetadataOrigin = NexusMetadataOrigin.NexusArchiveImport;

		var record = ReduxDatabaseContributionService.CreateAsync(new[] { mod })
			.GetAwaiter()
			.GetResult()
			.Report.Mods[0];

		RegressionAssert.Equal(12345L, record.Nexus?.ModId);
		RegressionAssert.Equal(67890L, record.Nexus?.FileId);
		RegressionAssert.Equal(nameof(NexusMetadataOrigin.NexusArchiveImport), record.Nexus?.MetadataOrigin);
	}

	public void ContributionReportsRejectCredentialBearingProviderUrls()
	{
		var mod = new RegressionModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = "Credential URL test",
			Folder = "CredentialUrlTest",
			FilePath = "CredentialUrlTest.pak",
			IsUserMod = true
		};
		mod.NexusModsData.ModId = 12345;
		mod.NexusModsData.PictureUrl = new Uri("https://username:password@example.test/image.png");
		mod.ModioData.ModId = 67890;
		mod.ModioData.ProfileUrl = "https://mod.io/g/example/m/project?token=private#account";

		var record = ReduxDatabaseContributionService.CreateAsync(new[] { mod })
			.GetAwaiter()
			.GetResult()
			.Report.Mods[0];

		RegressionAssert.Equal<string?>(null, record.Nexus?.PictureUrl);
		RegressionAssert.Equal("https://mod.io/g/example/m/project", record.Modio?.PageUrl);
	}

	public void TamperedContributionReportsCannotBeSaved()
	{
		var outputPath = Path.Combine(
			Path.GetTempPath(),
			$"Redux-Contribution-Privacy-{Guid.NewGuid():N}.bg3redux-report");
		var report = new ReduxDatabaseContributionReport
		{
			SchemaVersion = ReduxDatabaseContributionService.SchemaVersion,
			ReportType = ReduxDatabaseContributionService.ReportType,
			Privacy = new ReduxDatabaseContributionPrivacy(),
			Mods =
			{
				new ReduxDatabaseContributionMod
				{
					Uuid = Guid.NewGuid().ToString(),
					FileName = Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
						"Mods",
						"PrivateMod.pak")
				}
			}
		};

		var rejected = false;
		try
		{
			ReduxDatabaseContributionService.Save(outputPath, report);
		}
		catch (InvalidDataException)
		{
			rejected = true;
		}

		RegressionAssert.True(rejected);
		RegressionAssert.False(File.Exists(outputPath));
	}
}
