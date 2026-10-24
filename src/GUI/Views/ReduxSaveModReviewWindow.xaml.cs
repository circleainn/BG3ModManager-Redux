using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;
using System.ComponentModel;
using System.Windows;

namespace DivinityModManager.Views;

public sealed class SaveModReviewChoice : INotifyPropertyChanged
{
    public SaveModRequirement Requirement { get; }
    public string DisplayName { get; }
    public string MetadataText { get; }
    public string IdentityText => $"Recorded as: {Requirement.Name}\nUUID: {Requirement.UUID}";
    private readonly bool _allowSelection;
    public Visibility SelectionVisibility => _allowSelection ? Visibility.Visible : Visibility.Collapsed;
    public bool NeedsAttention => _allowSelection ? Requirement.Status != SaveModStatus.Active : Requirement.Status is SaveModStatus.Missing or SaveModStatus.Unavailable;
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { var allowed = value && _allowSelection && Requirement.CanActivate; if (_selected == allowed) return; _selected = allowed; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
    }
    public event PropertyChangedEventHandler PropertyChanged;
    public SaveModReviewChoice(SaveModRequirement requirement, bool allowSelection = true)
    {
        _allowSelection = allowSelection; Requirement = requirement; _selected = allowSelection && requirement.CanActivate;
        var project = ReduxModDatabaseService.TryResolveModuleUuid(requirement.UUID)?.Project;
        DisplayName = String.IsNullOrWhiteSpace(project?.Name) ? requirement.Name : project.Name;
        MetadataText = project == null ? "" : String.Join(" · ", new[] {
            "Nexus Mods", project.Authors?.Count > 0 ? "by " + String.Join(", ", project.Authors) : null,
            project.Categories?.Count > 0 ? String.Join(", ", project.Categories) : null
        }.Where(value => !String.IsNullOrWhiteSpace(value)));
    }
}

public partial class ReduxSaveModReviewWindow : AdonisUI.Controls.AdonisWindow
{
    private readonly SaveModReviewChoice[] _choices;
    private readonly bool _collectionOrder;
    public IReadOnlyList<string> SelectedIds { get; private set; } = Array.Empty<string>();
    public ReduxSaveModReviewWindow(Window owner, string saveName, IReadOnlyList<SaveModRequirement> requirements, DivinityModManagerSettings settings, bool collectionOrder = false)
    {
        InitializeComponent();
        if (owner?.IsLoaded == true) Owner = owner;
        ReduxWindowBehavior.AttachDialogTransitions(this, 40);
        ReduxWindowBehavior.AttachRoundedCorners(this);
        if (settings != null) ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
        _collectionOrder = collectionOrder;
        if (collectionOrder)
        {
            Title = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Collection Load Order");
            InstructionsText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review the collection’s order before saving. Missing and unmatched entries stay in the saved order so you can install their mods later.");
            FooterText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Saving adds a separate order to the Load Order dropdown. It does not activate mods or change your current order.");
            ApplyLabel.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Save load order");
            requirements = requirements.Select(r => r with { Detail = r.Status switch {
                SaveModStatus.Active => "Installed in Active Mods. Your current order will not change.",
                SaveModStatus.Inactive => "Installed in Inactive Mods. Saving will not activate it.",
                _ => r.Detail.Replace("The save contains", "The collection contains")
            }}).ToArray();
        }
        SaveNameText.Text = saveName;
        _choices = requirements.Select(r => new SaveModReviewChoice(r, !collectionOrder)).ToArray();
        RequirementsGrid.ItemsSource = _choices;
        foreach (var choice in _choices) choice.PropertyChanged += (_, _) => RefreshSelection();
        RefreshSelection();
        Loaded += (_, _) => RequirementsGrid.Focus();
    }
    private void RefreshSelection()
    {
        if (_collectionOrder)
        {
            ApplyButton.IsEnabled = _choices.Length > 0;
            SummaryText.Text = DivinityModManager.Localization.ReduxLanguageService.Current.Format($"{_choices.Count(c => c.Requirement.Status == SaveModStatus.Active)} active · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Inactive)} inactive · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Missing)} missing · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Unavailable)} unmatched / need attention");
            return;
        }
        ApplyButton.IsEnabled = _choices.Any(c => c.Selected);
        SummaryText.Text = _choices.Length == 0 ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("No non-built-in mods are recorded in this save.") :
            $"{_choices.Count(c => c.Requirement.Status == SaveModStatus.Active)} active · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Inactive)} inactive · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Missing)} missing · {_choices.Count(c => c.Requirement.Status == SaveModStatus.Unavailable)} need attention · {_choices.Count(c => c.Selected)} selected";
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_collectionOrder) { if (_choices.Length > 0) DialogResult = true; return; }
        SelectedIds = SaveModReviewService.SelectedActivationIds(_choices.Select(c => c.Requirement), _choices.Where(c => c.Selected).Select(c => c.Requirement.UUID));
        if (SelectedIds.Count > 0) DialogResult = true;
    }
}
