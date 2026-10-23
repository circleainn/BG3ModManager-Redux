using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivinityModManager;
using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.Cache;
using DivinityModManager.Util;
using DivinityModManager.Views;
using Newtonsoft.Json;
using NexusModsNET.DataModels;

namespace Redux.Core.Tests;

public sealed class NexusUpdateSkipTests
{
	public void SkippedNexusUpdateSurvivesRestartAndLaterReplacementStillNotifies()
	{
		var now = DateTimeOffset.UtcNow;
		var settings = new DivinityModManagerSettings();
		var cache = new NexusModsCachedData();
		cache.RecordFileUpdateCheck("module", 507, 101, [202], now);
		RegressionAssert.SequenceEqual([202L], Notifiable(settings, cache, now));
		string? saved = null;
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], true,
			() => { saved = JsonConvert.SerializeObject(settings); return true; }));
		settings = JsonConvert.DeserializeObject<DivinityModManagerSettings>(saved!)!;
		cache = JsonConvert.DeserializeObject<NexusModsCachedData>(JsonConvert.SerializeObject(cache))!;
		RegressionAssert.SequenceEqual(Array.Empty<long>(), Notifiable(settings, cache, now));
		// Rechecking the same update doesn't undo the user's choice.
		cache.RecordFileUpdateCheck("module", 507, 101, [202], now.AddHours(1));
		RegressionAssert.SequenceEqual(Array.Empty<long>(), Notifiable(settings, cache, now.AddHours(1)));
		cache.RecordFileUpdateCheck("module", 507, 101, [202, 303], now.AddHours(2));
		RegressionAssert.SequenceEqual([303L], Notifiable(settings, cache, now.AddHours(2)));
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], false, () => true));
		RegressionAssert.SequenceEqual([202L, 303L], Notifiable(settings, cache, now.AddHours(2)));
	}

	public void SkippingNexusFilesPreservesOtherModsVariantsAndSourceLinks()
	{
		var settings = new DivinityModManagerSettings();
		var mod = new DivinityModData { UUID = "module", Name = "My mod", Index = 4, IsActive = true };
		mod.NexusModsData.ModId = 507;
		mod.NexusModsData.LastFileId = 101;
		var before = JsonConvert.SerializeObject(mod.NexusModsData);
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, mod.UUID, 507, 101, [202], true, () => true));
		RegressionAssert.SequenceEqual(Array.Empty<long>(), NexusUpdateSkipPolicy.GetNotifiableFiles(settings, "MODULE", 507, 101, [202]));
		foreach (var identity in new[] { ("other", 507L, 101L), ("module", 508L, 101L), ("module", 507L, 102L) })
			RegressionAssert.SequenceEqual([202L], NexusUpdateSkipPolicy.GetNotifiableFiles(settings, identity.Item1, identity.Item2, identity.Item3, [202]));
		RegressionAssert.Equal(before, JsonConvert.SerializeObject(mod.NexusModsData));
		RegressionAssert.Equal(4, mod.Index);
		RegressionAssert.True(mod.IsActive);
		// One-file restoration leaves the independent skip on another variant intact.
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 102, [202], true, () => true));
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], false, () => true));
		RegressionAssert.SequenceEqual([202L], NexusUpdateSkipPolicy.GetSkippedFiles(settings, "module", 507, 102));
	}

	public void FailedSkipAndRestoreSaveKeepPreviousNexusChoices()
	{
		var settings = new DivinityModManagerSettings();
		var previous = settings.SkippedNexusFileUpdates;
		RegressionAssert.False(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], true, () => false));
		RegressionAssert.True(ReferenceEquals(previous, settings.SkippedNexusFileUpdates));
		RegressionAssert.Throws<IOException>(() => NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101,
			[202], true, () => throw new IOException("Test save failure")));
		RegressionAssert.True(ReferenceEquals(previous, settings.SkippedNexusFileUpdates));
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], true, () => true));
		previous = settings.SkippedNexusFileUpdates;
		RegressionAssert.False(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], false, () => false));
		RegressionAssert.True(ReferenceEquals(previous, settings.SkippedNexusFileUpdates));
		RegressionAssert.Throws<IOException>(() => NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101,
			[202], false, () => throw new IOException("Test restore failure")));
		RegressionAssert.True(ReferenceEquals(previous, settings.SkippedNexusFileUpdates));
	}

	public void NexusSkipChoicesSurviveCacheExpiryAndIgnoreMalformedIdentities()
	{
		var settings = JsonConvert.DeserializeObject<DivinityModManagerSettings>("{}")!;
		var cache = new NexusModsCachedData();
		var now = DateTimeOffset.UtcNow;
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202, 202, 0, -1, 101], true, () => true));
		RegressionAssert.SequenceEqual([202L], NexusUpdateSkipPolicy.GetSkippedFiles(settings, "module", 507, 101));
		RegressionAssert.False(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 0, 101, [202], true, () => true));
		RegressionAssert.False(NexusUpdateSkipPolicy.SetSkipped(settings, "", 507, 101, [202], true, () => true));
		cache.RecordFileUpdateCheck("module", 507, 101, [202], now);
		RegressionAssert.False(cache.HasVerifiedUpdate("module", 507, 101, now.AddDays(8)));
		RegressionAssert.SequenceEqual([202L], NexusUpdateSkipPolicy.GetSkippedFiles(settings, "module", 507, 101));
		// Even after the provider cache is removed, the choice remains reversible.
		cache = new NexusModsCachedData();
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], false, () => true));
		cache.RecordFileUpdateCheck("module", 507, 101, [202], now);
		RegressionAssert.SequenceEqual([202L], Notifiable(settings, cache, now));
		settings.SkippedNexusFileUpdates = [null!, new() { ModUuid = "module", ProjectId = 507, InstalledFileId = 101, ReplacementFileId = 0 }];
		RegressionAssert.SequenceEqual([202L], Notifiable(settings, cache, now));
	}

	public void ReplacementIdentityIncludesLaterFilesBeyondSkippedIntermediateVersions()
	{
		var result = NexusModsDataLoader.GetAvailableFileReplacements(101,
			[new() { OldFileId = 101, NewFileId = 202 }, new() { OldFileId = 202, NewFileId = 303 },
			 new() { OldFileId = 303, NewFileId = 101 }, new() { OldFileId = 101, NewFileId = 404 }],
			[new() { FileId = 202, Category = NexusModFileCategory.Optional }, new() { FileId = 303, Category = NexusModFileCategory.Optional },
			 new() { FileId = 404, Category = (NexusModFileCategory)7 }, new() { FileId = 999, Category = NexusModFileCategory.Main }]);
		RegressionAssert.SequenceEqual([202L, 303L], result);
		var settings = new DivinityModManagerSettings();
		RegressionAssert.True(NexusUpdateSkipPolicy.SetSkipped(settings, "module", 507, 101, [202], true, () => true));
		RegressionAssert.SequenceEqual([303L], NexusUpdateSkipPolicy.GetNotifiableFiles(settings, "module", 507, 101, result));
	}

	public void NexusSkipMenusExposeSpecificReversibleActionsAndBusyState()
	{
		var factory = typeof(HorizontalModLayout).GetMethod("CreateNexusUpdateChoiceActions", BindingFlags.NonPublic | BindingFlags.Static)!;
		long[]? affected = null;
		bool? skip = null;
		Action<long[], bool> action = (files, value) => { affected = files; skip = value; };
		MenuItem[] Build(long[] offered, long[] skipped, bool enabled) =>
			((IEnumerable<MenuItem>)factory.Invoke(null, [offered, skipped, enabled, action])!).ToArray();
		RegressionAssert.Equal(0, Build([], [], true).Length);
		var choices = Build([303], [202], true);
		RegressionAssert.Equal("Skip this update", choices[0].Header.ToString());
		RegressionAssert.Equal("Show skipped updates", choices[1].Header.ToString());
		foreach (var choice in choices) RegressionAssert.True(choice.Icon != null && choice.ToolTip != null);
		choices[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
		RegressionAssert.SequenceEqual([303L], affected!);
		RegressionAssert.True(skip == true);
		choices[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
		RegressionAssert.SequenceEqual([202L], affected!);
		RegressionAssert.True(skip == false);
		RegressionAssert.True(Build([202], [303], false).All(item => !item.IsEnabled));
		RegressionAssert.Equal("Skip these updates", Build([202, 303], [], true)[0].Header.ToString());
		RegressionAssert.Equal("Show skipped updates", Build([], [202], true).Single().Header.ToString());
	}

	private static long[] Notifiable(DivinityModManagerSettings settings, NexusModsCachedData cache, DateTimeOffset now) =>
		NexusUpdateSkipPolicy.GetNotifiableFiles(settings, "module", 507, 101, cache.GetVerifiedReplacements("module", 507, 101, now));

	public void NexusSkipMenusRemainReadableAcrossThemesAndTextSizes()
	{
		var originalResources = Application.Current.Resources;
		var originalMotion = ReduxWindowBehavior.ReduceMotion;
		var originalEffects = ReduxWindowBehavior.BackgroundEffectsDisabled;
		var originalIcons = DivinityApp.ShowInterfaceIcons;
		var factory = typeof(HorizontalModLayout).GetMethod("CreateNexusUpdateChoiceActions", BindingFlags.NonPublic | BindingFlags.Static)!;
		try
		{
			foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment, (ReduxThemeType)0 })
			foreach (var textSize in Enum.GetValues<ReduxTextSize>())
			foreach (var reduced in new[] { false, true })
			{
				var resources = new ResourceDictionary();
				foreach (var name in new[] { "Typography", "Light", "Dark" })
					resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Redux;component/Themes/{name}.xaml") });
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml") });
				Application.Current.Resources = resources;
				resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Redux;component/Themes/MainResourceDictionary.xaml") });
				var custom = theme == 0 ? ReduxThemeService.CreateFromBase("Preview", ReduxThemeType.ReduxDark) : null;
				if (custom != null) custom.AccentColor = "#2EAF9C";
				if (custom != null) ReduxThemeService.Apply(resources, ReduxThemeType.ReduxDark, custom);
				else ReduxThemeService.Apply(resources, theme);
				ReduxTypographyService.ApplyTextSize(resources, textSize);
				ReduxWindowBehavior.ConfigureAccessibility(reduced, originalEffects);
				DivinityApp.ShowInterfaceIcons = !reduced;
				var menu = new ContextMenu();
				var choices = (IEnumerable<MenuItem>)factory.Invoke(null, [new long[] { 202 }, new long[] { 303 }, true, (Action<long[], bool>)((_, _) => { })])!;
				foreach (var choice in choices) menu.Items.Add(choice);
				menu.Measure(new Size(600, 240));
				menu.Arrange(new Rect(new Point(), menu.DesiredSize));
				menu.UpdateLayout();
				RegressionAssert.True(menu.ActualWidth > 100 && menu.ActualWidth <= 600);
				RegressionAssert.True(menu.ActualHeight > 30 && menu.ActualHeight <= 240);
				foreach (MenuItem choice in menu.Items)
				{
					RegressionAssert.True(choice.ActualHeight >= 20 && choice.ActualWidth <= menu.ActualWidth);
					RegressionAssert.True(((ReduxIcon)choice.Icon).StrokeData != null);
				}
				// Render at common display scales; keep one large-text preview per theme.
				foreach (var scale in new[] { 1d, 1.25d, 1.5d })
				{
					var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth * scale),
						(int)Math.Ceiling(menu.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
					bitmap.Render(menu);
					if (textSize == ReduxTextSize.Large && !reduced && scale == 1.5)
					{
						var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
						using var file = File.Create(Path.Combine(Path.GetTempPath(), $"redux-skip-menu-{theme}.png"));
						encoder.Save(file);
					}
				}
			}
		}
		finally
		{
			Application.Current.Resources = originalResources;
			DivinityApp.ShowInterfaceIcons = originalIcons;
			ReduxWindowBehavior.ConfigureAccessibility(originalMotion, originalEffects);
		}
	}
}
