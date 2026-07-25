using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using System.Windows;
using System.Windows.Media.Animation;

namespace DivinityModManager.Views;

public partial class ReduxStartupWindow : Window, IReduxTypographyIsolated
{
	private static readonly Duration EntranceDuration = TimeSpan.FromMilliseconds(180);
	private static readonly Duration ExitDuration = TimeSpan.FromMilliseconds(150);

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
		FontFamily = ReduxTypographyService.ResolveFontFamily(ReduxTypographyFont.Manrope);
		FontSize = 12;
		VersionText = $"v{DivinityApp.REDUX_DISPLAY_VERSION}";
		// The splash must be fully opaque on its first rendered frame. Fading the
		// entire transparent window in briefly exposes the desktop beneath the card.
		StartupContent.Opacity = 1;
		Loaded += OnLoaded;
	}

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
		var scaleX = new DoubleAnimation(1, 0.985, ExitDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		var scaleY = scaleX.Clone();
		opacity.Completed += (_, _) =>
		{
			Close();
			completion.TrySetResult();
		};
		StartupContent.BeginAnimation(OpacityProperty, opacity);
		StartupContent.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
		StartupContent.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
		return completion.Task;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (!SystemParameters.ClientAreaAnimation)
		{
			StartupContent.Opacity = 1;
			return;
		}

		var transform = (System.Windows.Media.ScaleTransform)StartupContent.RenderTransform;
		transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		});
		transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		});
	}
}
