using System.Security.Cryptography;

namespace DivinityModManager.AppServices;

/// <summary>Keeps verified package bytes read-only for one review and installation.</summary>
public sealed class VerifiedPackageReadLease : IDisposable
{
	private FileStream? _stream;
	public string FilePath { get; }

	private VerifiedPackageReadLease(string filePath, FileStream stream)
	{
		FilePath = filePath;
		_stream = stream;
	}

	public static async Task<VerifiedPackageReadLease> OpenAsync(string path, long expectedSize,
		string expectedSha256, CancellationToken cancellationToken = default)
	{
		if (String.IsNullOrWhiteSpace(expectedSha256))
			throw new InvalidDataException("The download does not have a verified SHA-256 identity. Download it again before review.");
		var fullPath = Path.GetFullPath(path);
		var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
			65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
		try
		{
			if (expectedSize > 0 && stream.Length != expectedSize)
				throw new InvalidDataException("The downloaded archive size changed after completion.");
			var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
			if (!String.Equals(digest, expectedSha256, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException("The downloaded archive changed after inspection. Download it again before review.");
			cancellationToken.ThrowIfCancellationRequested();
			return new VerifiedPackageReadLease(fullPath, stream);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	public void RequirePath(string path)
	{
		ObjectDisposedException.ThrowIf(_stream == null, this);
		if (!String.Equals(FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException("The preflight result belongs to a different package.", nameof(path));
	}

	public void Dispose()
	{
		_stream?.Dispose();
		_stream = null;
	}
}
