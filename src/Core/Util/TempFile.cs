namespace DivinityModManager.Util;

public sealed class TempFile : IDisposable
{
	private readonly FileStream _stream;
	private readonly int _bufferSize;

	public FileStream Stream => _stream;

	// 128 KB for asynchronous copies; FileStream's default is 4 KB.
	private TempFile(string sourcePath, int bufferSize = 128000)
	{
		_bufferSize = bufferSize;
		var tempDir = DivinityApp.GetAppDirectory("Temp");
		Directory.CreateDirectory(tempDir);
		var path = Path.Join(tempDir, Path.GetFileName(sourcePath));
		_stream = File.Create(path, _bufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
	}

	public static async Task<TempFile> CreateAsync(string sourcePath, Stream sourceStream, CancellationToken token)
	{
		var temp = new TempFile(sourcePath);
		await temp.CopyAsync(sourceStream, token);
		return temp;
	}

	private async Task CopyAsync(Stream sourceStream, CancellationToken token)
	{
		await sourceStream.CopyToAsync(_stream, _bufferSize, token);
	}

	public void Dispose()
	{
		_stream?.Dispose();
	}
}
