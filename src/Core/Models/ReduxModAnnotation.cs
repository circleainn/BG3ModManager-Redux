using Newtonsoft.Json;

namespace DivinityModManager.Models;

public sealed class ReduxModAnnotation
{
	public string ModUuid { get; set; } = String.Empty;
	public string PrivateNote { get; set; } = String.Empty;
	public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

	[JsonIgnore]
	public bool HasPrivateNote => !String.IsNullOrWhiteSpace(PrivateNote);

	[JsonIgnore]
	public bool HasContent => HasPrivateNote;

	public ReduxModAnnotation Clone() => new()
	{
		ModUuid = ModUuid,
		PrivateNote = PrivateNote,
		UpdatedUtc = UpdatedUtc
	};
}

public sealed class ReduxModAnnotationStore
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public List<ReduxModAnnotation> Mods { get; set; } = [];

	public ReduxModAnnotationStore Clone() => new()
	{
		SchemaVersion = SchemaVersion,
		Mods = (Mods ?? []).Where(annotation => annotation != null)
			.Select(annotation => annotation.Clone())
			.ToList()
	};
}
