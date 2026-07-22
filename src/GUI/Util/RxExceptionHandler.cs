using DivinityModManager.Views;

using System.Windows;

namespace DivinityModManager.Util;

class RxExceptionHandler : IObserver<Exception>
{
	public static MainWindow view { get; set; }
	public void OnNext(Exception value)
	{
		//if (Debugger.IsAttached) Debugger.Break();

		var message = $"(OnNext) Exception encountered:\nType: {value.GetType()}\tMessage: {value.Message}\nSource: {value.Source}\nStackTrace: {value.StackTrace}";
		DivinityApp.Log(message);
		if (view != null)
		{
			ReduxMessageBox.Show(view, message, "Error Encountered", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
		else
		{
			ReduxMessageBox.Show(message, "Error Encountered", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
		//RxApp.MainThreadScheduler.Schedule(() => { throw value; });
	}

	public void OnError(Exception value)
	{
		var message = $"(OnError) Exception encountered:\nType: {value.GetType()}\tMessage: {value.Message}\nSource: {value.Source}\nStackTrace: {value.StackTrace}";
		DivinityApp.Log(message);
	}

	public void OnCompleted()
	{
		//if (Debugger.IsAttached) Debugger.Break();
		//RxApp.MainThreadScheduler.Schedule(() => { throw new NotImplementedException(); });
	}
}
