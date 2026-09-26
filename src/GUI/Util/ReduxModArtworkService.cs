using DivinityModManager.Util;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DivinityModManager.Util;

/// <summary>
/// Normalizes user-selected mod previews into path-safe Redux-owned PNG files.
/// References are scoped to a package UUID, so packages linked to the same
/// provider project can keep independent artwork.
/// </summary>
public static partial class ReduxModArtworkService
{
	public const string ReferencePrefix = "custom-artwork:";
	private const int MaximumSourceBytes = 20 * 1024 * 1024;
	private const int MaximumSourceDimension = 16384;
	private const long MaximumSourcePixels = 64L * 1024 * 1024;
	private const int MaximumStoredDimension = 2048;

	[GeneratedRegex("^[0-9a-f]{16}-[0-9a-f]{32}\\.png$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex SafeFileNamePattern();

	public static bool TryImport(
		string sourcePath,
		string modUuid,
		out string artworkReference,
		out string error)
	{
		artworkReference = String.Empty;
		error = String.Empty;
		try
		{
			if (String.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
			{
				error = "Choose an existing image file.";
				return false;
			}
			if (String.IsNullOrWhiteSpace(modUuid))
			{
				error = "This mod does not have a stable UUID for custom artwork.";
				return false;
			}

			var source = new FileInfo(sourcePath);
			if (source.Length is <= 0 or > MaximumSourceBytes)
			{
				error = "Choose an image smaller than 20 MB.";
				return false;
			}

			using (var identifyStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete))
			{
				var info = Image.Identify(identifyStream);
				if (info == null || info.Width <= 0 || info.Height <= 0 ||
					info.Width > MaximumSourceDimension || info.Height > MaximumSourceDimension ||
					(long)info.Width * info.Height > MaximumSourcePixels)
				{
					error = "That image is too large for a mod preview.";
					return false;
				}
			}

			using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete);
			using var image = Image.Load(input);
			while (image.Frames.Count > 1) image.Frames.RemoveFrame(image.Frames.Count - 1);
			image.Mutate(context =>
			{
				context.AutoOrient();
				if (image.Width > MaximumStoredDimension || image.Height > MaximumStoredDimension)
				{
					context.Resize(new ResizeOptions
					{
						Mode = ResizeMode.Max,
						Size = new Size(MaximumStoredDimension, MaximumStoredDimension)
					});
				}
			});

			using var output = new MemoryStream();
			image.Save(output, new PngEncoder());
			var normalizedBytes = output.ToArray();
			var packageHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modUuid.Trim().ToLowerInvariant())))
				.ToLowerInvariant()[..16];
			var contentHash = Convert.ToHexString(SHA256.HashData(normalizedBytes)).ToLowerInvariant()[..32];
			var fileName = $"{packageHash}-{contentHash}.png";
			var storageDirectory = GetStorageDirectory();
			Directory.CreateDirectory(storageDirectory);
			var destinationPath = Path.Combine(storageDirectory, fileName);
			if (!File.Exists(destinationPath)) AtomicFileWriter.WriteAllBytes(destinationPath, normalizedBytes);

			artworkReference = ReferencePrefix + fileName;
			return true;
		}
		catch (UnknownImageFormatException)
		{
			error = "That file is not a supported image.";
			return false;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to import custom mod artwork: {exception}");
			error = "Redux could not import that image. Check that the file is readable and try again.";
			return false;
		}
	}

	public static bool TryResolvePath(string artworkReference, out string path)
	{
		path = String.Empty;
		if (String.IsNullOrWhiteSpace(artworkReference) ||
			!artworkReference.StartsWith(ReferencePrefix, StringComparison.OrdinalIgnoreCase)) return false;
		var fileName = artworkReference[ReferencePrefix.Length..];
		if (!SafeFileNamePattern().IsMatch(fileName) ||
			!Path.GetFileName(fileName).Equals(fileName, StringComparison.Ordinal)) return false;

		var storageDirectory = Path.GetFullPath(GetStorageDirectory());
		var candidate = Path.GetFullPath(Path.Combine(storageDirectory, fileName));
		if (!candidate.StartsWith(storageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
		path = candidate;
		return true;
	}

	public static bool TryDelete(string artworkReference, out string error)
	{
		error = String.Empty;
		try
		{
			if (!TryResolvePath(artworkReference, out var path) || !File.Exists(path)) return true;
			File.Delete(path);
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to remove custom mod artwork: {exception.Message}");
			error = "Redux could not remove the previous custom artwork file.";
			return false;
		}
	}

	private static string GetStorageDirectory()
	{
		var assemblyDirectory = Path.GetDirectoryName(typeof(ReduxModArtworkService).Assembly.Location);
		var applicationDirectory = String.IsNullOrWhiteSpace(assemblyDirectory)
			? DivinityApp.GetAppDirectory()
			: assemblyDirectory;
		return Path.Combine(applicationDirectory, "Data", "CustomArtwork");
	}
}
