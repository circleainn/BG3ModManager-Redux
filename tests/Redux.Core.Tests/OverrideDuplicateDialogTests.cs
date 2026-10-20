using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class OverrideDuplicateDialogTests
{
	public void CancelWithAnOccupiedRollbackTargetKeepsTheRecoveryErrorAvailable()
	{
		var fixture = Path.Combine(Path.GetTempPath(), "redux-duplicate-dialog-cancel-" + Guid.NewGuid().ToString("N"));
		var mods = Directory.CreateDirectory(Path.Combine(fixture, "Mods")).FullName;
		var held = Directory.CreateDirectory(Path.Combine(fixture, "Held")).FullName;
		var backups = Path.Combine(fixture, "Backups");
		var previousShutdownMode = Application.Current.ShutdownMode;
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		ReduxOverrideDuplicateWindow? window = null;
		try
		{
			File.WriteAllText(Path.Combine(mods, "Example.pak"), "keep");
			File.WriteAllText(Path.Combine(held, "Example.pak"), "save");
			var closeRequested = false;
			var closed = false;
			var service = new OverrideDuplicateRecoveryService(mods, held, backups)
			{
				AfterStagedFile = _ =>
				{
					// Simulate an external process occupying the original location just
					// before the user cancels. Recovery must retain every version.
					File.WriteAllText(Path.Combine(held, "Example.pak"), "external");
					window!.Dispatcher.Invoke(() => { closeRequested = true; window.Close(); });
				}
			};
			window = new ReduxOverrideDuplicateWindow(null!, service);
			AddApplicationResourceFixtures(window);
			window.Closed += (_, _) => closed = true;
			var review = window.Dispatcher.Invoke(() => (Task)typeof(ReduxOverrideDuplicateWindow)
				.GetMethod("ReviewAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!);
			PumpUntilComplete(review);
			var row = ((ItemsControl)window.FindName("ConflictItems")).Items.Cast<ReduxOverrideDuplicateWindow.ConflictRow>().Single();
			row.Choice = OverrideDuplicateChoice.KeepMods;
			window.Width = 650;
			window.Height = 480;
			var root = (Grid)window.Content;
			root.Measure(new Size(650, 480));
			root.Arrange(new Rect(0, 0, 650, 480));
			root.UpdateLayout();
			window.Dispatcher.Invoke(() => ((Button)window.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
			var idle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
			timer.Tick += (_, _) =>
			{
				if (!(bool)typeof(ReduxOverrideDuplicateWindow).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!)
					idle.TrySetResult(true);
			};
			timer.Start();
			try { PumpUntilComplete(idle.Task); }
			finally { timer.Stop(); }
			RegressionAssert.True(closeRequested);
			RegressionAssert.False(closed);
			RegressionAssert.False(window.Resolved);
			RegressionAssert.False(((Button)window.FindName("ReviewButton")).IsEnabled);
			var status = (TextBlock)window.FindName("StatusText");
			RegressionAssert.Contains(status.Text, "Recovery stopped without overwriting files");
			window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
			root.UpdateLayout();
			var scroll = (ScrollViewer)window.FindName("ReviewScrollViewer");
			var statusBounds = status.TransformToAncestor(scroll).TransformBounds(new Rect(status.RenderSize));
			if (!new Rect(scroll.RenderSize).Contains(statusBounds))
				throw new InvalidOperationException($"Recovery error is not visible after cancellation: status={statusBounds}; viewport={scroll.RenderSize}.");
			RegressionAssert.Equal("keep", File.ReadAllText(Path.Combine(mods, "Example.pak")));
			RegressionAssert.Equal("external", File.ReadAllText(Path.Combine(held, "Example.pak")));
			var transaction = Directory.GetDirectories(backups).Single();
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(transaction, "Held", "Example.pak")));
			RegressionAssert.True(File.Exists(Path.Combine(transaction, "pending.json")));
			RegressionAssert.Equal("save", File.ReadAllText(Path.Combine(Directory.GetDirectories(held).Single(), "Example.pak")));
		}
		finally
		{
			window?.Close();
			Application.Current.ShutdownMode = previousShutdownMode;
			Directory.Delete(fixture, recursive: true);
		}
	}

	public void RecoveryReviewRequiresExplicitChoicesAndKeepsActionsReachableInEveryTheme()
	{
		var fixture = Path.Combine(Path.GetTempPath(), "redux-duplicate-dialog-" + Guid.NewGuid().ToString("N"));
		var mods = Directory.CreateDirectory(Path.Combine(fixture, "Player Mods")).FullName;
		var held = Directory.CreateDirectory(Path.Combine(fixture, "Override Order Holding")).FullName;
		var backups = Path.Combine(fixture, "Override Duplicate Backups");
		var frames = new List<BitmapSource>();
		var names = new[] { "Identical_Override_Example.pak", "Different_Override_Example.pak", "Another_Override_With_A_Longer_Name.pak" };
		var previousShutdownMode = Application.Current.ShutdownMode;
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		try
		{
			foreach (var name in names)
			{
				File.WriteAllText(Path.Combine(mods, name), "Installed example bytes");
				File.WriteAllText(Path.Combine(held, name), name == names[0] ? "Installed example bytes" : "Different example bytes");
			}
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			{
				var window = new ReduxOverrideDuplicateWindow(null!, new OverrideDuplicateRecoveryService(mods, held, backups));
				AddApplicationResourceFixtures(window);
				try
				{
					// Run the production asynchronous review against disposable files. No
					// window is shown and no apply, clipboard, or folder action is invoked.
					var review = window.Dispatcher.Invoke(() => (Task)typeof(ReduxOverrideDuplicateWindow)
						.GetMethod("ReviewAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!);
					PumpUntilComplete(review);
					var items = (ItemsControl)window.FindName("ConflictItems");
					RegressionAssert.Equal(3, items.Items.Count);
					var rows = items.Items.Cast<ReduxOverrideDuplicateWindow.ConflictRow>().ToArray();
					RegressionAssert.True(rows.Single(row => row.Name == names[0]).Choice == OverrideDuplicateChoice.KeepMods);
					RegressionAssert.True(rows.Where(row => row.Name != names[0]).All(row => row.Choice == null));
					var apply = (Button)window.FindName("ApplyButton");
					RegressionAssert.False(apply.IsEnabled);
					foreach (var (width, height, fontSize) in new[] { (880, 700, 12d), (650, 480, 18d) })
					{
						ReduxThemeService.Apply(window.Resources, theme);
						// Scale every typography token locally without ApplyTextSize's
						// side effect on other windows. The 18-DIP case is a stress case
						// beyond the production Large setting's 1.15 scale.
						foreach (var baseline in new[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 32, 34 })
							window.Resources[$"Redux.FontSize.{baseline}"] = Math.Round(baseline * (fontSize / 12) * 2, MidpointRounding.AwayFromZero) / 2;
						window.FontFamily = ReduxTypographyService.ResolveFontFamily(ReduxTypographyService.GetThemeDefault(theme));
						var root = (Grid)window.Content;
						root.Opacity = 1;
						root.RenderTransform = Transform.Identity;
						root.Background = window.Background;
						root.Measure(new Size(width, height));
						root.Arrange(new Rect(0, 0, width, height));
						root.UpdateLayout();
						window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
						var viewport = new Rect(0, 0, width, height);
						foreach (var button in Descendants<Button>(root).Where(button => button.Content is string))
						{
							var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
							if (!(bounds.Width > 0 && bounds.Height > 0 && viewport.Contains(bounds)))
								throw new InvalidOperationException($"Override dialog action '{button.Content}' ({button.Name}) is outside its viewport. "
									+ $"Theme={theme}; viewport={width}x{height}; bodyFont={fontSize}; bounds={bounds}; visibility={button.Visibility}; "
									+ $"rootRender={root.RenderSize}; rootDesired={root.DesiredSize}; windowSize={window.Width}x{window.Height}; "
									+ $"windowActual={window.ActualWidth}x{window.ActualHeight}; buttonDesired={button.DesiredSize}; buttonFont={button.FontSize}.");
						}
						var cancel = Descendants<Button>(root).Single(button => Equals(button.Content, "Cancel"));
						RegressionAssert.True(cancel.IsCancel);
						var scroll = (ScrollViewer)window.FindName("ReviewScrollViewer");
						RegressionAssert.True(scroll.ViewportHeight > 0 && scroll.ScrollableHeight > 0);
						var combos = Descendants<ComboBox>(items).ToArray();
						RegressionAssert.Equal(3, combos.Length);
						RegressionAssert.True(combos.All(combo => AutomationProperties.GetName(combo).Contains(((ReduxOverrideDuplicateWindow.ConflictRow)combo.DataContext).Name, StringComparison.Ordinal)));
						RegressionAssert.True(AutomationProperties.GetLiveSetting((TextBlock)window.FindName("StatusText")) == AutomationLiveSetting.Polite);
						var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
						bitmap.Render(root);
						frames.Add(bitmap);
						WritePreview(bitmap, $"override-duplicate-{theme}-{width}x{height}.png");
					}
					foreach (var combo in Descendants<ComboBox>(items)) combo.SelectedValue = OverrideDuplicateChoice.KeepHeld;
					window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
					RegressionAssert.True(rows.All(row => row.Choice == OverrideDuplicateChoice.KeepHeld));
					RegressionAssert.True(apply.IsEnabled);
					RegressionAssert.False(window.Resolved);
				}
				finally { window.Close(); }
			}
			// Reviewing and choosing never changes packages or creates backups.
			RegressionAssert.False(Directory.Exists(backups));
			foreach (var name in names)
			{
				RegressionAssert.Equal("Installed example bytes", File.ReadAllText(Path.Combine(mods, name)));
				RegressionAssert.Equal(name == names[0] ? "Installed example bytes" : "Different example bytes", File.ReadAllText(Path.Combine(held, name)));
			}
			WriteContactSheet(frames);
		}
		finally
		{
			Application.Current.ShutdownMode = previousShutdownMode;
			Directory.Delete(fixture, recursive: true);
		}
	}

	private static void AddApplicationResourceFixtures(Window window)
	{
		// Match App.xaml's inherited resources within this one disposable window.
		// Keep the production window dictionary later in the merge order.
		window.Resources.MergedDictionaries.Insert(0, new ResourceDictionary
		{ Source = new Uri("pack://application:,,,/Redux;component/Themes/Typography.xaml") });
		window.Resources.MergedDictionaries.Insert(1, new ResourceDictionary
		{ Source = new Uri("pack://application:,,,/Redux;component/Themes/Light.xaml") });
		window.Resources.MergedDictionaries.Insert(2, new ResourceDictionary
		{ Source = new Uri("pack://application:,,,/Redux;component/Themes/Dark.xaml") });
		// Adonis's SpaceExtension parses Style setters without a FrameworkElement
		// target. Its supported fallback supplies this isolated resource owner
		// instead of looking in the intentionally empty test Application.Resources.
		AdonisUI.SpaceExtension.SetSpaceResourceOwnerFallback(window);
		try
		{
			window.Resources.MergedDictionaries.Insert(3, new ResourceDictionary
			{ Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml") });
		}
		finally { AdonisUI.SpaceExtension.SetSpaceResourceOwnerFallback(null); }
	}

	private static void PumpUntilComplete(Task task)
	{
		if (!task.IsCompleted)
		{
			var frame = new DispatcherFrame();
			var dispatcher = Dispatcher.CurrentDispatcher;
			var timer = new DispatcherTimer(TimeSpan.FromSeconds(20), DispatcherPriority.Send,
				(_, _) => frame.Continue = false, dispatcher);
			_ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
			try { Dispatcher.PushFrame(frame); }
			finally { timer.Stop(); }
		}
		RegressionAssert.True(task.IsCompleted);
		task.GetAwaiter().GetResult();
	}

	private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
		{
			var child = VisualTreeHelper.GetChild(element, i);
			if (child is T result) yield return result;
			foreach (var nested in Descendants<T>(child)) yield return nested;
		}
	}

	private static void WritePreview(BitmapSource image, string name)
	{
		var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
		if (String.IsNullOrWhiteSpace(output)) return;
		Directory.CreateDirectory(output);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(image));
		using var file = File.Create(Path.Combine(output, name));
		encoder.Save(file);
	}

	private static void WriteContactSheet(IReadOnlyList<BitmapSource> frames)
	{
		var sheet = new DrawingVisual();
		using (var drawing = sheet.RenderOpen())
		{
			drawing.DrawRectangle(Brushes.DimGray, null, new Rect(0, 0, 1950, 1000));
			for (var theme = 0; theme < 3; theme++)
			{
				drawing.DrawImage(frames[theme * 2], new Rect(theme * 650, 0, 650, 517));
				drawing.DrawImage(frames[theme * 2 + 1], new Rect(theme * 650, 520, 650, 480));
			}
		}
		var image = new RenderTargetBitmap(1950, 1000, 96, 96, PixelFormats.Pbgra32);
		image.Render(sheet);
		WritePreview(image, "override-duplicate-theme-contact-sheet.png");
	}
}
