

using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;

using System.Globalization;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace DivinityModManager;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	// The main window's arrival is a hard cut -- it is opaque and its bounds are not animated,
	// so nothing can soften the HWND appearing. What the fade can do is let the UI resolve out
	// of the window's own surface colour rather than landing fully formed. A shallow value
	// (0.85) is invisible against an opaque background; this needs to be deep enough to read.
	private const double StartupRevealOpacity = 0.4;
	private static readonly Duration StartupRevealDuration = TimeSpan.FromMilliseconds(280);

	private readonly ConcurrentQueue<string> _pendingNxmActivations;

	public App(ConcurrentQueue<string> pendingNxmActivations = null)
	{
		_pendingNxmActivations = pendingNxmActivations ?? new ConcurrentQueue<string>();
		Services.RegisterSingleton<IFileWatcherService>(new FileWatcherService());
		Services.RegisterSingleton<IScreenReaderService>(new ScreenReaderService());

		var client = new HttpClient();
		client.DefaultRequestHeaders.Add("User-Agent", AppDomain.CurrentDomain.FriendlyName);
		Services.RegisterSingleton(client);

		var updateChannel = new ReduxUpdateChannelService(client);
		Services.RegisterSingleton(updateChannel);
		var updatePackages = new ReduxUpdatePackageService(client);
		Services.RegisterSingleton(updatePackages);
		var updateLauncher = new ReduxUpdateLaunchService();
		Services.RegisterSingleton(updateLauncher);
		var appUpdateVM = new AppUpdateWindowViewModel(updateChannel, updatePackages, updateLauncher);
		Services.RegisterSingleton(appUpdateVM);

		// POCO type warning suppression
		Services.Register<ICreatesObservableForProperty>(() => new DivinityModManager.Util.CustomPropertyResolver());
#if DEBUG
		RxApp.SuppressViewCommandBindingMessage = false;
#else
		RxApp.DefaultExceptionHandler = new RxExceptionHandler();
		RxApp.SuppressViewCommandBindingMessage = true;
#endif
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		//For making date display use the current system's culture
		FrameworkElement.LanguageProperty.OverrideMetadata(
			typeof(FrameworkElement),
			new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

		EventManager.RegisterClassHandler(typeof(Window), Window.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown));
		EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));

		var protocolStartup = !_pendingNxmActivations.IsEmpty;
		var startupWindow = new ReduxStartupWindow();
		MainWindow = startupWindow;
		if (!protocolStartup) startupWindow.Show();

		// Let the compact startup surface render before constructing the much larger
		// main visual tree. The main window still drives the existing initialization
		// pipeline; it is simply prepared out of sight until that pipeline completes.
		Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(async () =>
		{
			// Building MainWindow occupies the UI thread for much longer than the splash
			// entrance lasts, and an animation whose clock runs while no frames are being
			// presented is never seen. Let the entrance finish before that work starts.
			if (!protocolStartup) await startupWindow.PlayEntranceAsync();

			var mainWindow = new MainWindow();
			mainWindow.PrepareForStartup();
			startupWindow.Attach(mainWindow.ViewModel);

			var revealStarted = false;
			startupWindow.Closed += (_, _) =>
			{
				if (!revealStarted && !Dispatcher.HasShutdownStarted) Shutdown();
			};
			PropertyChangedEventHandler initializedHandler = null;
			initializedHandler = async (_, args) =>
			{
				if (args.PropertyName != nameof(MainWindowViewModel.IsInitialized) ||
					!mainWindow.ViewModel.IsInitialized ||
					revealStarted)
				{
					return;
				}

				revealStarted = true;
				mainWindow.ViewModel.PropertyChanged -= initializedHandler;
				await mainWindow.PrepareVisualTreeForRevealAsync();
				if (protocolStartup && !mainWindow.ViewModel.Settings.BringNxmDownloadsToFront)
				{
					// Restore the real bounds while hidden, then expose only a taskbar entry.
					mainWindow.Hide();
					mainWindow.RevealAfterStartup();
					mainWindow.WindowState = WindowState.Minimized;
					mainWindow.Show();
					startupWindow.Close();
					mainWindow.ViewModel.NotifyMainWindowReady();
					mainWindow.ViewModel.ShowReduxWelcome(onlyIfUnseen: true);
					return;
				}
				// Crossfade the prepared main surface with the splash. Both top-level
				// windows stay opaque; only their WPF content roots animate. Main starts
				// only slightly transparent -- its window background is opaque, so a low
				// starting opacity reads as a large blank rectangle rather than a fade.
				ReduxWindowBehavior.PrepareEntrance(mainWindow, StartupRevealOpacity);
				mainWindow.RevealAfterStartup();
				// RevealAfterStartup moves the window onscreen; let that frame present at
				// the start opacity before the entrance clock begins.
				await ReduxWindowBehavior.WaitForRenderFrameAsync();

				await Task.WhenAll(
					ReduxWindowBehavior.AnimateEntranceAsync(mainWindow, StartupRevealOpacity, StartupRevealDuration),
					protocolStartup ? Task.CompletedTask : startupWindow.CloseWithTransitionAsync());
				if (protocolStartup) startupWindow.Close();

				mainWindow.Activate();
				await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
				mainWindow.ViewModel.NotifyMainWindowReady();
				mainWindow.ViewModel.ShowReduxWelcome(onlyIfUnseen: true);
			};
			mainWindow.ViewModel.PropertyChanged += initializedHandler;

			MainWindow = mainWindow;
			mainWindow.Show();
			DrainNxmActivations();
			if (!protocolStartup) startupWindow.Activate();
		}));
	}

	public void NotifyNxmActivationAvailable()
	{
		Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
		{
			if (MainWindow is Views.MainWindow mainWindow
				&& mainWindow.ViewModel.Settings.BringNxmDownloadsToFront)
			{
				mainWindow.ShowActivated = true;
				if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
				mainWindow.Show();
				mainWindow.Activate();
			}
			DrainNxmActivations();
		}));
	}

	private void DrainNxmActivations()
	{
		if (MainWindow is not Views.MainWindow mainWindow) return;
		while (_pendingNxmActivations.TryDequeue(out var value))
			mainWindow.ViewModel.EnqueueNxmActivation(value);
	}

	private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		DivinityApp.IsKeyboardNavigating = false;
	}

	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down
			or Key.Home or Key.End or Key.PageUp or Key.PageDown)
		{
			DivinityApp.IsKeyboardNavigating = true;
		}
	}
}
