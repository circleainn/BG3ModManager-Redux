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
		new("", "Dot marker (default)"),
		new("marker-diamond", "Diamond marker", "Redux.Icon.MarkerDiamond"),
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
		new("body", "Body / character", "Redux.Icon.Body"),
		new("music", "Music / audio", "Redux.Icon.MusicalNotes"),
		new("cube", "Equipment / item", "Redux.Icon.Cube"),
		new("shield-half", "Armor / defense", "Redux.Icon.ShieldHalfFill"),
		new("construct", "Utility / construction", "Redux.Icon.Construct"),
		new("film", "Animation / motion", "Redux.Icon.Film"),
		new("sword", "Crossed swords / weapons", "Redux.Icon.SwordStroke", true),
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
		new("refresh", "Refresh / overhaul", "Redux.Icon.RefreshStroke", true),
		new("dna", "Race / species", "Redux.Icon.Dna", true),
		new("moon-star", "Spells / arcane", "Redux.Icon.MoonStar", true),
		new("scroll", "Quest / scroll", "Redux.Icon.Scroll", true),
		new("glasses", "Accessories", "Redux.Icon.Glasses", true),
		new("package", "Equipment / package", "Redux.Icon.Package", true),
		new("aperture", "Visuals / camera settings", "Redux.Icon.Aperture", true),
		new("bandage", "Patches / fixes", "Redux.Icon.Bandage", true),
		new("blocks", "Libraries / framework", "Redux.Icon.Blocks", true),
		new("database", "Resources / data", "Redux.Icon.Database", true),
		new("toolbox", "Utilities", "Redux.Icon.Toolbox", true),
		new("shapes", "Miscellaneous", "Redux.Icon.Shapes", true),
		new("shield-alert", "Overrides / conflict", "Redux.Icon.ShieldAlert", true),
		new("paintbrush", "Cosmetics", "Redux.Icon.Paintbrush", true),
		new("crown", "Legendary / boss", "Redux.Icon.Crown", true),
		new("compass", "Exploration / travel", "Redux.Icon.Compass", true),
		new("target", "Objectives / targets", "Redux.Icon.Target", true)
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
