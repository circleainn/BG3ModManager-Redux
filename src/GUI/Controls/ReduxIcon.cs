using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager.Controls;

/// <summary>
/// Theme-aware vector icon surface used by Redux-owned UI.
/// Geometry resources are vendored from Ionicons under the MIT license.
/// </summary>
public sealed class ReduxIcon : Control
{
	public ReduxIcon()
	{
		Loaded += (_, _) => ApplyIconKey();
	}

	/// <summary>
	/// Creates a consistently styled menu icon from an application geometry resource.
	/// </summary>
	public static ReduxIcon FromResource(string resourceKey, bool useStroke = false, string foregroundResourceKey = null)
	{
		var icon = new ReduxIcon();
		var geometry = Application.Current?.MainWindow?.TryFindResource(resourceKey) as Geometry
			?? Application.Current?.TryFindResource(resourceKey) as Geometry;
		if (geometry != null)
		{
			if (useStroke)
			{
				icon.StrokeData = geometry;
			}
			else
			{
				icon.Data = geometry;
			}
		}
		if (!String.IsNullOrWhiteSpace(foregroundResourceKey))
		{
			icon.SetResourceReference(ForegroundProperty, foregroundResourceKey);
		}
		return icon;
	}

	public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
		nameof(Data),
		typeof(Geometry),
		typeof(ReduxIcon));

	public static readonly DependencyProperty StrokeDataProperty = DependencyProperty.Register(
		nameof(StrokeData),
		typeof(Geometry),
		typeof(ReduxIcon));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
		nameof(StrokeThickness),
		typeof(double),
		typeof(ReduxIcon),
		new FrameworkPropertyMetadata(32d));

	public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(
		nameof(IconKey),
		typeof(string),
		typeof(ReduxIcon),
		new FrameworkPropertyMetadata(String.Empty, (dependencyObject, _) => ((ReduxIcon)dependencyObject).ApplyIconKey()));

	public Geometry Data
	{
		get => (Geometry)GetValue(DataProperty);
		set => SetValue(DataProperty, value);
	}

	public Geometry StrokeData
	{
		get => (Geometry)GetValue(StrokeDataProperty);
		set => SetValue(StrokeDataProperty, value);
	}

	public double StrokeThickness
	{
		get => (double)GetValue(StrokeThicknessProperty);
		set => SetValue(StrokeThicknessProperty, value);
	}

	/// <summary>
	/// Stable catalog ID used by persisted presentation settings such as category icons.
	/// Existing callers may continue to provide Data or StrokeData directly.
	/// </summary>
	public string IconKey
	{
		get => (string)GetValue(IconKeyProperty);
		set => SetValue(IconKeyProperty, value);
	}

	private void ApplyIconKey()
	{
		// A default, unset IconKey must not erase geometry supplied directly by legacy callers.
		if (ReadLocalValue(IconKeyProperty) == DependencyProperty.UnsetValue) return;
		// Catalog geometry is selected by the shared XAML style's IconKey triggers.
		// Clearing local values lets those deterministic StaticResource setters win.
		ClearValue(DataProperty);
		ClearValue(StrokeDataProperty);
	}
}

public sealed class ReduxIconChoice
{
	public string Id { get; }
	public string DisplayName { get; }
	public string ResourceKey { get; }
	public bool UseStroke { get; }
	public bool IsNone => String.IsNullOrWhiteSpace(Id);

	public ReduxIconChoice(string id, string displayName, string resourceKey = null, bool useStroke = false)
	{
		Id = id ?? String.Empty;
		DisplayName = displayName;
		ResourceKey = resourceKey;
		UseStroke = useStroke;
	}
}

/// <summary>
/// Curated, small-size-safe vector icons exposed to user-created categories and separators.
/// IDs are persisted; resource names remain an implementation detail.
/// </summary>
public static class ReduxIconCatalog
{
	public static IReadOnlyList<ReduxIconChoice> Choices { get; } = new List<ReduxIconChoice>
	{
		new("", "No icon (use default marker)"),
		new("list", "List", "Redux.Icon.ListStroke", true),
		new("palette", "Palette", "Redux.Icon.ColorPalette"),
		new("shirt", "Clothing / outfit", "Redux.Icon.Shirt"),
		new("sparkles", "Magic / sparkles", "Redux.Icon.Sparkles"),
		new("shield", "Armor / shield", "Redux.Icon.Shield"),
		new("flask", "Alchemy / flask", "Redux.Icon.Flask"),
		new("skull", "Skull", "Redux.Icon.Skull"),
		new("paw", "Creature / familiar", "Redux.Icon.Paw"),
		new("dice", "Dice", "Redux.Icon.Dice"),
		new("map", "Map", "Redux.Icon.Map"),
		new("hammer", "Weapon / crafting", "Redux.Icon.Hammer"),
		new("book", "Lore / spellbook", "Redux.Icon.Book"),
		new("eye", "Appearance / eye", "Redux.Icon.Eye"),
		new("camera", "Camera / photo mode", "Redux.Icon.Camera"),
		new("person", "Person / character", "Redux.Icon.Person"),
		new("people", "Party / companions", "Redux.Icon.People"),
		new("albums", "Collection / all mods", "Redux.Icon.Albums"),
		new("desktop", "Desktop / interface", "Redux.Icon.Desktop"),
		new("school", "Class / education", "Redux.Icon.School"),
		new("body", "Body / customization", "Redux.Icon.Body"),
		new("music", "Music / audio", "Redux.Icon.MusicalNotes"),
		new("cube", "Equipment / item", "Redux.Icon.Cube"),
		new("shield-half", "Armor / defense", "Redux.Icon.ShieldHalfFill"),
		new("construct", "Utility / construction", "Redux.Icon.Construct"),
		new("film", "Animation / motion", "Redux.Icon.Film"),
		new("sword", "Sword / weapon", "Redux.Icon.SwordStroke", true),
		new("flame", "Fire / flame", "Redux.Icon.Flame"),
		new("star", "Star / favorite", "Redux.Icon.Star"),
		new("moon", "Moon / night", "Redux.Icon.Moon"),
		new("sunny", "Sun / light", "Redux.Icon.Sunny"),
		new("leaf", "Nature / druid", "Redux.Icon.Leaf"),
		new("rose", "Flower / romance", "Redux.Icon.Rose"),
		new("diamond", "Gem / treasure", "Redux.Icon.Diamond"),
		new("planet", "Planar / cosmic", "Redux.Icon.Planet"),
		new("flash", "Lightning / power", "Redux.Icon.Flash"),
		new("bonfire", "Campfire", "Redux.Icon.Bonfire"),
		new("trail", "Travel / direction", "Redux.Icon.TrailSign"),
		new("wand", "Magic wand", "Redux.Icon.ColorWand"),
		new("bug", "Creature / bug", "Redux.Icon.Bug"),
		new("nutrition", "Food / consumable", "Redux.Icon.Nutrition"),
		new("gameplay", "Game controller", "Redux.Icon.GameController"),
		new("puzzle", "Puzzle piece", "Redux.Icon.ExtensionPuzzle"),
		new("terminal", "Scripts / terminal", "Redux.Icon.Terminal"),
		new("tools", "Tools", "Redux.Icon.Build"),
		new("heart", "Heart", "Redux.Icon.Heart"),
		new("key", "Key", "Redux.Icon.Key"),
		new("tag", "Tag", "Redux.Icon.Pricetag"),
		new("archive", "Archive", "Redux.Icon.Archive"),
		new("document", "Document", "Redux.Icon.DocumentText"),
		new("folder", "Folder", "Redux.Icon.FolderOpen"),
		new("audio", "Audio", "Redux.Icon.VolumeHigh"),
		new("settings", "Settings", "Redux.Icon.Settings"),
		new("download", "Download", "Redux.Icon.Download"),
		new("warning", "Warning", "Redux.Icon.Warning"),
		new("info", "Information", "Redux.Icon.Information"),
		new("help", "Help", "Redux.Icon.HelpCircle"),
		new("refresh", "Refresh / overhaul", "Redux.Icon.RefreshStroke", true)
	};

	private static readonly Dictionary<string, ReduxIconChoice> ById = Choices
		.Where(choice => !choice.IsNone)
		.ToDictionary(choice => choice.Id, StringComparer.OrdinalIgnoreCase);

	public static bool TryGet(string id, out ReduxIconChoice choice) =>
		ById.TryGetValue(id ?? String.Empty, out choice);

	public static string Normalize(string id) => TryGet(id, out var choice) ? choice.Id : String.Empty;
}
