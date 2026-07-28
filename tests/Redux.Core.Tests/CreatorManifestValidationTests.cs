using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Metadata;

using Newtonsoft.Json.Linq;

using System;
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

	public void CompactNexusManifestLinksThePrimaryModule()
	{
		var manifest = ReduxCreatorManifestService.Validate(
			CreateCompactManifest(ModuleUuid, 23799).ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Valid, manifest.State);
		RegressionAssert.Equal("Example Mod", manifest.Name);
		RegressionAssert.Equal(1, manifest.Sources.Count);
		RegressionAssert.Equal(23799L, manifest.Sources[0].ProjectId);
		RegressionAssert.Equal(ModuleUuid, manifest.Modules[0].Uuid);
	}

	public void CompactNexusManifestRejectsAnUnrelatedModule()
	{
		var manifest = ReduxCreatorManifestService.Validate(
			CreateCompactManifest("79dacebf-7f07-45f0-84b2-1bd5a13194b7", 23799).ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "does not exist");
	}

	public void CompactNexusManifestRejectsASecondaryModule()
	{
		const string secondaryUuid = "79dacebf-7f07-45f0-84b2-1bd5a13194b7";
		var manifest = ReduxCreatorManifestService.Validate(
			CreateCompactManifest(secondaryUuid, 23799).ToString(),
			"Example.pak",
			new[]
			{
				CreateParsedModule(),
				CreateParsedModule(secondaryUuid, "Secondary Module", "SecondaryModule")
			});

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "primary module");
	}

	public void CompactNexusManifestPreservesAnOptionalFileId()
	{
		var json = CreateCompactManifest(ModuleUuid, 23799);
		json["nexus"]!["fileId"] = 123456;

		var manifest = ReduxCreatorManifestService.Validate(
			json.ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Valid, manifest.State);
		RegressionAssert.Equal(123456L, manifest.Sources[0].FileId);
	}

	public void CompactAndDetailedManifestFormsCannotBeMixed()
	{
		var json = CreateCompactManifest(ModuleUuid, 23799);
		json["mod"] = CreateManifest(new[] { "Creator" })["mod"];

		var manifest = ReduxCreatorManifestService.Validate(
			json.ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "either");
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

	public void DuplicateJsonPropertiesAreRejected()
	{
		var json = CreateCompactManifest(ModuleUuid, 23799).ToString();
		json = json.Replace(
			"\"schemaVersion\": 1,",
			"\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
			StringComparison.Ordinal);

		var manifest = ReduxCreatorManifestService.Validate(
			json,
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "already exists");
	}

	public void TrailingJsonContentIsRejected()
	{
		var manifest = ReduxCreatorManifestService.Validate(
			$"{CreateCompactManifest(ModuleUuid, 23799)}\n{{}}",
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
	}

	public void HomepageMustUsePublicHttpOrHttps()
	{
		var json = CreateManifest(new[] { "Creator" });
		json["mod"]!["homepage"] = "file:///C:/private/mod";

		var manifest = ReduxCreatorManifestService.Validate(
			json.ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Invalid, manifest.State);
		RegressionAssert.Contains(manifest.Diagnostic, "HTTP");
	}

	public void PakExtensionMatchingIsCaseInsensitive()
	{
		var json = CreateManifest(new[] { "Creator" });
		json["mod"]!["modules"]![0]!["pak"] = "EXAMPLE.PAK";

		var manifest = ReduxCreatorManifestService.Validate(
			json.ToString(),
			"Example.pak",
			new[] { CreateParsedModule() });

		RegressionAssert.Equal(ReduxCreatorManifestState.Valid, manifest.State);
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

	private static JObject CreateCompactManifest(string moduleUuid, long nexusProjectId) => new()
	{
		["schemaVersion"] = 1,
		["manifestType"] = "bg3-redux-mod",
		["moduleUuid"] = moduleUuid,
		["nexus"] = new JObject
		{
			["projectId"] = nexusProjectId
		}
	};

	private static DivinityModData CreateParsedModule(
		string uuid = ModuleUuid,
		string name = "Example Mod",
		string folder = "ExampleMod") => new RegressionModData
	{
		UUID = uuid,
		Name = name,
		Folder = folder,
		HasMetadata = true
	};
}
