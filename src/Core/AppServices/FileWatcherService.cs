using DivinityModManager.AppServices;

namespace DivinityModManager
{
	public interface IFileWatcherService
	{
		IFileWatcherWrapper WatchDirectory(string directory, string filter);
	}
}

namespace DivinityModManager.AppServices
{
	public interface IFileWatcherWrapper
	{
		string DefaultDirectory { get; }

		string DirectoryPath { get; }

		bool IsEnabled { get; }

		IObservable<FileSystemEventArgs> FileChanged { get; }
		IObservable<FileSystemEventArgs> FileCreated { get; }
		IObservable<FileSystemEventArgs> FileDeleted { get; }

		void SetDirectory(string path);
		void PauseWatcher(bool paused, double pauseFor = -1);
	}

	internal class FileWatcherWrapper : ReactiveObject, IFileWatcherWrapper
	{
		public string DefaultDirectory => GetDefaultDirectory();

		[Reactive] public string DirectoryPath { get; private set; }
		[ObservableAsProperty] public bool IsEnabled { get; }

		public IObservable<FileSystemEventArgs> FileChanged { get; }
		public IObservable<FileSystemEventArgs> FileCreated { get; }
		public IObservable<FileSystemEventArgs> FileDeleted { get; }

		private readonly FileSystemWatcher _watcher;
		internal FileSystemWatcher Watcher => _watcher;

		internal virtual string GetDefaultDirectory()
		{
			return String.Empty;
		}

		public void SetDirectory(string path)
		{
			if (!String.IsNullOrEmpty(path))
			{
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);
				DirectoryPath = path;
			}
			else
			{
				DirectoryPath = DefaultDirectory;
			}
		}

		private static bool IsDirectoryPath(string path) => !String.IsNullOrEmpty(path) && Directory.Exists(path);
		private static string PathOrEmpty(string path) => IsDirectoryPath(path) ? path : String.Empty;

		private IDisposable _pauseToggleTask = null;

		public void PauseWatcher(bool paused, double pauseFor = -1)
		{
			_pauseToggleTask?.Dispose();
			_pauseToggleTask = null;
			_watcher.EnableRaisingEvents = !paused;
			if (paused && pauseFor > 0)
			{
				_pauseToggleTask = RxApp.TaskpoolScheduler.Schedule(TimeSpan.FromMilliseconds(pauseFor), () =>
				{
					_watcher.EnableRaisingEvents = IsEnabled;
				});
			}
		}

		public FileWatcherWrapper(string filter, string directoryPath = "")
		{
			DirectoryPath = directoryPath;

			_watcher = new FileSystemWatcher()
			{
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
				Filter = filter
			};

			FileChanged = Observable.FromEventPattern<FileSystemEventArgs>(_watcher, nameof(FileSystemWatcher.Changed)).Select(x => x.EventArgs);
			FileCreated = Observable.FromEventPattern<FileSystemEventArgs>(_watcher, nameof(FileSystemWatcher.Created)).Select(x => x.EventArgs);
			FileDeleted = Observable.FromEventPattern<FileSystemEventArgs>(_watcher, nameof(FileSystemWatcher.Deleted)).Select(x => x.EventArgs);

			this.WhenAnyValue(x => x.DirectoryPath).Select(IsDirectoryPath).ToPropertyEx(this, x => x.IsEnabled);
			this.WhenAnyValue(x => x.DirectoryPath).Select(PathOrEmpty).BindTo(this, x => x._watcher.Path);

			this.WhenAnyValue(x => x.IsEnabled).Throttle(TimeSpan.FromMilliseconds(250)).BindTo(_watcher, x => x.EnableRaisingEvents);

			SetDirectory(DefaultDirectory);
		}
	}

	public class FileWatcherService : IFileWatcherService
	{
		public IFileWatcherWrapper WatchDirectory(string directory, string filter)
		{
			var watcherWrapper = new FileWatcherWrapper(filter, directory);
			return watcherWrapper;
		}
	}
}
