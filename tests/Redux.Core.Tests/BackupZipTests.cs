using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class BackupZipTests
{
	public void AsyncBackupZipPreservesPackageContents()
	{
		WithFixture((directory, source, bytes) =>
		{
			var path = Path.Combine(directory, "backup.zip");
			async Task WriteAsync()
			{
				await using var writer = await WriterFactory.OpenAsyncWriter(path, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate));
				await WritePackageAsync(writer, source, CancellationToken.None);
			}
			WriteAsync().GetAwaiter().GetResult();
			using var zip = ZipFile.OpenRead(path);
			using var contents = zip.Entries.Single().Open();
			using var copy = new MemoryStream();
			contents.CopyTo(copy);
			RegressionAssert.Equal("Example.pak", zip.Entries.Single().FullName);
			RegressionAssert.SequenceEqual(bytes, copy.ToArray());
		});
	}

	public void CancelingDuringZipEntryPreservesPreviousBackupAndReleasesFiles()
	{
		WithFixture((directory, source, bytes) =>
		{
			var path = Path.Combine(directory, "backup.zip");
			File.WriteAllText(path, "previous backup");
			using var cancellation = new CancellationTokenSource();
			var bytesWritten = 0L;
			RegressionAssert.Throws<OperationCanceledException>(() => AtomicFileWriter.WriteFileAsync(path,
				async (temporaryPath, token) =>
				{
					await using var output = File.Create(temporaryPath);
					using var cancelingOutput = new CancelingWriteStream(output, cancellation);
					try
					{
						await using var writer = await WriterFactory.OpenAsyncWriter(cancelingOutput, ArchiveType.Zip,
							new WriterOptions(CompressionType.Deflate), token);
						await WritePackageAsync(writer, source, token);
					}
					finally { bytesWritten = cancelingOutput.BytesWritten; }
				}, cancellationToken: cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.True(bytesWritten >= 32768 && bytesWritten < bytes.Length);
			RegressionAssert.Equal("previous backup", File.ReadAllText(path));
			RegressionAssert.Equal(0, Directory.GetFiles(directory, "*.tmp").Length);
			using var exclusive = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None);
			RegressionAssert.Equal((long)bytes.Length, exclusive.Length);
		});
	}

	public void CanceledEditorPackageBuildDoesNotCreateOutput()
	{
		WithFixture((directory, source, _) =>
		{
			var output = Path.Combine(directory, "editor.pak");
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			RegressionAssert.Throws<OperationCanceledException>(() => DivinityFileUtils.CreatePackageAsync(
				directory, [source], output, cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.False(File.Exists(output));
		});
	}

	private static Task WritePackageAsync(IAsyncWriter writer, string source, CancellationToken token) =>
		(Task)typeof(MainWindowViewModel).GetMethod("WriteZipAsync", BindingFlags.NonPublic | BindingFlags.Static)!
			.Invoke(null, [writer, "Example.pak", source, token])!;

	private static void WithFixture(Action<string, string, byte[]> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxBackupZipTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			var bytes = new byte[2 * 1024 * 1024];
			new Random(47).NextBytes(bytes);
			var source = Path.Combine(directory, "Example.pak");
			File.WriteAllBytes(source, bytes);
			action(directory, source, bytes);
		}
		finally { Directory.Delete(directory, true); }
	}

	private sealed class CancelingWriteStream(Stream inner, CancellationTokenSource cancellation) : Stream
	{
		public long BytesWritten { get; private set; }
		private void Wrote(int count)
		{
			BytesWritten += count;
			if (BytesWritten >= 32768) cancellation.Cancel();
		}
		public override bool CanRead => false;
		public override bool CanSeek => inner.CanSeek;
		public override bool CanWrite => true;
		public override long Length => inner.Length;
		public override long Position { get => inner.Position; set => inner.Position = value; }
		public override void Flush() => inner.Flush();
		public override Task FlushAsync(CancellationToken token) => inner.FlushAsync(token);
		public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
		public override void SetLength(long value) => inner.SetLength(value);
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); Wrote(count); }
		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
		{ await inner.WriteAsync(buffer.AsMemory(offset, count), token); Wrote(count); }
		public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
		{ await inner.WriteAsync(buffer, token); Wrote(buffer.Length); }
	}
}
