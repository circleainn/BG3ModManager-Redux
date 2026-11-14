using System.Text;
using DivinityModManager.Models;
using LSLib.LS;
using LSLib.LS.Enums;
using LSLib.Stats.StatParser;

namespace DivinityModManager.AppServices;

/// <summary>
/// On-demand, bounded, read-only package inspection. Only Generated/Data Stats
/// declarations are interpreted. Other content is inventoried without executing it.
/// </summary>
public static class ModContentInspectionService
{
	public const int MaxStatsFileBytes = 2 * 1024 * 1024;
	public const int MaxTotalStatsBytes = 16 * 1024 * 1024;
	public const int MaxStatsFiles = 512;
	public const int MaxPackageFiles = 100_000;
	public const int MaxRecords = 10_000;
	public const int MaxParsedFields = 100_000;
	public const int MaxResolvedFields = 200_000;
	private const int MaxFileTableBytes = 32 * 1024 * 1024;
	private const int MaxInheritanceDepth = 128;
	private const int MaxFieldsPerResolvedRecord = 4_096;
	private const int MaxIssues = 1_000;
	private static readonly UTF8Encoding StrictUtf8 = new(false, true);

	public static ModContentInspectionResult InspectPackage(
		string packagePath, CancellationToken cancellationToken = default,
		IProgress<ModContentInspectionProgress> progress = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var state = new InspectionState(Path.GetFileName(packagePath ?? String.Empty), cancellationToken);
		try
		{
			if (String.IsNullOrWhiteSpace(packagePath) || !String.Equals(Path.GetExtension(packagePath), ".pak", StringComparison.OrdinalIgnoreCase))
				throw new ContentInspectionException("File is not a PAK package.");
			// Retain a read-only handle across preflight and inspection so the primary
			// package cannot be replaced or modified between the two reads on Windows.
			using var lockedFile = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (lockedFile.Length < 16) throw new ContentInspectionException("Package header is incomplete.");
			using (var header = new PackageReader().Read(packagePath, metadataOnly: true))
				ValidatePackageHeader(header.Metadata, lockedFile);
			cancellationToken.ThrowIfCancellationRequested();
			using var package = new PackageReader().Read(packagePath);
			var candidates = package.Files.Count(file => IsStatsPath(NormalizePath(file.Name)));
			var visitedStats = 0;
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var file in package.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var path = NormalizePath(file.Name);
				var category = Classify(path);
				var inspected = false;
				var before = state.Declarations.Count;
				if (!IsSafeInternalPath(path))
					state.AddIssue("Package contains an invalid internal path; its contents were not inspected.", path);
				else if (!seenPaths.Add(path))
					state.AddIssue("Package contains a duplicate internal path; the duplicate was not inspected.", path);
				else if (IsStatsPath(path))
				{
					progress?.Report(new ModContentInspectionProgress(visitedStats, candidates, path));
					try
					{
						if (file.IsDeletion()) throw new ContentInspectionException("This package entry deletes a file and has no readable content.");
						state.ReserveFile(file.Size(), path);
						if (file.SizeOnDisk > MaxStatsFileBytes || file.UncompressedSize > MaxStatsFileBytes)
							throw new ContentInspectionException("Stats file exceeds the inspection size limit.");
						using var content = file.CreateContentReader();
						var bytes = ReadStatsContent(content, cancellationToken);
						if ((ulong)bytes.Length != file.Size()) throw new ContentInspectionException("Stats file length does not match the package metadata.");
						inspected = state.ParseText(path, DecodeText(bytes));
					}
					catch (Exception ex) when (IsInspectionFailure(ex))
					{
						state.AddIssue(FailureMessage(ex), path);
					}
					visitedStats++;
					progress?.Report(new ModContentInspectionProgress(visitedStats, candidates, path));
				}
				state.Files.Add(new ModContentFile(path, file.Size(), category, inspected, state.Declarations.Count - before));
			}
		}
		catch (Exception ex) when (IsInspectionFailure(ex))
		{
			state.AddIssue(FailureMessage(ex));
		}
		return state.Finish();
	}

	// Tests use the same parser, limits and resolver without needing copyrighted mods.
	internal static ModContentInspectionResult InspectTexts(
		IEnumerable<KeyValuePair<string, string>> files, CancellationToken cancellationToken = default)
	{
		var state = new InspectionState("fixture.pak", cancellationToken);
		foreach (var file in files)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var path = NormalizePath(file.Key);
			var before = state.Declarations.Count;
			var bytes = (ulong)StrictUtf8.GetByteCount(file.Value);
			var inspected = false;
			try
			{
				state.ReserveFile(bytes, path);
				inspected = state.ParseText(path, file.Value);
			}
			catch (Exception ex) when (IsInspectionFailure(ex)) { state.AddIssue(FailureMessage(ex), path); }
			state.Files.Add(new ModContentFile(path, bytes, "Stats", inspected, state.Declarations.Count - before));
		}
		return state.Finish();
	}

	private static void ValidatePackageHeader(PackageHeaderCommon header, FileStream file)
	{
		// Legacy and solid archives need separate bounded readers. LSLib otherwise
		// expands an entire solid archive before per-file limits can be enforced.
		if (header.Version is not (15 or 16 or 18))
			throw new ContentInspectionException("This package version is not supported by the content inspector.");
		if (header.Flags.HasFlag(PackageFlags.Solid))
			throw new ContentInspectionException("Solid-compressed packages are not supported by the bounded content inspector.");
		if (header.NumParts == 0 || header.NumParts > 32)
			throw new ContentInspectionException("Package part count exceeds the inspection limit.");
		if (header.FileListOffset > (ulong)(file.Length - 8))
			throw new ContentInspectionException("Package file table is outside the package.");
		file.Position = (long)header.FileListOffset;
		using var reader = new BinaryReader(file, Encoding.UTF8, leaveOpen: true);
		var count = reader.ReadInt32();
		var compressedSize = reader.ReadInt32();
		if (count < 0 || count > MaxPackageFiles || compressedSize < 0 || compressedSize > MaxFileTableBytes
			|| compressedSize > file.Length - file.Position)
			throw new ContentInspectionException("Package file table exceeds the inspection limit or is incomplete.");
	}

	internal static byte[] ReadStatsContent(Stream stream, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		// A Zstandard frame can request a large history window even when its
		// declared and actual output are tiny. Bound decoder memory before reading.
		if (stream is ZstdSharp.DecompressionStream zstd)
			zstd.SetParameter(ZstdSharp.Unsafe.ZSTD_dParameter.ZSTD_d_windowLogMax, 21);
		using var result = new MemoryStream();
		var buffer = new byte[16 * 1024];
		int count;
		while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			token.ThrowIfCancellationRequested();
			if (result.Length + count > MaxStatsFileBytes) throw new ContentInspectionException("Stats file exceeds the inspection size limit.");
			result.Write(buffer, 0, count);
		}
		return result.ToArray();
	}

	private static string DecodeText(byte[] bytes)
	{
		var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
		return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
	}

	private static string NormalizePath(string path) => (path ?? String.Empty).Replace('\\', '/');
	private static bool IsSafeInternalPath(string path) => !String.IsNullOrWhiteSpace(path)
		&& !path.StartsWith('/') && !path.Contains(':') && !path.Any(Char.IsControl)
		&& !path.Split('/').Any(part => part is ".." or "." or "");
	private static bool IsStatsPath(string path) => path.StartsWith("Public/", StringComparison.OrdinalIgnoreCase)
		&& path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase)
		&& path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
	private static string Classify(string path)
	{
		if (path.Contains("/Stats/", StringComparison.OrdinalIgnoreCase)) return "Stats";
		var extension = Path.GetExtension(path).ToLowerInvariant();
		if (extension is ".lua" or ".osi" or ".div") return "Scripts";
		if (extension is ".dds" or ".png" or ".webp" or ".jpg" or ".gts" or ".gtp") return "Textures";
		if (extension == ".loca" || path.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)) return "Localization";
		if (extension is ".lsx" or ".lsf" or ".lsj" or ".lsb" or ".gr2") return "Resources";
		return "Other";
	}

	private static bool IsInspectionFailure(Exception ex) => ex is not
		(OperationCanceledException or OutOfMemoryException or StackOverflowException);
	private static string FailureMessage(Exception ex) => ex is ContentInspectionException
		? ex.Message : "Content could not be inspected.";
	private sealed class ContentInspectionException(string message) : Exception(message);

	private sealed class InspectionState(string fileName, CancellationToken token)
	{
		internal readonly List<Declaration> Declarations = [];
		internal readonly List<ModContentFile> Files = [];
		private readonly List<ModContentInspectionIssue> issues = [];
		private int fileCount;
		private ulong totalBytes;
		private int parsedFields;
		private bool partial;

		internal void AddIssue(string message, string path = "", int line = 0)
		{
			partial = true;
			if (issues.Count < MaxIssues) issues.Add(new ModContentInspectionIssue(message, path, line));
		}

		internal void ReserveFile(ulong bytes, string path)
		{
			token.ThrowIfCancellationRequested();
			if (++fileCount > MaxStatsFiles) throw new ContentInspectionException("Stats file count exceeds the inspection limit.");
			if (bytes > MaxStatsFileBytes) throw new ContentInspectionException("Stats file exceeds the inspection size limit.");
			if (bytes + totalBytes > MaxTotalStatsBytes) throw new ContentInspectionException("Total Stats data exceeds the inspection size limit.");
			totalBytes += bytes;
		}

		internal bool ParseText(string path, string text)
		{
			token.ThrowIfCancellationRequested();
			var scanner = new InspectionScanner(path, token);
			scanner.SetSource(text.TrimStart('\ufeff') + "\n", 0);
			var parser = new InspectionParser(scanner);
			if (!parser.Parse() || scanner.ErrorLine > 0)
			{
				AddIssue("Stats syntax could not be parsed; this file's records were not included.", path,
					scanner.ErrorLine > 0 ? scanner.ErrorLine : scanner.LastLocation().StartLine);
				return false;
			}
			var complete = true;
			foreach (var line in scanner.DuplicateFieldLines)
				AddIssue("A Stats field is declared more than once; the last declared value is shown.", path, line);
			foreach (var declaration in parser.GetDeclarations())
			{
				token.ThrowIfCancellationRequested();
				if (Declarations.Count >= MaxRecords || parsedFields + declaration.Properties.Count > MaxParsedFields)
				{
					AddIssue("Record or field count exceeds the inspection limit; remaining records were skipped.", path);
					return false;
				}
				var name = GetString(declaration, "Name");
				var type = GetString(declaration, "EntityType");
				var parent = GetString(declaration, "Using");
				var line = declaration.Properties.TryGetValue("Name", out var nameProperty)
					? nameProperty.Location?.StartLine ?? 0 : declaration.Location?.StartLine ?? 0;
				if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(type))
				{
					AddIssue("A declaration has no supported name/type; it was not included as a Stats record.", path, line);
					complete = false;
					continue;
				}
				var fields = new Dictionary<string, ModContentField>(StringComparer.Ordinal);
				foreach (var property in declaration.Properties.Values)
				{
					if (property.Key is "Name" or "EntityType" or "Using") continue;
					if (property.Value is not string value)
					{
						AddIssue("A structured Stats field is outside this inspector's supported scope.", path, property.Location?.StartLine ?? line);
						complete = false;
						continue;
					}
					fields[property.Key] = new ModContentField(property.Key, value, name, false, path, property.Location?.StartLine ?? line);
				}
				parsedFields += fields.Count;
				Declarations.Add(new Declaration(type, name, parent, path, line, fields));
			}
			return complete;
		}

		internal ModContentInspectionResult Finish()
		{
			token.ThrowIfCancellationRequested();
			var byName = Declarations.GroupBy(d => d.Name, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
			var previousLayers = new Dictionary<Declaration, Declaration>(ReferenceEqualityComparer.Instance);
			var latestByFile = new Dictionary<(string Path, string Name), Declaration>();
			foreach (var declaration in Declarations)
			{
				if (latestByFile.TryGetValue((declaration.Path, declaration.Name), out var previous))
					previousLayers[declaration] = previous;
				latestByFile[(declaration.Path, declaration.Name)] = declaration;
			}
			var result = new List<ModContentRecord>(Declarations.Count);
			var inheritedFieldBudget = MaxResolvedFields - parsedFields;
			foreach (var declaration in Declarations)
			{
				token.ThrowIfCancellationRequested();
				var (fields, status) = Resolve(declaration, byName, previousLayers);
				var inheritedCount = fields.Values.Count(field => field.IsInherited);
				if (inheritedCount > inheritedFieldBudget)
				{
					fields = declaration.Fields;
					status = ModContentInheritanceStatus.LimitReached;
				}
				else inheritedFieldBudget -= inheritedCount;
				if (status is not (ModContentInheritanceStatus.None or ModContentInheritanceStatus.ResolvedWithinPackage))
					AddIssue(StatusMessage(status), declaration.Path, declaration.Line);
				result.Add(new ModContentRecord(declaration.Type, declaration.Name, declaration.Parent, declaration.Path, declaration.Line,
					fields.Values.OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase).ToArray(), status));
			}
			return new ModContentInspectionResult(fileName, result.ToArray(), issues.ToArray(), Files.ToArray(), partial);
		}

		// Adapted from BG3 Forge StatsCollection.resolved (MIT), copyright (c) 2026
		// BG3 Forge contributors, commit 10fa2959b472bb71ba2714d274b9828e59cd4576:
		// https://github.com/crazyace/bg3-forge/blob/10fa2959b472bb71ba2714d274b9828e59cd4576/src/bg3forge/parsers/stats.py
		// Preserve the iterative ancestor-chain/reverse-overlay algorithm; Redux
		// adds provenance, cancellation, limits and refuses uncertain cross-file layers.
		private (Dictionary<string, ModContentField> Fields, ModContentInheritanceStatus Status) Resolve(
			Declaration start, Dictionary<string, List<Declaration>> byName,
			Dictionary<Declaration, Declaration> previousLayers)
		{
			var chain = new List<Declaration>();
			var seen = new HashSet<Declaration>(ReferenceEqualityComparer.Instance);
			var cursor = start;
			var status = String.IsNullOrEmpty(start.Parent) ? ModContentInheritanceStatus.None : ModContentInheritanceStatus.ResolvedWithinPackage;
			while (cursor != null)
			{
				token.ThrowIfCancellationRequested();
				if (!seen.Add(cursor)) { status = ModContentInheritanceStatus.Cycle; break; }
				if (chain.Count >= MaxInheritanceDepth) { status = ModContentInheritanceStatus.LimitReached; break; }
				chain.Add(cursor);
				if (String.IsNullOrEmpty(cursor.Parent)) break;
				if (!byName.TryGetValue(cursor.Parent, out var parents)) { status = ModContentInheritanceStatus.ExternalParent; break; }
				if (cursor.Parent == cursor.Name)
				{
					// Same-file textual layers have a known order. Different files have
					// no verified engine order here, so never select a filename winner.
					if (!previousLayers.TryGetValue(cursor, out var prior))
					{
						status = parents.Any(p => !ReferenceEquals(p, cursor))
							? ModContentInheritanceStatus.AmbiguousParent : ModContentInheritanceStatus.ExternalParent;
						break;
					}
					cursor = prior;
				}
				else
				{
					if (parents.Count != 1) { status = ModContentInheritanceStatus.AmbiguousParent; break; }
					cursor = parents[0];
				}
			}
			var fields = new Dictionary<string, ModContentField>(StringComparer.Ordinal);
			if (status is ModContentInheritanceStatus.Cycle or ModContentInheritanceStatus.LimitReached)
				return (start.Fields, status);
			for (var index = chain.Count - 1; index >= 0; index--)
				foreach (var field in chain[index].Fields.Values)
				{
					if (fields.Count >= MaxFieldsPerResolvedRecord && !fields.ContainsKey(field.Name))
						return (start.Fields, ModContentInheritanceStatus.LimitReached);
					fields[field.Name] = field with { IsInherited = !ReferenceEquals(chain[index], start) };
				}
			return (fields, status);
		}

		private static string StatusMessage(ModContentInheritanceStatus status) => status switch
		{
			ModContentInheritanceStatus.ExternalParent => "Inheritance is incomplete: the parent or an earlier definition is outside this package.",
			ModContentInheritanceStatus.AmbiguousParent => "Inheritance is incomplete: multiple parent definitions have no verified order.",
			ModContentInheritanceStatus.Cycle => "Inheritance is incomplete: the parent chain contains a cycle.",
			_ => "Inheritance is incomplete: the inspection limit was reached."
		};
	}

	private static string GetString(StatDeclaration declaration, string key) =>
		declaration.Properties.TryGetValue(key, out var property) && property.Value is string value ? value : String.Empty;
	private sealed record Declaration(string Type, string Name, string Parent, string Path, int Line,
		Dictionary<string, ModContentField> Fields);

	// GPLEX seals the generated scanner. Compose it behind the public scanner
	// abstraction, then use the parser's protected Scanner hook; do not change
	// generated or vendored files to add cancellation or diagnostic collection.
	private sealed class InspectionParser : StatParser
	{
		public InspectionParser(InspectionScanner scanner) : base(scanner.Inner) => Scanner = scanner;
	}

	private sealed class InspectionScanner(string path, CancellationToken token) : StatScanBase
	{
		internal StatScanner Inner { get; } = new(path);
		public int ErrorLine { get; private set; }
		public List<int> DuplicateFieldLines { get; } = [];
		private readonly HashSet<string> fieldNames = new(StringComparer.Ordinal);
		public void SetSource(string text, int offset) => Inner.SetSource(text, offset);
		public LSLib.Parser.CodeLocation LastLocation() => Inner.LastLocation();
		public override LSLib.Parser.CodeLocation yylloc
		{
			get => Inner.yylloc;
			set => Inner.yylloc = value;
		}
		public override int yylex()
		{
			token.ThrowIfCancellationRequested();
			var next = Inner.yylex();
			yylval = Inner.yylval;
			if (next == (int)StatTokens.NEW) fieldNames.Clear();
			if (next == (int)StatTokens.DATA_ITEM && yylval is StatProperty property
				&& !fieldNames.Add(property.Key) && DuplicateFieldLines.Count < MaxIssues)
				DuplicateFieldLines.Add(property.Location?.StartLine ?? LastLocation().StartLine);
			return next;
		}
		public override void yyerror(string format, params object[] args) => ErrorLine = LastLocation().StartLine;
	}
}
