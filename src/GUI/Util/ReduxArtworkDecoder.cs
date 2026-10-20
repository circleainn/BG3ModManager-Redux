using SkiaSharp;

namespace DivinityModManager.Util;

/// <summary>
/// Decodes the first artwork frame into an oriented, bounded PNG for storage or WPF.
/// Callers enforce their encoded-file byte limits before passing a seekable stream.
/// </summary>
internal static class ReduxArtworkDecoder
{
	private const int MaximumSourceDimension = 16384;
	private const long MaximumSourcePixels = 64L * 1024 * 1024;

	public static byte[] NormalizeToPng(Stream encoded, int? maximumDimension = null)
	{
		if (maximumDimension is <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDimension));
		// The codec owns this wrapper, but the caller retains ownership of its stream.
		using var managedStream = new SKManagedStream(encoded, disposeManagedStream: false);
		using var codec = SKCodec.Create(managedStream)
			?? throw new NotSupportedException("That file is not a supported image.");
		var sourceInfo = codec.Info;
		if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0 ||
			sourceInfo.Width > MaximumSourceDimension || sourceInfo.Height > MaximumSourceDimension ||
			(long)sourceInfo.Width * sourceInfo.Height > MaximumSourcePixels)
			throw new InvalidDataException("That image is too large for a mod preview.");

		// Convert embedded color profiles to sRGB before dropping source metadata.
		using var colorSpace = SKColorSpace.CreateSrgb();
		var decodeInfo = new SKImageInfo(sourceInfo.Width, sourceInfo.Height,
			SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
		using var decoded = new SKBitmap(decodeInfo);
		var result = codec.GetPixels(decodeInfo, decoded.GetPixels(), new SKCodecOptions(0));
		if (result != SKCodecResult.Success)
			throw new InvalidDataException("That image is damaged or incomplete.");

		var origin = codec.EncodedOrigin;
		var swapAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
			SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
		var orientedWidth = swapAxes ? sourceInfo.Height : sourceInfo.Width;
		var orientedHeight = swapAxes ? sourceInfo.Width : sourceInfo.Height;
		var scale = maximumDimension.HasValue
			? Math.Min(1d, (double)maximumDimension.Value / Math.Max(orientedWidth, orientedHeight))
			: 1d;
		var width = Math.Max(1, (int)Math.Round(orientedWidth * scale));
		var height = Math.Max(1, (int)Math.Round(orientedHeight * scale));
		using var normalized = new SKBitmap(new SKImageInfo(width, height,
			SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace));
		using (var canvas = new SKCanvas(normalized))
		{
			canvas.Clear(SKColors.Transparent);
			canvas.Scale((float)width / orientedWidth, (float)height / orientedHeight);
			canvas.Concat(OrientationMatrix(origin, sourceInfo.Width, sourceInfo.Height));
			var sampling = scale < 1d
				? new SKSamplingOptions(SKCubicResampler.Mitchell)
				: new SKSamplingOptions(SKFilterMode.Nearest);
			canvas.DrawBitmap(decoded, 0, 0, sampling);
		}

		using var image = SKImage.FromBitmap(normalized);
		using var png = image.Encode(SKEncodedImageFormat.Png, 100)
			?? throw new InvalidDataException("Redux could not encode that image.");
		return png.ToArray();
	}

	private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
	{
		SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
		SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
		SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
		SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
		SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
		SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
		SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
		_ => SKMatrix.Identity
	};
}
