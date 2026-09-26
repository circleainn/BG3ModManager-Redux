using DivinityModManager.Util;
using System.Windows;

namespace DivinityModManager.Views;

public partial class SeparatorUpgradeWindow : AdonisUI.Controls.AdonisWindow
{
    public bool Accepted { get; private set; }
    public bool MakePersistent { get; private set; }
    public bool DisableLines { get; private set; }

    public SeparatorUpgradeWindow(Window owner, string orderName, int separatorCount, bool canMakePersistent, bool canDisableLines)
    {
        InitializeComponent();
        if (owner?.IsLoaded == true) Owner = owner;
        var settings = MainWindow.Self?.ViewModel?.Settings;
        if (settings != null)
            ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 32);
        Height = Math.Min(Height, MaxHeight);
        OrderSummary.Text = $"{orderName}: {separatorCount} existing separator{(separatorCount == 1 ? String.Empty : "s")}. Choose what to update, or leave everything as it is.";
        PersistentChoice.IsEnabled = canMakePersistent;
        PersistentChoice.ToolTip = canMakePersistent ? null : "These separators are already persistent.";
        DisableLinesChoice.IsEnabled = canDisableLines;
        ReduxWindowBehavior.AttachDialogTransitions(this, 40);
        ReduxWindowBehavior.AttachRoundedCorners(this);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        MakePersistent = PersistentChoice.IsEnabled && PersistentChoice.IsChecked == true;
        DisableLines = DisableLinesChoice.IsEnabled && DisableLinesChoice.IsChecked == true;
        Accepted = true;
        Close();
    }

    private void LeaveUnchanged_Click(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }
}
