using Newtonsoft.Json;

namespace DivinityModManager.Models;

/// <summary>
/// A bounded, Redux-owned snapshot of a profile's exported load order.
/// Restore points are deliberately separate from normal saved orders and never
/// contain package files, private paths, or game configuration data.
/// </summary>
public sealed class LoadOrderRestorePoint
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
	public string Reason { get; set; } = String.Empty;
	public string ProfileUuid { get; set; } = String.Empty;
	public string ProfileName { get; set; } = String.Empty;
	public string SourceOrderName { get; set; } = String.Empty;
	public List<DivinityLoadOrderEntry> Order { get; set; } = [];

	[JsonIgnore]
	public int ModCount => Order?.Count ?? 0;

	[JsonIgnore]
	public DateTimeOffset CreatedLocal => CreatedUtc.ToLocalTime();

	[JsonIgnore]
	public string CreatedSummary => CreatedLocal.ToString("g");

	[JsonIgnore]
	public string ModCountSummary => ModCount == 1 ? "1 mod" : $"{ModCount} mods";
}
