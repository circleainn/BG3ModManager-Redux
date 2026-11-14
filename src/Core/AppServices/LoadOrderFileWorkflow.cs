using DivinityModManager.Models;
using DivinityModManager.Util;

namespace DivinityModManager.AppServices;

public sealed record LoadOrderRenamePlan(
	string Name,
	string SourcePath,
	string DestinationPath,
	bool IsSamePath,
	bool SourceExists,
	bool DestinationExists);

/// <summary>
/// Applies managed load-order file copies and renames independently from the UI.
/// </summary>
public static class LoadOrderFileWorkflow
{
	/// <summary>
	/// Saves a detached working copy and updates matching loaded orders only after
	/// the complete file has been committed. Empty Override selections remain opt-in.
	/// </summary>
	public static DivinityLoadOrder SaveCopy(DivinityLoadOrder workingOrder, string destinationPath,
		IEnumerable<DivinityLoadOrder> loadedOrders)
	{
		ArgumentNullException.ThrowIfNull(workingOrder);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
		var fullPath = Path.GetFullPath(destinationPath);
		var saved = new DivinityLoadOrder
		{
			Name = Path.GetFileNameWithoutExtension(fullPath),
			FilePath = fullPath,
			Order = workingOrder.Order.Select(entry => entry.Clone()).ToList(),
			VisualDividers = workingOrder.VisualDividers == null ? null
				: LoadOrderPersistencePolicy.CloneActiveVisualDividers(workingOrder.VisualDividers),
			OverrideModFiles = workingOrder.OverrideModFiles?.ToList()
		};
		var updates = (loadedOrders ?? []).Where(order => order != null
			&& String.Equals(order.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)).Distinct()
			.Select(loaded => new
			{
				Loaded = loaded,
				Order = saved.Order.Select(entry => entry.Clone()).ToList(),
				VisualDividers = saved.VisualDividers == null ? null
					: LoadOrderPersistencePolicy.CloneActiveVisualDividers(saved.VisualDividers),
				OverrideModFiles = saved.OverrideModFiles?.ToList()
			}).ToArray();
		if (!DivinityModDataLoader.ExportLoadOrderToFile(fullPath, saved))
			throw new IOException("The load-order copy could not be saved.");
		saved.LastModifiedDate = File.GetLastWriteTime(fullPath);
		foreach (var update in updates)
		{
			update.Loaded.Order = update.Order;
			update.Loaded.VisualDividers = update.VisualDividers;
			update.Loaded.OverrideModFiles = update.OverrideModFiles;
			update.Loaded.LastModifiedDate = saved.LastModifiedDate;
		}
		return saved;
	}

	public static LoadOrderRenamePlan PlanRename(DivinityLoadOrder order, string requestedName)
	{
		ArgumentNullException.ThrowIfNull(order);
		if (String.IsNullOrWhiteSpace(order.FilePath))
			throw new InvalidOperationException("The load order does not have a valid file path.");
		if (String.IsNullOrWhiteSpace(requestedName))
			throw new ArgumentException("A load-order name is required.", nameof(requestedName));

		var sourcePath = Path.GetFullPath(order.FilePath);
		var directory = Path.GetDirectoryName(sourcePath);
		if (String.IsNullOrWhiteSpace(directory))
			throw new InvalidOperationException("The load-order file does not have a valid parent folder.");

		var extension = Path.GetExtension(sourcePath);
		if (String.IsNullOrWhiteSpace(extension)) extension = ".json";
		var destinationFileName = DivinityModDataLoader.MakeSafeFilename(requestedName.Trim() + extension, '_');
		var name = Path.GetFileNameWithoutExtension(destinationFileName);
		if (String.IsNullOrWhiteSpace(name))
			throw new ArgumentException("The load-order name does not contain any valid filename characters.", nameof(requestedName));

		var destinationPath = Path.GetFullPath(Path.Combine(directory, destinationFileName));
		var isSamePath = String.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase);
		return new LoadOrderRenamePlan(
			name,
			sourcePath,
			destinationPath,
			isSamePath,
			File.Exists(sourcePath),
			!isSamePath && File.Exists(destinationPath));
	}

	public static void ApplyRename(
		DivinityLoadOrder order,
		LoadOrderRenamePlan plan,
		bool replaceExisting = false)
	{
		ArgumentNullException.ThrowIfNull(order);
		ArgumentNullException.ThrowIfNull(plan);
		if (!String.Equals(Path.GetFullPath(order.FilePath), plan.SourcePath, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("The load-order path changed before the rename could be completed.");

		if (!plan.IsSamePath)
		{
			var sourceExists = File.Exists(plan.SourcePath);
			var destinationExists = File.Exists(plan.DestinationPath);
			if (destinationExists && !replaceExisting)
				throw new IOException($"A load order named '{plan.Name}' already exists.");
			if (!sourceExists && destinationExists)
				throw new FileNotFoundException("The original load-order file no longer exists.", plan.SourcePath);

			if (sourceExists)
			{
				if (destinationExists)
					File.Replace(plan.SourcePath, plan.DestinationPath, null, true);
				else
					File.Move(plan.SourcePath, plan.DestinationPath);
			}
		}

		order.Name = plan.Name;
		order.FilePath = plan.DestinationPath;
		order.LastModifiedDate = File.Exists(plan.DestinationPath)
			? File.GetLastWriteTime(plan.DestinationPath)
			: DateTime.Now;
	}
}
