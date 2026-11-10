namespace DivinityModManager.Util;

public static class ModColumnOrder
{
	public static List<string> Normalize(IEnumerable<string> saved, IEnumerable<string> defaults)
	{
		var known = defaults.ToList();
		return (saved ?? Enumerable.Empty<string>()).Concat(known)
			.Where(key => known.Contains(key, StringComparer.OrdinalIgnoreCase))
			.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static List<string> MergeVisible(IEnumerable<string> saved, IEnumerable<string> visible, IEnumerable<string> defaults)
	{
		var order = Normalize(saved, defaults);
		var shown = visible.ToList();
		var queue = new Queue<string>(shown);
		return order.Select(key => shown.Contains(key, StringComparer.OrdinalIgnoreCase) ? queue.Dequeue() : key).ToList();
	}
}
