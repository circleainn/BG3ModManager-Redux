using DivinityModManager.Models;
using JsonException = Newtonsoft.Json.JsonException;
using Newtonsoft.Json.Linq;

namespace DivinityModManager.AppServices;

/// <summary>Reads exported load orders without mistaking bundled metadata for an empty order.</summary>
public static class ArchiveLoadOrderService
{
	public static DivinityLoadOrder? Read(string entryPath, string contents)
	{
		var document = JToken.Parse(contents);
		if (document is not JObject root
			|| !root.TryGetValue("Order", StringComparison.OrdinalIgnoreCase, out var value)) return null;
		if (value is not JArray entries)
			throw new JsonException("The archived load order must contain an Order array.");
		foreach (var entry in entries)
		{
			if (entry is not JObject item
				|| !item.TryGetValue("UUID", StringComparison.OrdinalIgnoreCase, out var uuid)
				|| uuid.Type != JTokenType.String || String.IsNullOrWhiteSpace((string?)uuid))
				throw new JsonException("An archived load-order entry is missing its mod UUID.");
		}
		var order = root.ToObject<DivinityLoadOrder>()
			?? throw new JsonException("The archived load order could not be read.");
		if (order.Order == null || order.Order.Any(entry => String.IsNullOrWhiteSpace(entry?.UUID)))
			throw new JsonException("The archived load order contains invalid mod entries.");
		order.Name = Path.GetFileNameWithoutExtension(entryPath.Replace('\\', '/'));
		order.LastModifiedDate = DateTime.Now;
		return order;
	}

	/// <summary>Updates the logical Current order while preserving its identity and other saved orders.</summary>
	public static DivinityLoadOrder? ApplyCurrent(IEnumerable<DivinityLoadOrder> orders, DivinityLoadOrder imported)
	{
		if (!String.Equals(imported?.Name, "Current", StringComparison.OrdinalIgnoreCase)) return null;
		var current = LoadOrderPersistencePolicy.FindGameBackedCurrentOrder(orders);
		if (current == null) return null;
		LoadOrderPersistencePolicy.RestoreSavedCurrentState(current, imported);
		return current;
	}
}
