namespace DivinityModManager.ViewModels;

public class MainWindowExceptionHandler : IObserver<Exception>
{
	private readonly MainWindowViewModel _viewModel;

	public MainWindowExceptionHandler(MainWindowViewModel vm)
	{
		_viewModel = vm;
	}

	public void OnNext(Exception value)
	{
		DivinityApp.Log($"Error: [{value.Source}]({value.GetType()}): {value.Message}\n{value.StackTrace}");
	}

	public void OnError(Exception error)
	{
		DivinityApp.Log($"Error: [{error.Source}]({error.GetType()}): {error.Message}\n{error.StackTrace}");
		RxApp.MainThreadScheduler.Schedule(() =>
		{
			if (_viewModel.MainProgressIsActive)
			{
				_viewModel.MainProgressIsActive = false;
			}
			_viewModel.View.AlertBar.SetDangerAlert(error.Message);
		});
	}

	public void OnCompleted()
	{
	}
}
