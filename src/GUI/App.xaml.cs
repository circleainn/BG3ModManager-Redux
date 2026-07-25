

using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;

using AutoUpdaterDotNET;

using System.Globalization;
using System.ComponentModel;
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
	public App()
	{
		Services.RegisterSingleton<IFileWatcherService>(new FileWatcherService());
		Services.RegisterSingleton<IScreenReaderService>(new ScreenReaderService());

		var client = new HttpClient();
		client.DefaultRequestHeaders.Add("User-Agent", AppDomain.CurrentDomain.FriendlyName);
		Services.RegisterSingleton(client);

		var appUpdateVM = new AppUpdateWindowViewModel();

		AutoUpdater.HttpUserAgent = "BG3ModManagerRedux";
		AutoUpdater.RunUpdateAsAdmin = false;
		AutoUpdater.Synchronous = false;
		AutoUpdater.CheckForUpdateEvent += (e) =>
		{
			RxApp.TaskpoolScheduler.Schedule(() =>
			{
				appUpdateVM.OnUpdateCheckCommand.Execute(e).Subscribe();
			});
		};

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

		var startupWindow = new ReduxStartupWindow();
		MainWindow = startupWindow;
		startupWindow.Show();

		// Let the compact startup surface render before constructing the much larger
		// main visual tree. The main window still drives the existing initialization
		// pipeline; it is simply prepared out of sight until that pipeline completes.
		Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
		{
			var mainWindow = new MainWindow();
			mainWindow.PrepareForStartup();
			startupWindow.Attach(mainWindow.ViewModel);

			var revealStarted = false;
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
				mainWindow.RevealAfterStartup();
				await startupWindow.CloseWithTransitionAsync();
				mainWindow.Activate();
				await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
				mainWindow.ViewModel.CompleteStartupPresentation();
			};
			mainWindow.ViewModel.PropertyChanged += initializedHandler;

			MainWindow = mainWindow;
			mainWindow.Show();
			startupWindow.Owner = mainWindow;
			startupWindow.Activate();
		}));
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
