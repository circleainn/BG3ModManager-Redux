using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DivinityModManager.AppServices;
using DivinityModManager.Localization;
using DivinityModManager.Models;
using LSLib.LS;
using LSLib.LS.Enums;

namespace Redux.Core.Tests;

public sealed class ModContentInspectionTests
{
	private const string StatsPath = "Public/Fixture/Stats/Generated/Data/Spells.txt";
	private static KeyValuePair<string, string> Text(string text, string path = StatsPath) => new(path, text);

	public void StatsInspectionKeepsDeclaredValuesAndInheritanceProvenance()
	{
		var result = ModContentInspectionService.InspectTexts([Text("""
			new entry "BaseSpell"
			type "SpellData"
			data "SpellType" "Target"
			data "Damage" "1d6"
			new entry "ChildSpell"
			type "SpellData"
			using "BaseSpell"
			data "Damage" "2d6"
			""")]);
		RegressionAssert.False(result.IsPartial);
		RegressionAssert.Equal(2, result.Records.Count);
		var child = result.Records.Single(record => record.Name == "ChildSpell");
		RegressionAssert.Equal(5, child.Line);
		RegressionAssert.Equal("BaseSpell", child.ParentName);
		RegressionAssert.Equal(ModContentInheritanceStatus.ResolvedWithinPackage, child.InheritanceStatus);
		var damage = child.Fields.Single(field => field.Name == "Damage");
		RegressionAssert.Equal("2d6", damage.Value);
		RegressionAssert.False(damage.IsInherited);
		var spellType = child.Fields.Single(field => field.Name == "SpellType");
		RegressionAssert.True(spellType.IsInherited);
		RegressionAssert.Equal("BaseSpell", spellType.SourceRecord);
		RegressionAssert.Equal(3, spellType.Line);
		RegressionAssert.Equal(StatsPath, spellType.SourcePath);
	}

	public void StatsInspectionRetainsSelfLayersButDoesNotGuessFileOrder()
	{
		var result = ModContentInspectionService.InspectTexts([
			Text("new entry \"Layer\"\ntype \"Object\"\ndata \"Value\" \"first\"\nnew entry \"Layer\"\ntype \"Object\"\nusing \"Layer\"\ndata \"Other\" \"second\""),
			Text("new entry \"Child\"\ntype \"Object\"\nusing \"Layer\"\ndata \"Own\" \"retained\"", "Public/Fixture/Stats/Generated/Data/Other.txt")]);
		RegressionAssert.Equal(3, result.Records.Count);
		var layer = result.Records[1];
		RegressionAssert.Equal(ModContentInheritanceStatus.ResolvedWithinPackage, layer.InheritanceStatus);
		RegressionAssert.Equal("first", layer.Fields.Single(field => field.Name == "Value").Value);
		RegressionAssert.True(layer.Fields.Single(field => field.Name == "Value").IsInherited);
		var child = result.Records[2];
		RegressionAssert.Equal(ModContentInheritanceStatus.AmbiguousParent, child.InheritanceStatus);
		RegressionAssert.Equal(1, child.Fields.Count);
		RegressionAssert.True(result.IsPartial);
	}

	public void StatsInspectionFlagsExternalParentsAndCyclesWithoutInventingFields()
	{
		var result = ModContentInspectionService.InspectTexts([Text("""
			new entry "MissingBase"
			type "Object"
			using "Vanilla_Not_Loaded"
			data "Own" "kept"
			new entry "CycleA"
			type "Object"
			using "CycleB"
			data "A" "a"
			new entry "CycleB"
			type "Object"
			using "CycleA"
			data "B" "b"
			""")]);
		RegressionAssert.Equal(ModContentInheritanceStatus.ExternalParent, result.Records[0].InheritanceStatus);
		RegressionAssert.Equal(ModContentInheritanceStatus.Cycle, result.Records[1].InheritanceStatus);
		RegressionAssert.Equal(1, result.Records[1].Fields.Count);
		RegressionAssert.False(result.Records[1].Fields[0].IsInherited);
		RegressionAssert.True(result.IsPartial);
	}

	public void StatsInspectionIsolatesMalformedAndOversizedFiles()
	{
		var result = ModContentInspectionService.InspectTexts([
			Text("new entry \"Broken\"\ntype \"SpellData\"\ndata \"Bad\"", "Public/Fixture/Stats/Generated/Data/Broken.txt"),
			Text("new entry \"Fine\"\ntype \"Object\"\ndata \"Value\" \"preserved\""),
			Text(new string(' ', ModContentInspectionService.MaxStatsFileBytes + 1), "Public/Fixture/Stats/Generated/Data/Large.txt")]);
		RegressionAssert.Equal(1, result.Records.Count);
		RegressionAssert.Equal("Fine", result.Records[0].Name);
		RegressionAssert.True(result.IsPartial);
		RegressionAssert.Equal(2, result.Issues.Count);
		RegressionAssert.False(result.Files[0].IsInspected);
		RegressionAssert.True(result.Files[1].IsInspected);
		RegressionAssert.False(result.Files[2].IsInspected);
		RegressionAssert.True(result.Issues[0].Line > 0);
	}

	public void StatsInspectionCancellationAndRecordLimitsRemainBounded()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		RegressionAssert.Throws<OperationCanceledException>(() => ModContentInspectionService.InspectPackage("missing.pak", cancellation.Token));
		RegressionAssert.Throws<OperationCanceledException>(() => ModContentInspectionService.InspectTexts([Text("invalid")], cancellation.Token));
		var many = new StringBuilder();
		for (var index = 0; index <= ModContentInspectionService.MaxRecords; index++)
			many.Append("new entry \"Entry").Append(index).Append("\"\ntype \"Object\"\ndata \"Value\" \"x\"\n");
		var result = ModContentInspectionService.InspectTexts([Text(many.ToString())]);
		RegressionAssert.Equal(ModContentInspectionService.MaxRecords, result.Records.Count);
		RegressionAssert.True(result.IsPartial);
		RegressionAssert.True(result.Issues.Any(issue => issue.Message.Contains("count exceeds", StringComparison.Ordinal)));
	}

	public void StatsInspectionReportsRepeatedFieldsWithoutDroppingTheRecord()
	{
		var result = ModContentInspectionService.InspectTexts([Text("new entry \"Repeated\"\ntype \"Object\"\ndata \"Value\" \"first\"\ndata \"Value\" \"last\"")]);
		RegressionAssert.Equal(1, result.Records.Count);
		RegressionAssert.Equal("last", result.Records[0].Fields.Single().Value);
		RegressionAssert.True(result.IsPartial);
		RegressionAssert.Equal(4, result.Issues.Single().Line);
	}

	public void ContentInspectionCancellationStopsDuringContentRead()
	{
		using var cancellation = new CancellationTokenSource();
		using var source = new CancellingReadStream(new byte[64 * 1024], cancellation);
		RegressionAssert.False(cancellation.IsCancellationRequested);
		RegressionAssert.Throws<OperationCanceledException>(() => ModContentInspectionService.ReadStatsContent(source, cancellation.Token));
		RegressionAssert.True(cancellation.IsCancellationRequested);
		RegressionAssert.Equal(1, source.ReadCalls);
		RegressionAssert.True(source.Position > 0 && source.Position < source.Length);
	}

	public void ContentInspectionBoundsZstdDecoderWindowIndependentlyOfOutput()
	{
		// Valid empty Zstandard frame with an 8 MiB decoder window. Its output
		// would fit any output-byte limit, but it exceeds this inspector's 2 MiB window.
		byte[] frame = [0x28, 0xb5, 0x2f, 0xfd, 0x00, 0x68, 0x01, 0x00, 0x00];
		using (var input = new MemoryStream(frame))
		using (var permissive = new ZstdSharp.DecompressionStream(input))
			RegressionAssert.Equal(0, permissive.Read(new byte[16], 0, 16));
		using var boundedInput = new MemoryStream(frame);
		using var bounded = new ZstdSharp.DecompressionStream(boundedInput);
		RegressionAssert.Throws<ZstdSharp.ZstdException>(() => ModContentInspectionService.ReadStatsContent(bounded, CancellationToken.None));
	}

	public void PackageContentInspectionReadsSelectedStatsWithoutWritesOrExtraction()
	{
		WithFolder(root =>
		{
			var packagePath = Path.Combine(root, "fixture.pak");
			WritePackage(packagePath, [
				(StatsPath, "new entry \"Example\"\ntype \"SpellData\"\ndata \"SpellType\" \"Target\""),
				("Mods/Fixture/ScriptExtender/Lua/BootstrapServer.lua", "error('must never execute')"),
				("Public/Fixture/Assets/Texture.dds", "not an image")]);
			var before = File.ReadAllBytes(packagePath);
			var result = ModContentInspectionService.InspectPackage(packagePath);
			RegressionAssert.False(result.IsPartial);
			RegressionAssert.Equal("fixture.pak", result.PackageFileName);
			RegressionAssert.Equal("Example", result.Records.Single().Name);
			RegressionAssert.Equal(3, result.Files.Count);
			RegressionAssert.Equal(1, result.Files.Count(file => file.IsInspected));
			RegressionAssert.True(result.Files.Any(file => file.Category == "Scripts"));
			RegressionAssert.SequenceEqual(before, File.ReadAllBytes(packagePath));
			RegressionAssert.Equal(1, Directory.GetFileSystemEntries(root).Length);
		});
	}

	public void PackageContentInspectionRejectsSolidAndOversizedTablesBeforeExpansion()
	{
		WithFolder(root =>
		{
			var packagePath = Path.Combine(root, "table.pak");
			WritePackage(packagePath, [(StatsPath, "new entry \"Example\"\ntype \"Object\"")]);
			ulong offset;
			using (var package = new PackageReader().Read(packagePath, metadataOnly: true)) offset = package.Metadata.FileListOffset;
			using (var stream = File.Open(packagePath, FileMode.Open, FileAccess.Write))
			using (var writer = new BinaryWriter(stream))
			{
				stream.Position = (long)offset;
				writer.Write(Int32.MaxValue);
			}
			var result = ModContentInspectionService.InspectPackage(packagePath);
			RegressionAssert.True(result.IsPartial);
			RegressionAssert.Equal(0, result.Records.Count);
			RegressionAssert.True(result.Issues.Single().Message.Contains("file table", StringComparison.Ordinal));
			WritePackage(packagePath, [(StatsPath, "new entry \"Example\"\ntype \"Object\"")], solid: true);
			result = ModContentInspectionService.InspectPackage(packagePath);
			RegressionAssert.True(result.IsPartial);
			RegressionAssert.True(result.Issues.Single().Message.Contains("Solid-compressed", StringComparison.Ordinal));
		});
	}

	public void ContentInspectionDiagnosticsHaveAllInterfaceTranslations()
	{
		var service = ReduxLanguageService.Current;
		string[] phrases =
		[
			"File is not a PAK package.",
			"Package header is incomplete.",
			"Package contains an invalid internal path; its contents were not inspected.",
			"Package contains a duplicate internal path; the duplicate was not inspected.",
			"This package entry deletes a file and has no readable content.",
			"Stats file exceeds the inspection size limit.",
			"Stats file length does not match the package metadata.",
			"This package version is not supported by the content inspector.",
			"Solid-compressed packages are not supported by the bounded content inspector.",
			"Package part count exceeds the inspection limit.",
			"Package file table is outside the package.",
			"Package file table exceeds the inspection limit or is incomplete.",
			"Content could not be inspected.",
			"Stats file count exceeds the inspection limit.",
			"Total Stats data exceeds the inspection size limit.",
			"Stats syntax could not be parsed; this file's records were not included.",
			"A Stats field is declared more than once; the last declared value is shown.",
			"Record or field count exceeds the inspection limit; remaining records were skipped.",
			"A declaration has no supported name/type; it was not included as a Stats record.",
			"A structured Stats field is outside this inspector's supported scope.",
			"Inheritance is incomplete: the parent or an earlier definition is outside this package.",
			"Inheritance is incomplete: multiple parent definitions have no verified order.",
			"Inheritance is incomplete: the parent chain contains a cycle.",
			"Inheritance is incomplete: the inspection limit was reached.",
			"Stats",
			"Scripts",
			"Resources",
			"Textures",
			"Localization",
			"Other",
		];
		foreach (var english in phrases)
		{
			var key = service.Keys.Single(candidate => service.GetTranslation(candidate, "en") == english);
			foreach (var language in ReduxLanguageService.Languages)
			{
				var translated = service.GetTranslation(key, language.Code);
				RegressionAssert.False(String.IsNullOrWhiteSpace(translated));
				if (language.Code != "en" && english.EndsWith('.')) RegressionAssert.False(translated == english);
			}
		}
	}

	private static void WritePackage(string path, (string Path, string Text)[] files, bool solid = false)
	{
		var build = new PackageBuildData { Version = PackageVersion.V18, Flags = solid ? PackageFlags.Solid : 0 };
		foreach (var file in files) build.Files.Add(new PackageBuildInputFile { Path = file.Path, Body = Encoding.UTF8.GetBytes(file.Text) });
		using var writer = PackageWriterFactory.Create(build, path);
		writer.Write();
	}

	private static void WithFolder(Action<string> action)
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxContentInspection", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try { action(root); }
		finally { Directory.Delete(root, true); }
	}

	private sealed class CancellingReadStream(byte[] contents, CancellationTokenSource cancellation) : MemoryStream(contents, writable: false)
	{
		public int ReadCalls { get; private set; }
		public override int Read(byte[] buffer, int offset, int count)
		{
			var read = base.Read(buffer, offset, count);
			ReadCalls++;
			cancellation.Cancel();
			return read;
		}
	}
}
