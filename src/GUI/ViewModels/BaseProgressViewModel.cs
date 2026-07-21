namespace DivinityModManager.ViewModels;

public class BaseProgressViewModel : ReactiveObject
{
	[Reactive] public bool CanRun { get; set; }
	[Reactive] public bool CanClose { get; set; }
	[Reactive] public bool IsVisible { get; set; }
	[Reactive] public bool IsProgressActive { get; set; }
	[Reactive] public string ProgressTitle { get; set; }
	[Reactive] public string ProgressWorkText { get; set; }
	[Reactive] public double ProgressValue { get; set; }

	private readonly ObservableAsPropertyHelper<bool> _isRunning;
	/// <summary>
	/// True when the RunCommand is executing.
	/// </summary>
	public bool IsRunning => _isRunning.Value;

	public ReactiveCommand<Unit, bool> RunCommand { get; private set; }
	public RxCommandUnit CancelRunCommand { get; private set; }
	public RxCommandUnit CloseCommand { get; private set; }

	internal async Task<Unit> UpdateProgress(string title = "", string workText = "", double value = -1)
	{
		await Observable.Start(() =>
		{
			if (!String.IsNullOrEmpty(title))
			{
				ProgressTitle = title;
			}
			if (!String.IsNullOrEmpty(workText))
			{
				ProgressWorkText = workText;
			}
			if (value > -1)
			{
				ProgressValue = value;
			}
		}, RxApp.MainThreadScheduler);
		return Unit.Default;
	}

	public virtual Task<bool> Run(CancellationToken cts) => Task.FromResult(true);

	public virtual void Close()
	{
		CanClose = true;
		IsVisible = false;
		IsProgressActive = false;
	}

	public BaseProgressViewModel()
	{
		CanClose = true;
		var canRun = this.WhenAnyValue(x => x.CanRun);
		RunCommand = ReactiveCommand.CreateFromObservable(() => Observable.StartAsync(cts => Run(cts)).TakeUntil(this.CancelRunCommand), canRun);

		_isRunning = this.RunCommand.IsExecuting.ToProperty(this, nameof(IsRunning), true, RxApp.MainThreadScheduler);

		CancelRunCommand = ReactiveCommand.Create(() => { }, this.WhenAnyValue(x => x.IsRunning));

		var canClose = this.WhenAnyValue(x => x.CanClose, x => x.IsRunning, (b1, b2) => b1 && !b2);
		CloseCommand = ReactiveCommand.Create(Close, canClose);
	}
}
