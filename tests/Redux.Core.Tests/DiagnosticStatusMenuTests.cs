using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Redux.Core.Tests;

public sealed class DiagnosticStatusMenuTests
{
	private static readonly string[] ToolbarNames =
	[
		"ToolbarModDiagnosticsStatusButton", "CompactToolbarModDiagnosticsStatusButton"
	];

	public void DiagnosticStatusMenusFollowReviewAndUpdateTransitions()
	{
		var view = CreateView(ReduxThemeType.ReduxDark, largeText: false);
		foreach (var toolbarName in ToolbarNames)
		{
			var fixture = new MenuFixture(view, toolbarName);
			fixture.Expect(false, false, false, "", "No review items", "shield-check", "ReduxSuccessBrush");
			RegressionAssert.True(ReferenceEquals(fixture.State.Keys.RefreshModUpdates.Command, fixture.CheckButton.Command));
			RegressionAssert.True(fixture.CheckButton.IsEnabled);
			fixture.CheckButton.Command.Execute(null);
			RegressionAssert.Equal(1, fixture.State.Keys.RefreshModUpdates.Command.Executions);
			fixture.Expect(false, false, true, "", "Checking mod updates", "refresh", "ReduxAccentBrush");
			RegressionAssert.False(fixture.CheckButton.IsEnabled);
			fixture.Expect(false, false, false, "", "No review items", "shield-check", "ReduxSuccessBrush");
			RegressionAssert.True(fixture.CheckButton.IsEnabled);
			fixture.Expect(true, false, false, "1 warning", "Review recommended", "search-alert", "ReduxWarningBrush");
			fixture.Expect(true, false, true, "1 warning", "Checking mod updates", "refresh", "ReduxAccentBrush");
			fixture.Expect(true, false, false, "1 warning", "Review recommended", "search-alert", "ReduxWarningBrush");
			fixture.Expect(true, true, false, "1 error · 1 warning", "Action recommended", "warning", "ReduxErrorBrush");
			fixture.Expect(true, true, true, "1 error · 1 warning", "Checking mod updates", "refresh", "ReduxAccentBrush");
			fixture.Expect(true, true, false, "1 error · 1 warning", "Action recommended", "warning", "ReduxErrorBrush");
			fixture.Expect(false, false, false, "", "No review items", "shield-check", "ReduxSuccessBrush");
		}
	}

	public void DiagnosticStatusMenusRemainReadableAcrossThemesAndLargeText()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		foreach (var largeText in new[] { false, true })
		{
			var view = CreateView(theme, largeText);
			var previews = new List<BitmapSource>();
			foreach (var toolbarName in ToolbarNames)
			{
				var fixture = new MenuFixture(view, toolbarName);
				fixture.Expect(false, false, false, "", "No review items", "shield-check", "ReduxSuccessBrush");
				previews.Add(fixture.Render());
				fixture.Expect(false, false, true, "", "Checking mod updates", "refresh", "ReduxAccentBrush");
				previews.Add(fixture.Render());
				fixture.Expect(true, false, false, "2 warnings · 3 guidance notes", "Review recommended", "search-alert", "ReduxWarningBrush");
				previews.Add(fixture.Render());
				fixture.Expect(true, true, false, "1 error · 2 warnings · 3 guidance notes", "Action recommended", "warning", "ReduxErrorBrush");
				previews.Add(fixture.Render());
			}
			WritePreview(previews, theme, largeText, (Brush)view.FindResource("ReduxWindowBrush"));
		}
	}

	private static MainViewControl CreateView(ReduxThemeType theme, bool largeText)
	{
		// Never load a real view model, persisted settings, or a top-level window.
		var view = new MainViewControl(null!, null!);
		view.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Redux;component/Themes/Typography.xaml")
		});
		ReduxThemeService.Apply(view.Resources, theme);
		if (largeText)
		{
			// Apply the production Large scale locally; ApplyTextSize also changes open windows.
			for (var baseline = 10; baseline <= 24; baseline++)
				view.Resources[$"Redux.FontSize.{baseline}"] = Math.Round(baseline * 1.15 * 2, MidpointRounding.AwayFromZero) / 2;
		}
		return view;
	}

	private sealed class MenuFixture
	{
		public DiagnosticState State { get; } = new();
		public ContextMenu Menu { get; }
		public Button CheckButton => (Button)Menu.Template.FindName("DiagnosticMenuCheckUpdatesButton", Menu);
		private readonly Button _owner;

		public MenuFixture(MainViewControl view, string toolbarName)
		{
			_owner = (Button)view.FindName(toolbarName);
			_owner.DataContext = State;
			Menu = _owner.ContextMenu;
			Menu.PlacementTarget = _owner;
			// Closed menus have no popup resource inheritance; keep this fixture scope explicit.
			Menu.Resources.MergedDictionaries.Add(view.Resources);
			Menu.Width = 360;
		}

		public void Expect(bool attention, bool errors, bool checking, string summary, string title, string iconKey, string brushKey)
		{
			State.Update(attention, errors, checking, summary);
			Layout();
			var titleBlock = (TextBlock)Menu.Template.FindName("DiagnosticMenuTitle", Menu);
			var subtitle = (TextBlock)Menu.Template.FindName("DiagnosticMenuSubtitle", Menu);
			var icon = (ReduxIcon)Menu.Template.FindName("DiagnosticMenuIcon", Menu);
			RegressionAssert.Equal(title, titleBlock.Text);
			RegressionAssert.Equal(checking ? State.ModUpdateCheckStatus : summary, subtitle.Text);
			RegressionAssert.Equal(iconKey, icon.IconKey);
			var expected = ((SolidColorBrush)Menu.FindResource(brushKey)).Color;
			RegressionAssert.Equal(expected, ((SolidColorBrush)titleBlock.Foreground).Color);
			RegressionAssert.Equal(expected, ((SolidColorBrush)icon.Foreground).Color);
			RegressionAssert.Equal(expected, ((SolidColorBrush)_owner.Foreground).Color);
			RegressionAssert.True(icon.StrokeData != null || icon.Data != null);
			foreach (var element in new FrameworkElement[] { titleBlock, subtitle, icon, CheckButton })
			{
				var bounds = element.TransformToAncestor(Menu).TransformBounds(new Rect(element.RenderSize));
				RegressionAssert.True(new Rect(Menu.RenderSize).Contains(bounds));
				RegressionAssert.True(element.ActualHeight + 0.5 >= element.DesiredSize.Height - element.Margin.Top - element.Margin.Bottom);
			}
			RegressionAssert.True(CheckButton.ActualWidth > 0 && CheckButton.ActualHeight >= 32);
		}

		private void Layout()
		{
			Menu.ApplyTemplate();
			Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
			Menu.Measure(new Size(360, Double.PositiveInfinity));
			Menu.Arrange(new Rect(new Point(), Menu.DesiredSize));
			Menu.UpdateLayout();
		}

		public BitmapSource Render()
		{
			var bitmap = new RenderTargetBitmap((int)Math.Ceiling(Menu.ActualWidth), (int)Math.Ceiling(Menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
			bitmap.Render(Menu);
			bitmap.Freeze();
			return bitmap;
		}
	}

	private static void WritePreview(IReadOnlyList<BitmapSource> previews, ReduxThemeType theme, bool largeText, Brush background)
	{
		var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
		if (String.IsNullOrWhiteSpace(output)) return;
		const int cellWidth = 380;
		var cellHeight = 0;
		foreach (var preview in previews) cellHeight = Math.Max(cellHeight, preview.PixelHeight + 12);
		var visual = new DrawingVisual();
		using (var drawing = visual.RenderOpen())
		{
			drawing.DrawRectangle(background, null, new Rect(0, 0, cellWidth * 2, cellHeight * 4));
			// Full toolbar states form the left column; compact toolbar states form the right.
			for (var i = 0; i < previews.Count; i++)
				drawing.DrawImage(previews[i], new Rect(10 + i / 4 * cellWidth, 6 + i % 4 * cellHeight, previews[i].PixelWidth, previews[i].PixelHeight));
		}
		var bitmap = new RenderTargetBitmap(cellWidth * 2, cellHeight * 4, 96, 96, PixelFormats.Pbgra32);
		bitmap.Render(visual);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		Directory.CreateDirectory(output);
		using var file = File.Create(Path.Combine(output, $"diagnostic-status-{theme}-{(largeText ? "large" : "default")}.png"));
		encoder.Save(file);
	}

	public sealed class DiagnosticState : INotifyPropertyChanged
	{
		public bool HasActiveDiagnosticAttention { get; private set; }
		public bool HasActiveDiagnosticErrors { get; private set; }
		public bool IsRefreshingModUpdates { get; private set; }
		public string ActiveDiagnosticSummaryText { get; private set; } = String.Empty;
		public string ModUpdateCheckStatus => "Checking linked Nexus file history…";
		public DiagnosticKeys Keys { get; } = new();
		public event PropertyChangedEventHandler? PropertyChanged;

		public void Update(bool attention, bool errors, bool checking, string summary)
		{
			HasActiveDiagnosticAttention = attention;
			HasActiveDiagnosticErrors = errors;
			IsRefreshingModUpdates = checking;
			ActiveDiagnosticSummaryText = summary;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(String.Empty));
			Keys.RefreshModUpdates.Command.SetChecking(checking);
		}
	}

	public sealed class DiagnosticKeys
	{
		public DiagnosticKey RefreshModUpdates { get; } = new();
	}

	public sealed class DiagnosticKey
	{
		public CheckUpdatesCommand Command { get; } = new();
	}

	public sealed class CheckUpdatesCommand : ICommand
	{
		private bool _checking;
		public int Executions { get; private set; }
		public event EventHandler? CanExecuteChanged;
		public bool CanExecute(object? parameter) => !_checking;
		public void Execute(object? parameter) => Executions++;
		public void SetChecking(bool checking)
		{
			_checking = checking;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
