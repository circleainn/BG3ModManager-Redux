using System.Runtime.Serialization;

namespace DivinityModManager.Models;

[DataContract]
public sealed class ReduxLoadOrderPresentation
{
	public const string CurrentFormat = "BG3ModManagerRedux.Presentation";
	public const int CurrentSchemaVersion = 1;

	[DataMember(Order = 1)] public string Format { get; set; } = CurrentFormat;
	[DataMember(Order = 2)] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	[DataMember(Order = 3)] public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
	[DataMember(Order = 4)] public string LoadOrderName { get; set; } = String.Empty;
	[DataMember(Order = 5)] public List<string> OrderedModUuids { get; set; } = new();
	[DataMember(Order = 6)] public List<ReduxLoadOrderCategory> CustomCategories { get; set; } = new();
	[DataMember(Order = 7)] public List<string> CustomCategoryDisplayOrder { get; set; } = new();
	[DataMember(Order = 8)] public Dictionary<string, List<string>> CategoryAssignments { get; set; } =
		new(StringComparer.OrdinalIgnoreCase);
	[DataMember(Order = 9)] public List<ReduxLoadOrderDivider> Dividers { get; set; } = new();
	[DataMember(Order = 10)] public Dictionary<string, string> CustomIconAssets { get; set; } =
		new(StringComparer.OrdinalIgnoreCase);
	[DataMember(Order = 11)] public string CreatorVersion { get; set; } = String.Empty;
	[DataMember(Order = 12)] public string CreatorInternalVersion { get; set; } = String.Empty;
}

[DataContract]
public sealed class ReduxLoadOrderCategory
{
	[DataMember(Order = 1)] public string Name { get; set; } = String.Empty;
	[DataMember(Order = 2)] public string Color { get; set; } = "#8A6AF1";
	[DataMember(Order = 3)] public string IconId { get; set; } = String.Empty;
}

[DataContract]
public sealed class ReduxLoadOrderDivider
{
	[DataMember(Order = 1)] public string Title { get; set; } = String.Empty;
	[DataMember(Order = 2)] public string Color { get; set; } = "#8A6AF1";
	[DataMember(Order = 3)] public string IconId { get; set; } = String.Empty;
	[DataMember(Order = 4)] public bool IsCollapsed { get; set; }
	[DataMember(Order = 5)] public int FallbackPosition { get; set; }
	[DataMember(Order = 6)] public string BeforeModUuid { get; set; } = String.Empty;
	[DataMember(Order = 7)] public string AfterModUuid { get; set; } = String.Empty;
}
