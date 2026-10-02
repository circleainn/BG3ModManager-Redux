using System.IO.Compression;

namespace DivinityModManager.AppServices;

/// <summary>Makes a loose multipart set one verifiable, retainable Download Manager item.</summary>
public sealed class PreparedPakInput : IDisposable
{
	public string Path { get; private set; }
	private string _ownedDirectory;
	private PreparedPakInput(string path) => Path = path;
	public static async Task<PreparedPakInput> OpenAsync(string path, CancellationToken token)
	{
		var prepared = new PreparedPakInput(path);
		if (!System.IO.Path.GetExtension(path).Equals(".pak", StringComparison.OrdinalIgnoreCase)) return prepared;
		var parts = PakFileSet.GetPaths(path);
		if (parts.Count == 1) return prepared;
		var inputs = new List<FileStream>();
		try
		{
			foreach (var part in parts)
			{
				token.ThrowIfCancellationRequested();
				inputs.Add(new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true));
			}
			if (inputs.Sum(input => input.Length) > 32L * 1024 * 1024 * 1024)
				throw new InvalidDataException("The PAK set exceeds Redux's 32 GB package limit.");
			prepared._ownedDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ReduxPakSet-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(prepared._ownedDirectory);
			prepared.Path = System.IO.Path.Combine(prepared._ownedDirectory, System.IO.Path.GetFileNameWithoutExtension(path) + ".zip");
			await using var output = new FileStream(prepared.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
			using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
			{
				for (var index = 0; index < inputs.Count; index++)
				{
					token.ThrowIfCancellationRequested();
					var entry = zip.CreateEntry(System.IO.Path.GetFileName(parts[index]), CompressionLevel.NoCompression);
					// Stable ZIP metadata gives identical sets the same retained content identity.
					entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
					await using var stream = entry.Open();
					await inputs[index].CopyToAsync(stream, 65536, token);
				}
			}
			token.ThrowIfCancellationRequested();
			return prepared;
		}
		catch { prepared.Dispose(); throw; }
		finally { foreach (var input in inputs) input.Dispose(); }
	}
	public void Dispose()
	{
		if (_ownedDirectory == null) return;
		try { Directory.Delete(_ownedDirectory, true); }
		catch (IOException) { }
		catch (UnauthorizedAccessException) { }
	}
}
