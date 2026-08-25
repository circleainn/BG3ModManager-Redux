using CrossSpeak;

using DivinityModManager.Util;

namespace DivinityModManager
{
	public interface IScreenReaderService
	{
		bool IsScreenReaderActive();
		void Output(string text, bool interrupt = false);
		void Speak(string text, bool interrupt = false);
		void Close();
		void Silence();
	}
}

namespace DivinityModManager.AppServices
{
	public class ScreenReaderService : IScreenReaderService
	{
		private static readonly string[] _dlls = ["nvdaControllerClient64.dll", "SAAPI64.dll", "Tolk.dll"];
		private static bool _loadedDlls = false;
		private readonly object _initializationLock = new();
		private bool _initializationUnavailable;
		private bool _hasDetectionResult;
		private bool _lastDetectionResult;
		private long _lastDetectionTick;
		private const long DetectionCacheMilliseconds = 15000;

		public bool IsScreenReaderActive()
		{
			var now = Environment.TickCount64;
			lock (_initializationLock)
			{
				if (_hasDetectionResult && now - _lastDetectionTick < DetectionCacheMilliseconds)
					return _lastDetectionResult;
			}
			if (!EnsureInit(false))
			{
				CacheDetectionResult(false, now);
				return false;
			}
			try
			{
				var detected = !String.IsNullOrWhiteSpace(CrossSpeakManager.Instance.DetectScreenReader());
				CacheDetectionResult(detected, now);
				return detected;
			}
			catch (Exception ex)
			{
				DisableAfterInteropFailure(ex);
				return false;
			}
		}

		public void Close()
		{
			try
			{
				if (CrossSpeakManager.Instance.IsLoaded()) CrossSpeakManager.Instance.Close();
			}
			catch (Exception ex) { DisableAfterInteropFailure(ex); }
		}

		public void Silence()
		{
			try
			{
				if (CrossSpeakManager.Instance.IsLoaded()) CrossSpeakManager.Instance.Silence();
			}
			catch (Exception ex) { DisableAfterInteropFailure(ex); }
		}

		private bool EnsureInit(bool trySAPI = false)
		{
			if (_initializationUnavailable) return false;
			lock (_initializationLock)
			{
				if (_initializationUnavailable) return false;
				try
				{
					if (!_loadedDlls)
					{
						var libPath = Path.Combine(DivinityApp.GetAppDirectory(), "_Lib");
						foreach (var dll in _dlls)
						{
							var filePath = Path.Combine(libPath, dll);
							if (File.Exists(filePath)) NativeLibraryHelper.LoadLibrary(filePath);
						}
						_loadedDlls = true;
					}

					if (!CrossSpeakManager.Instance.IsLoaded())
					{
						CrossSpeakManager.Instance.Initialize();
						if (trySAPI && !CrossSpeakManager.Instance.HasSpeech())
							CrossSpeakManager.Instance.TrySAPI(true);
					}
					return CrossSpeakManager.Instance.IsLoaded();
				}
				catch (Exception ex)
				{
					DisableAfterInteropFailure(ex);
					return false;
				}
			}
		}

		public void Output(string text, bool interrupt = true)
		{
			if (!EnsureInit(true)) return;
			try
			{
				CrossSpeakManager.Instance.Output(text, interrupt);
			}
			catch (Exception ex) { DisableAfterInteropFailure(ex); }
		}

		public void Speak(string text, bool interrupt = true) => Output(text, interrupt);

		private void CacheDetectionResult(bool detected, long tick)
		{
			lock (_initializationLock)
			{
				_hasDetectionResult = true;
				_lastDetectionResult = detected;
				_lastDetectionTick = tick;
			}
		}

		private void DisableAfterInteropFailure(Exception ex)
		{
			if (_initializationUnavailable) return;
			_initializationUnavailable = true;
			_hasDetectionResult = true;
			_lastDetectionResult = false;
			_lastDetectionTick = Environment.TickCount64;
			DivinityApp.Log($"Screen reader integration was disabled after an interop failure:\n{ex}");
		}
	}
}
