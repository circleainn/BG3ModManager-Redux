

using DivinityModManager.AppServices;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;

using AutoUpdaterDotNET;

using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

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

		var mainWindow = new MainWindow();
		mainWindow.Show();
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
