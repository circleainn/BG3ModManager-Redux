using AdonisUI;
using AdonisUI.Controls;

using AutoUpdaterDotNET;

using DivinityModManager.Controls;
using DivinityModManager.Extensions;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Util.ScreenReader;
using DivinityModManager.ViewModels;

using DynamicData;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

using WpfScreenHelper;

namespace DivinityModManager.Views;

public partial class MainWindow : AdonisWindow, IViewFor<MainWindowViewModel>, INotifyPropertyChanged
{
	private static MainWindow self;
	public static MainWindow Self => self;

	[DllImport("user32")] public static extern int FlashWindow(IntPtr hwnd, bool bInvert);

	public MainViewControl MainView { get; private set; }

	public SettingsWindow SettingsWindow { get; private set; }
	public AboutWindow AboutWindow { get; private set; }
	public VersionGeneratorWindow VersionGeneratorWindow { get; private set; }
	public AppUpdateWindow UpdateWindow { get; private set; }
	public HelpWindow HelpWindow { get; private set; }

	public event PropertyChangedEventHandler PropertyChanged;

	private MainWindowViewModel viewModel;
	public MainWindowViewModel ViewModel
	{
		get => viewModel;
		set
		{
			viewModel = value;
			// ViewModel is POCO type warning suppression
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ViewModel"));
		}
	}

	object IViewFor.ViewModel
	{
		get => ViewModel;
		set => ViewModel = (MainWindowViewModel)value;
	}

	private readonly System.Windows.Interop.WindowInteropHelper _hwnd;

	public LogTraceListener DebugLogListener { get; private set; }

	private readonly string _logsDir;
	private readonly string _logFileName;

	public AlertBar AlertBar => MainView.AlertBar;

	public void ToggleLogging(bool enabled)
	{
		if (enabled || ViewModel?.DebugMode == true)
		{
			if (DebugLogListener == null)
			{
				if (!_logsDir.IsExistingDirectory())
				{
					Directory.CreateDirectory(_logsDir);
					DivinityApp.Log($"Creating logs directory: {_logsDir}");
				}

				DebugLogListener = new LogTraceListener(_logFileName, "DebugLogListener");
				Trace.Listeners.Add(DebugLogListener);
				Trace.AutoFlush = true;
			}
		}
		else if (DebugLogListener != null && ViewModel?.DebugMode != true)
		{
			Trace.Listeners.Remove(DebugLogListener);
			DebugLogListener.Dispose();
			DebugLogListener = null;
			Trace.AutoFlush = false;
		}
	}

	public void DisplayError(string msg)
	{
		ToggleLogging(true);
		DivinityApp.Log(msg);
		var result = ReduxMessageBox.Show(msg,
			"Open the logs folder?",
			System.Windows.MessageBoxButton.YesNo,
			System.Windows.MessageBoxImage.Error,
			System.Windows.MessageBoxResult.No);
		if (result == System.Windows.MessageBoxResult.Yes)
		{
			ProcessHelper.TryOpenPath("_Logs");
		}
	}

	public void DisplayError(string msg, string caption, bool showLog = false)
	{
		if (!showLog)
		{
			ReduxMessageBox.Show(msg, caption,
			System.Windows.MessageBoxButton.OK,
			System.Windows.MessageBoxImage.Warning,
			System.Windows.MessageBoxResult.OK);
		}
		else
		{
			DisplayError(msg);
		}
	}

	private void OnUIException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		e.Handled = true;
		ToggleLogging(true);
		var doShutdown = ViewModel?.IsInitialized != true;
		var shutdownText = doShutdown ? " The program will close." : "";
		DivinityApp.Log($"An exception in the UI occurred.{shutdownText}\n{e.Exception}");

		var result = ReduxMessageBox.Show($"An exception in the UI occurred.{shutdownText}\n{e.Exception}",
			"Open the logs folder?",
			System.Windows.MessageBoxButton.YesNo,
			System.Windows.MessageBoxImage.Error,
			System.Windows.MessageBoxResult.No);
		if (result == System.Windows.MessageBoxResult.Yes)
		{
			ProcessHelper.TryOpenPath("_Logs");
		}

		//Shutdown if we had an exception when loading.
		if (doShutdown)
		{
			App.Current.Shutdown(1);
		}
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		ToggleLogging(true);
		var doShutdown = ViewModel?.IsInitialized != true;
		var shutdownText = doShutdown ? " The program will close." : "";

		DivinityApp.Log($"An unhandled exception occurred.{shutdownText}\n{e.ExceptionObject}");
		var result = ReduxMessageBox.Show($"An unhandled exception occurred.{shutdownText}\n{e.ExceptionObject}",
			"Open the logs folder?",
			System.Windows.MessageBoxButton.YesNo,
			System.Windows.MessageBoxImage.Error,
			System.Windows.MessageBoxResult.No);
		if (result == System.Windows.MessageBoxResult.Yes)
		{
			ProcessHelper.TryOpenPath("_Logs");
		}

		if (doShutdown)
		{
			App.Current.Shutdown(1);
		}
	}

	private void UpdateWindowSettings()
	{
		if (ViewModel?.Settings?.Loaded == true)
		{
			var win = ViewModel.Settings.Window;
			win.Maximized = WindowState == WindowState.Maximized;

			win.X = Left;
			win.Y = Top;
			win.Width = Width;
			win.Height = Height;

			win.Screen = Screen.AllScreens.IndexOf(Screen.FromHandle(_hwnd.Handle));
		}
	}

	private static IDisposable _saveWindowPositionTask = null;
	private IDisposable _resizeGlowFadeTask = null;

	private void SaveWindowSettings()
	{
		UpdateWindowSettings();
		ViewModel?.QueueSave();
	}

	private void OnWindowSettingsChanged(object sender, EventArgs e)
	{
		_saveWindowPositionTask?.Dispose();
		_saveWindowPositionTask = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(500), SaveWindowSettings);
	}

	private void OnWindowResizeFeedback(object sender, SizeChangedEventArgs e)
	{
		if (!IsLoaded || WindowState == WindowState.Maximized)
		{
			WindowResizeGlow.Opacity = 0;
			return;
		}

		WindowResizeGlow.BeginAnimation(OpacityProperty, null);
		WindowResizeGlow.Opacity = 0.9;
		_resizeGlowFadeTask?.Dispose();
		_resizeGlowFadeTask = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(220), () =>
		{
			var fade = new DoubleAnimation
			{
				From = WindowResizeGlow.Opacity,
				To = 0,
				Duration = TimeSpan.FromMilliseconds(180),
				FillBehavior = FillBehavior.HoldEnd
			};
			WindowResizeGlow.BeginAnimation(OpacityProperty, fade);
		});
	}

	public void ApplyWindowPosition(WindowSettings win)
	{
		WindowStartupLocation = WindowStartupLocation.Manual;

		if (win.Maximized)
		{
			if (win.Screen > -1)
			{
				var screens = Screen.AllScreens.ToArray();
				if (win.Screen < screens.Length)
				{
					var screen = screens[win.Screen];
					WindowHelper.SetWindowPosition(this, WpfScreenHelper.Enum.WindowPositions.Maximize, screen);
				}
			}
			WindowState = WindowState.Maximized;
		}
		else if (win.X > -1 || win.Y > -1 || win.Width > -1 || win.Height > -1)
		{
			var winX = win.X;
			var winY = win.Y;
			var width = win.Width;
			var height = win.Height;

			if (width <= 0) win.Width = width = 1440;
			if (height <= 0) win.Height = height = 900;
			if (winX < 0) winX = Left;
			if (winY < 0) winY = Top;

			Width = width;
			Height = height;
			Left = winX;
			Top = winY;
		}
	}

	public void ToggleWindowPositionSaving(bool b)
	{
		if (b)
		{
			StateChanged += OnWindowSettingsChanged;
			LocationChanged += OnWindowSettingsChanged;
			SizeChanged += OnWindowSettingsChanged;
			UpdateWindowSettings();
			ViewModel.QueueSave();
		}
		else
		{
			StateChanged -= OnWindowSettingsChanged;
			LocationChanged -= OnWindowSettingsChanged;
			SizeChanged -= OnWindowSettingsChanged;
			_saveWindowPositionTask?.Dispose();
		}
	}

	public void OpenPreferences(bool switchToKeybindings = false, bool forceOpen = false)
	{
		if (switchToKeybindings)
		{
			OpenPreferences(SettingsWindowTab.Keybindings, forceOpen);
			return;
		}

		if (!SettingsWindow.IsVisible)
		{
			SettingsWindow.Owner = this;
			SettingsWindow.ApplyAdaptiveDefaultSize(this);
			SettingsWindow.Show();
			ViewModel.Settings.SettingsWindowIsOpen = true;
		}
		else if (!forceOpen)
		{
			SettingsWindow.Hide();
			ViewModel.Settings.SettingsWindowIsOpen = false;
		}
	}

	public void OpenPreferences(SettingsWindowTab targetTab, bool forceOpen = true)
	{
		SettingsWindow.ViewModel.SelectedTabIndex = targetTab;
		if (!SettingsWindow.IsVisible)
		{
			SettingsWindow.Show();
			SettingsWindow.Owner = this;
			ViewModel.Settings.SettingsWindowIsOpen = true;
		}
		else
		{
			SettingsWindow.Activate();
		}
	}

	private void ToggleAboutWindow()
	{
		if (AboutWindow == null)
		{
			AboutWindow = new AboutWindow();
			ApplyCurrentTheme(AboutWindow);
		}

		if (!AboutWindow.IsVisible)
		{
			AboutWindow.DataContext = ViewModel;
			AboutWindow.Show();
			AboutWindow.Owner = this;
		}
		else
		{
			AboutWindow.Hide();
		}
	}

	public void ShowHelpWindow(string title, string helpText)
	{
		if (HelpWindow == null)
		{
			HelpWindow = new HelpWindow();
			ApplyCurrentTheme(HelpWindow);
		}

		HelpWindow.ViewModel.HelpTitle = title;
		HelpWindow.ViewModel.HelpText = helpText;

		if (!HelpWindow.IsVisible)
		{
			HelpWindow.Show();
			HelpWindow.Owner = this;
		}
	}

	private static System.Windows.Shell.TaskbarItemProgressState BoolToTaskbarItemProgressState(bool b)
	{
		return b ? System.Windows.Shell.TaskbarItemProgressState.Normal : System.Windows.Shell.TaskbarItemProgressState.None;
	}

	protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
	{
		return new CachedAutomationPeer(this);
	}

	/// <summary>
	/// Lazily-created windows (About, Help, Update, Version Generator) only receive their theme
	/// retroactively if the user happens to switch themes after opening one for the first time.
	/// Call this immediately after constructing any such window so it's correct on first show.
	/// </summary>
	private void ApplyCurrentTheme(Window window)
	{
		if (ViewModel?.Settings == null) return;
		ReduxThemeService.Apply(window.Resources, ViewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(ViewModel.Settings));
	}

	public void UpdateColorTheme(ReduxThemeType theme, ReduxCustomTheme customTheme = null)
	{
		ReduxThemeService.Apply(this.Resources, theme, customTheme);
		ReduxThemeService.Apply(SettingsWindow.Resources, theme, customTheme);
		if (AboutWindow != null)
		{
			ReduxThemeService.Apply(AboutWindow.Resources, theme, customTheme);
		}
		if (VersionGeneratorWindow != null)
		{
			ReduxThemeService.Apply(VersionGeneratorWindow.Resources, theme, customTheme);
		}
		if (UpdateWindow != null)
		{
			ReduxThemeService.Apply(UpdateWindow.Resources, theme, customTheme);
		}
		if (HelpWindow != null)
		{
			ReduxThemeService.Apply(HelpWindow.Resources, theme, customTheme);
		}
	}

	private void OnClosing()
	{
		if (ViewModel.Settings.SaveWindowLocation) UpdateWindowSettings();
		ViewModel.SaveSettings();
		Application.Current.Shutdown();
	}

	private void AutoUpdater_OnClosing()
	{
		ViewModel.Settings.LastUpdateCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
		OnClosing();
	}

	private WindowInteropHelper _wih;

	public void FlashTaskbar()
	{
		FlashWindow(_wih.Handle, true);
	}

	public MainWindow()
	{
		InitializeComponent();
		ApplyAdaptiveDefaultSize();
		SizeChanged += OnWindowResizeFeedback;
		self = this;

		_logsDir = DivinityApp.GetAppDirectory("_Logs");
		var sysFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace("/", "-");
#if DEBUG
		_logFileName = Path.Combine(_logsDir, "debug_" + DateTime.Now.ToString(sysFormat + "_HH-mm-ss") + ".log");
#else
		_logFileName = Path.Combine(_logsDir, "release_" + DateTime.Now.ToString(sysFormat + "_HH-mm-ss") + ".log");
#endif

		if (File.Exists(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "debug")))
		{
			ToggleLogging(true);
			DivinityApp.Log("Enable logging due to the debug file next to the exe.");
		}

		_hwnd = new System.Windows.Interop.WindowInteropHelper(this);

		Application.Current.DispatcherUnhandledException += OnUIException;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

		DivinityApp.DateTimeColumnFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
		DivinityApp.DateTimeTooltipFormat = CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern;

		RxExceptionHandler.view = this;

		ViewModel = new MainWindowViewModel();
		MainView = new MainViewControl(this, ViewModel);
		MainGrid.Children.Add(MainView);

		SettingsWindow = new SettingsWindow();
		SettingsWindow.Closed += delegate
		{
			if (ViewModel?.Settings != null)
			{
				ViewModel.Settings.SettingsWindowIsOpen = false;
			}
		};
		SettingsWindow.Hide();
		SettingsWindow.Init(ViewModel);

		UpdateWindow = new AppUpdateWindow();
		ApplyCurrentTheme(UpdateWindow);
		UpdateWindow.ViewModel.AppTitle = ViewModel.AppTitle;
		UpdateWindow.ViewModel.AppVersion = ViewModel.Version;
		UpdateWindow.ViewModel.WhenAnyValue(x => x.IsVisible).Subscribe(b =>
		{
			if (b)
			{
				if (!UpdateWindow.IsVisible)
				{
					UpdateWindow.Show();
					UpdateWindow.Owner = this;
				}
			}
			else if (UpdateWindow.IsVisible)
			{
				UpdateWindow.Hide();
			}
		});
		UpdateWindow.Hide();

		this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

		Closed += (o, e) => OnClosing();
		AutoUpdater.ApplicationExitEvent += AutoUpdater_OnClosing;

		DataContext = ViewModel;

		_wih = new WindowInteropHelper(this);

		if (DebugLogListener != null)
		{
			ViewModel.DebugMode = true;
		}

		this.WhenActivated(d =>
		{
			ViewModel.OnViewActivated(this, MainView);
			this.WhenAnyValue(x => x.ViewModel.Title).BindTo(this, view => view.Title);
			this.OneWayBind(ViewModel, vm => vm.MainProgressIsActive, view => view.TaskbarItemInfo.ProgressState, BoolToTaskbarItemProgressState);

			ViewModel.Keys.OpenPreferences.AddAction(() => OpenPreferences(false));
			ViewModel.Keys.OpenKeybindings.AddAction(() => OpenPreferences(SettingsWindowTab.Keybindings));
			ViewModel.Keys.OpenAboutWindow.AddAction(ToggleAboutWindow);

			ViewModel.Keys.ToggleVersionGeneratorWindow.AddAction(() =>
			{
				if (VersionGeneratorWindow == null)
				{
					VersionGeneratorWindow = new VersionGeneratorWindow();
					ApplyCurrentTheme(VersionGeneratorWindow);
				}

				if (!VersionGeneratorWindow.IsVisible)
				{
					VersionGeneratorWindow.Show();
					VersionGeneratorWindow.Owner = this;
				}
				else
				{
					VersionGeneratorWindow.Hide();
				}
			});

			this.WhenAnyValue(x => x.ViewModel.MainProgressValue).BindTo(this, view => view.TaskbarItemInfo.ProgressValue);

			MainView.OnActivated();
		});
	}

	private void ApplyAdaptiveDefaultSize()
	{
		var workArea = SystemParameters.WorkArea;
		var targetWidth = Math.Clamp(workArea.Width * 0.72, 1100, 1800);
		var targetHeight = Math.Clamp(workArea.Height * 0.80, 700, 1050);
		Width = Math.Max(MinWidth, Math.Min(targetWidth, workArea.Width - 32));
		Height = Math.Max(MinHeight, Math.Min(targetHeight, workArea.Height - 32));
	}
}
