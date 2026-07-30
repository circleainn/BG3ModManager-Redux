using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Reflection;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace DivinityModManager.Views;

public sealed class ReduxCommandPaletteItem
{
	public string Name { get; }
	public string Category { get; }
	public string Description { get; }
	public string Gesture { get; }
	public string IconKey { get; }
	public int MinimumQueryLength { get; }
	public bool HasGesture => !String.IsNullOrWhiteSpace(Gesture);
	public bool CanExecute => _canExecute();

	private readonly Action _execute;
	private readonly Func<bool> _canExecute;

	public ReduxCommandPaletteItem(
		string name,
		string category,
		string description,
		string gesture,
		string iconKey,
		Action execute,
		Func<bool> canExecute = null,
		int minimumQueryLength = 0)
	{
		Name = name?.Trim() ?? String.Empty;
		Category = category?.Trim() ?? String.Empty;
		Description = description?.Trim() ?? String.Empty;
		Gesture = gesture?.Trim() ?? String.Empty;
		IconKey = iconKey?.Trim() ?? "terminal";
		MinimumQueryLength = Math.Max(0, minimumQueryLength);
		_execute = execute ?? (() => { });
		_canExecute = canExecute ?? (() => true);
	}

	public void Execute()
	{
		if (CanExecute)
		{
			_execute();
		}
	}

	public bool Matches(string query)
	{
		var normalizedQuery = query?.Trim() ?? String.Empty;
		if (normalizedQuery.Length < MinimumQueryLength)
		{
			return false;
		}
		if (normalizedQuery.Length == 0)
		{
			return true;
		}

		return Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
			|| Category.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
			|| Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
			|| Gesture.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
	}
}

public partial class ReduxCommandPaletteWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly IReadOnlyList<ReduxCommandPaletteItem> _commands;

	public bool Accepted { get; private set; }
	public ReduxCommandPaletteItem SelectedItem { get; private set; }

	public ReduxCommandPaletteWindow(
		Window owner,
		MainWindowViewModel viewModel,
		Action<DivinityModData> focusMod)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true)
		{
			Owner = owner;
		}

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
		{
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));
		}

		_commands = BuildCommandList(viewModel, focusMod);
		Loaded += (_, _) =>
		{
			RefreshResults();
			SearchBox.Focus();
			Keyboard.Focus(SearchBox);
		};
	}

	private static IReadOnlyList<ReduxCommandPaletteItem> BuildCommandList(
		MainWindowViewModel viewModel,
		Action<DivinityModData> focusMod)
	{
		if (viewModel?.Keys == null)
		{
			return [];
		}

		var commands = typeof(AppKeys)
			.GetRuntimeProperties()
			.Where(property => property.PropertyType == typeof(Hotkey))
			.OrderBy(property => property.MetadataToken)
			.Select(property => (
				Property: property,
				Hotkey: property.GetValue(viewModel.Keys) as Hotkey,
				Settings: property.GetCustomAttribute<MenuSettingsAttribute>()))
			.Where(item =>
				item.Hotkey?.HasActions == true
				&& item.Settings != null
				&& item.Property.Name != nameof(AppKeys.OpenCommandPalette))
			.Select(item =>
			{
				var command = (ICommand)item.Hotkey.Command;
				return new ReduxCommandPaletteItem(
					item.Settings.DisplayName,
					item.Hotkey.Category,
					item.Settings.Tooltip,
					item.Hotkey.Key == Key.None ? String.Empty : item.Hotkey.DisplayBindingText,
					"terminal",
					() => command.Execute(null),
					() => item.Hotkey.CanExecuteCommand);
			})
			.ToList();

		commands.AddRange(viewModel.Profiles.Select((profile, index) =>
			new ReduxCommandPaletteItem(
				$"Switch to profile: {profile.Name}",
				"Profiles",
				"Select this Baldur's Gate 3 profile.",
				String.Empty,
				"person",
				() => viewModel.SelectedProfileIndex = index,
				() => viewModel.SelectedProfileIndex != index && !viewModel.IsLocked)));

		commands.AddRange(viewModel.ModOrderList.Select((order, index) =>
			new ReduxCommandPaletteItem(
				$"Load order: {order.Name}",
				"Load orders",
				"Load this saved order into the active and inactive lists.",
				String.Empty,
				"list",
				() => viewModel.SelectedModOrderIndex = index,
				() => viewModel.SelectedModOrderIndex != index && !viewModel.IsLocked)));

		commands.AddRange(viewModel.ModCategoryFilters.Select(category =>
			new ReduxCommandPaletteItem(
				$"Filter category: {category.Name}",
				"Category filters",
				"Show mods assigned to this category.",
				String.Empty,
				"tag",
				() => viewModel.SelectedModCategory = category.Name,
				() => !String.Equals(
					viewModel.SelectedModCategory,
					category.Name,
					StringComparison.OrdinalIgnoreCase))));

		if (focusMod != null)
		{
			commands.AddRange(viewModel.UserMods
				.Where(mod => mod != null
					&& !mod.IsVisualDivider
					&& !String.IsNullOrWhiteSpace(mod.UUID))
				.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.OrderBy(mod => mod.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
				.Select(mod =>
				{
					var categories = mod.DisplayCategories?
						.Select(category => category?.Name)
						.Where(name => !String.IsNullOrWhiteSpace(name))
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.ToArray() ?? [];
					var listState = mod.IsForceLoaded && !mod.IsForceLoadedMergedMod && !mod.ForceAllowInLoadOrder
						? "Always loaded"
						: mod.IsActive
							? "Active"
							: "Inactive";
					var categorySummary = categories.Length == 0
						? listState
						: $"{listState} · {String.Join(" · ", categories)}";
					var searchableDetails = String.Join(
						" ",
						new[]
						{
							mod.Author,
							mod.FileName,
							mod.Folder,
							mod.SourceComponentSummary,
							String.Join(" ", categories)
						}.Where(value => !String.IsNullOrWhiteSpace(value)));
					var iconKey = mod.DisplayCategories?
						.Select(category => category?.IconId)
						.FirstOrDefault(value => !String.IsNullOrWhiteSpace(value)) ?? "package";
					return new ReduxCommandPaletteItem(
						$"Open mod: {mod.GetDisplayName()}",
						$"Mods · {categorySummary}",
						searchableDetails,
						String.Empty,
						iconKey,
						() => focusMod(mod),
						minimumQueryLength: 2);
				}));
		}

		return commands;
	}

	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		=> RefreshResults();

	private void RefreshResults()
	{
		if (CommandList == null || SearchBox == null)
		{
			return;
		}

		var query = SearchBox.Text?.Trim() ?? String.Empty;
		var matches = _commands
			.Where(command => command.Matches(query))
			.ToArray();
		CommandList.ItemsSource = matches;
		CommandList.SelectedIndex = matches.Length > 0 ? 0 : -1;
		EmptyState.Visibility = matches.Length == 0
			? Visibility.Visible
			: Visibility.Collapsed;
	}

	private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		switch (e.Key)
		{
			case Key.Down:
				MoveSelection(1);
				e.Handled = true;
				break;
			case Key.Up:
				MoveSelection(-1);
				e.Handled = true;
				break;
			case Key.Enter:
				AcceptSelection();
				e.Handled = true;
				break;
			case Key.Escape:
				Close();
				e.Handled = true;
				break;
		}
	}

	private void MoveSelection(int offset)
	{
		var count = CommandList.Items.Count;
		if (count <= 0)
		{
			return;
		}

		var current = Math.Max(0, CommandList.SelectedIndex);
		CommandList.SelectedIndex = (current + offset + count) % count;
		CommandList.ScrollIntoView(CommandList.SelectedItem);
	}

	private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (ItemsControl.ContainerFromElement(
				CommandList,
				e.OriginalSource as DependencyObject) is ListBoxItem)
		{
			AcceptSelection();
		}
	}

	private void AcceptSelection()
	{
		if (CommandList.SelectedItem is not ReduxCommandPaletteItem item
			|| !item.CanExecute)
		{
			return;
		}

		Accepted = true;
		SelectedItem = item;
		Close();
	}
}
