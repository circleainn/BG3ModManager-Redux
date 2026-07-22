using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DivinityModManager.Util;

namespace DivinityModManager.Controls;

/// <summary>
/// Theme-aware vector icon surface used by Redux-owned UI.
/// Geometry resources are vendored from Lucide under the ISC license.
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
		new FrameworkPropertyMetadata(2d));

	public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(
		nameof(IconKey),
		typeof(string),
		typeof(ReduxIcon),
		new FrameworkPropertyMetadata(String.Empty, (dependencyObject, _) => ((ReduxIcon)dependencyObject).ApplyIconKey()));

	public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
		nameof(ImageSource),
		typeof(ImageSource),
		typeof(ReduxIcon),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty TintCustomImageProperty = DependencyProperty.Register(
		nameof(TintCustomImage),
		typeof(bool),
		typeof(ReduxIcon),
		new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

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

	public ImageSource ImageSource
	{
		get => (ImageSource)GetValue(ImageSourceProperty);
		private set => SetValue(ImageSourceProperty, value);
	}

	public bool TintCustomImage
	{
		get => (bool)GetValue(TintCustomImageProperty);
		private set => SetValue(TintCustomImageProperty, value);
	}

	private void ApplyIconKey()
	{
		// A default, unset IconKey must not erase geometry supplied directly by legacy callers
		// (ReduxIcon.FromResource sets Data/StrokeData directly and never touches IconKey).
		// ReadLocalValue is not a reliable signal for that here: it reports UnsetValue even for
		// IconKey values applied through a DataTemplate binding. An empty IconKey is the actual
		// reliable "never set" signal.
		if (String.IsNullOrEmpty(IconKey)) return;
		// Catalog geometry is selected by the shared XAML style's IconKey triggers.
		// Clearing local values lets those deterministic StaticResource setters win.
		ClearValue(DataProperty);
		ClearValue(StrokeDataProperty);
		ClearValue(ImageSourceProperty);
		ClearValue(TintCustomImageProperty);
		TintCustomImage = ReduxCustomIconService.IsTintedReference(IconKey);
		if (ReduxCustomIconService.IsCustomReference(IconKey) && ReduxCustomIconService.TryLoad(IconKey, out var imageSource))
		{
			ImageSource = imageSource;
			// Imported PNGs are stored up to 1024x1024 but drawn at icon size (often under
			// 20px), a >50x reduction where WPF's default scaling aliases badly. Match the
			// HighQuality mode already used for other bitmap content in the app (mod thumbnails).
			RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
		}
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		if (ImageSource == null || ActualWidth <= 0 || ActualHeight <= 0) return;

		var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
		if (!TintCustomImage)
		{
			drawingContext.DrawImage(ImageSource, bounds);
			return;
		}

		var mask = new ImageBrush(ImageSource) { Stretch = Stretch.Uniform };
		if (mask.CanFreeze) mask.Freeze();
		drawingContext.PushOpacityMask(mask);
		drawingContext.DrawRectangle(Foreground, null, bounds);
		drawingContext.Pop();
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
		// Markers and broad categories.
		new("", "Dot marker (default)"),
		new("marker-diamond", "Diamond marker", "Redux.Icon.MarkerDiamond"),
		new("list", "List", "Redux.Icon.ListStroke", true),
		new("albums", "Collection / all mods", "Redux.Icon.Albums"),
		new("tag", "Tag", "Redux.Icon.Pricetag"),
		new("shapes", "Miscellaneous", "Redux.Icon.Shapes", true),

		// Characters, parties, and creatures.
		new("body", "Body / character", "Redux.Icon.Body"),
		new("person", "Person / character", "Redux.Icon.Person"),
		new("people", "Party / companions", "Redux.Icon.People"),
		new("crown", "Legendary / ruler", "Redux.Icon.Crown", true),
		new("dna", "Race / species", "Redux.Icon.Dna", true),
		new("paw", "Creature / familiar", "Redux.Icon.Paw"),
		new("skull", "Undead / danger", "Redux.Icon.Skull"),
		new("bug", "Creature / bug", "Redux.Icon.Bug"),

		// Equipment, combat, and crafting.
		new("sword", "Crossed swords / weapons", "Redux.Icon.SwordStroke", true),
		new("axe", "Battle axe", "Redux.Icon.Axe", true),
		new("bow-arrow", "Bow and arrow", "Redux.Icon.BowArrow", true),
		new("shield", "Armor / shield", "Redux.Icon.Shield"),
		new("shield-half", "Armor / defense", "Redux.Icon.ShieldHalfFill"),
		new("shield-check", "Protected / verified", "Redux.Icon.ShieldCheck", true),
		new("shirt", "Clothing / outfit", "Redux.Icon.Shirt"),
		new("glasses", "Accessories", "Redux.Icon.Glasses", true),
		new("package", "Equipment / package", "Redux.Icon.Package", true),
		new("cube", "Equipment / item", "Redux.Icon.Cube"),
		new("diamond", "Gem / treasure", "Redux.Icon.Diamond"),
		new("key", "Key", "Redux.Icon.Key"),
		new("hammer", "Hammer / crafting", "Redux.Icon.Hammer"),
		new("anvil", "Anvil / smithing", "Redux.Icon.Anvil", true),

		// Magic, lore, and nature.
		new("sparkles", "Magic / sparkles", "Redux.Icon.Sparkles"),
		new("moon-star", "Spells / arcane", "Redux.Icon.MoonStar", true),
		new("wand", "Magic wand", "Redux.Icon.ColorWand"),
		new("wand-sparkles", "Enchanted wand", "Redux.Icon.WandSparkles", true),
		new("flask", "Alchemy / flask", "Redux.Icon.Flask"),
		new("book", "Lore / spellbook", "Redux.Icon.Book"),
		new("book-open", "Open spellbook", "Redux.Icon.BookOpen", true),
		new("scroll", "Quest / scroll", "Redux.Icon.Scroll", true),
		new("scroll-text", "Written scroll", "Redux.Icon.ScrollText", true),
		new("flame", "Fire / flame", "Redux.Icon.Flame"),
		new("flash", "Lightning / power", "Redux.Icon.Flash"),
		new("leaf", "Nature / druid", "Redux.Icon.Leaf"),
		new("rose", "Flower / romance", "Redux.Icon.Rose"),
		new("star", "Star / favorite", "Redux.Icon.Star"),
		new("moon", "Moon / night", "Redux.Icon.Moon"),
		new("sunny", "Sun / light", "Redux.Icon.Sunny"),
		new("planet", "Planar / cosmic", "Redux.Icon.Planet"),
		new("heart", "Heart", "Redux.Icon.Heart"),
		new("nutrition", "Food / consumable", "Redux.Icon.Nutrition"),
		new("bonfire", "Campfire", "Redux.Icon.Bonfire"),

		// World, travel, and activities.
		new("map", "Map", "Redux.Icon.Map"),
		new("compass", "Exploration / travel", "Redux.Icon.Compass", true),
		new("trail", "Travel / direction", "Redux.Icon.TrailSign"),
		new("castle", "Castle / stronghold", "Redux.Icon.Castle", true),
		new("target", "Objectives / targets", "Redux.Icon.Target", true),
		new("dice", "Dice", "Redux.Icon.Dice"),
		new("gameplay", "Game controller", "Redux.Icon.GameController"),

		// Media, appearance, and interface.
		new("eye", "Appearance / eye", "Redux.Icon.Eye"),
		new("palette", "Palette / cosmetics", "Redux.Icon.ColorPalette"),
		new("paintbrush", "Paintbrush / cosmetics", "Redux.Icon.Paintbrush", true),
		new("camera", "Camera / photo mode", "Redux.Icon.Camera"),
		new("aperture", "Visuals / camera settings", "Redux.Icon.Aperture", true),
		new("film", "Animation / motion", "Redux.Icon.Film"),
		new("music", "Music / audio", "Redux.Icon.MusicalNotes"),
		new("audio", "Audio", "Redux.Icon.VolumeHigh"),
		new("desktop", "Desktop / interface", "Redux.Icon.Desktop"),

		// Organization, frameworks, and tools.
		new("folder", "Folder", "Redux.Icon.FolderOpen"),
		new("archive", "Archive", "Redux.Icon.Archive"),
		new("document", "Document", "Redux.Icon.DocumentText"),
		new("blocks", "Libraries / framework", "Redux.Icon.Blocks", true),
		new("database", "Resources / data", "Redux.Icon.Database", true),
		new("puzzle", "Patch / extension", "Redux.Icon.ExtensionPuzzle"),
		new("bandage", "Patch / repair", "Redux.Icon.Bandage", true),
		new("construct", "Utility / construction", "Redux.Icon.Construct"),
		new("tools", "Tools", "Redux.Icon.Build"),
		new("toolbox", "Utilities", "Redux.Icon.Toolbox", true),
		new("terminal", "Scripts / terminal", "Redux.Icon.Terminal"),
		new("settings", "Settings", "Redux.Icon.Settings"),
		new("refresh", "Refresh / overhaul", "Redux.Icon.RefreshStroke", true),
		new("download", "Download", "Redux.Icon.Download"),
		new("shield-alert", "Overrides / conflict", "Redux.Icon.ShieldAlert", true),
		new("warning", "Warning", "Redux.Icon.Warning"),
		new("info", "Information", "Redux.Icon.Information"),
		new("help", "Help", "Redux.Icon.HelpCircle"),
		new("school", "Class / education", "Redux.Icon.School")
	};

	private static readonly Dictionary<string, ReduxIconChoice> ById = Choices
		.Where(choice => !choice.IsNone)
		.ToDictionary(choice => choice.Id, StringComparer.OrdinalIgnoreCase);

	public static bool TryGet(string id, out ReduxIconChoice choice) =>
		ById.TryGetValue(id ?? String.Empty, out choice);

	public static string Normalize(string id)
	{
		if (TryGet(id, out var choice)) return choice.Id;
		return ReduxCustomIconService.NormalizeReference(id);
	}
}
