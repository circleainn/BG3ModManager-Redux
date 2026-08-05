using DivinityModManager.Models;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.Util;

/// <summary>
/// Resolves Redux's automatic presentation categories without changing a
/// user's explicit category assignments. Provider taxonomy is authoritative
/// when it is available; strong package evidence may add secondary categories.
/// </summary>
public static class AutomaticModCategoryClassifier
{
	public const string UncategorizedCategory = "No Category";
	public const int MaxAutomaticCategories = 3;
	private const int StrongSecondaryScore = 12;

	private static readonly (string Name, string[] Keywords)[] CategoryRules =
	[
		("User Interface", ["user interface", "interface", "improvedui", "ui", "hud", "menu", "hotbar", "panel", "sidebar", "widget", "tooltip", "inventory"]),
		("Classes", ["class", "subclass", "multiclass"]),
		("Spells", ["spell", "spells", "cantrip", "cantrips", "spellbook"]),
		("Accessories", ["accessory", "accessories", "jewelry", "jewellery", "earring", "necklace", "ring"]),
		("Animations", ["animation", "animations", "pose", "poses", "emote"]),
		("Armor", ["armor", "armour", "helmet", "shield"]),
		("Audio", ["audio", "music", "sound", "voice", "voices"]),
		("Character Customization", ["character customisation", "character customization", "hair", "hairstyle", "face", "head", "makeup", "tattoo", "appearance"]),
		("Clothing", ["clothing", "clothes", "dress", "outfit", "underwear", "dye"]),
		("Companions", ["companion", "companions", "astarion", "gale", "karlach", "laezel", "shadowheart", "wyll", "minthara", "halsin", "jaheira", "minsc"]),
		("Dice", ["dice", "die skin", "dice skin"]),
		("Equipment", ["equipment", "gear", "item pack"]),
		("Maps", ["map", "maps", "location", "area"]),
		("Photo Mode", ["photo mode", "photomode", "camera preset"]),
		("Quests", ["quest", "quests", "adventure", "story expansion"]),
		("Races", ["race", "races", "species", "origin"]),
		("Resources", ["resource", "resources", "asset", "assets", "modders resource"]),
		("Visuals", ["visual", "visuals", "graphics", "lighting", "reshade", "texture", "textures"]),
		("Weapons", ["weapon", "weapons", "sword", "bow", "staff"]),
		("Cosmetics", ["cosmetic", "cosmetics", "vanity"]),
		("Libraries", ["library", "framework", "dependency", "communitylibrary", "api"]),
		("Patches", ["patch", "compatibility", "hotfix", "bugfix"]),
		("Overhauls", ["overhaul", "total conversion"]),
		("Utilities", ["utility", "tool", "mod fixer", "script extender", "achievement enabler", "native camera"]),
		("Gameplay", ["gameplay", "quality of life", "balance", "combat", "feat", "gold", "weight", "carry", "level", "camp chest", "chest anywhere", "chest everywhere"]),
		("Miscellaneous", ["miscellaneous", "misc", "other"]),
		("Overrides", ["override", "always loaded", "file override"])
	];

	// Baldur's Gate 3 category IDs returned by the Nexus Mods API. These are
	// provider identifiers, not values embedded in a .pak's meta.lsx.
	private static readonly IReadOnlyDictionary<long, string> NexusCategories =
		new Dictionary<long, string>
		{
			[2] = "Miscellaneous",
			[3] = "Character Customization",
			[4] = "Visuals",
			[5] = "Gameplay",
			[6] = "User Interface",
			[7] = "Utilities",
			[9] = "Audio",
			[10] = "Equipment",
			[12] = "Classes",
			[13] = "Spells",
			[15] = "Races",
			[16] = "Dice",
			[17] = "Armor",
			[18] = "Animations",
			[19] = "Quests",
			[20] = "Accessories",
			[21] = "Companions",
			[22] = "Weapons",
			[23] = "Clothing",
			[24] = "Resources",
			[25] = "Maps",
			[26] = "Photo Mode"
		};

	private static readonly IReadOnlyDictionary<string, long> NexusCategoryIds =
		new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
		{
			["miscellaneous"] = 2,
			["character customisation"] = 3,
			["character customization"] = 3,
			["visuals"] = 4,
			["gameplay"] = 5,
			["user interface"] = 6,
			["utilities"] = 7,
			["audio"] = 9,
			["equipment"] = 10,
			["classes"] = 12,
			["spells"] = 13,
			["races"] = 15,
			["dice"] = 16,
			["armor"] = 17,
			["armour"] = 17,
			["animations"] = 18,
			["quests"] = 19,
			["accessories"] = 20,
			["companions"] = 21,
			["weapons"] = 22,
			["clothing"] = 23,
			["resources"] = 24,
			["maps"] = 25,
			["photo mode"] = 26
		};

	// mod.io exposes tags rather than a single Nexus-style category. Only exact
	// taxonomy labels and spelling variants are mapped here; descriptive tags
	// continue through the conservative keyword scorer below.
	private static readonly IReadOnlyDictionary<string, string> ModioTagCategories =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["accessory"] = "Accessories",
			["accessories"] = "Accessories",
			["animation"] = "Animations",
			["animations"] = "Animations",
			["armor"] = "Armor",
			["armour"] = "Armor",
			["audio"] = "Audio",
			["character customisation"] = "Character Customization",
			["character customization"] = "Character Customization",
			["class"] = "Classes",
			["classes"] = "Classes",
			["clothes"] = "Clothing",
			["clothing"] = "Clothing",
			["companion"] = "Companions",
			["companions"] = "Companions",
			["cosmetic"] = "Cosmetics",
			["cosmetics"] = "Cosmetics",
			["dice"] = "Dice",
			["equipment"] = "Equipment",
			["framework"] = "Libraries",
			["gameplay"] = "Gameplay",
			["libraries"] = "Libraries",
			["library"] = "Libraries",
			["map"] = "Maps",
			["maps"] = "Maps",
			["misc"] = "Miscellaneous",
			["miscellaneous"] = "Miscellaneous",
			["overhaul"] = "Overhauls",
			["overhauls"] = "Overhauls",
			["override"] = "Overrides",
			["overrides"] = "Overrides",
			["patch"] = "Patches",
			["patches"] = "Patches",
			["photo mode"] = "Photo Mode",
			["photomode"] = "Photo Mode",
			["quest"] = "Quests",
			["quests"] = "Quests",
			["race"] = "Races",
			["races"] = "Races",
			["resource"] = "Resources",
			["resources"] = "Resources",
			["spell"] = "Spells",
			["spells"] = "Spells",
			["ui"] = "User Interface",
			["user interface"] = "User Interface",
			["utilities"] = "Utilities",
			["utility"] = "Utilities",
			["visual"] = "Visuals",
			["visuals"] = "Visuals",
			["weapon"] = "Weapons",
			["weapons"] = "Weapons"
		};

	public static IReadOnlyList<string> CategoryNames { get; } =
		CategoryRules.Select(category => category.Name).ToArray();

	public static string Classify(
		DivinityModData mod,
		Predicate<string> isCategoryEnabled,
		string uncategorizedCategory = UncategorizedCategory)
		=> ClassifyCategories(mod, isCategoryEnabled, uncategorizedCategory).First();

	public static IReadOnlyList<string> ClassifyCategories(
		DivinityModData mod,
		Predicate<string> isCategoryEnabled,
		string uncategorizedCategory = UncategorizedCategory,
		int maxCategories = MaxAutomaticCategories)
	{
		ArgumentNullException.ThrowIfNull(mod);
		ArgumentNullException.ThrowIfNull(isCategoryEnabled);
		if (maxCategories < 1) throw new ArgumentOutOfRangeException(nameof(maxCategories));

		if (mod.IsForceLoaded && !mod.IsForceLoadedMergedMod && !mod.ForceAllowInLoadOrder &&
			isCategoryEnabled("Overrides"))
		{
			return new[] { "Overrides" };
		}

		var resolved = new List<string>(maxCategories);
		void AddCategory(string category)
		{
			if (resolved.Count >= maxCategories || String.IsNullOrWhiteSpace(category) ||
				resolved.Contains(category, StringComparer.OrdinalIgnoreCase)) return;
			resolved.Add(category);
		}

		foreach (var providerCategory in GetProviderCategories(mod, isCategoryEnabled))
		{
			AddCategory(providerCategory);
			if (resolved.Count >= maxCategories) return resolved;
		}

		var nameSource = JoinSource(
			mod.Name,
			mod.DisplayName,
			mod.Folder,
			mod.NexusModsData?.Name,
			mod.ModioData?.Name);
		var tagSource = JoinSource(
			String.Join(" ", mod.Tags ?? Enumerable.Empty<string>()),
			String.Join(" ", GetModioTagLabels(mod.ModioData)));
		var summarySource = JoinSource(
			mod.NexusModsData?.Summary,
			mod.ModioData?.Summary,
			mod.Description);
		var descriptionSource = JoinSource(
			mod.NexusModsData?.Description,
			mod.ModioData?.Description);

		var matches = CategoryRules
			.Where(category => isCategoryEnabled(category.Name))
			.Select((category, index) => new
			{
				category.Name,
				Index = index,
				// An explicit package/project name is a stronger signal than broad
				// provider tags or descriptive prose (for example, "5e Spells").
				Score = (SourceContainsAny(nameSource, category.Keywords) ? 20 : 0)
					+ (SourceContainsAny(tagSource, category.Keywords) ? 12 : 0)
					+ (SourceContainsAny(summarySource, category.Keywords) ? 4 : 0)
					+ (SourceContainsAny(descriptionSource, category.Keywords) ? 1 : 0)
			})
			.OrderByDescending(match => match.Score)
			.ThenBy(match => match.Index)
			.Where(match => match.Score > 0)
			.ToList();

		foreach (var match in matches)
		{
			// Preserve Redux's best-effort single fallback when no provider data is
			// available. Additional pills require a name or tag-level signal so a
			// passing mention in descriptive prose cannot create category noise.
			if (resolved.Count == 0 || match.Score >= StrongSecondaryScore)
			{
				AddCategory(match.Name);
				if (resolved.Count >= maxCategories) break;
			}
		}

		return resolved.Count > 0 ? resolved : new[] { uncategorizedCategory };
	}

	public static bool TryGetNexusCategory(long categoryId, out string category) =>
		NexusCategories.TryGetValue(categoryId, out category);

	public static bool TryGetNexusCategoryId(string category, out long categoryId) =>
		NexusCategoryIds.TryGetValue(NormalizeProviderLabel(category), out categoryId);

	private static IEnumerable<string> GetProviderCategories(
		DivinityModData mod,
		Predicate<string> isCategoryEnabled)
	{
		var nexusIsExplicit = mod.NexusModsData?.MetadataOrigin is
			NexusMetadataOrigin.Manual or NexusMetadataOrigin.NexusArchiveImport;

		if (nexusIsExplicit && TryGetEnabledNexusCategory(mod, isCategoryEnabled, out var nexusCategory))
		{
			yield return nexusCategory;
			yield break;
		}

		// A native mod.io association is the active provider unless the user made
		// an explicit Nexus choice. Do not let a secondary cached Nexus match
		// override the native provider's taxonomy.
		if (!nexusIsExplicit && mod.ModioData?.HasMetadata == true)
		{
			foreach (var category in GetEnabledModioCategories(mod.ModioData, isCategoryEnabled))
			{
				yield return category;
			}
			yield break;
		}

		if (TryGetEnabledNexusCategory(mod, isCategoryEnabled, out nexusCategory))
		{
			yield return nexusCategory;
			yield break;
		}

		foreach (var category in GetEnabledModioCategories(mod.ModioData, isCategoryEnabled))
		{
			yield return category;
		}
	}

	private static bool TryGetEnabledNexusCategory(
		DivinityModData mod,
		Predicate<string> isCategoryEnabled,
		out string category)
	{
		if (TryGetNexusCategory(mod.NexusModsData?.CategoryId ?? 0, out category) &&
			isCategoryEnabled(category))
		{
			return true;
		}

		category = null;
		return false;
	}

	private static IEnumerable<string> GetEnabledModioCategories(
		ModioModData metadata,
		Predicate<string> isCategoryEnabled)
	{
		var returned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var label in GetModioTagLabels(metadata))
		{
			if (ModioTagCategories.TryGetValue(NormalizeProviderLabel(label), out var category) &&
				isCategoryEnabled(category) && returned.Add(category))
			{
				yield return category;
			}
		}
	}

	private static IEnumerable<string> GetModioTagLabels(ModioModData metadata)
	{
		foreach (var tag in metadata?.Tags ?? Enumerable.Empty<ModioTagData>())
		{
			if (!String.IsNullOrWhiteSpace(tag?.Name)) yield return tag.Name;
			if (!String.IsNullOrWhiteSpace(tag?.LocalizedName) &&
				!String.Equals(tag.LocalizedName, tag.Name, StringComparison.OrdinalIgnoreCase))
			{
				yield return tag.LocalizedName;
			}
		}
	}

	private static string NormalizeProviderLabel(string value)
	{
		if (String.IsNullOrWhiteSpace(value)) return String.Empty;
		var normalized = new string(value.Trim().ToLowerInvariant()
			.Select(character => Char.IsLetterOrDigit(character) ? character : ' ')
			.ToArray());
		return String.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	private static string JoinSource(params string[] values) =>
		String.Join(" ", values.Where(value => !String.IsNullOrWhiteSpace(value))).ToLowerInvariant();

	private static bool SourceContainsAny(string source, IEnumerable<string> keywords) =>
		!String.IsNullOrWhiteSpace(source) && keywords.Any(keyword => SourceContains(source, keyword));

	private static bool SourceContains(string source, string keyword)
	{
		if (keyword.Contains(' '))
		{
			return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
		}

		var searchIndex = 0;
		while (searchIndex < source.Length)
		{
			var matchIndex = source.IndexOf(keyword, searchIndex, StringComparison.OrdinalIgnoreCase);
			if (matchIndex < 0) return false;

			var beforeIsWord = matchIndex > 0 && IsWordCharacter(source[matchIndex - 1]);
			var afterIndex = matchIndex + keyword.Length;
			var afterIsWord = afterIndex < source.Length && IsWordCharacter(source[afterIndex]);
			if (!beforeIsWord && !afterIsWord) return true;
			searchIndex = matchIndex + 1;
		}

		return false;
	}

	private static bool IsWordCharacter(char value) => Char.IsLetterOrDigit(value) || value == '_';
}
