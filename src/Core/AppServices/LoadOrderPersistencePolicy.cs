using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

/// <summary>
/// Keeps manager-owned load-order saves separate from the game's modsettings file.
/// </summary>
public static class LoadOrderPersistencePolicy
{
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
