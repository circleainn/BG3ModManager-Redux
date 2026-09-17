using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using DivinityModManager.Util;

namespace Redux.Core.Tests;

public sealed class CustomIconTests
{
	public void OpaqueRectangularPngIsNormalizedToASquareIcon()
	{
		var source = CreateOpaquePng(width: 12, height: 6);
		RegressionAssert.True(ReduxCustomIconService.TryImportBytes(source, tint: false,
			out var iconReference, out _));
		try
		{
			RegressionAssert.True(ReduxCustomIconService.TryLoad(iconReference, out var image));
			var bitmap = image as BitmapSource;
			RegressionAssert.True(bitmap != null);
			RegressionAssert.Equal(12, bitmap!.PixelWidth);
			RegressionAssert.Equal(12, bitmap.PixelHeight);
			RegressionAssert.True(!ReduxCustomIconService.IsTintedReference(iconReference));
			RegressionAssert.True(ReduxCustomIconService.IsTintedReference(
				ReduxCustomIconService.WithTint(iconReference, tint: true)));
		}
		finally
		{
			ReduxCustomIconService.TryDelete(iconReference, out _);
		}
	}

	private static byte[] CreateOpaquePng(int width, int height)
	{
		var stride = width * 4;
		var pixels = new byte[stride * height];
		for (var index = 0; index < pixels.Length; index += 4)
		{
			pixels[index] = 0x70;
			pixels[index + 1] = 0x55;
			pixels[index + 2] = 0xA8;
			pixels[index + 3] = byte.MaxValue;
		}
		var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var stream = new MemoryStream();
		encoder.Save(stream);
		return stream.ToArray();
	}
}
