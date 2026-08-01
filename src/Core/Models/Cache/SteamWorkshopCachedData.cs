using System.Runtime.Serialization;

namespace DivinityModManager.Models.Cache;

[DataContract]
public class SteamWorkshopCachedData : BaseModCacheData<DivinityModWorkshopCachedData>
{
	[DataMember] public List<string> NonWorkshopMods { get; set; }

	public SteamWorkshopCachedData() : base()
	{
		NonWorkshopMods = new List<string>();
	}
}
