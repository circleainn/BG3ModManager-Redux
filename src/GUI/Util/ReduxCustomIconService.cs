using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DivinityModManager.Util;

/// <summary>
/// Imports small transparent PNG icons into Redux-owned storage and resolves their
/// persisted, path-free references. Custom icons are presentation data only.
/// </summary>
public static partial class ReduxCustomIconService
{
	public const string ReferencePrefix = "custom-png:";
	public const string TintedReferencePrefix = "custom-png-tint:";
	private const int MaximumFileBytes = 2 * 1024 * 1024;
	private const int MaximumPixelDimension = 1024;

	[GeneratedRegex("^[0-9a-f]{64}\\.png$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex SafeFileNamePattern();

	public static bool IsCustomReference(string value) =>
		!String.IsNullOrWhiteSpace(value) &&
		(value.StartsWith(ReferencePrefix, StringComparison.OrdinalIgnoreCase) ||
		 value.StartsWith(TintedReferencePrefix, StringComparison.OrdinalIgnoreCase));

	public static bool IsTintedReference(string value) =>
		!String.IsNullOrWhiteSpace(value) && value.StartsWith(TintedReferencePrefix, StringComparison.OrdinalIgnoreCase);

	public static string WithTint(string iconReference, bool tint)
	{
		if (!TryResolvePath(iconReference, out var path)) return String.Empty;
		return (tint ? TintedReferencePrefix : ReferencePrefix) + Path.GetFileName(path);
	}

	public static IReadOnlyList<string> GetStoredReferences()
	{
		try
		{
			var storageDirectory = GetStorageDirectory();
			if (!Directory.Exists(storageDirectory)) return Array.Empty<string>();
			return Directory.EnumerateFiles(storageDirectory, "*.png", SearchOption.TopDirectoryOnly)
				.Select(path => new FileInfo(path))
				.Where(file => SafeFileNamePattern().IsMatch(file.Name))
				.OrderBy(file => file.CreationTimeUtc)
				.ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
				.Select(file => ReferencePrefix + file.Name)
				.ToList();
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to enumerate custom Redux icons: {exception.Message}");
			return Array.Empty<string>();
		}
	}

	public static bool TryDelete(string iconReference, out string error)
	{
		error = String.Empty;
		if (!TryResolvePath(iconReference, out var path) || !File.Exists(path))
		{
			error = "That custom icon is no longer available.";
			return false;
		}
		if (RecycleBinHelper.DeleteFile(path, false, false, out var deleteError) && !File.Exists(path)) return true;
		error = String.IsNullOrWhiteSpace(deleteError)
			? "Redux could not remove that custom icon. Close any program using the file and try again."
			: $"Redux could not remove that custom icon.\n\n{deleteError}";
		return false;
	}

	public static bool TryImport(string sourcePath, out string iconReference, out string error)
	{
		iconReference = String.Empty;
		error = String.Empty;
		try
		{
			if (String.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
			{
				error = "Choose an existing PNG file.";
				return false;
			}
			if (!Path.GetExtension(sourcePath).Equals(".png", StringComparison.OrdinalIgnoreCase))
			{
				error = "Custom icons must be PNG files.";
				return false;
			}

			var file = new FileInfo(sourcePath);
			if (file.Length <= 0 || file.Length > MaximumFileBytes)
			{
				error = "Choose a PNG smaller than 2 MB.";
				return false;
			}

			var bytes = File.ReadAllBytes(sourcePath);
			if (!TryValidatePng(bytes, out error)) return false;

			var fileName = $"{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}.png";
			var storageDirectory = GetStorageDirectory();
			Directory.CreateDirectory(storageDirectory);
			var destinationPath = Path.Combine(storageDirectory, fileName);
			if (!File.Exists(destinationPath))
			{
				var temporaryPath = Path.Combine(storageDirectory, $".{Guid.NewGuid():N}.tmp");
				try
				{
					File.WriteAllBytes(temporaryPath, bytes);
					File.Move(temporaryPath, destinationPath, true);
				}
				finally
				{
					if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
				}
			}

			iconReference = ReferencePrefix + fileName;
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to import a custom Redux icon: {exception}");
			error = "Redux could not import that PNG. Check that the file is readable and try again.";
			return false;
		}
	}

	public static bool TryLoad(string iconReference, out ImageSource imageSource)
	{
		imageSource = null;
		if (!TryResolvePath(iconReference, out var path) || !File.Exists(path)) return false;
		try
		{
			using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			if (decoder.Frames.Count != 1) return false;
			var frame = decoder.Frames[0];
			if (frame.CanFreeze) frame.Freeze();
			imageSource = frame;
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to load a custom Redux icon: {exception.Message}");
			return false;
		}
	}

	public static string NormalizeReference(string value) =>
		TryResolvePath(value, out var path) && File.Exists(path)
			? (IsTintedReference(value) ? TintedReferencePrefix : ReferencePrefix) + Path.GetFileName(path)
			: String.Empty;

	private static bool TryValidatePng(byte[] bytes, out string error)
	{
		error = String.Empty;
		try
		{
			using var stream = new MemoryStream(bytes, writable: false);
			var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			if (decoder.Frames.Count != 1)
			{
				error = "Animated or multi-frame PNG files are not supported.";
				return false;
			}

			var frame = decoder.Frames[0];
			if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || frame.PixelWidth != frame.PixelHeight)
			{
				error = "Choose a square PNG so the icon is not stretched.";
				return false;
			}
			if (frame.PixelWidth > MaximumPixelDimension)
			{
				error = "Custom icons must be 1024 × 1024 pixels or smaller.";
				return false;
			}

			var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
			var stride = converted.PixelWidth * 4;
			var pixels = new byte[stride * converted.PixelHeight];
			converted.CopyPixels(pixels, stride, 0);
			for (var index = 3; index < pixels.Length; index += 4)
			{
				if (pixels[index] < Byte.MaxValue) return true;
			}

			error = "Choose a PNG with a transparent background.";
			return false;
		}
		catch
		{
			error = "That file is not a valid PNG image.";
			return false;
		}
	}

	private static bool TryResolvePath(string iconReference, out string path)
	{
		path = String.Empty;
		if (!IsCustomReference(iconReference)) return false;
		var prefixLength = IsTintedReference(iconReference) ? TintedReferencePrefix.Length : ReferencePrefix.Length;
		var fileName = iconReference[prefixLength..];
		if (!SafeFileNamePattern().IsMatch(fileName) || !Path.GetFileName(fileName).Equals(fileName, StringComparison.Ordinal)) return false;

		var storageDirectory = Path.GetFullPath(GetStorageDirectory());
		var candidate = Path.GetFullPath(Path.Combine(storageDirectory, fileName));
		if (!candidate.StartsWith(storageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
		path = candidate;
		return true;
	}

	private static string GetStorageDirectory()
	{
		var assemblyDirectory = Path.GetDirectoryName(typeof(ReduxCustomIconService).Assembly.Location);
		var applicationDirectory = String.IsNullOrWhiteSpace(assemblyDirectory)
			? DivinityApp.GetAppDirectory()
			: assemblyDirectory;
		return Path.Combine(applicationDirectory, "Data", "CustomIcons");
	}
}
