using DivinityModManager.Util.ScreenReader;

using System.Reflection;

namespace DivinityModManager;

internal class Program
{
	private static string _libDirectory;

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

		var app = new App();
		app.DispatcherUnhandledException += OnEarlyDispatcherException;
		app.Exit += OnAppExit;
		app.InitializeComponent();
		app.Run();
	}
}
