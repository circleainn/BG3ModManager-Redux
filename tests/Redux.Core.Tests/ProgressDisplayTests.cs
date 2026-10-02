using DivinityModManager.Controls;
using System.Windows.Controls;

namespace Redux.Core.Tests;

internal sealed class ProgressDisplayTests
{
	public void UnknownDurationShowsActivityAndReturnsToMeasuredProgress()
	{
		var progress = new ReduxOperationProgress { ProgressValue = 0.25 };
		var label = (TextBlock)progress.FindName("ProgressLabel");
		var bar = (ProgressBar)progress.FindName("OperationProgressBar");
		RegressionAssert.Equal(0.25.ToString("P0"), label.Text);

		progress.IsIndeterminate = true;
		bar.GetBindingExpression(ProgressBar.IsIndeterminateProperty)!.UpdateTarget();
		RegressionAssert.True(bar.IsIndeterminate);
		RegressionAssert.Equal("Working…", label.Text);
		progress.ProgressValue = 0.5;
		RegressionAssert.Equal("Working…", label.Text);
		progress.ReduceMotion = true;
		RegressionAssert.False(bar.HasAnimatedProperties);
		RegressionAssert.Equal(0.55, bar.Opacity);

		progress.IsIndeterminate = false;
		bar.GetBindingExpression(ProgressBar.IsIndeterminateProperty)!.UpdateTarget();
		RegressionAssert.False(bar.IsIndeterminate);
		RegressionAssert.Equal(1d, bar.Opacity);
		RegressionAssert.Equal(0.5.ToString("P0"), label.Text);
	}
}
