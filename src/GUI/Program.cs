using DivinityModManager.Util.ScreenReader;

using DivinityModManager.AppServices;
using DivinityModManager.Util;

using System.Collections.Concurrent;
using System.Reflection;

namespace DivinityModManager;

internal class Program
{
	private static string _libDirectory;
	private static int _fatalStartupExceptionState;

	private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
	{
		var assyName = new AssemblyName(args.Name);

		var newPath = Path.Combine(_libDirectory, assyName.Name);
		if (!newPath.EndsWith(".dll"))
		{
			newPath += ".dll";
		}

		if (File.Exists(newPath))
		{
			var assy = Assembly.LoadFile(newPath);
			return assy;
		}
		return null;
	}

	private static void OnAppExit(object sender, EventArgs e)
	{
		//CrossSpeakManager: Make sure to always call the Close() method before your application closes.
		Services.ScreenReader?.Close();
	}

	/// <summary>
	/// Safety net for exceptions thrown before MainWindow wires up its own (richer) handlers -
	/// e.g. during the App() constructor, OnStartup, or MainWindow's InitializeComponent().
	/// MainWindow unsubscribes these once it takes over, so there's no double-handling.
	/// </summary>
	internal static void OnEarlyUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		ReportFatalStartupException(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
	}

	internal static void OnEarlyDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		e.Handled = true;
		ReportFatalStartupException(e.Exception);
	}

	private static void ReportFatalStartupException(Exception ex)
	{
		// Dispatcher rendering failures can be raised repeatedly while the first
		// modal error is open. Never allow one fatal startup failure to create an
		// unbounded cascade of MessageBox windows.
		if (Interlocked.CompareExchange(ref _fatalStartupExceptionState, 1, 0) != 0)
		{
			Environment.Exit(1);
			return;
		}

		try
		{
			var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_Logs");
			Directory.CreateDirectory(logDirectory);
			File.AppendAllText(Path.Combine(logDirectory, "startup_crash.log"), $"{DateTime.Now}: {ex}\n\n");
		}
		catch { }

		System.Windows.MessageBox.Show($"A fatal error occurred while starting up and the application must close.\n\n{ex.Message}",
			"Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

		Environment.Exit(1);
	}

	[STAThread]
	static void Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += OnEarlyUnhandledException;

		_libDirectory = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "_Lib");
		AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
		RunApplication(args);
	}

	private static void RunApplication(string[] args)
	{
		var launchTarget = ReduxLaunchTarget.Current();
		string initialNxmLink = null;
		if (args.Length >= 2 && args[0].Equals("--nxm", StringComparison.OrdinalIgnoreCase))
		{
			if (args.Length != 2 || !NexusModManagerLinkParser.TryReadGame(args[1], out var game))
			{
				System.Windows.MessageBox.Show("Redux received an invalid Nexus Mod Manager link.", "Nexus Link Error",
					System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
				return;
			}
			initialNxmLink = args[1];
			if (!game.Equals(DivinityApp.NEXUSMODS_GAME_DOMAIN, StringComparison.OrdinalIgnoreCase))
			{
				if (!NxmPreviousHandlerForwarder.TryForward(new NxmRegistryStore(), initialNxmLink, launchTarget, out var error))
					System.Windows.MessageBox.Show(error, "Unsupported Nexus Game",
						System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
				return;
			}
		}

		using var activationCoordinator = NxmActivationCoordinator.CreateForUser();
		if (initialNxmLink != null
			&& activationCoordinator.TryForwardAsync(initialNxmLink, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult()) return;

		using var instanceGuard = new ReduxInstanceGuard();
		if (!instanceGuard.TryAcquire() || ReduxInstanceGuard.HasOlderRunningInstance())
		{
			if (initialNxmLink != null && activationCoordinator.TryForwardAsync(initialNxmLink, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult()) return;
			System.Windows.MessageBox.Show("Redux is already running. Close the other Redux window before opening this copy, even if it is a different version."
				+ (initialNxmLink == null ? "" : "\n\nThe download link was not delivered. After choosing which copy to keep open, click Mod Manager Download on Nexus again."),
				"Redux Is Already Running", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
			return;
		}

		var pendingActivations = new ConcurrentQueue<string>();
		if (initialNxmLink != null) pendingActivations.Enqueue(initialNxmLink);
		App app = null;
		var listening = activationCoordinator.StartListening(value =>
		{
			pendingActivations.Enqueue(value);
			app?.NotifyNxmActivationAvailable();
			return Task.CompletedTask;
		});
		if (!listening && initialNxmLink != null)
		{
			System.Windows.MessageBox.Show("Redux could not deliver the Nexus link to the running instance.",
				"Nexus Link Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
			return;
		}

		Util.SmoothLogicalScrollBehavior.Initialize();
		app = new App(pendingActivations);
		app.DispatcherUnhandledException += OnEarlyDispatcherException;
		app.Exit += OnAppExit;
		app.InitializeComponent();
		app.Run();
	}
}
