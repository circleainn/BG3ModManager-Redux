using DivinityModManager.Views;

using System.Windows;

namespace DivinityModManager.Util;

internal sealed class RxExceptionHandler : IObserver<Exception>
{
	public static MainWindow View { get; set; }

	public void OnNext(Exception value)
	{
		var message = $"(OnNext) Exception encountered:\nType: {value.GetType()}\tMessage: {value.Message}\nSource: {value.Source}\nStackTrace: {value.StackTrace}";
		DivinityApp.Log(message);
		if (View != null)
		{
			ReduxMessageBox.Show(View, message, "Error Encountered", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
		else
		{
			ReduxMessageBox.Show(message, "Error Encountered", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	public void OnError(Exception value)
	{
		var message = $"(OnError) Exception encountered:\nType: {value.GetType()}\tMessage: {value.Message}\nSource: {value.Source}\nStackTrace: {value.StackTrace}";
		DivinityApp.Log(message);
	}

	public void OnCompleted() { }
}
