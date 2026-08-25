using System.Windows;

namespace DivinityModManager.Controls.Behavior;

public static class ScreenReaderHelperBehavior
{
	private sealed class ScreenReaderPropertyMap
	{
		public System.Reflection.PropertyInfo Name { get; init; }
		public System.Reflection.PropertyInfo HelpText { get; init; }
	}

	private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ScreenReaderPropertyMap[]> AutomaticPropertyMaps = new();

	public static string GetName(DependencyObject element)
	{
		return (string)element.GetValue(NameProperty);
	}

	public static void SetName(DependencyObject element, string value)
	{
		element.SetValue(NameProperty, value);
	}

	public static readonly DependencyProperty NameProperty =
		DependencyProperty.RegisterAttached(
		"Name",
		typeof(string),
		typeof(ScreenReaderHelperBehavior),
		new UIPropertyMetadata("", OnName));

	static void OnName(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
	{
		System.Windows.Automation.AutomationProperties.SetName(depObj, (string)e.NewValue);
	}

	public static string GetHelpText(DependencyObject element)
	{
		return (string)element.GetValue(HelpTextProperty);
	}

	public static void SetHelpText(DependencyObject element, string value)
	{
		element.SetValue(HelpTextProperty, value);
	}

	public static readonly DependencyProperty HelpTextProperty =
		DependencyProperty.RegisterAttached(
		"HelpText",
		typeof(string),
		typeof(ScreenReaderHelperBehavior),
		new UIPropertyMetadata("", OnHelpText));

	static void OnHelpText(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
	{
		System.Windows.Automation.AutomationProperties.SetHelpText(depObj, (string)e.NewValue);
	}

	public static bool GetAutomatic(DependencyObject element)
	{
		return (bool)element.GetValue(AutomaticProperty);
	}

	public static void SetAutomatic(DependencyObject element, bool value)
	{
		element.SetValue(AutomaticProperty, value);
	}

	public static readonly DependencyProperty AutomaticProperty =
		DependencyProperty.RegisterAttached(
		"Automatic",
		typeof(bool),
		typeof(ScreenReaderHelperBehavior),
		new UIPropertyMetadata(false, OnAutomaticChanged));

	static void OnAutomaticChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
	{
		if (depObj is FrameworkElement element)
		{
			if (e.NewValue is bool enabled)
			{
				if (enabled)
				{
					element.DataContextChanged += Element_DataContextChanged;
					if (element.DataContext != null)
					{
						Element_DataContextChanged(element, new DependencyPropertyChangedEventArgs(FrameworkElement.DataContextProperty, null, element.DataContext));
					}
				}
				else
				{
					element.DataContextChanged -= Element_DataContextChanged;
				}
			}
		}
	}

	private static void Element_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is not DependencyObject depObj || e.NewValue == null)
		{
			return;
		}

		var maps = AutomaticPropertyMaps.GetOrAdd(e.NewValue.GetType(), BuildAutomaticPropertyMaps);
		foreach (var map in maps)
		{
			if (map.Name?.GetValue(e.NewValue) is string name && !String.IsNullOrEmpty(name))
			{
				System.Windows.Automation.AutomationProperties.SetName(depObj, name);
			}
			if (map.HelpText?.GetValue(e.NewValue) is string helpText && !String.IsNullOrEmpty(helpText))
			{
				System.Windows.Automation.AutomationProperties.SetHelpText(depObj, helpText);
			}
		}
	}

	private static ScreenReaderPropertyMap[] BuildAutomaticPropertyMaps(Type type)
	{
		var maps = new System.Collections.Generic.List<ScreenReaderPropertyMap>();
		foreach (ScreenReaderHelperAttribute attribute in type.GetCustomAttributes(typeof(ScreenReaderHelperAttribute), true))
		{
			var map = new ScreenReaderPropertyMap
			{
				Name = String.IsNullOrEmpty(attribute.Name) ? null : type.GetProperty(attribute.Name),
				HelpText = String.IsNullOrEmpty(attribute.HelpText) ? null : type.GetProperty(attribute.HelpText)
			};
			if (map.Name != null || map.HelpText != null)
			{
				maps.Add(map);
			}
		}
		return maps.ToArray();
	}
}
