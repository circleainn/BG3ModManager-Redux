using DivinityModManager.Models;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DivinityModManager.Util;

public sealed record ReduxFontChoice(string Name, ReduxTypographyFont BuiltInFont, string CustomReference = "")
{
	public bool IsCustom => !String.IsNullOrWhiteSpace(CustomReference);
}

/// <summary>
/// Imports user fonts into Redux-owned storage and resolves portable, path-free
/// references. Missing or invalid custom fonts always fall back to Manrope.
/// </summary>
public static partial class ReduxCustomFontService
{
	public const string ReferencePrefix = "custom-font:";
	private const int MaximumFileBytes = 10 * 1024 * 1024;
	private const string PendingDeletionFileName = ".pending-delete";
	private static readonly object DeletionSync = new();
	private static readonly ConcurrentDictionary<string, string> KnownFamilyNames = new(StringComparer.OrdinalIgnoreCase);
	private static int _pendingDeletionPassCompleted;

	[GeneratedRegex("^[0-9a-f]{64}\\.(ttf|otf)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex SafeFileNamePattern();

	public static IReadOnlyList<ReduxFontChoice> GetChoices()
	{
		ProcessPendingDeletions();
		var choices = Enum.GetValues<ReduxTypographyFont>()
			.Select(font => new ReduxFontChoice(font.GetDescription(), font))
			.ToList();
		try
		{
			var storageDirectory = GetStorageDirectory();
			if (!Directory.Exists(storageDirectory)) return choices;
			var pendingNames = ReadPendingDeletionNames();
			foreach (var path in Directory.EnumerateFiles(storageDirectory, "*.*", SearchOption.TopDirectoryOnly)
				.Where(path => SafeFileNamePattern().IsMatch(Path.GetFileName(path)))
				.Where(path => !pendingNames.Contains(Path.GetFileName(path)))
				.OrderBy(path => File.GetCreationTimeUtc(path))
				.ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
			{
				var reference = ReferencePrefix + Path.GetFileName(path);
				if (TryGetFamilyName(path, out var familyName))
				{
					choices.Add(new ReduxFontChoice(familyName, ReduxTypographyFont.Manrope, reference));
				}
			}
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to enumerate custom Redux fonts: {exception.Message}");
		}
		return choices;
	}

	public static bool TryImport(string sourcePath, out ReduxFontChoice choice, out string error)
	{
		choice = null;
		error = String.Empty;
		try
		{
			if (String.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
			{
				error = "Choose an existing font file.";
				return false;
			}
			var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
			if (extension is not ".ttf" and not ".otf")
			{
				error = "Custom fonts must be TrueType (.ttf) or OpenType (.otf) files.";
				return false;
			}
			var file = new FileInfo(sourcePath);
			if (file.Length <= 0 || file.Length > MaximumFileBytes)
			{
				error = "Choose a font file smaller than 10 MB.";
				return false;
			}
			if (!TryReadFamilyName(sourcePath, out var familyName))
			{
				error = "Redux could not read a usable font family from that file.";
				return false;
			}

			var bytes = File.ReadAllBytes(sourcePath);
			var fileName = $"{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}{extension}";
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

			var reference = ReferencePrefix + fileName;
			KnownFamilyNames[fileName] = familyName;
			choice = new ReduxFontChoice(familyName, ReduxTypographyFont.Manrope, reference);
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to import a custom Redux font: {exception}");
			error = "Redux could not import that font. Check that the file is readable and try again.";
			return false;
		}
	}

	public static bool TryCreateFontFamily(string reference, out FontFamily family)
	{
		family = null;
		if (!TryResolvePath(reference, out var path) || !File.Exists(path) || !TryGetFamilyName(path, out var familyName)) return false;
		try
		{
			var directory = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
			// WPF caches directory-wide font discovery. Address the imported file
			// explicitly so a font added during this session is available immediately.
			var candidate = new FontFamily(new Uri(directory, UriKind.Absolute), $"./{Path.GetFileName(path)}#{familyName}");
			if (!candidate.GetTypefaces().Any(typeface => typeface.TryGetGlyphTypeface(out _))) return false;
			family = candidate;
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to load custom Redux font '{reference}': {exception.Message}");
			return false;
		}
	}

	public static bool TryDelete(string reference, out string error)
	{
		error = String.Empty;
		if (!TryResolvePath(reference, out var path) || !File.Exists(path))
		{
			error = "That custom font is no longer available.";
			return false;
		}
		// WPF maps loaded fonts for the lifetime of the process. Always remove the
		// font from Redux immediately and recycle the physical file on next launch;
		// attempting deletion now can surface Windows' repeated "Try Again" prompt.
		var fileName = Path.GetFileName(path);
		if (AddPendingDeletion(fileName))
		{
			KnownFamilyNames.TryRemove(fileName, out _);
			return true;
		}
		error = "Redux could not queue that custom font for removal. Try again after restarting Redux.";
		return false;
	}

	public static string GetLibraryDirectory()
	{
		var directory = GetStorageDirectory();
		Directory.CreateDirectory(directory);
		return directory;
	}

	public static void ProcessPendingDeletions()
	{
		// Run once, before this process starts loading fonts. A font queued later in
		// the session must wait for the next launch, when WPF no longer holds it.
		if (Interlocked.Exchange(ref _pendingDeletionPassCompleted, 1) != 0) return;
		lock (DeletionSync)
		{
			var pending = ReadPendingDeletionNames();
			if (pending.Count == 0) return;
			var remaining = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var fileName in pending)
			{
				var path = Path.Combine(GetStorageDirectory(), fileName);
				if (!File.Exists(path)) continue;
				if (!RecycleBinHelper.DeleteFile(path, false, false, out _) || File.Exists(path)) remaining.Add(fileName);
			}
			WritePendingDeletionNames(remaining);
		}
	}

	public static string NormalizeReference(string value) =>
		TryResolvePath(value, out var path) && File.Exists(path)
			? ReferencePrefix + Path.GetFileName(path)
			: String.Empty;

	public static string GetDisplayName(ReduxTypographyFont builtIn, string customReference)
	{
		if (TryResolvePath(customReference, out var path) && TryGetFamilyName(path, out var familyName)) return familyName;
		return builtIn.GetDescription();
	}

	private static bool TryGetFamilyName(string path, out string familyName)
	{
		var fileName = Path.GetFileName(path);
		if (KnownFamilyNames.TryGetValue(fileName, out familyName)) return true;
		if (!TryReadFamilyName(path, out familyName)) return false;
		KnownFamilyNames[fileName] = familyName;
		return true;
	}

	private static bool TryReadFamilyName(string path, out string familyName)
	{
		familyName = String.Empty;
		try
		{
			var glyph = new GlyphTypeface(new Uri(path, UriKind.Absolute));
			familyName = glyph.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out var englishName)
				? englishName
				: glyph.FamilyNames.Values.FirstOrDefault();
			return !String.IsNullOrWhiteSpace(familyName);
		}
		catch
		{
			return false;
		}
	}

	private static bool TryResolvePath(string reference, out string path)
	{
		path = String.Empty;
		if (String.IsNullOrWhiteSpace(reference) || !reference.StartsWith(ReferencePrefix, StringComparison.OrdinalIgnoreCase)) return false;
		var fileName = reference[ReferencePrefix.Length..];
		if (!SafeFileNamePattern().IsMatch(fileName) || !Path.GetFileName(fileName).Equals(fileName, StringComparison.Ordinal)) return false;

		var storageDirectory = Path.GetFullPath(GetStorageDirectory());
		var candidate = Path.GetFullPath(Path.Combine(storageDirectory, fileName));
		if (!candidate.StartsWith(storageDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
		path = candidate;
		return true;
	}

	private static HashSet<string> ReadPendingDeletionNames()
	{
		try
		{
			var markerPath = Path.Combine(GetStorageDirectory(), PendingDeletionFileName);
			if (!File.Exists(markerPath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			return File.ReadAllLines(markerPath)
				.Select(line => line.Trim())
				.Where(line => SafeFileNamePattern().IsMatch(line) && Path.GetFileName(line).Equals(line, StringComparison.Ordinal))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to read pending custom-font deletions: {exception.Message}");
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private static bool AddPendingDeletion(string fileName)
	{
		lock (DeletionSync)
		{
			try
			{
				var pending = ReadPendingDeletionNames();
				pending.Add(fileName);
				WritePendingDeletionNames(pending);
				return true;
			}
			catch (Exception exception)
			{
				DivinityApp.Log($"Failed to queue custom-font deletion: {exception.Message}");
				return false;
			}
		}
	}

	private static void WritePendingDeletionNames(IEnumerable<string> names)
	{
		var directory = GetLibraryDirectory();
		var markerPath = Path.Combine(directory, PendingDeletionFileName);
		var normalized = names.Where(name => SafeFileNamePattern().IsMatch(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToArray();
		if (normalized.Length == 0)
		{
			if (File.Exists(markerPath)) File.Delete(markerPath);
			return;
		}
		var temporaryPath = markerPath + ".tmp";
		try
		{
			File.WriteAllLines(temporaryPath, normalized);
			File.Move(temporaryPath, markerPath, true);
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
	}

	private static string GetStorageDirectory()
	{
		var assemblyDirectory = Path.GetDirectoryName(typeof(ReduxCustomFontService).Assembly.Location);
		var applicationDirectory = String.IsNullOrWhiteSpace(assemblyDirectory)
			? DivinityApp.GetAppDirectory()
			: assemblyDirectory;
		return Path.Combine(applicationDirectory, "Data", "CustomFonts");
	}
}
