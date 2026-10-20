using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Redux.Core.Tests;

public sealed class CustomArtworkTests
{
	public void HoverDescriptionsUseBoundedPlainTextFromCachedMetadata()
	{
		var mod = new DivinityModData
		{
			Description = "&lt;p&gt;A local &amp; readable description.&lt;/p&gt;<script>hidden()</script> " + new string('x', 400)
		};

		RegressionAssert.True(mod.Metadata.HasHoverDescription);
		RegressionAssert.Contains(mod.Metadata.HoverDescriptionExcerpt, "A local & readable description.");
		RegressionAssert.False(mod.Metadata.HoverDescriptionExcerpt.Contains("<p>", StringComparison.Ordinal));
		RegressionAssert.False(mod.Metadata.HoverDescriptionExcerpt.Contains("hidden", StringComparison.Ordinal));
		RegressionAssert.True(mod.Metadata.HoverDescriptionExcerpt.Length <= 281);
	}

	public void MissingDescriptionsDoNotCreatePlaceholderHoverText()
	{
		var mod = new DivinityModData { Description = "   " };

		RegressionAssert.False(mod.Metadata.HasHoverDescription);
		RegressionAssert.Equal(String.Empty, mod.Metadata.HoverDescriptionExcerpt);
	}

	public void JpegArtworkIsCopiedAsAPathSafePackageScopedPng()
	{
		var jpeg = Encode(CreateBitmap(96, 48, (_, _) => Colors.CornflowerBlue), new JpegBitmapEncoder());
		WithImportedArtwork(jpeg, ".jpg", (sourcePath, reference, stored) =>
		{
			RegressionAssert.True(reference.StartsWith(ReduxModArtworkService.ReferencePrefix, StringComparison.Ordinal));
			RegressionAssert.False(reference.Contains(sourcePath, StringComparison.OrdinalIgnoreCase));
			RegressionAssert.True(ReduxModArtworkService.TryResolvePath(reference, out var storedPath));
			RegressionAssert.True(File.Exists(storedPath));
			RegressionAssert.Equal(".png", Path.GetExtension(storedPath).ToLowerInvariant());
			RegressionAssert.Equal(96, stored.PixelWidth);
			RegressionAssert.Equal(48, stored.PixelHeight);
		});
	}

	public void CustomArtworkHonorsAllEightExifOrientations()
	{
		var colors = new[] { Colors.Red, Colors.Lime, Colors.Blue, Colors.Cyan, Colors.Magenta, Colors.Yellow };
		var jpeg = Encode(CreateBitmap(60, 40, (x, y) => colors[(y / 20) * 3 + x / 20]),
			new JpegBitmapEncoder { QualityLevel = 100 });
		// Expected visual cell order, independent of the decoder's transform matrices.
		int[][] expected =
		[
			[0, 1, 2, 3, 4, 5], [2, 1, 0, 5, 4, 3],
			[5, 4, 3, 2, 1, 0], [3, 4, 5, 0, 1, 2],
			[0, 3, 1, 4, 2, 5], [3, 0, 4, 1, 5, 2],
			[5, 2, 4, 1, 3, 0], [2, 5, 1, 4, 0, 3]
		];
		for (ushort orientation = 1; orientation <= 8; orientation++)
		{
			var columns = orientation <= 4 ? 3 : 2;
			var rows = orientation <= 4 ? 2 : 3;
			WithImportedArtwork(AddExifOrientation(jpeg, orientation), ".jpg", (_, _, stored) =>
			{
				RegressionAssert.Equal(columns * 20, stored.PixelWidth);
				RegressionAssert.Equal(rows * 20, stored.PixelHeight);
				for (var cell = 0; cell < 6; cell++)
				{
					var actual = ReadPixel(stored, (cell % columns) * 20 + 10, (cell / columns) * 20 + 10);
					AssertColorNear(colors[expected[orientation - 1][cell]], actual, 24);
				}
			});
		}
	}

	public void AnimatedCustomArtworkStoresOnlyTheFirstFrame()
	{
		var encoder = new GifBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(8, 6, (_, _) => Colors.Red)));
		encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(8, 6, (_, _) => Colors.Blue)));
		using var output = new MemoryStream();
		encoder.Save(output);
		output.Position = 0;
		var source = new GifBitmapDecoder(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
		RegressionAssert.Equal(2, source.Frames.Count);
		WithImportedArtwork(output.ToArray(), ".gif", (_, _, stored) =>
		{
			RegressionAssert.Equal(1, stored.Decoder!.Frames.Count);
			RegressionAssert.Equal(8, stored.PixelWidth);
			RegressionAssert.Equal(6, stored.PixelHeight);
			AssertColorNear(Colors.Red, ReadPixel(stored, 4, 3), 0);
		});
	}

	public void LargeCustomArtworkFitsWithin2048PixelsWithoutChangingItsAspectRatio()
	{
		var png = Encode(CreateBitmap(3072, 1536, (_, _) => Colors.CornflowerBlue), new PngBitmapEncoder());
		WithImportedArtwork(png, ".png", (_, _, stored) =>
		{
			RegressionAssert.Equal(2048, stored.PixelWidth);
			RegressionAssert.Equal(1024, stored.PixelHeight);
		});
	}

	public void CustomArtworkPreservesTransparencyAndDoesNotUpscaleSmallImages()
	{
		var translucent = Color.FromArgb(128, 220, 40, 70);
		var png = Encode(CreateBitmap(2, 1, (x, _) => x == 0 ? translucent : Colors.Transparent), new PngBitmapEncoder());
		WithImportedArtwork(png, ".png", (_, _, stored) =>
		{
			RegressionAssert.Equal(2, stored.PixelWidth);
			RegressionAssert.Equal(1, stored.PixelHeight);
			AssertColorNear(translucent, ReadPixel(stored, 0, 0), 1);
			RegressionAssert.Equal((byte)0, ReadPixel(stored, 1, 0).A);
		});
	}

	public void CorruptAndTruncatedCustomArtworkDoesNotPublishAReference()
	{
		AssertRejectedImport([1, 2, 3, 4, 5], ".png");
		var png = Encode(CreateBitmap(32, 32,
			(x, y) => Color.FromRgb((byte)(x * 7), (byte)(y * 7), (byte)(x * 3 + y * 5))), new PngBitmapEncoder());
		var dataOffset = FindPngChunk(png, "IDAT");
		var dataLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataOffset, 4));
		AssertRejectedImport(png[..(dataOffset + 8 + dataLength / 2)], ".png");
	}

	public void CustomArtworkRejectsOversizedDimensionsBeforePixelDecoding()
	{
		var png = Encode(CreateBitmap(1, 1, (_, _) => Colors.Red), new PngBitmapEncoder());
		foreach (var dimensions in new[] { (Width: 16385, Height: 1), (Width: 8193, Height: 8193) })
		{
			var oversized = WithPngDimensions(png, dimensions.Width, dimensions.Height);
			AssertRejectedImport(oversized, ".png");
			using var stream = new MemoryStream(oversized);
			var decoder = typeof(ReduxModArtworkService).Assembly.GetType("DivinityModManager.Util.ReduxArtworkDecoder")!;
			var normalize = decoder.GetMethod("NormalizeToPng", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
			var exception = RegressionAssert.Throws<TargetInvocationException>(() => normalize.Invoke(null, [stream, 2048]));
			RegressionAssert.True(exception.InnerException is InvalidDataException);
			RegressionAssert.Contains(exception.InnerException!.Message, "too large");
		}
	}

	public void CustomArtworkOverridesAndThenFallsBackToProviderArtwork()
	{
		var customPath = Path.Combine(Path.GetTempPath(), "custom-preview.png");
		var providerUri = new Uri("https://static.example.test/provider.png");
		var mod = new DivinityModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = "Local name",
			CustomPreviewImagePath = customPath,
			HasCustomPreviewImage = true,
			HasMetadata = true,
			OnlineMetadataEnabled = true,
			NexusModsEnabled = true
		};
		mod.NexusModsData.Update(new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 12345,
			Name = "Provider name",
			PictureUrl = providerUri,
			MetadataOrigin = NexusMetadataOrigin.Manual
		});

		RegressionAssert.True(mod.Metadata.PreviewImageUri.IsFile);
		RegressionAssert.True(mod.Metadata.UsesCustomPreviewImage);
		mod.HasCustomPreviewImage = false;
		RegressionAssert.Equal(providerUri, mod.Metadata.PreviewImageUri);
		RegressionAssert.False(mod.Metadata.UsesCustomPreviewImage);
	}

	private static BitmapSource CreateBitmap(int width, int height, Func<int, int, Color> getColor)
	{
		var pixels = new byte[checked(width * height * 4)];
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var color = getColor(x, y);
				var offset = (y * width + x) * 4;
				pixels[offset] = color.B;
				pixels[offset + 1] = color.G;
				pixels[offset + 2] = color.R;
				pixels[offset + 3] = color.A;
			}
		}
		return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
	}

	private static byte[] Encode(BitmapSource bitmap, BitmapEncoder encoder)
	{
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var output = new MemoryStream();
		encoder.Save(output);
		return output.ToArray();
	}

	private static void WithImportedArtwork(byte[] bytes, string extension, Action<string, string, BitmapFrame> inspect)
	{
		var sourcePath = Path.Combine(Path.GetTempPath(), $"redux-artwork-{Guid.NewGuid():N}{extension}");
		var reference = String.Empty;
		try
		{
			File.WriteAllBytes(sourcePath, bytes);
			if (!ReduxModArtworkService.TryImport(sourcePath, Guid.NewGuid().ToString(), out reference, out var error))
				throw new InvalidOperationException($"Test artwork could not be imported: {error}");
			RegressionAssert.True(ReduxModArtworkService.TryResolvePath(reference, out var storedPath));
			using var stream = File.OpenRead(storedPath);
			var png = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			inspect(sourcePath, reference, png.Frames[0]);
		}
		finally
		{
			if (!String.IsNullOrWhiteSpace(reference)) ReduxModArtworkService.TryDelete(reference, out _);
			if (File.Exists(sourcePath)) File.Delete(sourcePath);
		}
	}

	private static void AssertRejectedImport(byte[] bytes, string extension)
	{
		var sourcePath = Path.Combine(Path.GetTempPath(), $"redux-artwork-{Guid.NewGuid():N}{extension}");
		var reference = String.Empty;
		try
		{
			File.WriteAllBytes(sourcePath, bytes);
			RegressionAssert.False(ReduxModArtworkService.TryImport(sourcePath, Guid.NewGuid().ToString(), out reference, out var error));
			RegressionAssert.Equal(String.Empty, reference);
			RegressionAssert.False(String.IsNullOrWhiteSpace(error));
		}
		finally
		{
			if (!String.IsNullOrWhiteSpace(reference)) ReduxModArtworkService.TryDelete(reference, out _);
			if (File.Exists(sourcePath)) File.Delete(sourcePath);
		}
	}

	private static Color ReadPixel(BitmapSource bitmap, int x, int y)
	{
		var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
		var pixel = new byte[4];
		converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
		return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
	}

	private static void AssertColorNear(Color expected, Color actual, int tolerance)
	{
		if (Math.Abs(expected.R - actual.R) > tolerance || Math.Abs(expected.G - actual.G) > tolerance ||
			Math.Abs(expected.B - actual.B) > tolerance || Math.Abs(expected.A - actual.A) > tolerance)
			throw new InvalidOperationException($"Expected color {expected}, received {actual} (tolerance {tolerance}).");
	}

	private static byte[] AddExifOrientation(byte[] jpeg, ushort orientation)
	{
		// A single standard EXIF orientation entry, inserted after JPEG's SOI marker.
		using var output = new MemoryStream();
		output.Write(jpeg, 0, 2);
		output.Write([0xFF, 0xE1, 0, 34, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0]);
		using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
		writer.Write((ushort)0x4949);
		writer.Write((ushort)42);
		writer.Write((uint)8);
		writer.Write((ushort)1);
		writer.Write((ushort)0x0112);
		writer.Write((ushort)3);
		writer.Write((uint)1);
		writer.Write(orientation);
		writer.Write((ushort)0);
		writer.Write((uint)0);
		output.Write(jpeg, 2, jpeg.Length - 2);
		return output.ToArray();
	}

	private static int FindPngChunk(byte[] png, string type)
	{
		for (var offset = 8; offset + 12 <= png.Length;)
		{
			if (System.Text.Encoding.ASCII.GetString(png, offset + 4, 4) == type) return offset;
			offset += checked(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4)) + 12);
		}
		throw new InvalidOperationException($"Fixture is missing its {type} chunk.");
	}

	private static byte[] WithPngDimensions(byte[] png, int width, int height)
	{
		var changed = (byte[])png.Clone();
		var offset = FindPngChunk(changed, "IHDR");
		BinaryPrimitives.WriteInt32BigEndian(changed.AsSpan(offset + 8, 4), width);
		BinaryPrimitives.WriteInt32BigEndian(changed.AsSpan(offset + 12, 4), height);
		var crc = UInt32.MaxValue;
		foreach (var value in changed.AsSpan(offset + 4, 17))
		{
			crc ^= value;
			for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
		}
		BinaryPrimitives.WriteUInt32BigEndian(changed.AsSpan(offset + 21, 4), ~crc);
		return changed;
	}
}
