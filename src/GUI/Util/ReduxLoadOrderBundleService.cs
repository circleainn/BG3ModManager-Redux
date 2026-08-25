using DivinityModManager.AppServices;
using DivinityModManager.Models;

using Newtonsoft.Json;

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace DivinityModManager.Util;

public sealed class ReduxLoadOrderBundleContents
{
	public DivinityLoadOrder LoadOrder { get; init; }
	public ReduxLoadOrderPresentation Presentation { get; init; }
	public IReadOnlyDictionary<string, byte[]> Assets { get; init; }
}

/// <summary>
/// Reads and writes Redux-only load-order bundles. These archives never contain
/// modsettings.lsx and do not call the game's export path.
/// </summary>
public static class ReduxLoadOrderBundleService
{
	public const string FileExtension = ".bg3redux";
	private const string LoadOrderEntryName = "loadorder.json";
	private const string PresentationEntryName = "presentation.json";
	private const long MaximumArchiveBytes = 32L * 1024 * 1024;
	private const long MaximumExpandedBytes = 32L * 1024 * 1024;
	private const int MaximumEntries = 256;
	private const int MaximumJsonBytes = 4 * 1024 * 1024;
	private const int MaximumAssetBytes = 2 * 1024 * 1024;
	private const int MaximumJsonDepth = 64;
	private static readonly JsonSerializerSettings ReaderSettings = new()
	{
		MaxDepth = MaximumJsonDepth
	};

	public static bool TryExport(string path, DivinityLoadOrder loadOrder,
		ReduxLoadOrderPresentation presentation, IReadOnlyDictionary<string, byte[]> assets, out string error)
	{
		error = String.Empty;
		var validationError = String.Empty;
		try
		{
			if (loadOrder?.Order == null || presentation == null)
				throw new InvalidDataException("The Redux load order is incomplete.");

			using var buffer = new MemoryStream();
			using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
			{
				WriteTextEntry(archive, LoadOrderEntryName, JsonConvert.SerializeObject(loadOrder, Formatting.Indented));
				WriteTextEntry(archive, PresentationEntryName, JsonConvert.SerializeObject(presentation, Formatting.Indented));
				foreach (var asset in assets ?? new Dictionary<string, byte[]>())
				{
					if (!IsSafeAssetPath(asset.Key) || asset.Value == null || asset.Value.Length is <= 0 or > MaximumAssetBytes)
						throw new InvalidDataException("A custom icon asset is invalid.");
					var entry = archive.CreateEntry(asset.Key, CompressionLevel.Optimal);
					using var stream = entry.Open();
					stream.Write(asset.Value, 0, asset.Value.Length);
				}
			}

			var bytes = buffer.ToArray();
			if (bytes.Length <= 0 || bytes.LongLength > MaximumArchiveBytes)
				throw new InvalidDataException("The Redux Modlist is too large.");

			AtomicFileWriter.WriteAllBytes(path, bytes, validateTemporaryFile: temporaryPath =>
				TryRead(temporaryPath, out _, out validationError));
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to export Redux load-order bundle: {exception}");
			error = !String.IsNullOrWhiteSpace(validationError)
				? validationError
				: exception is InvalidDataException
					? exception.Message
					: "The Redux Modlist could not be created.";
			return false;
		}
	}

	public static bool TryRead(string path, out ReduxLoadOrderBundleContents contents, out string error)
	{
		contents = null;
		error = String.Empty;
		try
		{
			var file = new FileInfo(path);
			if (!file.Exists || file.Length <= 0 || file.Length > MaximumArchiveBytes)
				throw new InvalidDataException("Choose a valid Redux Modlist smaller than 32 MB.");

			using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
			if (archive.Entries.Count is < 2 or > MaximumEntries)
				throw new InvalidDataException("The Redux Modlist contains an invalid number of files.");
			if (archive.Entries.Select(entry => entry.FullName)
				.Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count)
				throw new InvalidDataException("The Redux Modlist contains duplicate file names.");
			if (archive.Entries.Sum(entry => entry.Length) > MaximumExpandedBytes)
				throw new InvalidDataException("The Redux Modlist expands beyond the supported size.");

			var loadOrderEntry = archive.GetEntry(LoadOrderEntryName)
				?? throw new InvalidDataException("The Redux Modlist is missing its load order.");
			var presentationEntry = archive.GetEntry(PresentationEntryName)
				?? throw new InvalidDataException("The Redux Modlist is missing its layout data.");

			var loadOrder = JsonConvert.DeserializeObject<DivinityLoadOrder>(
					ReadTextEntry(loadOrderEntry), ReaderSettings)
				?? throw new InvalidDataException("The Redux Modlist load order is invalid.");
			var presentation = JsonConvert.DeserializeObject<ReduxLoadOrderPresentation>(
					ReadTextEntry(presentationEntry), ReaderSettings)
				?? throw new InvalidDataException("The Redux Modlist layout data is invalid.");
			Validate(loadOrder, presentation);

			var expectedEntries = presentation.CustomIconAssets.Values
				.Append(LoadOrderEntryName)
				.Append(PresentationEntryName)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (archive.Entries.Any(entry => !expectedEntries.Contains(entry.FullName)))
				throw new InvalidDataException("The Redux Modlist contains an unexpected file.");

			var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
			foreach (var assetPath in presentation.CustomIconAssets.Values.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (!IsSafeAssetPath(assetPath))
					throw new InvalidDataException("The Redux Modlist contains an unsafe custom-icon path.");
				var entry = archive.GetEntry(assetPath)
					?? throw new InvalidDataException($"The Redux Modlist is missing custom icon '{assetPath}'.");
				if (entry.Length is <= 0 or > MaximumAssetBytes)
					throw new InvalidDataException("A custom icon in the Redux Modlist is too large.");
				using var assetStream = entry.Open();
				using var assetBuffer = new MemoryStream();
				assetStream.CopyTo(assetBuffer);
				if (assetBuffer.Length > MaximumAssetBytes)
					throw new InvalidDataException("A custom icon in the Redux Modlist expands beyond the allowed size.");
				assets[assetPath] = assetBuffer.ToArray();
			}

			contents = new ReduxLoadOrderBundleContents
			{
				LoadOrder = loadOrder,
				Presentation = presentation,
				Assets = assets
			};
			return true;
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Failed to read Redux load-order bundle: {exception}");
			error = exception is InvalidDataException ? exception.Message : "The Redux Modlist could not be read.";
			return false;
		}
	}

	private static void Validate(DivinityLoadOrder loadOrder, ReduxLoadOrderPresentation presentation)
	{
		if (!String.Equals(presentation.Format, ReduxLoadOrderPresentation.CurrentFormat, StringComparison.Ordinal) ||
			presentation.SchemaVersion != ReduxLoadOrderPresentation.CurrentSchemaVersion)
			throw new InvalidDataException("This Redux Modlist uses an unsupported format version.");
		if (presentation.OrderedModUuids == null ||
			presentation.CustomCategories == null ||
			presentation.CustomCategoryDisplayOrder == null ||
			presentation.CategoryAssignments == null ||
			presentation.Dividers == null ||
			presentation.CustomIconAssets == null ||
			presentation.PrivateModNotes == null)
			throw new InvalidDataException("The Redux Modlist layout data is incomplete.");
		if (loadOrder.Order == null || loadOrder.Order.Count > 10000 ||
			loadOrder.Order.Any(entry => entry == null || String.IsNullOrWhiteSpace(entry.UUID) ||
				entry.UUID.Length > 128 || (entry.Name?.Length ?? 0) > 256))
			throw new InvalidDataException("The Redux Modlist contains invalid load-order entries.");
		if (String.IsNullOrWhiteSpace(presentation.LoadOrderName) || presentation.LoadOrderName.Length > 256)
			throw new InvalidDataException("The Redux Modlist name is invalid.");
		if ((presentation.CreatorVersion?.Length ?? 0) > 64 ||
			(presentation.CreatorInternalVersion?.Length ?? 0) > 32 ||
			(!String.IsNullOrWhiteSpace(presentation.CreatorInternalVersion) &&
			 !Version.TryParse(presentation.CreatorInternalVersion, out _)))
			throw new InvalidDataException("The Redux Modlist contains invalid version information.");

		// DivinityLoadOrder's established serialized contract contains only Order.
		// Redux presentation metadata owns the portable bundle name.
		loadOrder.Name = presentation.LoadOrderName;

		var orderUuids = loadOrder.Order.Select(entry => entry.UUID).ToList();
		if (orderUuids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != orderUuids.Count ||
			!orderUuids.SequenceEqual(presentation.OrderedModUuids, StringComparer.OrdinalIgnoreCase))
			throw new InvalidDataException("The Redux Modlist contains inconsistent load-order data.");
		if (presentation.CustomCategories.Count > 128 ||
			presentation.Dividers.Count > 256 ||
			presentation.CategoryAssignments.Count > 10000 ||
			presentation.CustomIconAssets.Count > 128 ||
			presentation.PrivateModNotes.Count > orderUuids.Count)
			throw new InvalidDataException("The Redux Modlist layout exceeds the supported limits.");

		if (presentation.CustomCategories.Any(category =>
				category == null || String.IsNullOrWhiteSpace(category.Name) || category.Name.Length > 80 ||
				!IsValidColor(category.Color) || (category.IconId?.Length ?? 0) > 160 ||
				(category.Description?.Length ?? 0) > 240) ||
			presentation.CustomCategories.Select(category => category.Name)
				.Distinct(StringComparer.OrdinalIgnoreCase).Count() != presentation.CustomCategories.Count)
			throw new InvalidDataException("The Redux Modlist contains an invalid custom category.");
		var customCategoryNames = presentation.CustomCategories
			.Select(category => category.Name)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (presentation.CustomCategoryDisplayOrder.Count > presentation.CustomCategories.Count ||
			presentation.CustomCategoryDisplayOrder.Any(name =>
				String.IsNullOrWhiteSpace(name) || name.Length > 80 || !customCategoryNames.Contains(name)) ||
			presentation.CustomCategoryDisplayOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
				presentation.CustomCategoryDisplayOrder.Count)
			throw new InvalidDataException("The Redux Modlist contains an invalid category order.");

		var orderedUuidSet = orderUuids.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (presentation.CategoryAssignments.Any(assignment =>
				String.IsNullOrWhiteSpace(assignment.Key) || !orderedUuidSet.Contains(assignment.Key) ||
				assignment.Value == null || assignment.Value.Count > 32 ||
				assignment.Value.Any(category => String.IsNullOrWhiteSpace(category) || category.Length > 80) ||
				assignment.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() != assignment.Value.Count))
			throw new InvalidDataException("The Redux Modlist contains invalid category assignments.");
		if (presentation.Dividers.Any(divider =>
				divider == null || (divider.Title?.Length ?? 0) > 160 ||
				!IsValidColor(divider.Color) || (divider.IconId?.Length ?? 0) > 160 ||
				(divider.Description?.Length ?? 0) > 240 ||
				divider.FallbackPosition < 0 || divider.FallbackPosition > orderUuids.Count ||
				(divider.MemberModUuids != null &&
					(divider.MemberModUuids.Count > orderUuids.Count ||
					 divider.MemberModUuids.Any(uuid => String.IsNullOrWhiteSpace(uuid) || !orderedUuidSet.Contains(uuid)) ||
					 divider.MemberModUuids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != divider.MemberModUuids.Count)) ||
				(divider.BeforeModUuid?.Length ?? 0) > 128 || (divider.AfterModUuid?.Length ?? 0) > 128 ||
				(!String.IsNullOrWhiteSpace(divider.BeforeModUuid) && !orderedUuidSet.Contains(divider.BeforeModUuid)) ||
				(!String.IsNullOrWhiteSpace(divider.AfterModUuid) && !orderedUuidSet.Contains(divider.AfterModUuid))))
			throw new InvalidDataException("The Redux Modlist contains an invalid separator.");
		var explicitlyAssignedDividerUuids = presentation.Dividers
			.Where(divider => divider.MemberModUuids != null)
			.SelectMany(divider => divider.MemberModUuids)
			.ToList();
		if (explicitlyAssignedDividerUuids.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
			explicitlyAssignedDividerUuids.Count)
			throw new InvalidDataException("The Redux Modlist assigns one mod to multiple separators.");
		if (presentation.PrivateModNotes.Any(note =>
				note == null || String.IsNullOrWhiteSpace(note.ModUuid) ||
				!orderedUuidSet.Contains(note.ModUuid) ||
				String.IsNullOrWhiteSpace(note.Note) ||
				note.Note.Length > ReduxModAnnotationService.MaximumNoteLength) ||
			presentation.PrivateModNotes.Select(note => note.ModUuid)
				.Distinct(StringComparer.OrdinalIgnoreCase).Count() != presentation.PrivateModNotes.Count)
			throw new InvalidDataException("The Redux Modlist contains invalid notes.");
		if (presentation.CustomIconAssets.Any(asset =>
				!ReduxCustomIconService.IsCustomReference(asset.Key) || asset.Key.Length > 160 ||
				!IsSafeAssetPath(asset.Value)))
			throw new InvalidDataException("The Redux Modlist contains an invalid custom-icon reference.");

		var referencedCustomIcons = presentation.CustomCategories.Select(category => category.IconId)
			.Concat(presentation.Dividers.Select(divider => divider.IconId))
			.Where(ReduxCustomIconService.IsCustomReference)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (!referencedCustomIcons.SetEquals(presentation.CustomIconAssets.Keys))
			throw new InvalidDataException("The Redux Modlist contains inconsistent custom-icon references.");
	}

	private static bool IsValidColor(string color) =>
		!String.IsNullOrWhiteSpace(color) &&
		Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

	private static void WriteTextEntry(ZipArchive archive, string name, string contents)
	{
		var bytes = new UTF8Encoding(false).GetBytes(contents ?? String.Empty);
		if (bytes.Length <= 0 || bytes.Length > MaximumJsonBytes)
			throw new InvalidDataException($"{name} exceeds the supported size.");
		var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
		using var stream = entry.Open();
		stream.Write(bytes, 0, bytes.Length);
	}

	private static string ReadTextEntry(ZipArchiveEntry entry)
	{
		if (entry.Length is <= 0 or > MaximumJsonBytes)
			throw new InvalidDataException($"{entry.FullName} exceeds the supported size.");
		using var stream = entry.Open();
		using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
		var text = reader.ReadToEnd();
		if (Encoding.UTF8.GetByteCount(text) > MaximumJsonBytes)
			throw new InvalidDataException($"{entry.FullName} expands beyond the supported size.");
		return text;
	}

	private static bool IsSafeAssetPath(string path) =>
		!String.IsNullOrWhiteSpace(path) &&
		path.StartsWith("assets/", StringComparison.Ordinal) &&
		!path.Contains('\\') &&
		!path.Contains("..", StringComparison.Ordinal) &&
		Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) &&
		String.Equals(path, $"assets/{Path.GetFileName(path)}", StringComparison.Ordinal);
}
