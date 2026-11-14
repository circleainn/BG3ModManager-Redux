using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

public sealed record StarterSection(string Title, string Color, string IconId, string LegacyTitle, params string[] Groups);

public static class ReduxOnboardingPolicy
{
	public static IReadOnlyList<StarterSection> StarterSections { get; } =
	[
		new("Frameworks & Utilities", "#438AAD", "blocks", "Foundations", "Top of Load Order", "Resources", "Utilities"),
		new("Visuals & Audio", "#AC69A1", "film", "Visuals", "Visuals", "Animations", "Audio"),
		new("Interface & Dice", "#738ED7", "desktop", "Interface", "User Interface", "Dice"),
		new("Gear & Items", "#BD873A", "backpack", "Equipment", "Clothing", "Equipment", "Dyes", "Armor", "Weapons", "Accessories"),
		new("Spells", "#866ACA", "moon-star", "", "Spells"),
		new("Gameplay", "#B45F51", "gameplay", "", "Gameplay"),
		new("Races & Classes", "#628A4B", "dna", "Classes & Subclasses", "Races", "Classes"),
		new("Character Creation", "#CB7992", "palette", "", "Character Customization", "Heads", "Hair", "Bodies"),
		new("Quests & Locations", "#40998A", "map", "", "Quests", "Environment"),
		new("Companions & NPCs", "#AF754F", "people", "", "Companions", "NPC"),
		new("Fixes & Patches", "#798394", "bandage", "Patches", "Bug Fixes", "Bottom of Load Order")
	];

	public static IReadOnlyList<string> StarterSeparatorTitles { get; } = StarterSections.Select(section => section.Title).ToArray();

	public static IReadOnlyList<string> MissingStarterSeparators(IEnumerable<ModListVisualDividerData> existing) =>
		StarterSections.Where(section => !(existing ?? []).Any(item => item.IsActiveList
			&& (String.Equals(item.Title?.Trim(), section.Title, StringComparison.OrdinalIgnoreCase)
				|| (section.LegacyTitle.Length > 0 && String.Equals(item.Title?.Trim(), section.LegacyTitle, StringComparison.OrdinalIgnoreCase)))))
			.Select(section => section.Title).ToArray();

	// Only standalone mods can provide anchors. Existing section membership belongs to the player.
	public static IReadOnlyList<ModListVisualDividerData> PlanStarterSeparators(IEnumerable<string> selected,
		IReadOnlyList<DivinityModData> sequence, IEnumerable<ModListVisualDividerData> existing, ReduxLoadOrderAdvisorKnowledge knowledge)
	{
		var titles = MissingStarterSeparators(existing).Intersect(selected, StringComparer.OrdinalIgnoreCase).ToHashSet();
		var sections = StarterSections.Where(section => titles.Contains(section.Title)).ToArray();
		var standaloneGroups = sequence.TakeWhile(mod => !mod.IsVisualDivider).Select(mod => knowledge.GetGroupName(mod.UUID)).ToArray();
		var positions = sections.Select(section => Array.FindIndex(standaloneGroups,
			group => section.Groups.Contains(group, StringComparer.OrdinalIgnoreCase))).ToArray();
		var next = sequence.Count;
		for (int i = sections.Length - 1; i >= 0; i--)
		{
			if (positions[i] < 0) positions[i] = next;
			else next = positions[i];
		}
		return sections.Select((section, i) => (section, anchor: positions[i]))
			.OrderBy(item => item.anchor)
			.Select((item, i) => new ModListVisualDividerData
		{
			Title = item.section.Title, Color = item.section.Color, IconId = item.section.IconId, HideLine = true,
			IsActiveList = true, Position = item.anchor + i, MemberModUuids = []
		}).ToArray();
	}

	/// <summary>
	/// Keeps optional online features and load-order guidance opt-in for a user's first Redux setup.
	/// Returning users retain the choices already stored in their settings.
	/// </summary>
	public static void ApplyFirstRunDefaults(DivinityModManagerSettings settings)
	{
		if (settings == null || settings.HasSeenReduxWelcome)
		{
			return;
		}

		settings.LocalOnlyMode = true;
		settings.EnableLoadOrderAdvisor = false;
	}
}
