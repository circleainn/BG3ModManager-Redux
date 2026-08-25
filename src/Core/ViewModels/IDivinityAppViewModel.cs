using DivinityModManager.Models;

using DynamicData.Binding;

namespace DivinityModManager.ViewModels;

public interface IDivinityAppViewModel
{
	IEnumerable<DivinityModData> ActiveMods { get; }
	IEnumerable<DivinityModData> InactiveMods { get; }
	ObservableCollectionExtended<DivinityProfileData> Profiles { get; }
	ReadOnlyObservableCollection<DivinityModData> Mods { get; }

	bool IsDragging { get; }
	bool IsRefreshing { get; }
	bool IsLocked { get; }

	int ActiveSelected { get; }
	int InactiveSelected { get; }

	void ShowAlert(string message, AlertType alertType = AlertType.Info, int timeout = 0);
	void DeleteMod(DivinityModData mod);
	void ClearMissingMods();
	void AddActiveMod(DivinityModData mod);
	void RemoveActiveMod(DivinityModData mod);
	void MoveModsBetweenLists(IEnumerable<DivinityModData> mods, bool moveToActive);
}
