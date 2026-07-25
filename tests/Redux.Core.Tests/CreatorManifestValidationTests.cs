using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Metadata;

using Newtonsoft.Json.Linq;

using System.Collections.Generic;
using System.Linq;

namespace Redux.Core.Tests;

public sealed class CreatorManifestValidationTests
{
	private const string ModuleUuid = "7a1731b4-1cc9-4495-9f4f-4e47c3eaf2ef";

	public void ValidManifestPreservesCreatorAuthorOrder()
	{
		var manifest = ReduxCreatorManifestService.Validate(
			CreateManifest(new[] { "First Author", "Second Author" }).ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Valid, manifest.State);
		RegressionAssert.SequenceEqual(
			new[] { "First Author", "Second Author" },
			manifest.Authors.ToArray());
	}

	public void DuplicateAuthorsAreRejected()
	{
		var manifest = ReduxCreatorManifestService.Validate(
			CreateManifest(new[] { "Same Author", "Same Author" }).ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "duplicate");
	}

	public void MismatchedPakClaimIsRejected()
	{
		var json = CreateManifest(new[] { "Creator" });
		json["mod"]!["modules"]![0]!["pak"] = "Different.pak";

		var manifest = ReduxCreatorManifestService.Validate(
			json.ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "PAK name");
	}

	private static JObject CreateManifest(IEnumerable<string> authors) => new()
	{
		["schemaVersion"] = 1,
		["manifestType"] = "bg3-redux-mod",
		["mod"] = new JObject
		{
			["name"] = "Example Mod",
			["authors"] = new JArray(authors),
			["sources"] = new JArray
			{
				new JObject
				{
					["service"] = "nexus",
					["projectId"] = 12345
				}
			},
			["modules"] = new JArray
			{
				new JObject
				{
					["uuid"] = ModuleUuid,
					["name"] = "Example Mod",
					["folder"] = "ExampleMod",
					["pak"] = "Example.pak"
				}
			}
		}
	};

	private static DivinityModData CreateParsedModule() => new RegressionModData
	{
		UUID = ModuleUuid,
		Name = "Example Mod",
		Folder = "ExampleMod",
		HasMetadata = true
	};
}
