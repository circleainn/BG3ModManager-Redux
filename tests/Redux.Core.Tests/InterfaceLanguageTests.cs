using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DivinityModManager.Controls;
using DivinityModManager.Localization;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;
using Newtonsoft.Json;

namespace Redux.Core.Tests;

internal sealed class InterfaceLanguageTests
{
	public void LanguagesPersistWithoutChangingCultureOrUserData()
	{
		var service = new ReduxLanguageService();
		RegressionAssert.SequenceEqual(new[] { "en", "fr", "es", "ru", "zh-Hans" }, ReduxLanguageService.Languages.Select(l => l.Code));
		RegressionAssert.Equal("en", JsonConvert.DeserializeObject<DivinityModManagerSettings>("{}")!.InterfaceLanguage);
		RegressionAssert.Equal("fr", ReduxLanguageService.NormalizeLanguage("fr-CA"));
		RegressionAssert.Equal("zh-Hans", ReduxLanguageService.NormalizeLanguage("zh_CN"));
		RegressionAssert.Equal("en", ReduxLanguageService.NormalizeLanguage("invalid"));
		var culture = CultureInfo.CurrentCulture;
		var uiCulture = CultureInfo.CurrentUICulture;
		foreach (var language in ReduxLanguageService.Languages)
		{
			var settings = new DivinityModManagerSettings { InterfaceLanguage = language.Code, LastOrder = "My order Русский 中文" };
			var restored = new DivinityModManagerSettings();
			restored.RestorePersistedSettings(JsonConvert.DeserializeObject<DivinityModManagerSettings>(JsonConvert.SerializeObject(settings))!);
			RegressionAssert.Equal(language.Code, restored.InterfaceLanguage);
			RegressionAssert.Equal(settings.LastOrder, restored.LastOrder);
			service.SetLanguage(restored.InterfaceLanguage);
			RegressionAssert.Equal("Unknown user text", service.Translate("Unknown user text"));
			foreach (var key in service.Keys) RegressionAssert.True(!String.IsNullOrWhiteSpace(service[key]));
		}
		RegressionAssert.Equal(culture, CultureInfo.CurrentCulture);
		RegressionAssert.Equal(uiCulture, CultureInfo.CurrentUICulture);
		var save = service.Keys.Single(k => service.GetTranslation(k, "en") == "Save");
		RegressionAssert.Equal("Save", service.GetTranslation(save, "unsupported"));
	}

	public void TranslatedColumnsAndMenusKeepTheirOriginalIdentities()
	{
		WithResources(resources =>
		{
			resources[typeof(Image)] = new Style(typeof(Image));
			var layout = new HorizontalModLayout();
			var grid = (GridView)layout.ForceLoadedModsView.View;
			var column = grid.Columns.Single(c => Equals(c.Header, "Name"));
			var presenter = new ContentPresenter { Content = column.Header, ContentTemplate = column.HeaderTemplate };
			var first = new DivinityModData { UUID = "language-z", Name = "Zebra", CustomAlias = "Zebra", HasCustomAlias = true };
			var second = new DivinityModData { UUID = "language-a", Name = "Alpha", CustomAlias = "Alpha", HasCustomAlias = true };
			layout.ForceLoadedModsView.ItemsSource = new[] { first, second };
			var menu = new MenuItem();
			LocalizationBindings.Set(menu, MenuItem.HeaderProperty, "Save");
			foreach (var language in ReduxLanguageService.Languages)
			{
				ReduxLanguageService.Current.SetLanguage(language.Code);
				Layout(presenter, 300, 60);
				RegressionAssert.Equal("Name", column.Header);
				RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Name"), Descendants<TextBlock>(presenter).First().Text);
				layout.Sort((string)column.Header, System.ComponentModel.ListSortDirection.Ascending, layout.ForceLoadedModsView);
				RegressionAssert.SequenceEqual(new[] { second, first }, layout.ForceLoadedModsView.Items.Cast<DivinityModData>());
				RegressionAssert.Equal("Save", LocalizationBindings.SourceText(menu, MenuItem.HeaderProperty, ""));
				ReduxMenuItemExtension.ApplySemanticHoverToMenu(new ContextMenu { Items = { menu } });
				RegressionAssert.True(ReduxMenuItemExtension.GetUseSemanticHover(menu));
				((ItemsControl)menu.Parent).Items.Remove(menu);
			}
			RegressionAssert.Equal("Zebra", first.Name);
			var view = new MainViewControl(null!, null!);
			foreach (var id in new[] { "File", "Edit", "Settings", "Tools", "Help" })
			{
				var item = (MenuItem)view.FindName(id + "MenuItem");
				RegressionAssert.Equal(id, item.Tag);
				RegressionAssert.Equal(ReduxLanguageService.Current.Translate(id), item.Header);
			}
		});
	}

	public void LanguageSelectorAndTypographyRemainIndependent()
	{
		WithResources(resources =>
		{
			var settings = new DivinityModManagerSettings();
			var window = new SettingsWindow();
			try
			{
				var grid = (AutoGrid)window.FindName("SettingsAutoGrid");
				typeof(SettingsWindow).GetMethod("CreateSettingsElements", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(window, new object[] { settings, typeof(DivinityModManagerSettings), grid });
				var combo = grid.Children.OfType<ComboBox>().Single(c => c.Name == "InterfaceLanguageComboBox");
				var root = (Grid)window.Content;
				root.Opacity = 1; root.RenderTransform = Transform.Identity;
				var label = grid.Children.OfType<TextBlock>().Single(t => Grid.GetRow(t) == Grid.GetRow(combo));
				var choices = ReduxCustomFontService.GetChoices().ToList();
				var fontFile = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts"), "*.ttf", SearchOption.AllDirectories).First();
				if (!ReduxCustomFontService.TryImport(fontFile, out var imported, out var error)) throw new InvalidOperationException(error);
				if (!choices.Any(c => c.CustomReference == imported.CustomReference)) choices.Add(imported);
				foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
				foreach (var size in Enum.GetValues<ReduxTextSize>())
				foreach (var choice in choices)
				foreach (var reduceMotion in new[] { false, true })
				{
					ReduxThemeService.Apply(window.Resources, theme);
					ReduxTypographyService.ApplyTextSize(resources, size);
					ReduxTypographyService.Apply(resources, choice.BuiltInFont, choice.CustomReference);
					ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, false);
					var selectedFamily = window.FontFamily;
					foreach (var language in ReduxLanguageService.Languages)
					{
						combo.SelectedValue = language.Code;
						RegressionAssert.Equal(language.Code, settings.InterfaceLanguage);
						ReduxLanguageService.Current.SetLanguage(settings.InterfaceLanguage);
						Layout(root, 860, 720);
						RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Interface language"), label.Text);
						RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Preferences"), window.Title);
						RegressionAssert.True(combo.ActualWidth >= 160 && combo.ActualHeight >= 20);
						RegressionAssert.True(label.ActualWidth > 0 && label.ActualHeight > 0);
						RegressionAssert.Equal(selectedFamily, window.FontFamily);
						var caption = Descendants<TextBlock>(combo).Single(t => t.Text == language.NativeName);
						var bounds = caption.TransformToAncestor(combo).TransformBounds(new Rect(caption.RenderSize));
						RegressionAssert.True(bounds.Top >= -1 && bounds.Bottom <= combo.ActualHeight + 1 && bounds.Right <= combo.ActualWidth);
						RegressionAssert.Equal(language.NativeName, ((InterfaceLanguageChoice)combo.SelectedItem).NativeName);
						var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
						if (!String.IsNullOrWhiteSpace(output) && size == ReduxTextSize.Large && reduceMotion)
							Render(root, Path.Combine(output, $"language-{theme}-{choice.Name.Replace(' ', '-')}-{language.Code}.png"), 860, 720, 144);
					}
				}
			}
			finally { window.Close(); }
		});
	}

	public void LanguagesRenderWithBuiltInCustomAndMissingFonts()
	{
		WithResources(resources =>
		{
			var choices = ReduxCustomFontService.GetChoices().Where(c => !c.IsCustom).ToList();
			var fontFile = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts"), "*.ttf", SearchOption.AllDirectories).First();
			if (!ReduxCustomFontService.TryImport(fontFile, out var imported, out var error)) throw new InvalidOperationException(error);
			choices.Add(imported);
			choices.Add(new ReduxFontChoice("Missing custom font", ReduxTypographyFont.Manrope, "custom-font:" + new string('a', 64) + ".ttf"));
			var sample = new TextBlock { TextWrapping = TextWrapping.Wrap, Padding = new Thickness(12) };
			var host = new Window { Content = sample };
			try
			{
			sample.SetResourceReference(TextBlock.FontFamilyProperty, "Redux.FontFamily.UI");
			sample.SetResourceReference(TextBlock.FontSizeProperty, "Redux.FontSize.18");
			LocalizationBindings.Set(sample, TextBlock.TextProperty, "Changes interface text immediately. Save your preferences to keep this language. Untranslated text uses English.");
			foreach (var choice in choices)
			foreach (var customTheme in new[] { false, true })
			{
				var settings = new DivinityModManagerSettings { TypographyFont = choice.BuiltInFont, CustomTypographyFont = choice.CustomReference, UseThemeDefaultTypography = false };
				if (customTheme)
				{
					var theme = ReduxThemeService.CreateFromBase("Language test", ReduxThemeType.ReduxDark, choice.BuiltInFont, ReduxTextSize.Large, choice.CustomReference);
					settings.CustomThemes.Add(theme); settings.ActiveCustomThemeId = theme.Id;
				}
				ReduxTypographyService.Apply(resources, settings);
				var original = ReduxTypographyService.ResolveSelection(settings);
				var family = (FontFamily)resources["Redux.FontFamily.UI"];
				foreach (var language in ReduxLanguageService.Languages)
				{
					settings.InterfaceLanguage = language.Code;
					ReduxLanguageService.Current.SetLanguage(language.Code);
					Layout(sample, 520, 180);
					RegressionAssert.Equal(original, ReduxTypographyService.ResolveSelection(settings));
					RegressionAssert.Equal(family, sample.FontFamily);
					var text = sample.Text + " " + language.NativeName;
					var drawing = new DrawingVisual();
					using (var context = drawing.RenderOpen()) context.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo(language.Code), FlowDirection.LeftToRight,
						new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 18, Brushes.Black, 1.5), new Point());
					var glyphs = Glyphs(drawing.Drawing).ToList();
					if (glyphs.Count == 0 || glyphs.Any(g => g.GlyphRun.GlyphIndices.Any(index => index == 0)))
						throw new InvalidOperationException($"Missing glyph: {choice.Name}, {language.Code}");
					var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
					if (!String.IsNullOrWhiteSpace(output) && !customTheme)
						Render(sample, Path.Combine(output, $"font-{choice.Name.Replace(' ', '-')}-{language.Code}.png"), 520, 180, 144);
				}
			}
			}
			finally { host.Close(); }
		});
	}

	public void TranslatedFormatsPreserveArgumentsAndCatalogIntegrity()
	{
		var service = new ReduxLanguageService();
		foreach (var key in service.Keys)
		{
			var english = service.GetTranslation(key, "en");
			if (!System.Text.RegularExpressions.Regex.IsMatch(english, @"\{\d+")) continue;
			var original = System.Text.CompositeFormat.Parse(english);
			foreach (var language in ReduxLanguageService.Languages)
				RegressionAssert.True(System.Text.CompositeFormat.Parse(service.GetTranslation(key, language.Code)).MinimumArgumentCount <= original.MinimumArgumentCount);
		}
		WithResources(_ =>
		{
			foreach (var language in ReduxLanguageService.Languages)
			{
				ReduxLanguageService.Current.SetLanguage(language.Code);
				service.SetLanguage(language.Code);
				const string path = @"C:\Mods\Save {0} Русский 中文.pak";
				var message = service.Format($"Copying {path}");
				RegressionAssert.Contains(message, path);
				if (language.Code != "en") RegressionAssert.False(message.StartsWith("Copying "));
				var item = new ReduxCommandPaletteItem("Save", "File", "Save", "", "save", () => { }, localize: true);
				RegressionAssert.True(item.Matches(service.Translate("Save")));
				RegressionAssert.Equal("Save", item.Name);
				RegressionAssert.Equal("Save", new ReduxCommandPaletteItem("Save", "File", "Save", "", "save", () => { }).DisplayName);
			}
		});
	}

	public void OnboardingLanguageChangesEveryStepAndKeepsActionsReachable()
	{
		WithResources(resources =>
		{
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			foreach (var size in new[] { ReduxTextSize.Default, ReduxTextSize.Large })
			{
				var settings = new DivinityModManagerSettings { ColorTheme = theme, TextSize = size, ReduceMotion = true };
				var window = new ReduxOnboardingWindow(null!, settings);
				try
				{
					var root = (FrameworkElement)window.Content;
					root.Opacity = 1; root.RenderTransform = Transform.Identity;
					var combo = (ComboBox)window.FindName("WelcomeLanguageComboBox");
					var font = window.FontFamily;
					foreach (var language in ReduxLanguageService.Languages)
					{
						combo.SelectedValue = language.Code;
						RegressionAssert.Equal(language.Code, ReduxLanguageService.Current.Language);
						RegressionAssert.Equal(font, window.FontFamily);
						for (var step = 0; step < 5; step++)
						{
							typeof(ReduxOnboardingWindow).GetMethod("ShowStep", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [step]);
							Layout(root, 640, 480);
							foreach (var name in new[] { "WelcomeLanguageComboBox", "NotNowButton", "SaveContinueButton", "BackButton" })
							{
								var control = (FrameworkElement)window.FindName(name);
								if (control.Visibility != Visibility.Visible) continue;
								var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
								if (bounds.Left < -1 || bounds.Right > 641 || bounds.Bottom > 481 || bounds.Top < -1)
									throw new InvalidOperationException($"Onboarding {name}: {theme}, {size}, {language.Code}, step {step}: {bounds}");
							}
							RegressionAssert.True(((ScrollViewer)window.FindName("OnboardingContentScrollViewer")).ActualHeight > 30);
							RegressionAssert.Equal(ReduxLanguageService.Current.Translate(step == 4 ? "Start using Redux" : "Continue"), ((TextBlock)window.FindName("ContinueLabel")).Text);
							var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
							if (!String.IsNullOrWhiteSpace(output) && size == ReduxTextSize.Large)
								Render(root, Path.Combine(output, $"onboarding-{theme}-{language.Code}-{step}.png"), 640, 480, 144);
						}
					}
					RegressionAssert.SequenceEqual(DivinityModManager.AppServices.ReduxOnboardingPolicy.StarterSeparatorTitles, window.SelectedStarterSeparators);
					((Button)window.FindName("NotNowButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					RegressionAssert.True(window.WasResolved);
					RegressionAssert.False(window.ApplyChanges);
					RegressionAssert.Equal("zh-Hans", window.SelectedInterfaceLanguage);
				}
				finally { window.Close(); }
			}
		});
	}

	public void ModalTranslationsPreserveDecisionsAndUserText()
	{
		WithResources(resources =>
		{
			foreach (var language in ReduxLanguageService.Languages)
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
			{
				ReduxLanguageService.Current.SetLanguage(language.Code);
				ReduxTypographyService.ApplyTextSize(resources, ReduxTextSize.Large);
				const string original = "Keep this window open. Cancellation is unavailable while files are being restored or recovered.";
				var window = new ReduxMessageBoxWindow(null!, original, "Snapshot needs attention", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.No);
				try
				{
					ReduxThemeService.Apply(window.Resources, theme);
					window.SetButtonLabel(MessageBoxResult.Yes, "Keep the Mods copy");
					window.AddExtraAction("Copy to Clipboard", "Redux.Icon.Copy", () => { });
					var root = (FrameworkElement)window.Content;
					root.Opacity = 1; root.RenderTransform = Transform.Identity;
					Layout(root, 560, 480);
					RegressionAssert.Equal(ReduxLanguageService.Current.Translate(original), ((TextBox)window.FindName("MessageTextBox")).Text);
					RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Keep the Mods copy"), ((TextBlock)window.FindName("YesButtonLabel")).Text);
					RegressionAssert.True(((Button)window.FindName("NoButton")).IsDefault);
					RegressionAssert.True(((Button)window.FindName("CancelButton")).IsCancel);
					foreach (var name in new[] { "YesButton", "NoButton", "CancelButton" })
					{
						var button = (Button)window.FindName(name);
						var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
						RegressionAssert.True(bounds.Left >= -1 && bounds.Right <= 561 && bounds.Bottom <= 481);
					}
					var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
					if (!String.IsNullOrWhiteSpace(output)) Render(root, Path.Combine(output, $"modal-{theme}-{language.Code}.png"), 560, 480, 144);
					((Button)window.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					RegressionAssert.Equal(MessageBoxResult.Cancel, window.Result);
				}
				finally { window.Close(); }
			}
		});
	}

	public void SnapshotRunnerCarriesLanguageAndPreservesCancellation()
	{
		WithResources(_ =>
		{
			foreach (var language in ReduxLanguageService.Languages)
			{
				ReduxLanguageService.Current.SetLanguage(language.Code);
				var appearance = new DivinityModManager.AppServices.ReduxSnapshotAppearance { Language = language.Code, TextScale = 1.5 };
				var transferred = System.Text.Json.JsonSerializer.Deserialize<ReduxUpdater.ReduxSnapshotAppearance>(System.Text.Json.JsonSerializer.Serialize(appearance))!;
				RegressionAssert.Equal(language.Code, transferred.Normalize().Language);
				var window = new ReduxUpdater.SnapshotProgressWindow(transferred);
				try
				{
					window.SetOperation("restore");
					var cancellations = 0;
					window.CancelRequested += () => cancellations++;
					window.Report(new("Mods/Save {0} Русский 中文.pak", 10, 20, false));
					var button = (Button)window.FindName("ActionButton");
					button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					RegressionAssert.Equal(0, cancellations);
					RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Restoring snapshot"), ((TextBlock)window.FindName("Heading")).Text);
					RegressionAssert.Equal("Mods/Save {0} Русский 中文.pak", ((TextBlock)window.FindName("Details")).Text);
					var root = (FrameworkElement)window.Content;
					root.Measure(new Size(580, Double.PositiveInfinity));
					var height = (int)Math.Ceiling(root.DesiredSize.Height);
					Layout(root, 580, height);
					RegressionAssert.True(height < 650);
					var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
					if (!String.IsNullOrWhiteSpace(output)) Render(root, Path.Combine(output, $"snapshot-language-{language.Code}.png"), 580, height, 144);
					window.Report(new("Verifying archive"));
					button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					RegressionAssert.Equal(1, cancellations);
					RegressionAssert.Equal(ReduxLanguageService.Current.Translate("Canceling safely…"), ((TextBlock)window.FindName("Details")).Text);
				}
				finally { window.Finish(); ReduxUpdater.Localization.ReduxLanguageService.Current.SetLanguage("en"); }
			}
		});
	}

	private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing)
	{
		if (drawing is GlyphRunDrawing glyph) yield return glyph;
		if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Glyphs(child)) yield return item;
	}
	private static void WithResources(Action<ResourceDictionary> test)
	{
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var saved = Application.Current.Resources;
		var language = ReduxLanguageService.Current.Language;
		var motion = ReduxWindowBehavior.ReduceMotion;
		var effects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		try
		{
			var resources = new ResourceDictionary();
			foreach (var file in new[] { "Typography", "Light", "Dark", "MainResourceDictionary" })
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Redux;component/Themes/{file}.xaml") });
			resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml") });
			Application.Current.Resources = resources;
			ReduxWindowBehavior.ConfigureAccessibility(true, effects);
			test(resources);
		}
		finally { ReduxLanguageService.Current.SetLanguage(language); Application.Current.Resources = saved; ReduxWindowBehavior.ConfigureAccessibility(motion, effects); }
	}
	private static void Layout(FrameworkElement element, int width, int height)
	{
		element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
		element.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
	}
	private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T typed) yield return typed;
			foreach (var item in Descendants<T>(child)) yield return item;
		}
	}
	private static void Render(FrameworkElement root, string path, int width, int height, int dpi)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var bitmap = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
		// Rendering window content alone omits its parent's background.
		var background = new DrawingVisual();
		using (var context = background.RenderOpen())
			context.DrawRectangle(root.TryFindResource("ReduxWindowBrush") as Brush ?? Brushes.Transparent, null, new Rect(0, 0, width, height));
		bitmap.Render(background);
		bitmap.Render(root);
		var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var file = File.Create(path); encoder.Save(file);
	}
}
