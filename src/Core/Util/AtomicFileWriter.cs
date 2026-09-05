using System.Collections.Concurrent;
using System.Text;

namespace DivinityModManager.Util;

/// <summary>
/// Writes a complete file beside its destination, validates it, and only then
/// replaces the live file. Keeping the temporary file on the same volume lets
/// Windows perform the final replacement atomically.
/// </summary>
public static class AtomicFileWriter
{
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> DestinationLocks =
		new(StringComparer.OrdinalIgnoreCase);

	public static void CopyFile(string sourcePath, string destinationPath, string backupPath = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
		if (!File.Exists(sourcePath)) throw new FileNotFoundException("The source file does not exist.", sourcePath);
		var expectedLength = new FileInfo(sourcePath).Length;
		WriteFile(destinationPath, temporaryPath => File.Copy(sourcePath, temporaryPath, false), backupPath,
			temporaryPath => new FileInfo(temporaryPath).Length == expectedLength);
	}

	public static Task CopyFileAsync(string sourcePath, string destinationPath,
		string backupPath = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
		if (!File.Exists(sourcePath)) throw new FileNotFoundException("The source file does not exist.", sourcePath);
		var expectedLength = new FileInfo(sourcePath).Length;
		return WriteFileAsync(destinationPath, async (temporaryPath, token) =>
		{
			await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
				128000, FileOptions.Asynchronous | FileOptions.SequentialScan);
			await using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
				FileShare.None, 128000, FileOptions.Asynchronous | FileOptions.WriteThrough);
			await source.CopyToAsync(destination, 128000, token);
			await destination.FlushAsync(token);
			destination.Flush(true);
		}, backupPath, temporaryPath => new FileInfo(temporaryPath).Length == expectedLength,
			cancellationToken);
	}

	public static void WriteFile(string destinationPath, Action<string> writeTemporaryFile,
		string backupPath = null, Func<string, bool> validateTemporaryFile = null)
	{
		ArgumentNullException.ThrowIfNull(writeTemporaryFile);
		var destinationLock = GetDestinationLock(destinationPath);
		destinationLock.Wait();
		try
		{
			var temporaryPath = GetTemporaryPath(destinationPath);
			PrepareDirectory(destinationPath);
			try
			{
				writeTemporaryFile(temporaryPath);
				Validate(temporaryPath, validateTemporaryFile);
				Commit(temporaryPath, destinationPath, backupPath);
			}
			catch
			{
				DeleteTemporaryFile(temporaryPath);
				throw;
			}
		}
		finally
		{
			destinationLock.Release();
		}
	}

	public static async Task WriteFileAsync(string destinationPath,
		Func<string, CancellationToken, Task> writeTemporaryFile,
		string backupPath = null, Func<string, bool> validateTemporaryFile = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(writeTemporaryFile);
		var destinationLock = GetDestinationLock(destinationPath);
		await destinationLock.WaitAsync(cancellationToken);
		try
		{
			var temporaryPath = GetTemporaryPath(destinationPath);
			PrepareDirectory(destinationPath);
			try
			{
				await writeTemporaryFile(temporaryPath, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				Validate(temporaryPath, validateTemporaryFile);
				Commit(temporaryPath, destinationPath, backupPath);
			}
			catch
			{
				DeleteTemporaryFile(temporaryPath);
				throw;
			}
		}
		finally
		{
			destinationLock.Release();
		}
	}

	public static void WriteAllText(string destinationPath, string contents, string backupPath = null,
		Func<string, bool> validateTemporaryFile = null)
	{
		var bytes = new UTF8Encoding(false).GetBytes(contents ?? String.Empty);
		WriteAllBytes(destinationPath, bytes, backupPath, validateTemporaryFile);
	}

	public static void WriteAllBytes(string destinationPath, byte[] contents, string backupPath = null,
		Func<string, bool> validateTemporaryFile = null)
	{
		ArgumentNullException.ThrowIfNull(contents);
		WriteFile(destinationPath, temporaryPath =>
		{
			using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
				Math.Max(4096, contents.Length), FileOptions.WriteThrough))
			{
				stream.Write(contents, 0, contents.Length);
				stream.Flush(true);
			}
		}, backupPath, validateTemporaryFile);
	}

	public static async Task WriteAllBytesAsync(string destinationPath, byte[] contents, string backupPath = null,
		Func<string, bool> validateTemporaryFile = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(contents);
		await WriteFileAsync(destinationPath, async (temporaryPath, token) =>
		{
			using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
				Math.Max(4096, contents.Length), FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				await stream.WriteAsync(contents, token);
				await stream.FlushAsync(token);
				stream.Flush(true);
			}
		}, backupPath, validateTemporaryFile, cancellationToken);
	}

	private static SemaphoreSlim GetDestinationLock(string destinationPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
		return DestinationLocks.GetOrAdd(Path.GetFullPath(destinationPath), _ => new SemaphoreSlim(1, 1));
	}

	private static string GetTemporaryPath(string destinationPath) =>
		$"{destinationPath}.{Guid.NewGuid():N}.tmp";

	private static void PrepareDirectory(string destinationPath)
	{
		var directory = Path.GetDirectoryName(destinationPath);
		if (String.IsNullOrWhiteSpace(directory))
			throw new ArgumentException("The destination must include a parent directory.", nameof(destinationPath));
		Directory.CreateDirectory(directory);
	}

	private static void Validate(string temporaryPath, Func<string, bool> validator)
	{
		if (!File.Exists(temporaryPath))
			throw new InvalidDataException($"Temporary file '{temporaryPath}' was not created.");
		if (validator != null && !validator(temporaryPath))
			throw new InvalidDataException($"Temporary file '{temporaryPath}' did not pass validation.");
	}

	private static void Commit(string temporaryPath, string destinationPath, string backupPath)
	{
		if (File.Exists(destinationPath))
		{
			File.Replace(temporaryPath, destinationPath, backupPath, true);
		}
		else
		{
			File.Move(temporaryPath, destinationPath);
		}
	}

	private static void DeleteTemporaryFile(string temporaryPath)
	{
		try
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not remove temporary file '{temporaryPath}': {ex}");
		}
	}
}
