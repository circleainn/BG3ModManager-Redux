using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DivinityModManager.Views;

public partial class ReduxStartupWindow : Window, IReduxTypographyIsolated
{
	private static readonly Duration EntranceDuration = TimeSpan.FromMilliseconds(280);
	private static readonly Duration ExitDuration = TimeSpan.FromMilliseconds(220);
	// The card leaves as a whole on exit, so scaling it there is fine. On entrance only
	// StartupForeground moves: see the note in the XAML.
	private const double ExitScale = 0.96;
	private const double ForegroundRiseDistance = 10;
	private const int DwmWindowCornerPreferenceAttribute = 33;
	private const int DwmWindowCornerPreferenceRound = 2;

	private bool _contentRendered;
	private Task? _entranceTask;

	public static readonly DependencyProperty VersionTextProperty = DependencyProperty.Register(
		nameof(VersionText),
		typeof(string),
		typeof(ReduxStartupWindow),
		new PropertyMetadata(String.Empty));

	public string VersionText
	{
		get => (string)GetValue(VersionTextProperty);
		set => SetValue(VersionTextProperty, value);
	}

	public ReduxStartupWindow()
	{
		InitializeComponent();
		// Read from disk rather than from the view model: the splash is shown before
		// MainWindowViewModel.LoadSettings runs, so the view model's theme is still the
		// default at this point. Doing it here means the first frame is already correct.
		ReduxThemeService.ApplyPersistedTheme(Resources);
		FontFamily = ReduxTypographyService.ResolveFontFamily(ReduxTypographyFont.Manrope);
		FontSize = 12;
		VersionText = $"v{DivinityApp.REDUX_DISPLAY_VERSION}";
		// The native window remains opaque throughout. Animating only its content
		// reveals the same solid Redux background instead of the desktop.
		if (SystemParameters.ClientAreaAnimation)
		{
			StartupForeground.Opacity = 0;
			((TranslateTransform)StartupForeground.RenderTransform).Y = ForegroundRiseDistance;
		}

		SourceInitialized += OnSourceInitialized;
		ContentRendered += (_, _) => _contentRendered = true;
	}

	/// <summary>
	/// Binds the splash to the startup progress. The theme is deliberately not applied here:
	/// this runs before the view model has loaded settings, so reading its theme would replace
	/// the correct persisted palette with the default one.
	/// </summary>
	public void Attach(MainWindowViewModel viewModel)
	{
		DataContext = viewModel;
	}

	public Task CloseWithTransitionAsync()
	{
		if (!SystemParameters.ClientAreaAnimation)
		{
			Close();
			return Task.CompletedTask;
		}

		var completion = new TaskCompletionSource();
		var opacity = new DoubleAnimation(StartupContent.Opacity, 0, ExitDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		var scale = new DoubleAnimation(1, ExitScale, ExitDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		opacity.Completed += (_, _) =>
		{
			Close();
			completion.TrySetResult();
		};
		StartupContent.BeginAnimation(UIElement.OpacityProperty, opacity);
		StartupContent.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
		StartupContent.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scale.Clone());
		return completion.Task;
	}

	/// <summary>
	/// Plays the splash entrance and completes when it has finished. Callers must await this
	/// before starting work that occupies the UI thread.
	/// </summary>
	/// <remarks>
	/// This used to run off <see cref="FrameworkElement.Loaded"/>, which is why it was never
	/// visible: <c>Loaded</c> fires inside <c>Show()</c>, and the caller then built the main
	/// window on the very next dispatcher pass. The animation clock elapsed while the UI
	/// thread was blocked by that construction, so the splash presented one frame at its
	/// start value and the next frame it presented was already the end state.
	/// </remarks>
	public Task PlayEntranceAsync() => _entranceTask ??= PlayEntranceCoreAsync();

	private async Task PlayEntranceCoreAsync()
	{
		if (!SystemParameters.ClientAreaAnimation)
		{
			StartupForeground.Opacity = 1;
			((TranslateTransform)StartupForeground.RenderTransform).Y = 0;
			return;
		}

		// Present a real frame at the start values before the clock begins.
		await WaitForContentRenderedAsync();
		await ReduxWindowBehavior.WaitForRenderFrameAsync();

		var transform = (TranslateTransform)StartupForeground.RenderTransform;
		var completion = new TaskCompletionSource();
		var opacity = new DoubleAnimation(0, 1, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		var rise = new DoubleAnimation(ForegroundRiseDistance, 0, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		opacity.Completed += (_, _) => completion.TrySetResult();

		StartupForeground.BeginAnimation(UIElement.OpacityProperty, opacity);
		transform.BeginAnimation(TranslateTransform.YProperty, rise);
		await completion.Task;
	}

	private Task WaitForContentRenderedAsync()
	{
		if (_contentRendered) return Task.CompletedTask;

		var completion = new TaskCompletionSource();
		EventHandler handler = null;
		handler = (_, _) =>
		{
			ContentRendered -= handler;
			completion.TrySetResult();
		};
		ContentRendered += handler;
		return completion.Task;
	}

	private void OnSourceInitialized(object? sender, EventArgs e)
	{
		// Keep the splash fully opaque while asking the desktop compositor to clip
		// the real window boundary. This avoids the startup artifacts caused by a
		// transparent WPF window while still matching Redux's rounded card language.
		try
		{
			var handle = new WindowInteropHelper(this).Handle;
			var preference = DwmWindowCornerPreferenceRound;
			_ = DwmSetWindowAttribute(
				handle,
				DwmWindowCornerPreferenceAttribute,
				ref preference,
				sizeof(int));
		}
		catch (DllNotFoundException)
		{
			// Older Windows versions retain the rectangular system boundary.
		}
		catch (EntryPointNotFoundException)
		{
			// Older Windows versions retain the rectangular system boundary.
		}
	}

	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(
		IntPtr windowHandle,
		int attribute,
		ref int attributeValue,
		int attributeSize);
}
