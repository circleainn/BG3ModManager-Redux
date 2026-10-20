using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivinityModManager.Models;
using DivinityModManager.Util;
using ReduxUpdater;

namespace Redux.Core.Tests;

public sealed class SnapshotProgressPresentationTests
{
	public void SnapshotRunnerUsesCapturedThemeAndKeepsActionsReachable()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		foreach (var large in new[] { false, true })
		foreach (var operation in new[] { "create", "restore", "recover" })
		{
			var resources = new ResourceDictionary();
			foreach (var name in new[] { "Typography", "Light", "Dark", "MainResourceDictionary" })
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Redux;component/Themes/{name}.xaml") });
			ReduxThemeService.Apply(resources, theme);
			resources["Redux.FontSize.12"] = large ? 18d : 12d;
			resources["Redux.FontFamily.UI"] = new FontFamily("Manrope");
			var source = new Border { Resources = resources };
			var capture = typeof(ReduxThemeService).Assembly.GetType("DivinityModManager.Util.ReduxSnapshotPresentation")!.GetMethod("Capture")!;
			var appearance = (DivinityModManager.AppServices.ReduxSnapshotAppearance)capture.Invoke(null, [source])!;
			var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
			appearance.Write(path);
			ReduxSnapshotAppearance transferred;
			try { transferred = ReduxSnapshotAppearance.Read(path); }
			finally { File.Delete(path); }
			RegressionAssert.Equal(((SolidColorBrush)source.FindResource("ReduxAccentBrush")).Color.ToString(), transferred.Colors["ReduxAccentBrush"]);
			var window = new SnapshotProgressWindow(transferred);
			try
			{
				window.SetOperation(operation);
				window.Report(new("Copying Mods/An unusually long mod package name for a texture collection with optional compatibility patches.pak", 104857600, 524288000, operation == "create"));
				var root = (Border)window.Content;
				root.Measure(new Size(580, Double.PositiveInfinity));
				root.Arrange(new Rect(0, 0, 580, root.DesiredSize.Height)); root.UpdateLayout();
				var button = (Button)window.FindName("ActionButton");
				var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
				RegressionAssert.True(bounds.Height >= 32 && bounds.Left >= 0 && bounds.Right <= 580 && bounds.Bottom <= root.ActualHeight);
				RegressionAssert.True(root.ActualHeight < 600);
				RegressionAssert.Equal(operation == "create", button.IsEnabled);
				RegressionAssert.Equal(20d, ((ProgressBar)window.FindName("Progress")).Value);
				RegressionAssert.Equal(transferred.Colors["ReduxTextPrimaryBrush"], ((SolidColorBrush)window.Foreground).Color.ToString());
				var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
				if (!String.IsNullOrWhiteSpace(output))
				{
					Directory.CreateDirectory(output);
					var dpi = large ? 144 : 96;
					var bitmap = new RenderTargetBitmap(580 * dpi / 96, (int)Math.Ceiling(root.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
					bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
					using var file = File.Create(Path.Combine(output, $"snapshot-{operation}-{theme}-{(large ? "large" : "normal")}.png")); encoder.Save(file);
				}
			}
			finally { window.Finish(); }
		}
	}

	public void SnapshotRunnerPreservesCancellationAndRecoveryLocks()
	{
		var appearance = new ReduxSnapshotAppearance { ShowIcons = false, FontFamily = "https://invalid/font", TextScale = 100 };
		appearance.Colors["ReduxAccentBrush"] = "not a color";
		var normalized = appearance.Normalize();
		RegressionAssert.Equal("Manrope", normalized.FontFamily);
		RegressionAssert.Equal(1.5d, normalized.TextScale);
		RegressionAssert.Equal(ReduxSnapshotAppearance.DefaultColors["ReduxAccentBrush"], normalized.Colors["ReduxAccentBrush"]);
		var window = new SnapshotProgressWindow(appearance);
		try
		{
			RegressionAssert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("ActionIcon")).Visibility);
			var cancellations = 0;
			window.CancelRequested += () => cancellations++;
			var action = (Button)window.FindName("ActionButton");
			window.Report(new("Restoring files", CanCancel: false));
			action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Equal(0, cancellations);
			RegressionAssert.False(((Button)window.FindName("CloseWindowButton")).IsEnabled);
			window.Report(new("Verifying archive"));
			action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Equal(1, cancellations);
			window.Report(new("Late queued progress"));
			RegressionAssert.False(action.IsEnabled);
			RegressionAssert.Equal("Canceling safely…", ((TextBlock)window.FindName("Details")).Text);
			var acknowledgement = window.ShowResultAsync("Recovery needs attention", "Keep the return snapshot and retry after resolving the file lock.");
			window.Report(new("Late restore progress", CanCancel: false));
			RegressionAssert.True(action.IsEnabled);
			RegressionAssert.Equal("Keep the return snapshot and retry after resolving the file lock.", ((TextBlock)window.FindName("Details")).Text);
			action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.True(acknowledgement.IsCompleted);
			RegressionAssert.Equal(1, cancellations);
		}
		finally { window.Finish(); }
	}
}
