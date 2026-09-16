using AdonisUI;
using AdonisUI.Controls;

using DivinityModManager.AppServices;
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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

using WpfScreenHelper;

namespace DivinityModManager.Views;

public partial class MainWindow : AdonisWindow, IViewFor<MainWindowViewModel>, INotifyPropertyChanged
{
	private static MainWindow self;
	public static MainWindow Self => self;

	[DllImport("user32")] public static extern int FlashWindow(IntPtr hwnd, bool bInvert);
	[DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}

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
		if (!_isPreparingStartup && WindowState != WindowState.Minimized && ViewModel?.Settings?.Loaded == true)
		{
			var win = ViewModel.Settings.Window;
			win.Maximized = WindowState == WindowState.Maximized;

			var bounds = WindowState == WindowState.Normal
				? new Rect(Left, Top, Width, Height) : RestoreBounds;
			if (bounds.IsEmpty) return;
			win.X = bounds.Left;
			win.Y = bounds.Top;
			win.Width = bounds.Width;
			win.Height = bounds.Height;

			win.Screen = Screen.AllScreens.IndexOf(Screen.FromHandle(_hwnd.Handle));
		}
	}

	private static IDisposable _saveWindowPositionTask = null;
	private IDisposable _resizeGlowFadeTask = null;
	private int _installDropOverlayTransitionVersion;
	private bool _installDropOverlayShown;
	private DispatcherTimer _installDropWatchdog;
	private DateTime _lastInstallDragOverUtc;
	private Effect _installDropPreviousEffect;
	private double _installDropPreviousOpacity = 1;
	private bool _installDropBackgroundEffectApplied;
	private bool _isPreparingStartup;
	private WindowSettings _deferredStartupWindowSettings;

	private void SaveWindowSettings()
	{
		UpdateWindowSettings();
		ViewModel?.QueueSave();
	}

	private void OnWindowSettingsChanged(object sender, EventArgs e)
	{
		if (_isPreparingStartup) return;
		_saveWindowPositionTask?.Dispose();
		_saveWindowPositionTask = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(500), SaveWindowSettings);
	}

	private void OnWindowResizeFeedback(object sender, SizeChangedEventArgs e) => ShowWindowFrameFeedback();

	private void OnWindowMoveFeedback(object sender, EventArgs e) => ShowWindowFrameFeedback();

	private void ShowWindowFrameFeedback()
	{
		if (!IsLoaded || WindowState == WindowState.Maximized || ReduxWindowBehavior.ReduceMotion)
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
				Duration = TimeSpan.FromMilliseconds(180)
			};
			fade.Completed += (_, _) =>
			{
				WindowResizeGlow.Opacity = 0;
				WindowResizeGlow.BeginAnimation(OpacityProperty, null);
			};
			WindowResizeGlow.BeginAnimation(OpacityProperty, fade);
		});
	}

	private static bool TryGetExternalInstallDropPaths(DragEventArgs e, out string[] paths)
	{
		paths = [];
		if (e?.Data?.GetDataPresent(DataFormats.FileDrop) != true
			|| e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths
			|| droppedPaths.Length == 0)
			return false;
		paths = droppedPaths;
		return true;
	}

	private static bool IsSupportedExternalInstallDrop(string path) =>
		MainWindowViewModel.IsSupportedDownloadManagerInput(path)
		|| Bg3SaveGameService.IsSupportedSaveInput(path);

	private void MainWindow_PreviewDragEnter(object sender, DragEventArgs e) => UpdateExternalInstallDrag(e);

	private void MainWindow_PreviewDragOver(object sender, DragEventArgs e) => UpdateExternalInstallDrag(e);

	private void UpdateExternalInstallDrag(DragEventArgs e)
	{
		if (ReduxWindowBehavior.HasActiveChild(this)) { HideInstallDropOverlay(); e.Effects = DragDropEffects.None; e.Handled = true; return; }
		if (!TryGetExternalInstallDropPaths(e, out var paths)) return;
		_lastInstallDragOverUtc = DateTime.UtcNow;
		var acceptsDrop = paths.All(IsSupportedExternalInstallDrop);
		e.Effects = acceptsDrop ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
		if (acceptsDrop) ShowInstallDropOverlay();
		else HideInstallDropOverlay();
	}

	private void MainWindow_PreviewDragLeave(object sender, DragEventArgs e)
	{
		if (!TryGetExternalInstallDropPaths(e, out _)) return;
		_lastInstallDragOverUtc = DateTime.UtcNow;
		EnsureInstallDropWatchdog();
	}

	private void EnsureInstallDropWatchdog()
	{
		if (_installDropWatchdog == null)
		{
			_installDropWatchdog = new DispatcherTimer(DispatcherPriority.Input)
			{
				Interval = TimeSpan.FromMilliseconds(90)
			};
			_installDropWatchdog.Tick += (_, _) =>
			{
				if (!_installDropOverlayShown)
				{
					_installDropWatchdog.Stop();
					return;
				}

				var cursorOutside = true;
				if (GetCursorPos(out var cursor))
				{
					var local = PointFromScreen(new Point(cursor.X, cursor.Y));
					cursorOutside = local.X < 0 || local.Y < 0 || local.X > ActualWidth || local.Y > ActualHeight;
				}
				var dragReleased = DateTime.UtcNow - _lastInstallDragOverUtc > TimeSpan.FromMilliseconds(180)
					&& Mouse.LeftButton != MouseButtonState.Pressed
					&& Mouse.RightButton != MouseButtonState.Pressed;
				if (cursorOutside || dragReleased) HideInstallDropOverlay();
			};
		}
		if (!_installDropWatchdog.IsEnabled) _installDropWatchdog.Start();
	}

	private void ShowInstallDropOverlay()
	{
		if (_installDropOverlayShown) return;
		_installDropOverlayShown = true;
		_lastInstallDragOverUtc = DateTime.UtcNow;
		EnsureInstallDropWatchdog();
		if (!ReduxWindowBehavior.BackgroundEffectsDisabled && !_installDropBackgroundEffectApplied)
		{
			_installDropPreviousEffect = WindowContentLayer.Effect;
			_installDropPreviousOpacity = WindowContentLayer.Opacity;
			WindowContentLayer.Effect = new BlurEffect { Radius = 2.5, RenderingBias = RenderingBias.Performance };
			WindowContentLayer.Opacity = 0.88;
			InstallDropScrim.Visibility = Visibility.Visible;
			_installDropBackgroundEffectApplied = true;
		}
		else if (ReduxWindowBehavior.BackgroundEffectsDisabled)
		{
			InstallDropScrim.Visibility = Visibility.Collapsed;
		}
		var transitionVersion = ++_installDropOverlayTransitionVersion;
		InstallDropOverlay.BeginAnimation(OpacityProperty, null);
		InstallDropOverlay.Visibility = Visibility.Visible;
		if (ReduxWindowBehavior.ReduceMotion)
		{
			InstallDropOverlay.Opacity = 1;
			return;
		}
		var fade = new DoubleAnimation(InstallDropOverlay.Opacity, 1, TimeSpan.FromMilliseconds(130))
		{
			EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
		};
		fade.Completed += (_, _) =>
		{
			if (transitionVersion == _installDropOverlayTransitionVersion)
				InstallDropOverlay.Opacity = 1;
		};
		InstallDropOverlay.BeginAnimation(OpacityProperty, fade);
	}

	private void HideInstallDropOverlay()
	{
		if (!_installDropOverlayShown && InstallDropOverlay.Visibility != Visibility.Visible) return;
		_installDropOverlayShown = false;
		_installDropWatchdog?.Stop();
		RestoreInstallDropBackgroundEffect();
		var transitionVersion = ++_installDropOverlayTransitionVersion;
		InstallDropOverlay.BeginAnimation(OpacityProperty, null);
		if (InstallDropOverlay.Visibility != Visibility.Visible) return;
		if (ReduxWindowBehavior.ReduceMotion)
		{
			InstallDropOverlay.Opacity = 0;
			InstallDropOverlay.Visibility = Visibility.Collapsed;
			return;
		}
		var fade = new DoubleAnimation(InstallDropOverlay.Opacity, 0, TimeSpan.FromMilliseconds(105))
		{
			EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
		};
		fade.Completed += (_, _) =>
		{
			if (transitionVersion != _installDropOverlayTransitionVersion) return;
			InstallDropOverlay.Opacity = 0;
			InstallDropOverlay.Visibility = Visibility.Collapsed;
		};
		InstallDropOverlay.BeginAnimation(OpacityProperty, fade);
	}

	private void RestoreInstallDropBackgroundEffect()
	{
		if (!_installDropBackgroundEffectApplied) return;
		WindowContentLayer.Effect = _installDropPreviousEffect;
		WindowContentLayer.Opacity = _installDropPreviousOpacity;
		_installDropPreviousEffect = null;
		_installDropBackgroundEffectApplied = false;
	}

	private async void MainWindow_PreviewDrop(object sender, DragEventArgs e)
	{
		if (ReduxWindowBehavior.HasActiveChild(this)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
		if (!TryGetExternalInstallDropPaths(e, out var paths)) return;
		HideInstallDropOverlay();
		e.Handled = true;

		try
		{
			var packageFiles = paths.Where(MainWindowViewModel.IsSupportedDownloadManagerInput).ToArray();
			if (packageFiles.Length > 0)
			{
				if (packageFiles.Length != paths.Length)
				{
					ReduxMessageBox.Show(this,
						"This drop mixes supported package files with folders or unsupported files. Add those separately so Download Manager can inspect each package safely.",
						"Separate Package Types", System.Windows.MessageBoxButton.OK,
						System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.OK);
					return;
				}
				await ViewModel.AddLocalPackagesToDownloadManagerAsync(packageFiles);
				await OpenNexusDownloadsAsync();
				return;
			}

			var saveSources = paths
				.Where(Bg3SaveGameService.IsSupportedSaveInput)
				.Select(path => new { Path = path, Names = Bg3SaveGameService.GetImportFolderNames(path) })
				.Where(source => source.Names.Count > 0)
				.ToArray();
			if (saveSources.Length == 0) return;
			if (saveSources.Length != paths.Length)
			{
				ReduxMessageBox.Show(this,
					"This drop contains both save-game and mod files. Drop saves and mods separately so Redux can use the correct installer.",
					"Separate Saves and Mods", System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.OK);
				return;
			}

			var names = saveSources.SelectMany(source => source.Names).ToArray();
			var storyFolder = ViewModel?.SelectedProfile?.Folder == null
				? null
				: Path.Combine(ViewModel.SelectedProfile.Folder, "Savegames", "Story");
			if (ReduxSaveManagerWindow.ConfirmDroppedSaveInstall(this, names, storyFolder))
				MainView.ShowSaveManager(saveSources.Select(source => source.Path));
		}
		catch (Exception ex) when (ex is InvalidDataException or SharpCompress.Common.InvalidFormatException)
		{
			// A non-save or malformed archive remains available to the normal mod-drop path.
		}
		catch (IOException)
		{
			// A locked archive remains available to the normal mod-drop path and its diagnostics.
		}
		catch (Exception ex)
		{
			// Save detection is best-effort. Unexpected archive-reader failures must not
			// prevent the established mod-drop path from handling the same input.
			DivinityApp.Log($"Could not classify dropped files as saves: {ex.Message}");
		}
	}

	public void ApplyWindowPosition(WindowSettings win)
	{
		if (_isPreparingStartup)
		{
			_deferredStartupWindowSettings = win;
			return;
		}

		WindowStartupLocation = WindowStartupLocation.Manual;

		var screens = Screen.AllScreens.ToArray();
		var fallback = win.Maximized && win.Screen >= 0 && win.Screen < screens.Length
			? screens[win.Screen].WorkingArea : SystemParameters.WorkArea;
		var workAreas = win.Maximized ? new[] { fallback } : screens.Select(screen => screen.WorkingArea).ToArray();
		var bounds = WindowPlacementPolicy.Restore(win, workAreas,
			fallback, new Size(Width, Height));
		WindowState = WindowState.Normal;
		Width = bounds.Width;
		Height = bounds.Height;
		Left = bounds.Left;
		Top = bounds.Top;
		if (win.Maximized) WindowState = WindowState.Maximized;
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
			ApplyCurrentTheme(SettingsWindow);
			SettingsWindow.Owner = this;
			SettingsWindow.ApplyAdaptiveDefaultSize(this);
			SettingsWindow.ShowWithTransition();
			ViewModel.Settings.SettingsWindowIsOpen = true;
		}
		else if (!forceOpen)
		{
			SettingsWindow.HideWithTransition();
			ViewModel.Settings.SettingsWindowIsOpen = false;
		}
	}

	public void OpenPreferences(SettingsWindowTab targetTab, bool forceOpen = true)
	{
		SettingsWindow.ViewModel.SelectedTabIndex = targetTab;
		if (!SettingsWindow.IsVisible)
		{
			ApplyCurrentTheme(SettingsWindow);
			SettingsWindow.Owner = this;
			SettingsWindow.ApplyAdaptiveDefaultSize(this);
			SettingsWindow.ShowWithTransition();
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
			ApplyCurrentTheme(AboutWindow);
			AboutWindow.DataContext = ViewModel;
			AboutWindow.Owner = this;
			AboutWindow.ShowWithTransition();
		}
		else
		{
			AboutWindow.HideWithTransition();
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
			ApplyCurrentTheme(HelpWindow);
			HelpWindow.Owner = this;
			HelpWindow.ShowWithTransition();
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
		ReduxThemeService.Apply(window.Resources, ViewModel.Settings.ColorTheme,
			ReduxThemeService.GetActiveTheme(ViewModel.Settings), ViewModel.Settings.UsesGeneratedGradients);
	}

	public void UpdateColorTheme(ReduxThemeType theme, ReduxCustomTheme customTheme = null)
	{
		bool? useBuiltInGradients = customTheme == null && ViewModel?.Settings != null
			? theme == ViewModel.Settings.ColorTheme
				? ViewModel.Settings.UsesGeneratedGradients
				: theme != ReduxThemeType.Parchment
			: null;
		ReduxThemeService.Apply(this.Resources, theme, customTheme, useBuiltInGradients);
		if (SettingsWindow.IsVisible)
		{
			ReduxThemeService.Apply(SettingsWindow.Resources, theme, customTheme, useBuiltInGradients);
		}
		if (AboutWindow?.IsVisible == true)
		{
			ReduxThemeService.Apply(AboutWindow.Resources, theme, customTheme, useBuiltInGradients);
		}
		if (VersionGeneratorWindow?.IsVisible == true)
		{
			ReduxThemeService.Apply(VersionGeneratorWindow.Resources, theme, customTheme, useBuiltInGradients);
		}
		if (UpdateWindow?.IsVisible == true)
		{
			ReduxThemeService.Apply(UpdateWindow.Resources, theme, customTheme, useBuiltInGradients);
		}
		if (HelpWindow?.IsVisible == true)
		{
			ReduxThemeService.Apply(HelpWindow.Resources, theme, customTheme, useBuiltInGradients);
		}
	}

	public void PreviewColorTheme(ReduxThemeType theme, ReduxCustomTheme customTheme = null)
	{
		// MainView inherits semantic palette resources from this window. Keeping one
		// owner avoids applying every theme change twice to the same visible tree.
		UpdateColorTheme(theme, customTheme);
	}

	private bool _closeConfirmed;
	private bool _nxmShutdownReady;
	private bool _nxmShutdownInProgress;
	private bool _updateRestartRequested;

	public void RequestExitForUpdate()
	{
		if (Services.Get<ReduxUpdateLaunchService>()?.HasPendingUpdate != true) return;
		_updateRestartRequested = true;
		Close();
	}

	private bool ConfirmDiscardUnsavedLoadOrder()
	{
		if (_closeConfirmed || ViewModel?.HasUnsavedLoadOrderChanges != true) return true;

		var result = ReduxMessageBox.Show(
			this,
			"You have unsaved load-order changes. Close Redux and discard them?",
			"Discard Unsaved Changes?",
			System.Windows.MessageBoxButton.YesNo,
			System.Windows.MessageBoxImage.Warning,
			System.Windows.MessageBoxResult.No);
		if (result != System.Windows.MessageBoxResult.Yes) return false;

		ViewModel.DiscardUnsavedLoadOrderPresentationChanges();
		_closeConfirmed = true;
		return true;
	}

	private void MainWindow_Closing(object sender, CancelEventArgs e)
	{
		if (!ConfirmDiscardUnsavedLoadOrder())
		{
			if (_updateRestartRequested)
			{
				Services.Get<ReduxUpdateLaunchService>()?.CancelPending();
				Services.Get<AppUpdateWindowViewModel>()?.NotifyUpdateExitCancelled(
					"The update was not applied because Redux remained open. Save or discard your changes, then try again.");
				_updateRestartRequested = false;
			}
			e.Cancel = true;
			return;
		}
		if (_nxmShutdownReady) return;
		e.Cancel = true;
		if (_nxmShutdownInProgress) return;
		_nxmShutdownInProgress = true;
		// Always leave the original WPF closing event before pausing the queue or
		// opening any failure UI. A synchronously completed shutdown must not call
		// Close again while Window is still inside its first Closing event.
		Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
			new Action(() => _ = ShutdownNxmDownloadsAndCloseAsync()));
	}

	private async Task ShutdownNxmDownloadsAndCloseAsync()
	{
		try
		{
			await ViewModel.ShutdownNxmDownloadsAsync();
			_nxmShutdownReady = true;
		}
		catch (Exception ex)
		{
			if (_updateRestartRequested)
			{
				Services.Get<ReduxUpdateLaunchService>()?.CancelPending();
				Services.Get<AppUpdateWindowViewModel>()?.NotifyUpdateExitCancelled(
					"The update was not applied because Redux could not safely finish closing. Try again after the download queue is saved.");
				_updateRestartRequested = false;
			}
			DivinityApp.Log($"Could not stop Nexus downloads during shutdown:\n{ex}");
			ReduxMessageBox.Show(this,
				"Redux could not safely pause and save the download queue. The window will remain open so you can try again.",
				"Shutdown Paused", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.OK);
		}
		finally
		{
			_nxmShutdownInProgress = false;
		}
		if (_nxmShutdownReady) Close();
	}

	private void OnClosed()
	{
		if (ViewModel.Settings.SaveWindowLocation) UpdateWindowSettings();
		ViewModel.SaveSettings();
		if (_updateRestartRequested)
		{
			var launcher = Services.Get<ReduxUpdateLaunchService>();
			if (launcher != null && !launcher.StartPending(out var error) && !String.IsNullOrWhiteSpace(error))
			{
				System.Windows.MessageBox.Show(error, "Redux Update Failed",
					System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
			}
		}
		Application.Current.Shutdown();
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
		LocationChanged += OnWindowMoveFeedback;
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

		RxExceptionHandler.View = this;

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
		UpdateWindow.ViewModel.WhenAnyValue(x => x.IsVisible).Subscribe(b =>
		{
			if (b)
			{
				if (!UpdateWindow.IsVisible)
				{
					ApplyCurrentTheme(UpdateWindow);
					UpdateWindow.Owner = this;
					UpdateWindow.ShowWithTransition();
				}
			}
			else if (UpdateWindow.IsVisible)
			{
				UpdateWindow.HideWithTransition();
			}
		});
		UpdateWindow.Hide();

		this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

		Closing += MainWindow_Closing;
		Closed += (o, e) => OnClosed();
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
			ViewModel.Keys.OpenThemeAppearance.AddAction(() => OpenPreferences(SettingsWindowTab.Appearance));
			ViewModel.Keys.OpenKeybindings.AddAction(() => OpenPreferences(SettingsWindowTab.Keybindings));
			ViewModel.Keys.OpenCommandPalette.AddAction(OpenCommandPalette);
			ViewModel.Keys.ToggleAllActiveSeparators.AddAction(MainView.ToggleAllActiveSeparators);
			ViewModel.Keys.ToggleModFileNames.AddAction(MainView.ModLayout.ToggleModFileNameColumn);
			ViewModel.Keys.OpenSaveGameManager.AddAction(
				() => MainView.ShowSaveManager(),
				ViewModel.WhenAnyValue(x => x.SelectedProfile).Select(profile => profile != null));
			ViewModel.Keys.OpenGameDirectoryModManager.AddAction(
				OpenGameDirectoryModManager,
				ViewModel.WhenAnyValue(x => x.Settings.GameExecutablePath)
					.Select(_ => ReduxGameDirectoryModManagerWindow.CanOpen(ViewModel)));
			ViewModel.Keys.DownloadScriptExtender.AddAction(
				() => OpenGameDirectoryModManager(true),
				ViewModel.WhenAnyValue(x => x.Settings.GameExecutablePath)
					.Select(_ => ReduxGameDirectoryModManagerWindow.CanOpen(ViewModel)));
			ViewModel.Keys.OpenNexusDownloads.AddAction(() => _ = OpenNexusDownloadsAsync());
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
					ApplyCurrentTheme(VersionGeneratorWindow);
					VersionGeneratorWindow.Owner = this;
					VersionGeneratorWindow.ShowWithTransition();
				}
				else
				{
					VersionGeneratorWindow.HideWithTransition();
				}
			});

			this.WhenAnyValue(x => x.ViewModel.MainProgressValue).BindTo(this, view => view.TaskbarItemInfo.ProgressValue);

			MainView.OnActivated();
		});
	}

	private void OpenGameDirectoryModManager()
		=> OpenGameDirectoryModManager(false);

	private void OpenGameDirectoryModManager(bool focusScriptExtender)
	{
		try
		{
			var manager = new ReduxGameDirectoryModManagerWindow(this, ViewModel, focusScriptExtender);
			ReduxWindowBehavior.ShowDialogWithOwnerBackdrop(manager, this);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			ReduxMessageBox.Show(this, ex.Message, "Could Not Open Game-Directory Mod Manager",
				System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error, System.Windows.MessageBoxResult.OK);
		}
	}

	private ReduxNexusDownloadsWindow _nexusDownloadsWindow;

	public async Task OpenNexusDownloadsAsync(bool bringToFront = true)
	{
		try
		{
			await ViewModel.EnsureNxmDownloadsInitializedAsync();
			if (_nexusDownloadsWindow?.IsVisible == true)
			{
				if (bringToFront) BringNexusDownloadsToFront();
				return;
			}
			_nexusDownloadsWindow = new ReduxNexusDownloadsWindow(this, ViewModel);
			_nexusDownloadsWindow.Closed += (_, _) => _nexusDownloadsWindow = null;
			_nexusDownloadsWindow.ShowActivated = bringToFront;
			_nexusDownloadsWindow.Show();
			if (bringToFront) BringNexusDownloadsToFront();
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
		{
			ReduxMessageBox.Show(this, ex.Message, "Could Not Open Download Manager",
				System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error, System.Windows.MessageBoxResult.OK);
		}
	}

	private void BringNexusDownloadsToFront()
	{
		if (_nexusDownloadsWindow == null) return;
		ShowActivated = true;
		_nexusDownloadsWindow.ShowActivated = true;
		if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
		Show();
		Activate();
		_nexusDownloadsWindow.Show();
		_nexusDownloadsWindow.Activate();
		_nexusDownloadsWindow.Topmost = true;
		_nexusDownloadsWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
			new Action(() =>
			{
				if (_nexusDownloadsWindow != null) _nexusDownloadsWindow.Topmost = false;
			}));
	}

	private void OpenCommandPalette()
	{
		var palette = new ReduxCommandPaletteWindow(
			this,
			ViewModel,
			MainView.FocusModEntry,
			BuildMainViewQuickAccessCommands());
		ReduxWindowBehavior.ShowDialogWithOwnerBackdrop(palette, this);
		if (palette.Accepted)
		{
			palette.SelectedItem?.Execute();
		}
	}

	private IReadOnlyList<ReduxCommandPaletteItem> BuildMainViewQuickAccessCommands() =>
	[
		new(
			ViewModel.IsCategoriesExpanded ? "Hide Categories" : "Show Categories",
			"Workspace layout",
			ViewModel.IsCategoriesExpanded
				? "Collapse the Categories pane to leave more room for the mod lists."
				: "Expand the Categories pane.",
			String.Empty,
			"tag",
			() => ViewModel.IsCategoriesExpanded = !ViewModel.IsCategoriesExpanded,
			searchTerms: "toggle collapse expand sidebar category pane visibility"),
		new(
			ViewModel.IsInactiveModsExpanded ? "Hide Inactive Mods" : "Show Inactive Mods",
			"Workspace layout",
			ViewModel.IsInactiveModsExpanded
				? "Collapse the Inactive Mods pane to leave more room for the active load order."
				: "Expand the Inactive Mods pane.",
			String.Empty,
			"list",
			() => ViewModel.IsInactiveModsExpanded = !ViewModel.IsInactiveModsExpanded,
			searchTerms: "toggle collapse expand inactive mods pane visibility"),
		new(
			ViewModel.IsModDetailsExpanded ? "Hide Mod Details" : "Show Mod Details",
			"Workspace layout",
			ViewModel.IsModDetailsExpanded
				? "Collapse the selected mod's details drawer."
				: "Expand the selected mod's details drawer.",
			String.Empty,
			"info",
			() => ViewModel.IsModDetailsExpanded = !ViewModel.IsModDetailsExpanded,
			searchTerms: "toggle collapse expand selected mod details drawer pane visibility"),
		new(
			"Create Custom Category...",
			"Categories",
			"Create a category with its own name, color, icon, and description.",
			String.Empty,
			"palette",
			MainView.ShowCreateCustomCategoryDialog,
			() => !ViewModel.IsLocked,
			searchTerms: "add new custom category tag"),
		new(
			"Edit Selected Category...",
			"Categories",
			"Edit the category currently selected in the category pane.",
			String.Empty,
			"tag",
			MainView.ShowEditSelectedCategoryDialog,
			() => MainView.CanEditSelectedCategory,
			searchTerms: "change rename style color icon category"),
		new(
			"Create Separator...",
			"Separators",
			"Add a new separator or divider after the selected active mod, or at the end of the active list.",
			String.Empty,
			"marker-diamond",
			MainView.ShowAddActiveSeparatorDialog,
			() => ViewModel.IsInitialized && !ViewModel.IsLocked,
			searchTerms: "add insert new divider section marker"),
		new(
			"Collapse All Separators",
			"Separators",
			"Collapse every separator in the active load order.",
			String.Empty,
			"list",
			() => MainView.SetAllActiveSeparatorsCollapsed(true),
			() => ViewModel.CanSetAllVisualDividersCollapsed(activeList: true, collapsed: true),
			searchTerms: "close hide fold divider sections"),
		new(
			"Expand All Separators",
			"Separators",
			"Expand every separator in the active load order.",
			String.Empty,
			"list",
			() => MainView.SetAllActiveSeparatorsCollapsed(false),
			() => ViewModel.CanSetAllVisualDividersCollapsed(activeList: true, collapsed: false),
			searchTerms: "open show unfold divider sections"),
		new(
			"Open Load Order Folder",
			"Load orders and files",
			"Open the folder containing saved load orders.",
			String.Empty,
			"folder",
			() => ViewModel.OpenLoadOrderFolderCommand.Execute(null),
			() => ViewModel.OpenLoadOrderFolderCommand?.CanExecute(null) == true,
			searchTerms: "browse directory saved orders"),
		new(
			"Open Save Games Folder",
			"Folders",
			"Open the selected profile's story save folder.",
			String.Empty,
			"folder",
			MainView.OpenSaveGamesFolder,
			() => ViewModel.SelectedProfile != null,
			searchTerms: "browse directory saves profile"),
		new(
			"Inspect Mod Package...",
			"Tools",
			"Inspect a PAK or release archive without installing it.",
			String.Empty,
			"package",
			MainView.ShowInspectModPackageDialog,
			() => !ViewModel.IsLocked,
			searchTerms: "scan preflight validate pak zip archive"),
		new(
			"Generate Redux Database Contribution...",
			"Help",
			"Create a privacy-limited contribution report from installed mods.",
			String.Empty,
			"database",
			MainView.ShowGenerateReduxDatabaseContributionDialog,
			() => !ViewModel.IsLocked && ViewModel.UserMods.Any(mod => mod != null && !mod.IsVisualDivider),
			searchTerms: "create export report metadata contribute"),
		new(
			"Welcome Setup...",
			"Help",
			"Reopen Redux's guided setup.",
			String.Empty,
			"sparkles",
			() => ViewModel.ShowReduxWelcome(),
			searchTerms: "onboarding first run configure"),
		new(
			"Report a Bug...",
			"Help",
			"Open the Redux issue report page in your browser.",
			String.Empty,
			"bug",
			() => ProcessHelper.TryOpenUrl(DivinityApp.URL_REDUX_BUG_REPORT),
			searchTerms: "issue problem github feedback")
	];

	public void PreviewCustomThemeColors(ReduxCustomTheme customTheme)
	{
		ReduxThemeService.PreviewColors(this.Resources, customTheme);
	}

	public void PrepareForStartup()
	{
		_isPreparingStartup = true;
		ShowActivated = false;
		ShowInTaskbar = false;
		WindowStartupLocation = WindowStartupLocation.Manual;
		Left = -32000;
		Top = -32000;
	}

	public async Task PrepareVisualTreeForRevealAsync()
	{
		// The native window remains fully opaque but off-screen. This avoids the
		// black surface Windows presents for a transparent top-level WPF window,
		// while still allowing the complete visual tree to measure and render.
		MainGrid.UpdateLayout();
		await Dispatcher.InvokeAsync(() => MainGrid.UpdateLayout(), DispatcherPriority.ContextIdle);
		await WaitForRenderingAsync();
		await WaitForRenderingAsync();
	}

	public void RevealAfterStartup()
	{
		_isPreparingStartup = false;
		if (_deferredStartupWindowSettings != null)
		{
			var settings = _deferredStartupWindowSettings;
			_deferredStartupWindowSettings = null;
			ApplyWindowPosition(settings);
		}
		else
		{
			var workArea = SystemParameters.WorkArea;
			Left = workArea.Left + Math.Max(0, (workArea.Width - ActualWidth) / 2);
			Top = workArea.Top + Math.Max(0, (workArea.Height - ActualHeight) / 2);
		}

		ShowInTaskbar = true;
	}

	private static Task WaitForRenderingAsync()
	{
		var completion = new TaskCompletionSource();
		EventHandler handler = null;
		handler = (_, _) =>
		{
			CompositionTarget.Rendering -= handler;
			completion.TrySetResult();
		};
		CompositionTarget.Rendering += handler;
		return completion.Task;
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
