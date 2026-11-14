using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DivinityModManager.Localization;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class ModContentWindowTests
{
	public void ModContentInspectorCatalogHasCompleteTranslations()
	{
		using var stream = typeof(ReduxLanguageService).Assembly.GetManifestResourceStream("Redux.InterfaceStrings.json")!;
		var catalog = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
		var byEnglish = catalog.Values.ToDictionary(values => values["en"], StringComparer.Ordinal);
		var required = new[]
		{
			"What this mod changes...",
			"Inspect the selected mod's Stats records and package contents without changing any files.",
			"The installed PAK could not be found. Refresh the mod list and try again.",
			"What this mod changes",
			"Browse this package's Stats records and file inventory. This view does not compare against the base game or other mods.",
			"Search records or package paths",
			"Search by record name, type, parent, or package path. Ctrl+F focuses search.",
			"Clear search", "Stats records", "Selected record fields", "Package file inventory", "Inspection notes",
			"Resize records and fields", "Record", "Type", "Parent record", "Package path", "Field", "Value", "Origin",
			"Source record", "Category", "Bytes", "Note", "Line", "Cancel", "Close", "Package files",
			"Inspection notes ({0})", "{0} of {1} records · {2} of {3} files", "{0}: {1}", "{0} · line {1}",
			"Select a record to inspect its fields.", "No record selected", "No fields were read for this record.",
			"Inherited", "Declared", "Inheritance is incomplete. Only values resolved within this package are shown.",
			"Declared fields in this record.", "Inherited fields come from parent records inside this package.",
			"Inspection cancelled", "Package could not be inspected", "Reading package contents...",
			"No supported Stats records were read. Package files and inspection notes may have more information.",
			"No matching records. Try a different name, type, parent, or path.", "File inventory is not available.",
			"No matching package files.", "Inspection notes are not available.",
			"No inspection notes. This does not establish compatibility with other mods.",
			"Cancelling...", "Reading Stats files: {0} of {1}", "Inspection failed. See the Redux log for details.",
			"Inspection cancelled. The package was not changed.",
			"Partial inspection. Open Inspection notes for skipped or unresolved content.",
			"Inspection complete. Only supported Stats content was interpreted; other files are listed by path.",
			"Stats", "Scripts", "Resources", "Textures", "Localization", "Other"
		};
		foreach (var english in required.Concat(catalog.Where(pair => pair.Key.StartsWith("ModContent_", StringComparison.Ordinal))
			.Select(pair => pair.Value["en"])).Distinct(StringComparer.Ordinal))
		{
			if (!byEnglish.TryGetValue(english, out var translations))
				throw new InvalidOperationException($"Missing mod content catalog entry: {english}");
			foreach (var language in ReduxLanguageService.Languages)
			{
				if (!translations.TryGetValue(language.Code, out var translated) || String.IsNullOrWhiteSpace(translated))
					throw new InvalidOperationException($"Missing mod content {language.Code} translation: {english}");
				// Inspect the catalog itself; service fallback must never make this check pass.
				var placeholders = System.Text.RegularExpressions.Regex.Matches(english, @"\{\d+(?:[^}]*)\}")
					.Select(match => match.Value).OrderBy(value => value).ToArray();
				var translatedPlaceholders = System.Text.RegularExpressions.Regex.Matches(translated, @"\{\d+(?:[^}]*)\}")
					.Select(match => match.Value).OrderBy(value => value).ToArray();
				RegressionAssert.SequenceEqual(placeholders, translatedPlaceholders);
			}
		}
	}

	public void ModContentInspectorCanCancelBeforeLoadWithoutReadingPackage()
	{
		WithResources(_ =>
		{
			ReduxLanguageService.Current.SetLanguage("en");
			var window = new ReduxModContentWindow("must-not-be-read.pak", "Sample");
			try
			{
				RegressionAssert.Equal("Cancel", ((TextBlock)window.FindName("ScanActionText")).Text);
				((Button)window.FindName("ScanActionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				typeof(ReduxModContentWindow).GetMethod("Window_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(window, [window, new RoutedEventArgs()]);
				RegressionAssert.Equal("Close", ((TextBlock)window.FindName("ScanActionText")).Text);
				RegressionAssert.Contains(((TextBlock)window.FindName("StatusText")).Text, "Inspection cancelled");
				RegressionAssert.False((bool)typeof(ReduxModContentWindow).GetField("_scanning", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!);
			}
			finally { window.Close(); }
			// A delayed Loaded notification after Close must not access the disposed token source.
			typeof(ReduxModContentWindow).GetMethod("Window_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(window, [window, new RoutedEventArgs()]);
			var closedBeforeLoad = new ReduxModContentWindow("must-not-be-read.pak", "Sample");
			closedBeforeLoad.Close();
			typeof(ReduxModContentWindow).GetMethod("Window_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(closedBeforeLoad, [closedBeforeLoad, new RoutedEventArgs()]);
			RegressionAssert.False((bool)typeof(ReduxModContentWindow).GetField("_started", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(closedBeforeLoad)!);
		});
	}

	public void ModContentInspectorKeepsActionsReachableAcrossThemesAndLanguages()
	{
		WithResources(resources =>
		{
			var custom = ReduxThemeService.CreateFromBase("Inspector test", ReduxThemeType.ReduxDark);
			custom.AccentColor = "#3CAFB5";
			var themes = new (ReduxThemeType Theme, ReduxCustomTheme? Custom)[]
			{
				(ReduxThemeType.ReduxDark, null), (ReduxThemeType.ReduxLight, null),
				(ReduxThemeType.Parchment, null), (ReduxThemeType.ReduxDark, custom)
			};
			foreach (var (theme, customTheme) in themes)
			foreach (var size in new[] { ReduxTextSize.Default, ReduxTextSize.Large })
			{
				ReduxTypographyService.ApplyTextSize(resources, size);
				var window = new ReduxModContentWindow("not-read.pak", "Name {0} Русский 中文");
				try
				{
					if (customTheme == null) ReduxThemeService.Apply(window.Resources, theme);
					else ReduxThemeService.Apply(window.Resources, theme, customTheme);
					Present(window, Sample());
					var root = Root(window);
					var width = (int)window.MinWidth;
					var height = (int)window.MinHeight;
					var tabs = (TabControl)window.FindName("ContentTabs");
					foreach (var language in ReduxLanguageService.Languages)
					{
						ReduxLanguageService.Current.SetLanguage(language.Code);
						foreach (var tab in new[] { "RecordsTab", "FilesTab", "IssuesTab" })
						{
							tabs.SelectedItem = window.FindName(tab);
							Layout(root, width, height);
							AssertInside(window, root, "ScanActionButton", width, height);
							AssertInside(window, root, "SearchTextBox", width, height);
							AssertInside(window, root, "ClearSearchButton", width, height);
							RegressionAssert.True(tabs.ActualHeight > 170);
							if (tab == "RecordsTab")
							{
								var records = (DataGrid)window.FindName("RecordsGrid");
								var fields = (DataGrid)window.FindName("FieldsGrid");
								RegressionAssert.True(records.ActualHeight > 55);
								RegressionAssert.True(fields.ActualHeight > 45);
								RegressionAssert.True(records.IsReadOnly && fields.IsReadOnly);
								RegressionAssert.True(records.EnableRowVirtualization && records.EnableColumnVirtualization);
							}
							RegressionAssert.Equal("Name {0} Русский 中文", ((TextBlock)window.FindName("ModNameText")).Text);
							RegressionAssert.Equal(ReduxLanguageService.Current.Translate("What this mod changes"), window.Title);
							RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Package path"),
								((DataGridColumn)window.FindName("RecordSourceColumn")).Header);
							if (size == ReduxTextSize.Large)
								RenderIfRequested(root, $"mod-content-{theme}-{(customTheme == null ? "built-in" : "custom")}-{language.Code}-{tab}", width, height);
						}
					}
				}
				finally { window.Close(); }
			}
		});
	}

	public void ModContentInspectorSearchAndSelectionPreservePackageValues()
	{
		WithResources(_ =>
		{
			var window = new ReduxModContentWindow("not-read.pak", "Sample");
			try
			{
				var result = Sample();
				Present(window, result);
				var root = Root(window);
				Layout(root, 1000, 740);
				var records = (DataGrid)window.FindName("RecordsGrid");
				var fields = (DataGrid)window.FindName("FieldsGrid");
				var search = (TextBox)window.FindName("SearchTextBox");
				RegressionAssert.Equal(2, fields.Items.Count);
				RegressionAssert.Equal("Inherited", Property(fields.Items[1], "Origin"));
				foreach (var language in ReduxLanguageService.Languages)
				{
					ReduxLanguageService.Current.SetLanguage(language.Code);
					Layout(root, 1000, 740);
					RegressionAssert.Equal("DealDamage(2d6,Fire);Name {0} Русский 中文", Property(fields.Items[0], "Value"));
					RegressionAssert.Equal("Parent_Base", Property(fields.Items[1], "SourceRecord"));
				}
				search.Text = "Second";
				Filter(window);
				RegressionAssert.Equal(1, records.Items.Count);
				RegressionAssert.Equal("Second_Record", ((ModContentRecord)records.SelectedItem).Name);
				RegressionAssert.Equal(1, fields.Items.Count);
				RegressionAssert.Equal("BonusActionPoint:1", Property(fields.Items[0], "Value"));
				search.Text = "does-not-exist";
				Filter(window);
				RegressionAssert.Equal(0, records.Items.Count);
				RegressionAssert.Equal(0, fields.Items.Count);
				RegressionAssert.Equal(Visibility.Visible, ((TextBlock)window.FindName("EmptyRecordsText")).Visibility);
				search.Text = "Spells.txt";
				Filter(window);
				RegressionAssert.Equal(1, records.Items.Count);
				RegressionAssert.Equal(1, ((DataGrid)window.FindName("FilesGrid")).Items.Count);
				((Button)window.FindName("ClearSearchButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				Filter(window);
				RegressionAssert.Equal(2, records.Items.Count);
				RegressionAssert.Equal(3, ((DataGrid)window.FindName("FilesGrid")).Items.Count);
				RegressionAssert.False(((Button)window.FindName("ClearSearchButton")).IsEnabled);
				RegressionAssert.Equal(2, result.Records.Count);
				RegressionAssert.Equal("Parent_Base", result.Records[0].ParentName);
			}
			finally { window.Close(); }
		});
	}

	public void ModContentInspectorShowsPartialAndEmptyResultsWithoutCompatibilityClaims()
	{
		WithResources(_ =>
		{
			ReduxLanguageService.Current.SetLanguage("en");
			var window = new ReduxModContentWindow("not-read.pak", "Sample");
			try
			{
				Present(window, new("sample.pak", [],
					[new("Stats syntax could not be parsed; this file's records were not included.", "Public/Test/Stats/Generated/Data/Broken.txt", 3)],
					[new("Public/Test/Stats/Generated/Data/Broken.txt", 1024, "Stats", false, 0)], true));
				var root = Root(window);
				Layout(root, 720, 680);
				RegressionAssert.Contains(((TextBlock)window.FindName("StatusText")).Text, "Partial inspection");
				RegressionAssert.Contains(((TextBlock)window.FindName("EmptyRecordsText")).Text, "No supported Stats records");
				RegressionAssert.Contains(((TextBlock)window.FindName("ScopeText")).Text, "does not compare");
				RegressionAssert.Equal(1, ((DataGrid)window.FindName("IssuesGrid")).Items.Count);
				RegressionAssert.True(((TextBox)window.FindName("SearchTextBox")).IsEnabled);
				Present(window, new("textures.pak", [], [], [new("Textures/example.dds", 4096, "Textures", false, 0)], false));
				RegressionAssert.Contains(((TextBlock)window.FindName("StatusText")).Text, "other files are listed by path");
				RegressionAssert.Equal(1, ((DataGrid)window.FindName("FilesGrid")).Items.Count);
				RegressionAssert.Equal(0, ((DataGrid)window.FindName("FieldsGrid")).Items.Count);
				RegressionAssert.Contains(((TextBlock)window.FindName("EmptyIssuesText")).Text, "does not establish compatibility");
			}
			finally { window.Close(); }
		});
	}

	public void ModContentInspectorVirtualizesLargeInventories()
	{
		WithResources(_ =>
		{
			var window = new ReduxModContentWindow("not-read.pak", "Large package");
			try
			{
				Present(window, new("large.pak", [], [], Enumerable.Range(0, 5000)
					.Select(index => new ModContentFile($"Textures/{index:D5}.dds", 4096, "Textures", false, 0)).ToArray(), false));
				((TabControl)window.FindName("ContentTabs")).SelectedItem = window.FindName("FilesTab");
				var root = Root(window);
				Layout(root, 1000, 740);
				var grid = (DataGrid)window.FindName("FilesGrid");
				RegressionAssert.Equal(5000, grid.Items.Count);
				var realized = Descendants<DataGridRow>(grid).Count();
				RegressionAssert.True(realized > 0 && realized < 100);
			}
			finally { window.Close(); }
		});
	}

	private static ModContentInspectionResult Sample() => new("sample.pak",
	[
		new("SpellData", "First_Record_Русский_中文", "Parent_Base", "Public/Test/Stats/Generated/Data/Spells.txt", 3,
			[new("SpellProperties", "DealDamage(2d6,Fire);Name {0} Русский 中文", "First_Record_Русский_中文", false),
			 new("Level", "2", "Parent_Base", true)], ModContentInheritanceStatus.ResolvedWithinPackage),
		new("SpellData", "Second_Record", "External_Base", "Public/Test/Stats/Generated/Data/Other.txt", 8,
			[new("UseCosts", "BonusActionPoint:1", "Second_Record", false)], ModContentInheritanceStatus.ExternalParent)
	], [new("Parent record is outside this package.", "Public/Test/Stats/Generated/Data/Other.txt", 8)],
	[
		new("Public/Test/Stats/Generated/Data/Spells.txt", 2048, "Stats", true, 1),
		new("Public/Test/Stats/Generated/Data/Other.txt", 512, "Stats", true, 1),
		new("Textures/Name {0} Русский 中文.dds", 8192, "Textures", false, 0)
	], true);

	private static void Present(ReduxModContentWindow window, ModContentInspectionResult result) =>
		typeof(ReduxModContentWindow).GetMethod("ApplyResult", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [result]);

	private static void Filter(ReduxModContentWindow window)
	{
		var task = (Task)typeof(ReduxModContentWindow).GetMethod("RefreshFilterAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
		if (!task.IsCompleted)
		{
			var frame = new DispatcherFrame();
			_ = task.ContinueWith(_ => window.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
			Dispatcher.PushFrame(frame);
		}
		task.GetAwaiter().GetResult();
	}

	private static string Property(object row, string name) => (string)row.GetType().GetProperty(name)!.GetValue(row)!;
	private static FrameworkElement Root(Window window)
	{
		var root = (FrameworkElement)window.Content;
		root.Opacity = 1;
		root.RenderTransform = Transform.Identity;
		return root;
	}
	private static void Layout(FrameworkElement root, int width, int height)
	{
		root.Measure(new Size(width, height));
		root.Arrange(new Rect(0, 0, width, height));
		root.UpdateLayout();
		root.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
	}
	private static void AssertInside(Window window, Visual root, string name, int width, int height)
	{
		var control = (FrameworkElement)window.FindName(name);
		var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
		if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -1 || bounds.Right > width + 1 || bounds.Top < -1 || bounds.Bottom > height + 1)
			throw new InvalidOperationException($"Mod content {name}: {ReduxLanguageService.Current.Language}, {bounds}");
	}
	private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T value) yield return value;
			foreach (var nested in Descendants<T>(child)) yield return nested;
		}
	}
	private static void WithResources(Action<ResourceDictionary> action)
	{
		var application = Application.Current;
		application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var savedResources = application.Resources;
		var savedLanguage = ReduxLanguageService.Current.Language;
		var savedMotion = ReduxWindowBehavior.ReduceMotion;
		var savedEffects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		try
		{
			var resources = new ResourceDictionary();
			foreach (var name in new[] { "Typography", "Light", "Dark", "MainResourceDictionary" })
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Redux;component/Themes/{name}.xaml") });
			resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml") });
			application.Resources = resources;
			ReduxWindowBehavior.ConfigureAccessibility(true, savedEffects);
			action(resources);
		}
		finally
		{
			ReduxLanguageService.Current.SetLanguage(savedLanguage);
			application.Resources = savedResources;
			ReduxWindowBehavior.ConfigureAccessibility(savedMotion, savedEffects);
		}
	}
	private static void RenderIfRequested(FrameworkElement root, string name, int width, int height)
	{
		var directory = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
		if (String.IsNullOrEmpty(directory)) return;
		Directory.CreateDirectory(directory);
		var bitmap = new RenderTargetBitmap(width * 3 / 2, height * 3 / 2, 144, 144, PixelFormats.Pbgra32);
		var background = new DrawingVisual();
		using (var drawing = background.RenderOpen())
			drawing.DrawRectangle(root.TryFindResource("ReduxWindowBrush") as Brush ?? Brushes.Transparent, null, new Rect(0, 0, width, height));
		bitmap.Render(background);
		bitmap.Render(root);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var file = File.Create(Path.Combine(directory, name + ".png"));
		encoder.Save(file);
	}
}
