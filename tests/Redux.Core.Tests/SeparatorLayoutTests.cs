using DivinityModManager;
using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Redux.Core.Tests;

internal sealed class SeparatorLayoutTests
{
	public void StandaloneModsUseTheSpaceBeforeTheFirstSeparator()
	{
		var resources = new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Redux;component/Themes/MainResourceDictionary.xaml")
		};
		resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Redux;component/Themes/Typography.xaml")
		});
		ReduxThemeService.Apply(resources, ReduxThemeType.ReduxDark);
		foreach (var key in new[] { "ModGridView", "InactiveModGridView", "OverrideModGridView" })
		{
			var standalone = Mod("Standalone", 0, false);
			standalone.IsInsideVisualDivider = false;
			var grouped = Mod("Grouped", 1, false);
			var list = new ModListView
			{
				Resources = resources, Width = 760, Height = 180,
				DataContext = new { Settings = new DivinityModManagerSettings() },
				Style = (Style)resources["ModOrderListView"], View = (GridView)resources[key],
				ItemsSource = new[] { standalone, Divider("Group", false), grouped }
			};
			list.Measure(new Size(760, 180));
			list.Arrange(new Rect(0, 0, 760, 180));
			list.UpdateLayout();
			var plainRow = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
			var groupedRow = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(2);
			var plainBorder = plainRow.FindVisualChildren<Border>().Single(border => border.Name == "RowBorder");
			var groupedBorder = groupedRow.FindVisualChildren<Border>().Single(border => border.Name == "RowBorder");
			RegressionAssert.Equal(0d, plainBorder.Margin.Left);
			RegressionAssert.Equal(list.GroupingContentInset.Left, groupedBorder.Margin.Left);
		}
	}

	public void BranchRangesFollowMovesWithoutWaitingForOuterLayout()
	{
		var parent = Divider("Parent", false);
		var child = Divider("Child", true);
		var next = Divider("Next", false);
		var rows = new ObservableCollection<DivinityModData> { parent, Mod("Root mod", 0, false), child,
			Mod("Child mod", 1, true), Mod("Moving mod", 2, true), next };
		var list = new ModListView { ItemsSource = rows };
		list.Measure(new Size(760, 330)); list.Arrange(new Rect(0, 0, 760, 330)); list.UpdateLayout();
		(int Start, int End)[] Ranges() => ((System.Collections.IEnumerable)typeof(ModListView)
			.GetProperty("GroupingRanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(list)!)
			.Cast<object>().Select(range => ((int)range.GetType().GetProperty("Start")!.GetValue(range)!,
				(int)range.GetType().GetProperty("End")!.GetValue(range)!)).ToArray();
		RegressionAssert.Equal((0, 4), Ranges()[0]);
		rows.Move(4, 5);
		RegressionAssert.Equal((0, 3), Ranges()[0]);
		RegressionAssert.Equal((2, 3), Ranges()[1]);
		RegressionAssert.Equal((4, 5), Ranges()[2]);
		rows.Move(5, 4);
		RegressionAssert.Equal((0, 4), Ranges()[0]);
		RegressionAssert.Equal((2, 4), Ranges()[1]);
		RegressionAssert.Equal((5, 5), Ranges()[2]);
	}

	public void SeparatorHeadersAndModNamesStayAlignedWhenColumnsChange()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		foreach (var key in new[] { "ModGridView", "InactiveModGridView", "OverrideModGridView" })
		{
			var resources = new ResourceDictionary
			{
				Source = new Uri("pack://application:,,,/Redux;component/Themes/MainResourceDictionary.xaml")
			};
			resources.MergedDictionaries.Add(new ResourceDictionary
			{
				Source = new Uri("pack://application:,,,/Redux;component/Themes/Typography.xaml")
			});
			ReduxThemeService.Apply(resources, theme);
			var settings = new DivinityModManagerSettings { ColorTheme = theme,
				UseCategoryColorsForSidebarText = theme == ReduxThemeType.ReduxDark };
			var columns = (GridView)resources[key];
			foreach (var column in columns.Columns.ToArray())
				if (column.Header is not string name || name is not ("#" or "Name" or "Author")) columns.Columns.Remove(column);
			var parent = Divider("Gameplay & convenience", false);
			var child = Divider("Camp quality of life", true);
			var rows = new DivinityModData[]
			{
				parent, Mod("Better Character and Party Panels", 0, false), child,
				Mod("Auto Send Read Books To Camp", 1, true), Mod("Auto Send Food To Camp", 2, true),
				Divider("Spells & abilities", true)
			};
			rows[^1].VisualDividerHiddenItemCount = 0;
			foreach (var divider in rows.Where(row => row.IsVisualDivider))
				divider.VisualDividerColor = ReduxThemeService.CreateFromBase("Preview", theme).AccentColor;
			var list = new ModListView
			{
				Resources = resources, Width = 760, Height = 330, DataContext = new { Settings = settings },
				Style = (Style)resources["ModOrderListView"], View = columns, ItemsSource = rows
			};
			void Arrange()
			{
				list.Measure(new Size(760, 330));
				list.Arrange(new Rect(0, 0, 760, 330));
				list.UpdateLayout();
				Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
				list.UpdateLayout();
			}
			void SettleAnimations()
			{
				var frame = new DispatcherFrame();
				var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(260) };
				timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
				timer.Start();
				Dispatcher.PushFrame(frame);
				Arrange();
			}
			TextBlock TextAt(int index, string name) => ((ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(index))
				.FindVisualChildren<TextBlock>().Single(text => text.Name == name &&
					(name == "ModOrderIndexText" && list.UsesHierarchyIndexAlignment
						? text.FindVisualParent<ContentPresenter>()?.Tag as string == "HierarchyIndex"
						: text.FindVisualParent<ContentPresenter>()?.Tag as string != "HierarchyIndex"));
			void CheckAlignment()
			{
				Arrange();
				var labels = new[] { TextAt(0, "VisualDividerTitleText"), TextAt(1, "ModNameText"),
					TextAt(2, "VisualDividerTitleText"), TextAt(3, "ModNameText") };
				var positions = labels.Select(label => label.TransformToAncestor(list).Transform(new Point()).X).ToArray();
				RegressionAssert.True(Math.Abs(positions[2] - positions[0] - 20) <= 1);
				var childNameShift = list.ChildModNameInset.Left - list.RootModNameInset.Left;
				if (Math.Abs(positions[3] - positions[1] - childNameShift) > 1)
					throw new InvalidOperationException($"{key}: root name {positions[1]}, child name {positions[3]}, expected shift {childNameShift}.");
				if (columns.Columns[0].Header as string == "#" && columns.Columns[1].Header as string == "Name")
					RegressionAssert.Equal(ModListView.HierarchyLevelStep, childNameShift);
				var header = list.FindVisualChildren<TextBlock>().Single(text => text.Name == "HeaderLabel" && text.Text == "Name");
				var headerLeft = header.TransformToAncestor(list).Transform(new Point()).X;
				if (Math.Abs(headerLeft + list.RootModNameInset.Left - positions[1]) > 1)
					throw new InvalidOperationException($"{key} {theme}: Name header {headerLeft} differs from row text {positions[1]}.");
				foreach (var index in new[] { 0, 2 })
				{
					var separatorMarker = ((ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(index))
						.FindVisualChildren<Grid>().Single(grid => grid.Name == "VisualDividerMarker");
					var markerRight = separatorMarker.TransformToAncestor(list).TransformBounds(new Rect(separatorMarker.RenderSize)).Right;
					RegressionAssert.True(Math.Abs(positions[index] - markerRight - 6) <= 1);
				}
				RegressionAssert.True(list.RootModNameInset.Left >= 0);
				RegressionAssert.True(list.ChildModNameInset.Left >= 0);
				if (columns.Columns[0].Header as string == "Name")
				{
					var leadingIcon = ((ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0))
						.FindVisualChildren<Grid>().Single(grid => grid.Name == "VisualDividerMarker");
					RegressionAssert.True(Math.Abs(positions[1] - leadingIcon.TransformToAncestor(list).Transform(new Point()).X) <= 1);
				}
                if (list.UsesHierarchyIndexAlignment)
                {
					var indexHeader = list.FindVisualChildren<TextBlock>().Single(text => text.Name == "HeaderLabel" && text.Text == "#");
					var indexHeaderBounds = indexHeader.TransformToAncestor(list).TransformBounds(new Rect(indexHeader.RenderSize));
					var rootNumber = TextAt(1, "ModOrderIndexText");
					var rootNumberBounds = rootNumber.TransformToAncestor(list).TransformBounds(new Rect(rootNumber.RenderSize));
					if (Math.Abs(indexHeaderBounds.Left + indexHeaderBounds.Width / 2 - rootNumberBounds.Left - rootNumberBounds.Width / 2) > 1)
						throw new InvalidOperationException($"{key}: # header center {indexHeaderBounds.Left + indexHeaderBounds.Width / 2}, number center {rootNumberBounds.Left + rootNumberBounds.Width / 2}.");
                    foreach (var pair in new[] { (Divider: 0, Mod: 1), (Divider: 2, Mod: 3) })
                    {
                        var dividerItem = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(pair.Divider);
                        var icon = dividerItem.FindVisualChildren<Grid>().Single(grid => grid.Name == "VisualDividerMarker");
                        var number = TextAt(pair.Mod, "ModOrderIndexText");
                        var iconBounds = icon.TransformToAncestor(list).TransformBounds(new Rect(icon.RenderSize));
                        var numberBounds = number.TransformToAncestor(list).TransformBounds(new Rect(number.RenderSize));
                        // Matching centers is not enough: a margin can push a number
                        // beyond its native cell's layout clip while retaining those bounds.
                        var ink = VisualTreeHelper.GetDrawing(number)?.Bounds ?? Rect.Empty;
                        RegressionAssert.False(ink.IsEmpty);
                        for (DependencyObject visual = number; visual != list; visual = VisualTreeHelper.GetParent(visual))
                        {
                            if (visual is FrameworkElement element && LayoutInformation.GetLayoutClip(element) is { } clip)
                            {
                                var inkInElement = ReferenceEquals(number, element) ? ink : number.TransformToAncestor(element).TransformBounds(ink);
                                RegressionAssert.True(clip.Bounds.Contains(inkInElement));
                            }
                        }
						var expectedCenter = indexHeaderBounds.Left + indexHeaderBounds.Width / 2
							+ (pair.Divider == 2 ? ModListView.HierarchyLevelStep : 0);
						if (Math.Abs(expectedCenter - numberBounds.Left - numberBounds.Width / 2) > 1)
							throw new InvalidOperationException($"Number center {numberBounds.Left + numberBounds.Width / 2} must match # header center {expectedCenter}.");
						if (Math.Abs(iconBounds.Left + iconBounds.Width / 2 - expectedCenter) > 1)
							throw new InvalidOperationException("The separator icon and its mod number must align.");
                    }
                }
				RegressionAssert.Equal(labels[0].FontSize, labels[2].FontSize);
				RegressionAssert.Equal(FontWeights.SemiBold, labels[0].FontWeight);
				RegressionAssert.Equal(FontWeights.Normal, labels[2].FontWeight);
				var parentItem = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
				var childItem = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(2);
				RegressionAssert.Equal(parentItem.ActualHeight, childItem.ActualHeight);
				var toggle = childItem.FindVisualChildren<Button>().Single(button => button.Name == "VisualDividerToggle");
				var marker = childItem.FindVisualChildren<Grid>().Single(grid => grid.Name == "VisualDividerMarker");
				var toggleBounds = toggle.TransformToAncestor(childItem).TransformBounds(new Rect(toggle.RenderSize));
				var markerBounds = marker.TransformToAncestor(childItem).TransformBounds(new Rect(marker.RenderSize));
				RegressionAssert.True(Math.Abs(markerBounds.Left - toggleBounds.Right - 7) <= 1);
				RegressionAssert.Equal("ReduxDividerToggle", toggle.Tag);
				RegressionAssert.Equal($"Collapse {child.VisualDividerTitle}", toggle.ToolTip);
				var ordinaryItem = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(1);
				var nestedItem = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(3);
				var ordinaryFeedback = ordinaryItem.FindVisualChildren<Grid>().Single(grid => grid.Name == "ModRowFeedback");
				var nestedFeedback = nestedItem.FindVisualChildren<Grid>().Single(grid => grid.Name == "ModRowFeedback");
				var ordinaryBounds = ordinaryFeedback.TransformToAncestor(list).TransformBounds(new Rect(ordinaryFeedback.RenderSize));
				var nestedBounds = nestedFeedback.TransformToAncestor(list).TransformBounds(new Rect(nestedFeedback.RenderSize));
				RegressionAssert.True(Math.Abs(ordinaryBounds.Left - nestedBounds.Left - list.RootModFeedbackInset.Left + list.ChildModFeedbackInset.Left) <= 1);
				RegressionAssert.True(Math.Abs(ordinaryBounds.Right - nestedBounds.Right) <= 1);
				var headerChrome = parentItem.FindVisualChildren<Border>().Single(border => border.Name == "VisualDividerChrome");
				RegressionAssert.Equal((byte)0, ((SolidColorBrush)headerChrome.Background).Color.A);
				RegressionAssert.Equal(new Thickness(0), headerChrome.BorderThickness);
				var hover = parentItem.FindVisualChildren<Border>().Single(border => border.Name == "VisualDividerHoverSurface");
				RegressionAssert.Equal(0d, parentItem.FindVisualChildren<Grid>().Single(grid => grid.Name == "VisualDividerFeedback").Opacity);
				RegressionAssert.Equal(theme != ReduxThemeType.Parchment, hover.OpacityMask is LinearGradientBrush);
			}
			CheckAlignment();
			var nameColumn = columns.Columns.Single(column => column.Header as string == "Name");
			var nameHeader = list.FindVisualChildren<GridViewColumnHeader>().Single(header =>
				header.Role == GridViewColumnHeaderRole.Normal && ReferenceEquals(header.Column, nameColumn));
			var gripper = (Thumb)nameHeader.Template.FindName("PART_HeaderGripper", nameHeader);
			RegressionAssert.True(gripper != null);
			var originalWidth = nameColumn.ActualWidth;
			gripper!.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
			gripper.RaiseEvent(new DragDeltaEventArgs(12, 0) { RoutedEvent = Thumb.DragDeltaEvent });
			CheckAlignment();
			var resizeFeedback = list.FindVisualChildren<Grid>().Single(grid => grid.Name == "ColumnResizeFeedback");
			RegressionAssert.True(resizeFeedback.ActualHeight <= 30);
			RegressionAssert.True(Math.Abs(resizeFeedback.TransformToAncestor(list).Transform(new Point()).Y) <= 1);
			var resizePreviewFolder = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
			if (!String.IsNullOrWhiteSpace(resizePreviewFolder))
			{
				SettleAnimations();
				RegressionAssert.True(resizeFeedback.Opacity > 0.99);
				RegressionAssert.True(Math.Abs(((TranslateTransform)resizeFeedback.RenderTransform).X - list.ColumnResizeOffset) < 1);
				var bitmap = new RenderTargetBitmap(760, 330, 96, 96, PixelFormats.Pbgra32);
				bitmap.Render(list);
				var encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(bitmap));
				Directory.CreateDirectory(resizePreviewFolder);
				using var file = File.Create(Path.Combine(resizePreviewFolder, $"column-resize-{key}-{theme}.png"));
				encoder.Save(file);
			}
			gripper.RaiseEvent(new DragCompletedEventArgs(12, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
			RegressionAssert.Equal(originalWidth + 12, nameColumn.Width);
			CheckAlignment();
			// Minimums apply during the gesture, including a drag past zero; the
			// following columns must never slide underneath the number header.
			gripper.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
			RegressionAssert.True(list.IsColumnResizing);
			gripper.RaiseEvent(new DragDeltaEventArgs(-10000, 0) { RoutedEvent = Thumb.DragDeltaEvent });
			RegressionAssert.Equal(ModListView.GetColumnWidthFloor("Name"), nameColumn.Width);
			CheckAlignment();
			RegressionAssert.True(nameHeader.ActualWidth >= ModListView.GetColumnWidthFloor("Name"));
			gripper.RaiseEvent(new DragCompletedEventArgs(-10000, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
			RegressionAssert.False(list.IsColumnResizing);
			nameColumn.Width = originalWidth;
			CheckAlignment();
			var paddingHeader = list.FindVisualChildren<GridViewColumnHeader>().Single(header => header.Role == GridViewColumnHeaderRole.Padding);
			RegressionAssert.True(paddingHeader.IsHitTestVisible);
			RegressionAssert.True(list.HasSeparatorRows);
			var parentContainer = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
			var countBadge = parentContainer.FindVisualChildren<Border>().Single(border => border.Name == "VisualDividerCountBadge");
			var countText = parentContainer.FindVisualChildren<TextBlock>().Single(text => text.Name == "VisualDividerCountText");
			RegressionAssert.True(ReferenceEquals(resources["ReduxCountBadgeStyle"], countBadge.Style.BasedOn));
			RegressionAssert.Equal("3", countText.Text);
			parent.VisualDividerHiddenItemCount = 1;
			Arrange();
			RegressionAssert.Equal("1", countText.Text);
			RegressionAssert.Equal("1 mod", countBadge.ToolTip);
			RegressionAssert.Equal("1 mod", System.Windows.Automation.AutomationProperties.GetName(countText));
			parent.VisualDividerHiddenItemCount = 3;
			Arrange();
			parentContainer.IsSelected = true;
			Arrange();
			RegressionAssert.True(!parentContainer.FindVisualChildren<Border>().Any(border => border.Name == "VisualDividerSelectionOutline"));
			parentContainer.IsSelected = false;
			Arrange();
			RegressionAssert.False(parentContainer.FindVisualChildren<Border>().Any(border => border.Name == "VisualDividerAccent"));
			var ranges = ((System.Collections.IEnumerable)typeof(ModListView).GetProperty("GroupingRanges",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(list)!).Cast<object>().ToArray();
			RegressionAssert.Equal(3, ranges.Length);
			RegressionAssert.Equal(5, (int)ranges[0].GetType().GetProperty("End")!.GetValue(ranges[0])!);
			RegressionAssert.Equal(4, (int)ranges[1].GetType().GetProperty("End")!.GetValue(ranges[1])!);
			var parentToggle = parentContainer.FindVisualChildren<Button>().Single(button => button.Name == "VisualDividerToggle");
			RegressionAssert.Equal(5d, parentToggle.TransformToAncestor(list).Transform(new Point()).X);
			RegressionAssert.Equal(24d, parentToggle.Width);
			parent.IsVisualDividerCollapsed = true;
			Arrange();
			RegressionAssert.Equal($"Expand {parent.VisualDividerTitle}", parentToggle.ToolTip);
			RegressionAssert.Equal(parentToggle.ToolTip, System.Windows.Automation.AutomationProperties.GetName(parentToggle));
			parent.IsVisualDividerCollapsed = false;
			Arrange();
			var gutter = list.FindVisualChildren<SeparatorGroupingGutter>().Single();
			var drawing = VisualTreeHelper.GetDrawing(gutter);
			RegressionAssert.True(drawing != null && drawing.Bounds.Height > 100);
			RegressionAssert.True(drawing!.Bounds.Right < 48);
			// The outline and chevron consume the row transition's same progress, in both directions.
			var chevron = parentToggle.FindVisualChildren<System.Windows.Shapes.Path>().Single();
			var paneToggle = new ToggleButton { Resources = resources, Style = (Style)resources["ReduxChromeToggleStyle"] };
			RegressionAssert.True(ReferenceEquals(paneToggle.Template, parentToggle.Template));
			RegressionAssert.Equal(new Thickness(0), parentToggle.BorderThickness);
			RegressionAssert.Equal(5d, chevron.Height);
			RegressionAssert.Equal(1.25d, chevron.StrokeThickness);
			parent.VisualDividerChevronAngle = -45;
			typeof(ModListView).GetMethod("RefreshGroupingAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(list, null);
			Arrange();
			RegressionAssert.Equal(-45d, ((RotateTransform)chevron.RenderTransform).Angle);
			var halfway = VisualTreeHelper.GetDrawing(gutter);
			static System.Collections.Generic.IEnumerable<GeometryDrawing> Strokes(Drawing value)
			{
				if (value is GeometryDrawing geometry) yield return geometry;
				if (value is DrawingGroup group)
					foreach (var entry in group.Children)
						foreach (var stroke in Strokes(entry)) yield return stroke;
			}
			RegressionAssert.True(halfway != null);
			RegressionAssert.True(Strokes(halfway!).First().Bounds.Height < Strokes(drawing).First().Bounds.Height * 0.6);
			parent.VisualDividerChevronAngle = -90;
			typeof(ModListView).GetMethod("RefreshGroupingAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(list, null);
			Arrange();
			RegressionAssert.True(VisualTreeHelper.GetDrawing(gutter)?.Bounds.IsEmpty != false);
			parent.VisualDividerChevronAngle = -45;
			typeof(ModListView).GetMethod("RefreshGroupingAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(list, null);
			Arrange();
			RegressionAssert.True(VisualTreeHelper.GetDrawing(gutter)?.Bounds.IsEmpty == false);
			parent.VisualDividerChevronAngle = 0;
			typeof(ModListView).GetMethod("RefreshGroupingAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(list, null);
			Arrange();
			RegressionAssert.Equal(drawing.Bounds, VisualTreeHelper.GetDrawing(gutter)!.Bounds);
			var line = parentContainer.FindVisualChildren<Border>().Single(border => border.Name == "VisualDividerLine");
			RegressionAssert.Equal(Visibility.Collapsed, line.Visibility);
			RegressionAssert.Equal(2d, line.Height);
			parent.ShowVisualDividerLine = true;
			Arrange();
			if (line.Visibility != Visibility.Visible || line.ActualWidth <= 0)
				throw new InvalidOperationException($"Separator line is {line.Visibility}, width {line.ActualWidth} in {theme} {key}.");
			parent.ShowVisualDividerLine = false;
			Arrange();
			RegressionAssert.Equal(Visibility.Collapsed, line.Visibility);
			var originalColor = parent.VisualDividerColor;
			parent.VisualDividerColor = "#2C9679";
			settings.UseCategoryColorsForSidebarText = true;
			Arrange();
			RegressionAssert.Equal(ColorConverter.ConvertFromString("#2C9679"), ((SolidColorBrush)TextAt(0, "VisualDividerTitleText").Foreground).Color);
			settings.UseCategoryColorsForSidebarText = false;
			Arrange();
			RegressionAssert.Equal(((SolidColorBrush)resources["ReduxTextPrimaryBrush"]).Color, ((SolidColorBrush)TextAt(0, "VisualDividerTitleText").Foreground).Color);
			parent.VisualDividerColor = originalColor;
			settings.UseCategoryColorsForSidebarText = theme == ReduxThemeType.ReduxDark;
			Arrange();
			var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
			if (!String.IsNullOrWhiteSpace(output))
			{
				SettleAnimations();
				RegressionAssert.True(list.FindVisualChildren<Grid>().Single(grid => grid.Name == "ColumnResizeFeedback").Opacity < 0.01);
				Directory.CreateDirectory(output);
				var bitmap = new RenderTargetBitmap(760, 330, 96, 96, PixelFormats.Pbgra32);
				bitmap.Render(list);
				var encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(bitmap));
				using var file = File.Create(Path.Combine(output, $"separator-{key}-{theme}.png"));
				encoder.Save(file);
				var hover = parentContainer.FindVisualChildren<Border>().Single(border => border.Name == "VisualDividerHoverSurface");
				var rail = parentContainer.FindVisualChildren<Border>().Single(border => border.Name == "VisualDividerHoverRail");
				RegressionAssert.Equal(hover.Margin, rail.Margin);
				RegressionAssert.Equal(hover.CornerRadius, rail.CornerRadius);
				var hoverBounds = hover.TransformToAncestor(list).TransformBounds(new Rect(hover.RenderSize));
				var chevronBounds = chevron.TransformToAncestor(list).TransformBounds(new Rect(chevron.RenderSize));
				RegressionAssert.True(hoverBounds.Contains(chevronBounds));
				RegressionAssert.True(chevronBounds.Left - hoverBounds.Left >= 5);
				var buttonHover = (Border)parentToggle.Template.FindName("HoverSurface", parentToggle);
				RegressionAssert.Equal(Visibility.Collapsed, buttonHover.Visibility);
				var feedbackSurface = parentContainer.FindVisualChildren<Grid>().Single(grid => grid.Name == "VisualDividerFeedback");
				feedbackSurface.Opacity = 1;
				Arrange();
				var hovered = new RenderTargetBitmap(760, 330, 96, 96, PixelFormats.Pbgra32);
				hovered.Render(list);
				var hoveredEncoder = new PngBitmapEncoder();
				hoveredEncoder.Frames.Add(BitmapFrame.Create(hovered));
				using var hoverFile = File.Create(Path.Combine(output, $"separator-hover-{key}-{theme}.png"));
				hoveredEncoder.Save(hoverFile);
				feedbackSurface.Opacity = 0;
			}
			var indexColumn = columns.Columns.FirstOrDefault(column => column.Header as string == "#");
			if (indexColumn != null) { indexColumn.Width = 72; CheckAlignment(); columns.Columns.Remove(indexColumn); CheckAlignment(); }
			var author = columns.Columns.Single(column => column.Header as string == "Author");
			columns.Columns.Move(columns.Columns.IndexOf(author), 0);
			author.Width = 160;
			CheckAlignment();
			// Removing separators (including a metadata-sorted view) must not retain an empty gutter.
			var plainRows = new ObservableCollection<DivinityModData>(rows.Where(row => !row.IsVisualDivider));
			list.ItemsSource = plainRows;
			Arrange();
			RegressionAssert.False(list.HasSeparatorRows);
			list.Items.SortDescriptions.Add(new SortDescription("Author", ListSortDirection.Descending));
			Arrange();
			RegressionAssert.Equal<ListSortDirection?>(ListSortDirection.Descending, ModListView.GetCurrentSortDirection(author));
			var authorHeader = list.FindVisualChildren<GridViewColumnHeader>().Single(header =>
				header.Role == GridViewColumnHeaderRole.Normal && ReferenceEquals(header.Column, author));
			var glyph = (System.Windows.Shapes.Path)authorHeader.Template.FindName("SortDirectionGlyph", authorHeader);

			RegressionAssert.Equal(Visibility.Visible, glyph.Visibility);
			RegressionAssert.Equal("Sorted descending", System.Windows.Automation.AutomationProperties.GetItemStatus(authorHeader));
			list.Items.SortDescriptions.Clear();
			list.Items.SortDescriptions.Add(new SortDescription("Author", ListSortDirection.Ascending));
			Arrange();
			RegressionAssert.Equal(180d, ((RotateTransform)glyph.RenderTransform).Angle);
			list.Items.SortDescriptions.Clear();
			Arrange();
			RegressionAssert.Equal(Visibility.Collapsed, glyph.Visibility);
			RegressionAssert.Equal<ListSortDirection?>(null, ModListView.GetCurrentSortDirection(author));
			var plainName = TextAt(0, "ModNameText");
			var plainHeader = list.FindVisualChildren<TextBlock>().Single(text => text.Name == "HeaderLabel" && text.Text == "Name");
			RegressionAssert.True(Math.Abs(plainName.TransformToAncestor(list).Transform(new Point()).X -
				plainHeader.TransformToAncestor(list).Transform(new Point()).X) <= 1);
			plainRows.Add(parent);
			Arrange();
			RegressionAssert.True(list.HasSeparatorRows);
			plainRows.Remove(parent);
			Arrange();
			RegressionAssert.False(list.HasSeparatorRows);
			list.ItemsSource = rows;
			CheckAlignment();
			parent.IsVisualDividerCollapsed = true;
			list.ItemsSource = new[] { parent };
			Arrange();
			RegressionAssert.True(VisualTreeHelper.GetDrawing(gutter)?.Bounds.IsEmpty != false);
			parent.IsVisualDividerCollapsed = false;
			list.ItemsSource = rows;
			CheckAlignment();
		}
		CheckCompactEditors();
	}

	private static void CheckCompactEditors()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		foreach (var separator in new[] { false, true })
		{
			var dialog = new CategoryNameDialog("Gameplay", "#2C9679", visualDividerMode: separator,
				iconId: "layers", allowGlobalSeparator: true, savedColors: new[] { "#2C9679", "#8A6AF1" });
			if (separator) dialog.ConfigureSeparatorParent([new() { Id = "parent", Title = "Gameplay & convenience", IsGlobal = true }], "", true, true);
			ReduxThemeService.Apply(dialog.Resources, theme);
			var root = (Grid)dialog.Content;
			var editorHeight = separator ? 636 : 550;
			void Layout()
			{
				root.Opacity = 1;
				root.RenderTransform = Transform.Identity;
				root.Background = dialog.Background;
				root.Measure(new Size(600, editorHeight));
				root.Arrange(new Rect(0, 0, 600, editorHeight));
				root.UpdateLayout();
				Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
				root.UpdateLayout();
			}
			Layout();
			RegressionAssert.True(dialog.FindName("SeparatorPreview") == null);
			var custom = (Expander)dialog.FindName("CustomColorExpander");
			RegressionAssert.False(custom.IsExpanded);
			var plane = (Grid)dialog.FindName("ColorPlane");
			RegressionAssert.True(plane.ActualWidth > 200 && plane.ActualHeight > 100);
			var scroll = (ScrollViewer)dialog.FindName("EditorScrollViewer");
			if (scroll.ScrollableHeight > 1) throw new InvalidOperationException($"Compact editor needs {scroll.ScrollableHeight}px scrolling in {theme} (separator={separator}).");
			RegressionAssert.True(dialog.HideSeparatorLine);
			var line = (CheckBox)dialog.FindName("ShowSeparatorLineCheckBox");
			RegressionAssert.Equal(separator ? Visibility.Visible : Visibility.Collapsed, line.Visibility);
			var hex = (TextBox)dialog.FindName("HexColorTextBox");
			hex.Text = "#336699";
			hex.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
			RegressionAssert.Equal("#336699", dialog.CategoryColor);
			hex.Text = "invalid";
			hex.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
			RegressionAssert.Equal("#336699", dialog.CategoryColor);
			RegressionAssert.Equal("#336699", hex.Text);
			var hue = (Slider)dialog.FindName("HueSlider");
			hue.Value = 120;
			RegressionAssert.Equal("#339933", dialog.CategoryColor);
			var pickPoint = typeof(CategoryNameDialog).GetMethod("SetColorPlaneFromPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
			pickPoint.Invoke(dialog, new object[] { new Point(plane.ActualWidth, 0) });
			RegressionAssert.Equal("#00FF00", dialog.CategoryColor);
			pickPoint.Invoke(dialog, new object[] { new Point(-10, -10) });
			RegressionAssert.Equal("#FFFFFF", dialog.CategoryColor);
			pickPoint.Invoke(dialog, new object[] { new Point(plane.ActualWidth + 10, plane.ActualHeight + 10) });
			RegressionAssert.Equal("#000000", dialog.CategoryColor);
			hex.Text = "#336699";
			hex.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
			var save = (Button)dialog.FindName("SaveColorButton");
			save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.True(dialog.SavedColors.Contains("#336699"));
			Layout();
			var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
			if (!String.IsNullOrWhiteSpace(output))
			{
				var bitmap = new RenderTargetBitmap(600, editorHeight, 96, 96, PixelFormats.Pbgra32);
				bitmap.Render(root);
				var encoder = new PngBitmapEncoder();
				encoder.Frames.Add(BitmapFrame.Create(bitmap));
				using var file = File.Create(Path.Combine(output, $"compact-editor-{theme}-{separator}.png"));
				encoder.Save(file);
			}
			custom.IsExpanded = true;
			Layout();
			RegressionAssert.True(plane.IsVisible || plane.ActualWidth > 0);
			var confirm = (Button)dialog.FindName("ConfirmButton");
			RegressionAssert.True(confirm.TransformToAncestor(root).TransformBounds(new Rect(confirm.RenderSize)).Bottom <= editorHeight);
			editorHeight = 380;
			Layout();
			RegressionAssert.True(scroll.ScrollableHeight > 0);
			RegressionAssert.True(confirm.TransformToAncestor(root).TransformBounds(new Rect(confirm.RenderSize)).Bottom <= editorHeight);
			dialog.Close();
		}
	}

	public void ParentPickerInheritsScopeAndUpgradeDialogFits()
	{
		var editor = new CategoryNameDialog("Child", visualDividerMode: true, allowGlobalSeparator: true);
		editor.ConfigureSeparatorParent([
			new() { Id = "persistent", Title = "Persistent parent", IsGlobal = true },
			new() { Id = "local", Title = "Local parent" }], "persistent", true, true);
		var picker = (ComboBox)editor.FindName("SeparatorParentComboBox");
		var scope = (CheckBox)editor.FindName("GlobalSeparatorCheckBox");
		RegressionAssert.Equal("persistent", editor.SelectedSeparatorParentId);
		RegressionAssert.True(editor.UseSeparatorInEveryLoadOrder);
		RegressionAssert.False(scope.IsEnabled);
		picker.SelectedValue = "local";
		RegressionAssert.False(editor.UseSeparatorInEveryLoadOrder);
		RegressionAssert.False(scope.IsEnabled);
		picker.SelectedValue = "";
		RegressionAssert.True(scope.IsEnabled);
		editor.ConfigureSeparatorParent([], "", false, true, "Expand this separator before changing its parent.");
		RegressionAssert.False(picker.IsEnabled);
		editor.Close();
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var dialog = new SeparatorUpgradeWindow(null!, "Current", 4, true, true);
			ReduxThemeService.Apply(dialog.Resources, theme);
			RegressionAssert.True(((CheckBox)dialog.FindName("DisableLinesChoice")).IsChecked != true);
			RegressionAssert.True(((CheckBox)dialog.FindName("PersistentChoice")).IsChecked != true);
			var root = (Grid)dialog.Content;
			foreach (var height in new[] { 480, 300 })
			{
				root.Opacity = 1;
				root.RenderTransform = Transform.Identity;
				root.Background = dialog.Background;
				root.Measure(new Size(560, height));
				root.Arrange(new Rect(0, 0, 560, height));
				root.UpdateLayout();
				var button = (Button)dialog.FindName("ApplyButton");
				RegressionAssert.True(button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize)).Bottom <= height);
				var scroll = (ScrollViewer)dialog.FindName("BodyScrollViewer");
				if (height == 300) RegressionAssert.True(scroll.ScrollableHeight > 0);
				var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
				if (height == 480 && !String.IsNullOrEmpty(output))
				{
					Directory.CreateDirectory(output);
					var bitmap = new RenderTargetBitmap(560, height, 96, 96, PixelFormats.Pbgra32);
					bitmap.Render(root);
					var encoder = new PngBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(bitmap));
					using var file = File.Create(Path.Combine(output, $"separator-upgrade-{theme}.png"));
					encoder.Save(file);
				}
			}
			dialog.Close();
		}
	}

	private static DivinityModData Divider(string title, bool child) => new()
	{
		UUID = Guid.NewGuid().ToString(), Name = title, VisualDividerTitle = title,
		IsVisualDivider = true, ShowVisualDivider = true, IsChildVisualDivider = child, ShowVisualDividerLine = false,
		VisualDividerIconId = child ? "sparkles" : "layers", VisualDividerHiddenItemCount = child ? 2 : 3
	};
	private static DivinityModData Mod(string name, int index, bool child) => new RegressionModData
	{
		UUID = Guid.NewGuid().ToString(), Name = name, Author = "Volitio", Index = index,
		InactiveIndex = index, IsInsideVisualDivider = true, IsInsideChildVisualDivider = child
	};
}
