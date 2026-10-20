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
		try
		{
			var resources = new ResourceDictionary();
			foreach (var file in new[] { "Typography", "Light", "Dark", "MainResourceDictionary" })
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Redux;component/Themes/{file}.xaml") });
			Application.Current.Resources = resources;
			var frames = new List<BitmapSource>();
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			foreach (var scale in new[] { 1d, 1.5d })
			{
				ReduxThemeService.Apply(resources, theme);
				foreach (var size in new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 32, 34 }) resources[$"Redux.FontSize.{size}"] = size * scale;
				var view = new ReduxBackupsView { Width = 700, Height = 560, FontSize = 12 * scale,
					Background = (Brush)resources["ReduxSurfaceBrush"] };
				((TextBlock)view.FindName("SnapshotFolderText")).Text = @"C:\Modding\Redux\Backups\Snapshots";
				((TextBox)view.FindName("SnapshotName")).Text = "Before installing a new mod collection";
				((ListBox)view.FindName("Snapshots")).ItemsSource = new[]
				{
					new { Name = "Working load order before updating", Summary = "0.1.0-alpha.16.6 · 10/20/2026 · 2,450 MB · Redux + mods and mod lists" },
					new { Name = "Before restoring the previous configuration", Summary = "0.1.0-alpha.16.6 · 10/19/2026 · 2,500 MB · Redux + mods and mod lists" }
				};
				view.Measure(new Size(700, 560)); view.Arrange(new Rect(0, 0, 700, 560)); view.UpdateLayout();
				view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
				var scroll = (ScrollViewer)view.Content;
				RegressionAssert.True(scroll.ExtentWidth <= scroll.ViewportWidth + 1);
				foreach (var button in Descendants<Button>(view).Where(b => b.Content is string && b.Visibility == Visibility.Visible))
				{
					button.BringIntoView(); view.UpdateLayout();
					var bounds = button.TransformToAncestor(scroll).TransformBounds(new Rect(button.RenderSize));
					RegressionAssert.True(bounds.Width > 0 && bounds.Left >= 0 && bounds.Right <= 700);
					var icon = Descendants<ReduxIcon>(button).Single();
					RegressionAssert.True(icon.StrokeData != null);
				}
				scroll.ScrollToTop(); view.UpdateLayout();
				var bitmap = new RenderTargetBitmap(700, 560, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); frames.Add(bitmap);
			}
			var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
			if (!String.IsNullOrWhiteSpace(output))
			{
				Directory.CreateDirectory(output);
				var visual = new DrawingVisual();
				using (var drawing = visual.RenderOpen())
					for (var i = 0; i < frames.Count; i++) drawing.DrawImage(frames[i], new Rect((i / 2) * 700, (i % 2) * 560, 700, 560));
				var sheet = new RenderTargetBitmap(2100, 1120, 96, 96, PixelFormats.Pbgra32); sheet.Render(visual);
				var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(sheet));
				using var file = File.Create(Path.Combine(output, "backup-preferences-themes.png")); encoder.Save(file);
			}
		}
		finally { Application.Current.Resources = originalResources; }
	}
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
