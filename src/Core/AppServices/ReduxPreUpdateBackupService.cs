using System.IO.Compression;
using System.Text;

namespace DivinityModManager.AppServices;

/// <summary>Creates a user-kept snapshot before the external updater changes Redux files.</summary>
public static class ReduxPreUpdateBackupService
{
	public sealed record Source(string ArchiveFolder, string Directory, bool ProfileLoadOrdersOnly = false);
	public sealed record Progress(string CurrentFile, long CopiedBytes, long TotalBytes);
	private sealed record Entry(string Path, string ArchivePath, long Length);

	public static async Task CreateAsync(string archivePath, IEnumerable<Source> sources,
		IProgress<Progress> progress = null, CancellationToken cancellationToken = default)
	{
		if (String.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("A backup file is required.", nameof(archivePath));
		var target = Path.GetFullPath(archivePath);
		var roots = (sources ?? throw new ArgumentNullException(nameof(sources)))
			.Where(source => source != null && Directory.Exists(source.Directory))
			.Select(source => (source.ArchiveFolder, Path: Path.GetFullPath(source.Directory), source.ProfileLoadOrdersOnly))
			.ToArray();
		if (roots.Length == 0) throw new DirectoryNotFoundException("No Redux or mod-list folders are available to back up.");
		if (roots.Select(root => root.ArchiveFolder).Distinct(StringComparer.OrdinalIgnoreCase).Count() != roots.Length)
			throw new ArgumentException("Backup folder names must be unique.", nameof(sources));
		foreach (var root in roots)
		{
			if (String.IsNullOrWhiteSpace(root.ArchiveFolder) || root.ArchiveFolder.Contains('/') || root.ArchiveFolder.Contains('\\')
				|| root.ArchiveFolder is "." or "..")
				throw new ArgumentException("Invalid backup folder name.", nameof(sources));
			if (IsWithin(target, root.Path))
				throw new IOException("Save the backup outside the Redux installation and mod folders.");
		}

		var entries = new List<Entry>();
		foreach (var root in roots)
		{
			var files = root.ProfileLoadOrdersOnly
				? EnumerateProfileLoadOrders(root.Path)
				: EnumerateFilesWithoutLinks(root.Path);
			foreach (var file in files)
			{
				var relative = Path.GetRelativePath(root.Path, file.FullName);
				entries.Add(new Entry(file.FullName,
					root.ArchiveFolder + "/" + relative.Replace('\\', '/'), file.Length));
			}
		}
		if (entries.Count == 0) throw new IOException("The selected backup sources contain no files.");
		var total = entries.Sum(entry => entry.Length);
		var copied = 0L;
		var partial = target + ".partial";
		if (File.Exists(partial)) throw new IOException("A partial backup already exists at the chosen location.");
		Directory.CreateDirectory(Path.GetDirectoryName(target)!);
		try
		{
			await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
			using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
			{
				var note = archive.CreateEntry("RESTORE.txt", CompressionLevel.Fastest);
				await using (var noteStream = note.Open())
				{
					var instructions = "Redux pre-update backup. Restore while Redux is closed. Copy each archive folder's contents into its original location listed below. Profile Load Orders/ contains only profile modsettings files, not saved games. This archive also contains Redux settings; keep it private.\n\nOriginal locations:\n"
						+ String.Join("\n", roots.Select(root => $"{root.ArchiveFolder}: {root.Path}")) + "\n";
					var bytes = Encoding.UTF8.GetBytes(instructions);
					await noteStream.WriteAsync(bytes, cancellationToken);
				}
				foreach (var entry in entries)
				{
					cancellationToken.ThrowIfCancellationRequested();
					progress?.Report(new Progress(entry.ArchivePath, copied, total));
					var extension = Path.GetExtension(entry.Path);
					var compression = extension.Equals(".pak", StringComparison.OrdinalIgnoreCase)
						|| extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
						|| extension.Equals(".7z", StringComparison.OrdinalIgnoreCase)
						? CompressionLevel.NoCompression : CompressionLevel.Fastest;
					var zipEntry = archive.CreateEntry(entry.ArchivePath, compression);
					await using var input = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
						1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
					await using var destination = zipEntry.Open();
					await input.CopyToAsync(destination, 1024 * 1024, cancellationToken);
					copied += entry.Length;
				}
			}
			File.Move(partial, target, overwrite: true);
			progress?.Report(new Progress("Backup complete", total, total));
		}
		catch
		{
			if (File.Exists(partial)) File.Delete(partial);
			throw;
		}
	}

	private static bool IsWithin(string path, string root) =>
		path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

	private static IEnumerable<FileInfo> EnumerateFilesWithoutLinks(string root)
	{
		var pending = new Stack<DirectoryInfo>();
		pending.Push(new DirectoryInfo(root));
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
				throw new IOException($"Backup source contains a linked directory: {directory.FullName}");
			foreach (var child in directory.EnumerateDirectories()) pending.Push(child);
			foreach (var file in directory.EnumerateFiles())
			{
				if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
					throw new IOException($"Backup source contains a linked file: {file.FullName}");
				yield return file;
			}
		}
	}

	private static IEnumerable<FileInfo> EnumerateProfileLoadOrders(string root)
	{
		var directory = new DirectoryInfo(root);
		if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
			throw new IOException($"Backup source is a linked directory: {root}");
		foreach (var file in directory.EnumerateFiles("playerprofiles8.lsx", SearchOption.TopDirectoryOnly))
			yield return file;
		foreach (var profile in directory.EnumerateDirectories())
		{
			if (profile.Attributes.HasFlag(FileAttributes.ReparsePoint))
				throw new IOException($"Backup source contains a linked profile: {profile.FullName}");
			foreach (var file in profile.EnumerateFiles("modsettings.lsx", SearchOption.TopDirectoryOnly))
				yield return file;
		}
	}
}
