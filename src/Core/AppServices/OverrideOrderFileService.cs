using System.Text.Json;

namespace DivinityModManager.AppServices;

/// <summary>Moves only reviewed Override PAKs between BG3's Mods folder and Redux's holding folder.</summary>
public sealed class OverrideOrderFileService
{
	public sealed record Move(string FileName, string Source, string Destination, long Length, DateTime ModifiedUtc, bool Activate);
	public sealed record Plan(IReadOnlyList<Move> Moves)
	{
		public IReadOnlyList<Move> ToActivate => Moves.Where(move => move.Activate).ToList();
		public IReadOnlyList<Move> ToHold => Moves.Where(move => !move.Activate).ToList();
	}

	private readonly string _modsFolder;
	private readonly string _holdingFolder;
	private readonly string _journalPath;

	public OverrideOrderFileService(string modsFolder, string holdingFolder)
	{
		_modsFolder = Path.GetFullPath(modsFolder ?? throw new ArgumentNullException(nameof(modsFolder)));
		_holdingFolder = Path.GetFullPath(holdingFolder ?? throw new ArgumentNullException(nameof(holdingFolder)));
		if (String.Equals(_modsFolder, _holdingFolder, StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException("The Override holding folder must be separate from the game's Mods folder.");
		_journalPath = Path.Combine(_holdingFolder, "pending-switch.json");
	}

	public IReadOnlyList<string> HeldFiles
	{
		get
		{
			if (!Directory.Exists(_holdingFolder)) return [];
			if ((new DirectoryInfo(_holdingFolder).Attributes & FileAttributes.ReparsePoint) != 0)
				throw new IOException("The Override holding folder is a link. Open it manually before switching orders.");
			return Directory.EnumerateFiles(_holdingFolder, "*.pak", SearchOption.TopDirectoryOnly)
				.Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
		}
	}

	public Plan Review(IEnumerable<string> installedOverridePaths, IEnumerable<string> wantedFileNames)
	{
		EnsureFoldersSafe();
		if (File.Exists(_journalPath))
			throw new IOException("An interrupted Override switch must be recovered before reviewing another one.");
		var wanted = NormalizeNames(wantedFileNames);
		var installed = (installedOverridePaths ?? [])
			.Where(path => !String.IsNullOrWhiteSpace(path) && File.Exists(path))
			.Select(Path.GetFullPath)
			.Where(path => String.Equals(Path.GetDirectoryName(path), _modsFolder, StringComparison.OrdinalIgnoreCase))
			.ToDictionary(Path.GetFileName, path => path, StringComparer.OrdinalIgnoreCase);
		var held = HeldFiles.ToDictionary(name => name, name => Path.Combine(_holdingFolder, name), StringComparer.OrdinalIgnoreCase);
		if (installed.Keys.Intersect(held.Keys, StringComparer.OrdinalIgnoreCase).Any())
			throw new IOException("An Override package exists in both Mods and Redux's holding folder. Resolve the duplicate before switching.");
		var unavailable = wanted.Except(installed.Keys.Concat(held.Keys), StringComparer.OrdinalIgnoreCase).ToList();
		if (unavailable.Count > 0)
			throw new FileNotFoundException("These saved Override packages are unavailable: " + String.Join(", ", unavailable));
		var moves = new List<Move>();
		foreach (var (name, path) in installed.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
			if (!wanted.Contains(name)) moves.Add(Inspect(name, path, Path.Combine(_holdingFolder, name), false));
		foreach (var (name, path) in held.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
			if (wanted.Contains(name)) moves.Add(Inspect(name, path, Path.Combine(_modsFolder, name), true));
		return new Plan(moves);
	}

	public void Apply(Plan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);
		if (plan.Moves.Count == 0) return;
		EnsureFoldersSafe();
		if (File.Exists(_journalPath)) throw new IOException("An interrupted Override switch must be recovered first.");
		foreach (var move in plan.Moves) Validate(move);
		Directory.CreateDirectory(_modsFolder);
		Directory.CreateDirectory(_holdingFolder);
		var journalTemp = _journalPath + ".tmp";
		File.WriteAllText(journalTemp, JsonSerializer.Serialize(plan.Moves));
		File.Move(journalTemp, _journalPath);
		try
		{
			foreach (var move in plan.Moves)
			{
				Validate(move);
				File.Move(move.Source, move.Destination);
			}
			File.Delete(_journalPath);
		}
		catch
		{
			Recover();
			throw;
		}
	}

	public void Recover()
	{
		if (!File.Exists(_journalPath)) return;
		EnsureFoldersSafe();
		Directory.CreateDirectory(_modsFolder);
		var moves = JsonSerializer.Deserialize<List<Move>>(File.ReadAllText(_journalPath))
			?? throw new IOException("The interrupted Override switch journal is invalid.");
		foreach (var move in moves.AsEnumerable().Reverse())
		{
			ValidateLocation(move);
			if (File.Exists(move.Source))
			{
				Validate(move);
				continue;
			}
			if (!File.Exists(move.Destination) || !Matches(move.Destination, move))
				throw new IOException($"Cannot safely recover '{move.FileName}'; its location or contents changed. The recovery journal was retained.");
			File.Move(move.Destination, move.Source);
		}
		File.Delete(_journalPath);
	}

	private static HashSet<string> NormalizeNames(IEnumerable<string> names)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var name in names ?? [])
		{
			if (String.IsNullOrWhiteSpace(name) || !String.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
				|| !String.Equals(Path.GetExtension(name), ".pak", StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException("An Override order contains an invalid PAK filename.");
			result.Add(name);
		}
		return result;
	}

	private void EnsureFoldersSafe()
	{
		foreach (var folder in new[] { _modsFolder, _holdingFolder })
			if (Directory.Exists(folder) &&
				(new DirectoryInfo(folder).Attributes & FileAttributes.ReparsePoint) != 0)
				throw new IOException("An Override mod folder is a link. Redux will not move files through it automatically.");
	}

	private static Move Inspect(string name, string source, string destination, bool activate)
	{
		var file = new FileInfo(source);
		if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
			throw new IOException($"'{name}' is a link. Redux will not move it automatically.");
		if (File.Exists(destination)) throw new IOException($"'{name}' already exists at the destination.");
		return new Move(name, source, destination, file.Length, file.LastWriteTimeUtc, activate);
	}

	private void ValidateLocation(Move move)
	{
		if (move == null || !String.Equals(Path.GetFileName(move.Source), move.FileName, StringComparison.OrdinalIgnoreCase)
			|| !String.Equals(Path.GetFileName(move.Destination), move.FileName, StringComparison.OrdinalIgnoreCase)
			|| !String.Equals(Path.GetExtension(move.FileName), ".pak", StringComparison.OrdinalIgnoreCase)
			|| !((String.Equals(Path.GetDirectoryName(move.Source), _modsFolder, StringComparison.OrdinalIgnoreCase)
				&& String.Equals(Path.GetDirectoryName(move.Destination), _holdingFolder, StringComparison.OrdinalIgnoreCase))
				|| (String.Equals(Path.GetDirectoryName(move.Source), _holdingFolder, StringComparison.OrdinalIgnoreCase)
					&& String.Equals(Path.GetDirectoryName(move.Destination), _modsFolder, StringComparison.OrdinalIgnoreCase))))
			throw new IOException("The Override switch journal references a path outside its managed folders.");
	}

	private void Validate(Move move)
	{
		ValidateLocation(move);
		if (!Matches(move.Source, move) || File.Exists(move.Destination))
			throw new IOException($"'{move.FileName}' changed since review, or its destination is occupied. No files were moved.");
	}

	private static bool Matches(string path, Move move)
	{
		if (!File.Exists(path)) return false;
		var file = new FileInfo(path);
		return (file.Attributes & FileAttributes.ReparsePoint) == 0 && file.Length == move.Length
			&& file.LastWriteTimeUtc == move.ModifiedUtc;
	}
}
