using DivinityModManager.Util;

using LSLib.LS;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class PackageExtractionTests
{
	public void RealPackageExtractionPreservesNestedContentsAcrossCompressionMethods()
	{
		WithDirectory(root =>
		{
			foreach (var compression in new[] { CompressionMethod.None, CompressionMethod.Zlib, CompressionMethod.LZ4 })
			{
				var packagePath = WritePackage(root, compression);
				var destination = Path.Combine(root, compression.ToString());
				Directory.CreateDirectory(Path.Combine(destination, "nested"));
				File.WriteAllText(Path.Combine(destination, "nested", "large.bin"), "previous contents");
				RegressionAssert.True(DivinityFileUtils.ExtractPackageAsync(packagePath, destination,
					CancellationToken.None).GetAwaiter().GetResult());
				RegressionAssert.Equal("before", File.ReadAllText(Path.Combine(destination, "before.txt")));
				RegressionAssert.SequenceEqual(LargeContents(), File.ReadAllBytes(Path.Combine(destination, "nested", "large.bin")));
				RegressionAssert.Equal("after", File.ReadAllText(Path.Combine(destination, "after.txt")));
				RegressionAssert.Equal(3, Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length);
			}
		});
	}

	public void CancellationDuringMemberCopyPreservesExistingFileAndSkipsLaterMembers()
	{
		CheckInterruptedCopy(cancel: true);
	}

	public void FailedMemberCopyPreservesExistingFileAndSkipsLaterMembers()
	{
		CheckInterruptedCopy(cancel: false);
	}

	public void UnsafeMemberPathsAreRejectedBeforeAnyOutputIsPublished()
	{
		WithDirectory(root =>
		{
			foreach (var name in new[] { "../escaped.txt", "nested/../../escaped.txt", "/rooted.txt",
				"C:/escaped.txt", @"\\server\share\escaped.txt", "nested/.. /escaped.txt", "safe.txt:stream" })
			{
				var packagePath = WritePackage(root, CompressionMethod.None, name);
				var destination = Path.Combine(root, "output");
				using var package = new PackageReader().Read(packagePath);
				RegressionAssert.Throws<InvalidDataException>(() => DivinityFileUtils.ExtractPackageFilesAsync(
					package, destination, CancellationToken.None).GetAwaiter().GetResult());
				RegressionAssert.False(Directory.Exists(destination));
				RegressionAssert.False(File.Exists(Path.Combine(root, "escaped.txt")));
			}
		});
	}

	public void PreCanceledExtractionNeverCreatesOutput()
	{
		WithDirectory(root =>
		{
			var packagePath = WritePackage(root, CompressionMethod.None);
			var destination = Path.Combine(root, "output");
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			RegressionAssert.Throws<OperationCanceledException>(() => DivinityFileUtils.ExtractPackageAsync(
				packagePath, destination, cancellation.Token).GetAwaiter().GetResult());
			RegressionAssert.False(Directory.Exists(destination));
		});
	}

	public void DeletedPackageMembersAreNotExtracted()
	{
		WithDirectory(root =>
		{
			var packagePath = WritePackage(root, CompressionMethod.None);
			var destination = Path.Combine(root, "output");
			using var package = new PackageReader().Read(packagePath);
			package.Files.Single(file => file.Name == "after.txt").OffsetInFile = 0xbeefdeadbeef;
			DivinityFileUtils.ExtractPackageFilesAsync(package, destination, CancellationToken.None).GetAwaiter().GetResult();
			RegressionAssert.True(File.Exists(Path.Combine(destination, "before.txt")));
			RegressionAssert.False(File.Exists(Path.Combine(destination, "after.txt")));
		});
	}

	private static void CheckInterruptedCopy(bool cancel)
	{
		WithDirectory(root =>
		{
			var packagePath = WritePackage(root, CompressionMethod.None);
			var destination = Path.Combine(root, "output");
			var existingPath = Path.Combine(destination, "nested", "large.bin");
			Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
			File.WriteAllText(existingPath, "previous contents");
			using var package = new PackageReader().Read(packagePath);
			using var cancellation = new CancellationTokenSource();
			var member = package.Files.Single(file => file.Name == "nested/large.bin");
			using var contents = member.CreateContentReader();
			using var controlled = new InterruptedReadStream(contents, cancel ? cancellation : null);
			// Use LSLib's real content-reader path with a deterministic interruption during the second read.
			member.Solid = true;
			member.SolidStream = controlled;
			member.SolidOffset = 0;
			member.UncompressedSize = (ulong)contents.Length;
			Action extract = () => DivinityFileUtils.ExtractPackageFilesAsync(package, destination,
				cancellation.Token).GetAwaiter().GetResult();
			if (cancel) RegressionAssert.Throws<OperationCanceledException>(extract);
			else RegressionAssert.Throws<IOException>(extract);
			RegressionAssert.Equal(2, controlled.ReadCount);
			RegressionAssert.True(controlled.BytesRead > 0 && controlled.BytesRead < contents.Length);
			RegressionAssert.Equal("before", File.ReadAllText(Path.Combine(destination, "before.txt")));
			RegressionAssert.Equal("previous contents", File.ReadAllText(existingPath));
			RegressionAssert.False(File.Exists(Path.Combine(destination, "after.txt")));
			RegressionAssert.Equal(2, Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length);
		});
	}

	private static byte[] LargeContents() => Enumerable.Range(0, 512 * 1024).Select(value => (byte)(value % 251)).ToArray();

	private static string WritePackage(string root, CompressionMethod compression, string lastName = "after.txt")
	{
		var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".pak");
		var build = new PackageBuildData { Compression = compression };
		build.Files.Add(PackageBuildInputFile.CreateFromBlob(Encoding.UTF8.GetBytes("before"), "before.txt"));
		build.Files.Add(PackageBuildInputFile.CreateFromBlob(LargeContents(), "nested/large.bin"));
		build.Files.Add(PackageBuildInputFile.CreateFromBlob(Encoding.UTF8.GetBytes("after"), lastName));
		using var writer = PackageWriterFactory.Create(build, path);
		writer.Write();
		return path;
	}

	private static void WithDirectory(Action<string> action)
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxPackageExtractionTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try { action(root); }
		finally { Directory.Delete(root, true); }
	}

	private sealed class InterruptedReadStream(Stream source, CancellationTokenSource? cancellation) : Stream
	{
		public int ReadCount { get; private set; }
		public long BytesRead { get; private set; }
		private int Observe(int count)
		{
			BytesRead += count;
			if (++ReadCount == 2)
			{
				if (cancellation != null) cancellation.Cancel();
				else throw new IOException("Simulated member read failure after the first output chunk.");
			}
			return count;
		}
		public override int Read(byte[] buffer, int offset, int count) => Observe(source.Read(buffer, offset, count));
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
			Observe(await source.ReadAsync(buffer, offset, count, token));
		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => false;
		public override long Length => source.Length;
		public override long Position { get => source.Position; set => source.Position = value; }
		public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
		public override void Flush() { }
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
