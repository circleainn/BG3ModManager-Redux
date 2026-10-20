using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class ReduxSnapshotUiTests
{
	public void BackupPreferencesRemainUsableAcrossThemesAndLargeText()
	{
		var originalResources = Application.Current.Resources;
		var originalMotion = ReduxWindowBehavior.ReduceMotion;
		var originalBackgroundEffects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		try
		{
			var resources = new ResourceDictionary();
			foreach (var file in new[] { "Typography", "Light", "Dark", "MainResourceDictionary" })
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Redux;component/Themes/{file}.xaml") });
			Application.Current.Resources = resources;
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			foreach (var textSize in Enum.GetValues<ReduxTextSize>())
			foreach (var reduceMotion in new[] { false, true })
			{
				ReduxThemeService.Apply(resources, theme);
				ReduxTypographyService.ApplyTextSize(resources, textSize);
				ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, originalBackgroundEffects);
				// Keep the real Preferences resource and tab ancestors. An isolated UserControl
				// misses its host's implicit horizontal/centered StackPanel style.
				var window = new SettingsWindow();
				try
				{
					ReduxThemeService.Apply(window.Resources, theme);
					var root = (Grid)window.Content;
					root.Opacity = 1;
					root.RenderTransform = Transform.Identity;
					var tabs = (TabControl)window.FindName("PreferencesTabControl");
					var tab = (TabItem)window.FindName("BackupsSettingsTab");
					var view = (ReduxBackupsView)window.FindName("BackupsView");
					tabs.SelectedItem = tab;
					var width = textSize == ReduxTextSize.Default ? 860 : 780;
					var height = textSize == ReduxTextSize.Default ? 940 : 600;
					void Layout()
					{
						root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
						root.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
					}
					((TextBlock)view.FindName("SnapshotFolderText")).Text = @"C:\Modding\Redux\Backups\Snapshots";
					((TextBox)view.FindName("SnapshotName")).Text = "Before installing a new mod collection";
					((ListBox)view.FindName("Snapshots")).ItemsSource = new[]
					{
						new { Name = "Working load order before updating", Summary = "0.1.0-alpha.16.6 · 10/20/2026 · 2,450 MB · Redux + mods and mod lists" },
						new { Name = "Before restoring the previous configuration", Summary = "0.1.0-alpha.16.6 · 10/19/2026 · 2,500 MB · Redux + mods and mod lists" }
					};
					Layout();
					var scroll = (ScrollViewer)view.Content;
					var heading = Descendants<TextBlock>(view).Single(t => t.Text == "Backups and snapshots");
					var headingBounds = Bounds(heading, scroll);
					RegressionAssert.True(headingBounds.Top >= 0 && headingBounds.Bottom < 65);
					var name = (TextBox)view.FindName("SnapshotName");
					RegressionAssert.True(Bounds(name, scroll).Top > headingBounds.Bottom);
					RegressionAssert.True(Bounds(name, scroll).Right <= scroll.ActualWidth);
					RegressionAssert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
					RegressionAssert.True(tab.IsEnabled && ((Button)view.FindName("CreateSnapshotButton")).IsEnabled);
					foreach (var item in Descendants<ListBoxItem>(view))
					{
						var lines = Descendants<TextBlock>(item).ToArray();
						RegressionAssert.Equal(2, lines.Length);
						RegressionAssert.True(Bounds(lines[1], item).Top >= Bounds(lines[0], item).Bottom);
					}
					foreach (var button in Descendants<Button>(view).Where(b => b.Content is string && b.Visibility == Visibility.Visible))
					{
						button.BringIntoView(); Layout();
						var bounds = Bounds(button, scroll);
						RegressionAssert.True(bounds.Width > 0 && bounds.Left >= 0 && bounds.Right <= scroll.ActualWidth);
						RegressionAssert.True(bounds.Top >= -1 && bounds.Bottom <= scroll.ActualHeight + 1);
						var icon = Descendants<ReduxIcon>(button).Single();
						RegressionAssert.True(icon.StrokeData != null);
					}
					tabs.SelectedItem = window.FindName("AdvancedSettingsTab"); Layout();
					RegressionAssert.True(tab.IsEnabled);
					var backupHeader = Descendants<TextBlock>(tab).Single(t => t.Text == "Backups");
					var appearanceHeader = Descendants<TextBlock>((TabItem)window.FindName("AppearanceSettingsTab")).Single(t => t.Text == "Appearance");
					RegressionAssert.Equal(((SolidColorBrush)appearanceHeader.Foreground).Color, ((SolidColorBrush)backupHeader.Foreground).Color);
					tabs.SelectedItem = tab; Layout();
					scroll.ScrollToTop(); Layout();
					var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
					if (!String.IsNullOrWhiteSpace(output) && reduceMotion)
					{
						Directory.CreateDirectory(output);
						var dpi = textSize == ReduxTextSize.Default ? 96 : textSize == ReduxTextSize.Compact ? 120 : 144;
						var bitmap = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
						bitmap.Render(root);
						var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
						using var file = File.Create(Path.Combine(output, $"backup-preferences-{theme}-{textSize}.png")); encoder.Save(file);
					}
				}
				finally { window.Close(); }
			}
		}
		finally { Application.Current.Resources = originalResources; ReduxWindowBehavior.ConfigureAccessibility(originalMotion, originalBackgroundEffects); }
	}
	private static Rect Bounds(FrameworkElement element, Visual ancestor) => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
	private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T typed) yield return typed;
			foreach (var descendant in Descendants<T>(child)) yield return descendant;
		}
	}
}
