using DivinityModManager.Models;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace DivinityModManager.AppServices;

public static class ReduxModAnnotationService
{
	public const int MaximumNoteLength = 8000;
	private const int MaximumAnnotationCount = 10000;

	public static ReduxModAnnotationStore Load(string path)
	{
		if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return new ReduxModAnnotationStore();
		}

		try
		{
			var store = JsonConvert.DeserializeObject<ReduxModAnnotationStore>(File.ReadAllText(path));
			if (TryValidate(store, out var error))
			{
				return store;
			}

			DivinityApp.Log($"Ignoring invalid Redux mod annotations: {error}");
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not load Redux mod annotations: {ex}");
		}

		return new ReduxModAnnotationStore();
	}

	public static bool TrySave(string path, ReduxModAnnotationStore store, out string error)
	{
		error = String.Empty;
		if (!TryValidate(store, out error))
		{
			return false;
		}

		try
		{
			store.Mods = store.Mods
				.Where(annotation => annotation.HasContent)
				.OrderBy(annotation => annotation.ModUuid, StringComparer.OrdinalIgnoreCase)
				.ToList();
			var contents = JsonConvert.SerializeObject(store, Formatting.Indented);
			AtomicFileWriter.WriteAllText(
				path,
				contents,
				validateTemporaryFile: temporaryPath =>
				{
					try
					{
						var saved = JsonConvert.DeserializeObject<ReduxModAnnotationStore>(
							File.ReadAllText(temporaryPath));
						return TryValidate(saved, out _);
					}
					catch
					{
						return false;
					}
				});
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			DivinityApp.Log($"Could not save Redux mod annotations: {ex}");
			return false;
		}
	}

	public static bool TrySet(
		ReduxModAnnotationStore store,
		string modUuid,
		string privateNote,
		out string error)
	{
		error = String.Empty;
		if (store == null)
		{
			error = "The annotation store is unavailable.";
			return false;
		}
		if (String.IsNullOrWhiteSpace(modUuid))
		{
			error = "This mod does not have a stable UUID.";
			return false;
		}

		var normalizedNote = NormalizeNote(privateNote);
		if (normalizedNote.Length > MaximumNoteLength)
		{
			error = $"Notes can contain up to {MaximumNoteLength:N0} characters.";
			return false;
		}

		store.Mods ??= [];
		var duplicates = store.Mods
			.Where(annotation => annotation != null
				&& String.Equals(annotation.ModUuid, modUuid, StringComparison.OrdinalIgnoreCase))
			.ToArray();
		foreach (var duplicate in duplicates.Skip(1))
		{
			store.Mods.Remove(duplicate);
		}

		var annotation = duplicates.FirstOrDefault();
		if (String.IsNullOrWhiteSpace(normalizedNote))
		{
			if (annotation != null)
			{
				store.Mods.Remove(annotation);
			}
			return true;
		}

		annotation ??= new ReduxModAnnotation { ModUuid = modUuid.Trim() };
		if (!store.Mods.Contains(annotation))
		{
			store.Mods.Add(annotation);
		}
		annotation.PrivateNote = normalizedNote;
		annotation.UpdatedUtc = DateTimeOffset.UtcNow;
		return TryValidate(store, out error);
	}

	public static bool TrySetMany(
		ReduxModAnnotationStore store,
		IEnumerable<string> modUuids,
		string privateNote,
		out string error)
	{
		error = String.Empty;
		if (store == null)
		{
			error = "The annotation store is unavailable.";
			return false;
		}

		var targets = (modUuids ?? Enumerable.Empty<string>())
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.Select(uuid => uuid.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (targets.Length == 0)
		{
			error = "Choose at least one mod before editing notes.";
			return false;
		}

		var proposed = store.Clone();
		foreach (var uuid in targets)
		{
			if (!TrySet(proposed, uuid, privateNote, out error))
			{
				return false;
			}
		}

		store.SchemaVersion = proposed.SchemaVersion;
		store.Mods = proposed.Mods;
		return true;
	}

	public static ReduxModAnnotation Find(ReduxModAnnotationStore store, string modUuid) =>
		store?.Mods?.FirstOrDefault(annotation =>
			annotation != null
			&& String.Equals(annotation.ModUuid, modUuid, StringComparison.OrdinalIgnoreCase));

	private static string NormalizeNote(string note) =>
		(note ?? String.Empty)
			.Replace("\r\n", "\n")
			.Replace('\r', '\n')
			.Trim();

	private static bool TryValidate(ReduxModAnnotationStore store, out string error)
	{
		error = String.Empty;
		if (store == null)
		{
			error = "The annotation store is empty.";
			return false;
		}
		if (store.SchemaVersion != ReduxModAnnotationStore.CurrentSchemaVersion)
		{
			error = $"Unsupported annotation schema {store.SchemaVersion}.";
			return false;
		}
		if (store.Mods == null || store.Mods.Count > MaximumAnnotationCount)
		{
			error = "The annotation store contains an invalid number of records.";
			return false;
		}

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var annotation in store.Mods)
		{
			if (annotation == null || String.IsNullOrWhiteSpace(annotation.ModUuid))
			{
				error = "An annotation is missing its mod UUID.";
				return false;
			}
			if (!seen.Add(annotation.ModUuid))
			{
				error = $"The annotation store contains duplicate UUID '{annotation.ModUuid}'.";
				return false;
			}
			if ((annotation.PrivateNote ?? String.Empty).Length > MaximumNoteLength)
			{
				error = $"An annotation exceeds the {MaximumNoteLength:N0}-character note limit.";
				return false;
			}
			if (!annotation.HasContent)
			{
				error = "The annotation store contains an empty record.";
				return false;
			}
		}

		return true;
	}
}
