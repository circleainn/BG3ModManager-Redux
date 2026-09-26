using DivinityModManager.Util;
using System.Windows;

namespace DivinityModManager.Views;

public partial class OverrideOrderSelectionWindow : AdonisUI.Controls.AdonisWindow
{
	public sealed class FileChoice
	{
		public string FileName { get; init; }
		public string Location { get; init; }
		public bool Selected { get; set; }
	}

	private readonly List<FileChoice> _choices;
	public bool Accepted { get; private set; }
	public List<string> SelectedFiles => ManageChoice.IsChecked == true
		? _choices.Where(choice => choice.Selected).Select(choice => choice.FileName).ToList()
		: null;

	public OverrideOrderSelectionWindow(Window owner, string orderName,
		IEnumerable<string> installed, IEnumerable<string> held, IEnumerable<string> saved)
	{
		InitializeComponent();
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
		MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 32);
		Height = Math.Min(Height, MaxHeight);
		Heading.Text = $"Override mods in {orderName}";
		var selected = saved?.ToHashSet(StringComparer.OrdinalIgnoreCase);
		ManageChoice.IsChecked = selected != null;
		_choices = installed.Select(name => (Name: name, Location: "In game Mods folder"))
			.Concat(held.Select(name => (Name: name, Location: "Held by Redux")))
			.DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
			.Select(item => new FileChoice
			{
				FileName = item.Name,
				Location = item.Location,
				Selected = selected?.Contains(item.Name) ?? item.Location == "In game Mods folder"
			}).ToList();
		FileChoices.ItemsSource = _choices;
		EmptyText.Visibility = _choices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		FileChoices.IsEnabled = ManageChoice.IsChecked == true;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
	}

	private void ManageChoice_Changed(object sender, RoutedEventArgs e)
	{
		if (FileChoices != null) FileChoices.IsEnabled = ManageChoice.IsChecked == true;
	}

	private void Apply_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
