using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager.Controls.Extensions;

/// <summary>
/// Clips Xceed message-box chrome to a rounded rectangle without replacing its
/// template or changing its buttons and result handling.
/// </summary>
public static class RoundedMessageBoxChrome
{
	public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
		"Radius",
		typeof(double),
		typeof(RoundedMessageBoxChrome),
		new FrameworkPropertyMetadata(0d, OnRadiusChanged));

	private static readonly DependencyProperty ClippedElementProperty = DependencyProperty.RegisterAttached(
		"ClippedElement",
		typeof(FrameworkElement),
		typeof(RoundedMessageBoxChrome));

	public static void SetRadius(DependencyObject element, double value) => element.SetValue(RadiusProperty, value);

	public static double GetRadius(DependencyObject element) => (double)element.GetValue(RadiusProperty);

	private static void OnRadiusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is not Control control)
		{
			return;
		}

		control.Loaded -= OnControlLoaded;
		control.Unloaded -= OnControlUnloaded;

		if ((double)args.NewValue > 0)
		{
			control.Loaded += OnControlLoaded;
			control.Unloaded += OnControlUnloaded;

			if (control.IsLoaded)
			{
				ApplyRoundedClip(control);
			}
		}
		else
		{
			DetachClippedElement(control);
		}
	}

	private static void OnControlLoaded(object sender, RoutedEventArgs args)
	{
		if (sender is Control control)
		{
			ApplyRoundedClip(control);
		}
	}

	private static void OnControlUnloaded(object sender, RoutedEventArgs args)
	{
		if (sender is Control control)
		{
			DetachClippedElement(control);
		}
	}

	private static void ApplyRoundedClip(Control control)
	{
		control.ApplyTemplate();

		var chrome = control.Template?.FindName("PART_WindowControl", control) as FrameworkElement
			?? FindNamedDescendant(control, "PART_WindowControl");
		if (chrome == null)
		{
			return;
		}

		var previousChrome = control.GetValue(ClippedElementProperty) as FrameworkElement;
		if (!ReferenceEquals(previousChrome, chrome))
		{
			DetachClippedElement(control);
			control.SetValue(ClippedElementProperty, chrome);
			chrome.SizeChanged += OnChromeSizeChanged;
		}

		UpdateClip(control, chrome);
	}

	private static void OnChromeSizeChanged(object sender, SizeChangedEventArgs args)
	{
		if (sender is not FrameworkElement chrome)
		{
			return;
		}

		var messageBox = FindAncestorWithRadius(chrome);
		if (messageBox != null)
		{
			UpdateClip(messageBox, chrome);
		}
	}

	private static void UpdateClip(Control control, FrameworkElement chrome)
	{
		var radius = Math.Max(0, GetRadius(control));
		chrome.Clip = new RectangleGeometry(
			new Rect(0, 0, Math.Max(0, chrome.ActualWidth), Math.Max(0, chrome.ActualHeight)),
			radius,
			radius);
	}

	private static void DetachClippedElement(Control control)
	{
		if (control.GetValue(ClippedElementProperty) is FrameworkElement chrome)
		{
			chrome.SizeChanged -= OnChromeSizeChanged;
			chrome.ClearValue(UIElement.ClipProperty);
			control.ClearValue(ClippedElementProperty);
		}
	}

	private static Control FindAncestorWithRadius(DependencyObject element)
	{
		for (var current = VisualTreeHelper.GetParent(element); current != null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is Control control && GetRadius(control) > 0)
			{
				return control;
			}
		}

		return null;
	}

	private static FrameworkElement FindNamedDescendant(DependencyObject parent, string name)
	{
		for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
		{
			var child = VisualTreeHelper.GetChild(parent, index);
			if (child is FrameworkElement element && element.Name == name)
			{
				return element;
			}

			var match = FindNamedDescendant(child, name);
			if (match != null)
			{
				return match;
			}
		}

		return null;
	}
}
