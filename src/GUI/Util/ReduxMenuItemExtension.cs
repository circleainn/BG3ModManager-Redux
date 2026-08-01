using DivinityModManager.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager.Util;

/// <summary>
/// Lets an individual menu row override the accent tint used by
/// <c>ReduxPopupMenuItemTemplate</c>'s hover surface.
/// </summary>
/// <remarks>
/// The category-assignment rows are built in code and want their own category colour on hover.
/// An attached property is used rather than repurposing <see cref="FrameworkElement.Tag"/>,
/// because sibling rows in the same menu already carry an unrelated marker in Tag
/// (<c>SourceLinkMenuTag</c>); binding the hover to Tag would leave those rows with a null
/// brush and no visible hover at all.
///
/// When unset the template falls back to <c>ReduxAccentSoftBrush</c> via a trigger rather than
/// a binding fallback, so the default keeps following theme changes.
/// </remarks>
public static class ReduxMenuItemExtension
{
	private static readonly List<WeakReference<MenuItem>> SemanticMenuItems = new();

	static ReduxMenuItemExtension()
	{
		EventManager.RegisterClassHandler(
			typeof(MenuItem),
			MenuItem.SubmenuOpenedEvent,
			new RoutedEventHandler(MenuItem_SubmenuOpened),
			true);

		DivinityApp.StaticPropertyChanged += (_, args) =>
		{
			if (args.PropertyName == nameof(DivinityApp.UseCategoryColorsForInteractions))
			{
				RefreshSemanticHoverItems();
			}
		};
	}

	private static void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem menuItem)
		{
			ApplySemanticHoverToMenu(menuItem);
		}
	}

	public static readonly DependencyProperty HoverBrushProperty =
		DependencyProperty.RegisterAttached(
			"HoverBrush",
			typeof(Brush),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(null));

	public static Brush GetHoverBrush(DependencyObject element) => (Brush)element.GetValue(HoverBrushProperty);

	public static void SetHoverBrush(DependencyObject element, Brush value) => element.SetValue(HoverBrushProperty, value);

	/// <summary>
	/// Colour of the row's leading rail, which the template paints as the hover surface's left
	/// border. Kept separate from <see cref="HoverBrushProperty"/> because the rail is drawn at
	/// full strength while the fill behind it is translucent.
	/// </summary>
	public static readonly DependencyProperty RailBrushProperty =
		DependencyProperty.RegisterAttached(
			"RailBrush",
			typeof(Brush),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(null));

	public static Brush GetRailBrush(DependencyObject element) => (Brush)element.GetValue(RailBrushProperty);

	public static void SetRailBrush(DependencyObject element, Brush value) => element.SetValue(RailBrushProperty, value);

	/// <summary>
	/// Enables a row-specific semantic hover treatment. The semantic brushes become the
	/// template's effective hover brushes only while category-coloured interactions are on;
	/// otherwise the row falls back to Redux's standard accent hover.
	/// </summary>
	public static readonly DependencyProperty UseSemanticHoverProperty =
		DependencyProperty.RegisterAttached(
			"UseSemanticHover",
			typeof(bool),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(false, SemanticHoverPropertyChanged));

	public static bool GetUseSemanticHover(DependencyObject element) =>
		(bool)element.GetValue(UseSemanticHoverProperty);

	public static void SetUseSemanticHover(DependencyObject element, bool value) =>
		element.SetValue(UseSemanticHoverProperty, value);

	public static readonly DependencyProperty SemanticHoverBrushProperty =
		DependencyProperty.RegisterAttached(
			"SemanticHoverBrush",
			typeof(Brush),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(null, SemanticHoverPropertyChanged));

	public static Brush GetSemanticHoverBrush(DependencyObject element) =>
		(Brush)element.GetValue(SemanticHoverBrushProperty);

	public static void SetSemanticHoverBrush(DependencyObject element, Brush value) =>
		element.SetValue(SemanticHoverBrushProperty, value);

	public static readonly DependencyProperty SemanticRailBrushProperty =
		DependencyProperty.RegisterAttached(
			"SemanticRailBrush",
			typeof(Brush),
			typeof(ReduxMenuItemExtension),
			new FrameworkPropertyMetadata(null, SemanticHoverPropertyChanged));

	public static Brush GetSemanticRailBrush(DependencyObject element) =>
		(Brush)element.GetValue(SemanticRailBrushProperty);

	public static void SetSemanticRailBrush(DependencyObject element, Brush value) =>
		element.SetValue(SemanticRailBrushProperty, value);

	private static void SemanticHoverPropertyChanged(
		DependencyObject dependencyObject,
		DependencyPropertyChangedEventArgs _)
	{
		if (dependencyObject is not MenuItem menuItem)
		{
			return;
		}

		if (!SemanticMenuItems.Any(reference =>
			reference.TryGetTarget(out var existing) && ReferenceEquals(existing, menuItem)))
		{
			SemanticMenuItems.Add(new WeakReference<MenuItem>(menuItem));
		}

		ApplySemanticHover(menuItem);
	}

	private static void RefreshSemanticHoverItems()
	{
		for (var index = SemanticMenuItems.Count - 1; index >= 0; index--)
		{
			if (!SemanticMenuItems[index].TryGetTarget(out var menuItem))
			{
				SemanticMenuItems.RemoveAt(index);
				continue;
			}

			ApplySemanticHover(menuItem);
		}
	}

	private static void ApplySemanticHover(MenuItem menuItem)
	{
		if (GetUseSemanticHover(menuItem) && DivinityApp.UseCategoryColorsForInteractions)
		{
			menuItem.SetCurrentValue(HoverBrushProperty, GetSemanticHoverBrush(menuItem));
			menuItem.SetCurrentValue(RailBrushProperty, GetSemanticRailBrush(menuItem));
			return;
		}

		menuItem.ClearValue(HoverBrushProperty);
		menuItem.ClearValue(RailBrushProperty);
	}

	public static void ApplySemanticHoverToMenu(ItemsControl menu)
	{
		if (menu == null) return;
		foreach (var menuItem in menu.Items.OfType<MenuItem>())
		{
			ApplyInferredSemanticHover(menuItem);
			if (menuItem.HasItems)
			{
				ApplySemanticHoverToMenu(menuItem);
			}
		}
	}

	private static void ApplyInferredSemanticHover(MenuItem menuItem)
	{
		if (GetUseSemanticHover(menuItem))
		{
			ApplySemanticHover(menuItem);
			return;
		}

		var header = menuItem.Header switch
		{
			string text => text,
			TextBlock textBlock => textBlock.Text,
			_ => String.Empty
		};

		string hoverBrushResource = null;
		string railBrushResource = null;
		if (header.Contains("Nexus Mods", StringComparison.OrdinalIgnoreCase))
		{
			hoverBrushResource = "Redux.Pill.Nexus.Background";
			railBrushResource = "Redux.Pill.Nexus.Border";
		}
		else if (header.Contains("mod.io", StringComparison.OrdinalIgnoreCase))
		{
			hoverBrushResource = "Redux.Pill.Modio.Background";
			railBrushResource = "Redux.Pill.Modio.Border";
		}
		else if (IsDestructiveOrStopAction(header))
		{
			hoverBrushResource = "ReduxErrorPillBackground";
			railBrushResource = "ReduxErrorBrush";
		}
		else if (IsPositiveCommitAction(header))
		{
			hoverBrushResource = "ReduxSuccessPillBackground";
			railBrushResource = "ReduxSuccessBrush";
			if (menuItem.Icon is ReduxIcon positiveIcon)
			{
				positiveIcon.SetResourceReference(Control.ForegroundProperty, "ReduxSuccessBrush");
			}
		}
		if (hoverBrushResource == null || railBrushResource == null)
		{
			return;
		}

		// Keep semantic menu roles live across built-in and custom theme changes. Resolving the
		// brush here would cache the object from whichever theme happened to be active when the
		// submenu first opened, while the icon's DynamicResource continued updating independently.
		menuItem.SetResourceReference(SemanticHoverBrushProperty, hoverBrushResource);
		menuItem.SetResourceReference(SemanticRailBrushProperty, railBrushResource);
		SetUseSemanticHover(menuItem, true);
	}

	private static bool IsDestructiveOrStopAction(string header)
	{
		if (String.IsNullOrWhiteSpace(header)) return false;
		return header.Contains("Delete", StringComparison.OrdinalIgnoreCase)
			|| header.Contains("Remove", StringComparison.OrdinalIgnoreCase)
			|| header.Contains("Unlink", StringComparison.OrdinalIgnoreCase)
			|| header.Contains("Clear", StringComparison.OrdinalIgnoreCase)
			|| header.StartsWith("Stop ", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPositiveCommitAction(string header)
	{
		if (String.IsNullOrWhiteSpace(header)) return false;
		return header.StartsWith("Export", StringComparison.OrdinalIgnoreCase)
			|| (header.StartsWith("Save ", StringComparison.OrdinalIgnoreCase)
				&& !header.Contains("Folder", StringComparison.OrdinalIgnoreCase))
			|| header.StartsWith("Generate Redux Database Contribution", StringComparison.OrdinalIgnoreCase);
	}
}
