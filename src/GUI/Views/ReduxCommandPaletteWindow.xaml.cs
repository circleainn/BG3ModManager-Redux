using DivinityModManager.Models.App;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace DivinityModManager.Views;

public sealed class ReduxCommandPaletteItem
{
	public string Name { get; }
	public string Category { get; }
	public string Description { get; }
	public string Gesture { get; }
	public bool HasGesture => !String.IsNullOrWhiteSpace(Gesture);
	public bool CanExecute => Hotkey?.CanExecuteCommand == true;
	public Hotkey Hotkey { get; }

	public ReduxCommandPaletteItem(
		Hotkey hotkey,
		MenuSettingsAttribute settings)
	{
		Hotkey = hotkey;
		Name = settings?.DisplayName?.Trim() ?? hotkey?.DisplayName ?? String.Empty;
		Category = hotkey?.Category ?? settings?.Parent ?? String.Empty;
		Description = settings?.Tooltip?.Trim() ?? String.Empty;
		Gesture = hotkey?.Key == Key.None
			? String.Empty
			: hotkey?.DisplayBindingText ?? String.Empty;
	}

	public bool Matches(string query)
	{
		if (String.IsNullOrWhiteSpace(query))
		{
			return true;
		}

		return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
			|| Category.Contains(query, StringComparison.OrdinalIgnoreCase)
			|| Description.Contains(query, StringComparison.OrdinalIgnoreCase)
			|| Gesture.Contains(query, StringComparison.OrdinalIgnoreCase);
	}
}

public partial class ReduxCommandPaletteWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly IReadOnlyList<ReduxCommandPaletteItem> _commands;

	public bool Accepted { get; private set; }
	public Hotkey SelectedHotkey { get; private set; }

	public ReduxCommandPaletteWindow(
		Window owner,
		AppKeys keys)
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

		_commands = BuildCommandList(keys);
		Loaded += (_, _) =>
		{
			RefreshResults();
			SearchBox.Focus();
			Keyboard.Focus(SearchBox);
		};
	}

	private static IReadOnlyList<ReduxCommandPaletteItem> BuildCommandList(AppKeys keys)
	{
		if (keys == null)
		{
			return [];
		}

		return typeof(AppKeys)
			.GetRuntimeProperties()
			.Where(property => property.PropertyType == typeof(Hotkey))
			.OrderBy(property => property.MetadataToken)
			.Select(property => (
				Property: property,
				Hotkey: property.GetValue(keys) as Hotkey,
				Settings: property.GetCustomAttribute<MenuSettingsAttribute>()))
			.Where(item =>
				item.Hotkey?.HasActions == true
				&& item.Settings != null
				&& item.Property.Name != nameof(AppKeys.OpenCommandPalette))
			.Select(item => new ReduxCommandPaletteItem(item.Hotkey, item.Settings))
			.ToArray();
	}

	private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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
		=> AcceptSelection();

	private void AcceptSelection()
	{
		if (CommandList.SelectedItem is not ReduxCommandPaletteItem item
			|| !item.CanExecute)
		{
			return;
		}

		Accepted = true;
		SelectedHotkey = item.Hotkey;
		Close();
	}
}
