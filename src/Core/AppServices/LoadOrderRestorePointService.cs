using DivinityModManager.Models;
using DivinityModManager.Util;

using Newtonsoft.Json;

namespace DivinityModManager.AppServices;

/// <summary>
/// Persists a small, atomic history of exported load orders without placing
/// Redux metadata in the user's normal Orders directory.
/// </summary>
public static class LoadOrderRestorePointService
{
	public const int MaximumRestorePointsPerProfile = 20;
	private const int MaximumEntriesPerRestorePoint = 10000;

	public static bool TryCreate(
		string rootDirectory,
		string profileUuid,
		string profileName,
		string sourceOrderName,
		string reason,
		IEnumerable<DivinityLoadOrderEntry> order,
		out LoadOrderRestorePoint restorePoint,
		out string error)
	{
		restorePoint = new LoadOrderRestorePoint
		{
			ProfileUuid = profileUuid?.Trim() ?? String.Empty,
			ProfileName = profileName?.Trim() ?? String.Empty,
			SourceOrderName = sourceOrderName?.Trim() ?? String.Empty,
			Reason = reason?.Trim() ?? String.Empty,
			Order = (order ?? [])
				.Where(entry => entry != null)
				.Select(entry => entry.Clone())
				.ToList()
		};

		return TrySave(rootDirectory, restorePoint, out error);
	}

	public static bool TrySave(string rootDirectory, LoadOrderRestorePoint restorePoint, out string error)
	{
		error = String.Empty;
		if (!TryValidate(restorePoint, restorePoint?.ProfileUuid, out error))
		{
			return false;
		}

		try
		{
			var profileDirectory = GetProfileDirectory(rootDirectory, restorePoint.ProfileUuid);
			var fileName = $"{restorePoint.CreatedUtc.UtcDateTime:yyyyMMddTHHmmssfffZ}_{restorePoint.Id}.json";
			var destinationPath = Path.Combine(profileDirectory, fileName);
			var contents = JsonConvert.SerializeObject(restorePoint, Formatting.Indented);
			AtomicFileWriter.WriteAllText(
				destinationPath,
				contents,
				validateTemporaryFile: temporaryPath =>
					TryRead(temporaryPath, restorePoint.ProfileUuid, out _, out _));
			Prune(profileDirectory, restorePoint.ProfileUuid);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			DivinityApp.Log($"Could not save load-order restore point: {ex}");
			return false;
		}
	}

	public static IReadOnlyList<LoadOrderRestorePoint> Load(string rootDirectory, string profileUuid)
	{
		if (String.IsNullOrWhiteSpace(rootDirectory) || String.IsNullOrWhiteSpace(profileUuid))
		{
			return [];
		}

		try
		{
			var profileDirectory = GetProfileDirectory(rootDirectory, profileUuid);
			if (!Directory.Exists(profileDirectory))
			{
				return [];
			}

			var restorePoints = new List<LoadOrderRestorePoint>();
			foreach (var filePath in Directory.EnumerateFiles(profileDirectory, "*.json", SearchOption.TopDirectoryOnly))
			{
				if (TryRead(filePath, profileUuid, out var restorePoint, out var error))
				{
					restorePoints.Add(restorePoint);
				}
				else
				{
					DivinityApp.Log($"Ignoring invalid load-order restore point '{filePath}': {error}");
				}
			}

			return restorePoints
				.OrderByDescending(point => point.CreatedUtc)
				.ThenByDescending(point => point.Id, StringComparer.OrdinalIgnoreCase)
				.Take(MaximumRestorePointsPerProfile)
				.ToArray();
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Could not load load-order restore points: {ex}");
			return [];
		}
	}

	public static bool TryRead(
		string filePath,
		string expectedProfileUuid,
		out LoadOrderRestorePoint restorePoint,
		out string error)
	{
		restorePoint = null;
		error = String.Empty;
		try
		{
			if (String.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
			{
				error = "The restore-point file does not exist.";
				return false;
			}

			restorePoint = JsonConvert.DeserializeObject<LoadOrderRestorePoint>(File.ReadAllText(filePath));
			return TryValidate(restorePoint, expectedProfileUuid, out error);
		}
		catch (Exception ex)
		{
			restorePoint = null;
			error = ex.Message;
			return false;
		}
	}

	private static bool TryValidate(
		LoadOrderRestorePoint restorePoint,
		string expectedProfileUuid,
		out string error)
	{
		error = String.Empty;
		if (restorePoint == null)
		{
			error = "The restore point is empty.";
			return false;
		}
		if (restorePoint.SchemaVersion != LoadOrderRestorePoint.CurrentSchemaVersion)
		{
			error = $"Unsupported restore-point schema {restorePoint.SchemaVersion}.";
			return false;
		}
		if (!Guid.TryParse(restorePoint.Id, out _))
		{
			error = "The restore point has an invalid identifier.";
			return false;
		}
		if (restorePoint.CreatedUtc == default
			|| restorePoint.CreatedUtc > DateTimeOffset.UtcNow.AddDays(1))
		{
			error = "The restore point has an invalid creation time.";
			return false;
		}
		if (String.IsNullOrWhiteSpace(restorePoint.ProfileUuid))
		{
			error = "The restore point does not identify a profile.";
			return false;
		}
		if (!String.IsNullOrWhiteSpace(expectedProfileUuid)
			&& !String.Equals(restorePoint.ProfileUuid, expectedProfileUuid, StringComparison.OrdinalIgnoreCase))
		{
			error = "The restore point belongs to a different profile.";
			return false;
		}
		if (restorePoint.Order == null || restorePoint.Order.Count > MaximumEntriesPerRestorePoint)
		{
			error = "The restore point contains an invalid number of entries.";
			return false;
		}

		var seenUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in restorePoint.Order)
		{
			if (entry == null || String.IsNullOrWhiteSpace(entry.UUID))
			{
				error = "The restore point contains an entry without a UUID.";
				return false;
			}
			if (!seenUuids.Add(entry.UUID))
			{
				error = $"The restore point contains duplicate UUID '{entry.UUID}'.";
				return false;
			}
		}

		return true;
	}

	private static string GetProfileDirectory(string rootDirectory, string profileUuid)
	{
		if (String.IsNullOrWhiteSpace(rootDirectory))
		{
			throw new ArgumentException("A restore-point root directory is required.", nameof(rootDirectory));
		}

		var safeProfileUuid = new string((profileUuid ?? String.Empty)
			.Where(character => Char.IsLetterOrDigit(character) || character is '-' or '_')
			.ToArray());
		if (String.IsNullOrWhiteSpace(safeProfileUuid))
		{
			throw new ArgumentException("A valid profile UUID is required.", nameof(profileUuid));
		}

		return Path.Combine(rootDirectory, safeProfileUuid);
	}

	private static void Prune(string profileDirectory, string profileUuid)
	{
		var validRestorePoints = Directory
			.EnumerateFiles(profileDirectory, "*.json", SearchOption.TopDirectoryOnly)
			.Select(filePath =>
			{
				var valid = TryRead(filePath, profileUuid, out var restorePoint, out _);
				return (FilePath: filePath, RestorePoint: restorePoint, Valid: valid);
			})
			.Where(item => item.Valid)
			.OrderByDescending(item => item.RestorePoint.CreatedUtc)
			.ThenByDescending(item => item.RestorePoint.Id, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		foreach (var stale in validRestorePoints.Skip(MaximumRestorePointsPerProfile))
		{
			try
			{
				File.Delete(stale.FilePath);
			}
			catch (Exception ex)
			{
				DivinityApp.Log($"Could not prune restore point '{stale.FilePath}': {ex}");
			}
		}
	}
}
