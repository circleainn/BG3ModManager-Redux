using System.Runtime.Serialization;

namespace DivinityModManager.Models;

[DataContract]
public class ModListVisualDividerData
{
	[DataMember] public string Id { get; set; } = Guid.NewGuid().ToString("N");
	[DataMember] public string Title { get; set; } = "New Section";
	[DataMember] public string Color { get; set; } = "#8A6AF1";
	[DataMember] public string IconId { get; set; } = String.Empty;
	[DataMember] public bool IsActiveList { get; set; } = true;
	[DataMember] public int Position { get; set; }
	[DataMember] public bool IsCollapsed { get; set; }
}
