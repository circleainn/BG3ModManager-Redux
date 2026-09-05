using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

/// <summary>
/// Keeps manager-owned load-order saves separate from the game's modsettings file.
/// </summary>
public static class LoadOrderPersistencePolicy
{
	/// <summary>
	/// Creates a detached working-order snapshot. Editing the active list must not
	/// mutate the selected saved order until the user explicitly saves it.
	/// </summary>
	public static DivinityLoadOrder CreateWorkingCopy(
		DivinityLoadOrder selectedOrder,
		IEnumerable<DivinityModData> activeMods)
	{
		var workingCopy = new DivinityLoadOrder
		{
			Name = selectedOrder?.Name,
			FilePath = selectedOrder?.FilePath,
			LastModifiedDate = DateTime.Now
		};
		workingCopy.AddRange(activeMods ?? Enumerable.Empty<DivinityModData>(), true);
		return workingCopy;
	}

	public static DivinityLoadOrder CreateBlankOrder(string name, string filePath)
	{
		return new DivinityLoadOrder
		{
			Name = name,
			FilePath = filePath,
			Order = []
		};
	}

	public static bool RequiresSaveAs(DivinityLoadOrder order)
	{
		return order?.IsModSettings == true
			|| String.Equals(Path.GetExtension(order?.FilePath), ".lsx", StringComparison.OrdinalIgnoreCase);
	}
}
