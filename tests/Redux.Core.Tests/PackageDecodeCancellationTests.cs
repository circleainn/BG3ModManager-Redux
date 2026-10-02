using LSLib.LS;
using LSLib.LS.Enums;
using LSLib.Native;
using DivinityModManager.Util;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Redux.Core.Tests;

public sealed class PackageDecodeCancellationTests
{
	public void CancellableBlockDecoderMatchesLegacyEncoder()
	{
		var random = new Random(321);
		foreach (var length in new[] { 0, 1, 4, 15, 255, 65535, 65536, 300001 })
		{
			var data = new byte[length];
			foreach (var pattern in new[] { 0, 1, 2 })
			{
				if (pattern == 1) random.NextBytes(data);
				if (pattern == 2) for (var index = 0; index < length; index++) data[index] = (byte)(index % 251);
				foreach (var level in new[] { LSCompressionLevel.Fast, LSCompressionLevel.Max })
				{
					var compressed = CompressionHelpers.CompressLZ4(data, level);
					RegressionAssert.SequenceEqual(data, PackageReadCancellation.DecodeBlock(compressed, length, CancellationToken.None));
				}
			}
		}
	}

	public void InvalidLz4BlocksFailWithoutPublishingOutput()
	{
		foreach (var bytes in new byte[][] { [0, 0, 0], [0, 1, 0], [0xf0], [0x10], [0xf0, 255] })
			RegressionAssert.Throws<InvalidDataException>(() => PackageReadCancellation.DecodeBlock(bytes, 16, CancellationToken.None));
	}

	public void CancellationInterruptsLargeRawAndSolidDecodes()
	{
		var data = new byte[128 * 1024 * 1024];
		var block = CompressionHelpers.CompressLZ4(data, LSCompressionLevel.Fast);
		var frame = LZ4FrameCompressor.Compress(data);
		foreach (var solid in new[] { false, true })
		{
			using var cancellation = new CancellationTokenSource();
			cancellation.CancelAfter(1);
			var error = RegressionAssert.Throws<OperationCanceledException>(() =>
			{
				if (solid) CancellableLZ4.Decompress(frame, data.Length, cancellation.Token);
				else PackageReadCancellation.DecodeBlock(block, data.Length, cancellation.Token);
			});
			RegressionAssert.Equal(cancellation.Token, error.CancellationToken);
		}
	}

	public void RealSolidPackageExtractsAndValidatesFrameSize()
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxSolidPakTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var data = Enumerable.Range(0, 700001).Select(index => (byte)(index % 251)).ToArray();
			var frame = LZ4FrameCompressor.Compress(data);
			RegressionAssert.SequenceEqual(data, CancellableLZ4.Decompress(frame, data.Length, CancellationToken.None));
			RegressionAssert.Throws<InvalidDataException>(() => CancellableLZ4.Decompress(frame, data.Length - 1, CancellationToken.None));
			RegressionAssert.Throws<InvalidDataException>(() => CancellableLZ4.Decompress(frame[..^1], data.Length, CancellationToken.None));
			var path = Path.Combine(root, "Solid.pak");
			WriteSolidPackage(path, frame, data.Length);
			var output = Path.Combine(root, "output");
			RegressionAssert.True(DivinityFileUtils.ExtractPackageAsync(path, output, CancellationToken.None).GetAwaiter().GetResult());
			RegressionAssert.SequenceEqual(data, File.ReadAllBytes(Path.Combine(output, "fixture.bin")));
		}
		finally { Directory.Delete(root, true); }
	}

	public void PackageCancellationScopeIsRestoredAndLoaderPropagatesCancellation()
	{
		using var cancellation = new CancellationTokenSource();
		using (var scope = new PackageReadCancellation(cancellation.Token))
			RegressionAssert.Equal(cancellation.Token, PackageReadCancellation.Current);
		RegressionAssert.Equal(CancellationToken.None, PackageReadCancellation.Current);
		cancellation.Cancel();
		RegressionAssert.Throws<OperationCanceledException>(() => DivinityModDataLoader.LoadModDataFromPakAsync(
			"unused.pak", [], cancellation.Token).GetAwaiter().GetResult());
	}

	private static void WriteSolidPackage(string path, byte[] frame, int size)
	{
		// A real v18 solid package: fixed 40-byte header and one compressed FileEntry18.
		using var index = new MemoryStream();
		using (var writer = new BinaryWriter(index, Encoding.UTF8, true))
		{
			var name = new byte[256];
			Encoding.UTF8.GetBytes("fixture.bin").CopyTo(name, 0);
			writer.Write(name);
			writer.Write(47u);
			writer.Write((ushort)0);
			writer.Write((byte)0);
			writer.Write((byte)CompressionFlags.MethodLZ4);
			writer.Write((uint)(frame.Length - 7));
			writer.Write((uint)size);
		}
		var compressedIndex = CompressionHelpers.CompressLZ4(index.ToArray(), LSCompressionLevel.Fast);
		using var file = new BinaryWriter(File.Create(path));
		file.Write(PackageHeaderCommon.Signature);
		file.Write(18u);
		file.Write((ulong)(40 + frame.Length));
		file.Write((uint)(8 + compressedIndex.Length));
		file.Write((byte)PackageFlags.Solid);
		file.Write((byte)0);
		file.Write(new byte[16]);
		file.Write((ushort)1);
		file.Write(frame);
		file.Write(1);
		file.Write(compressedIndex.Length);
		file.Write(compressedIndex);
	}
}
