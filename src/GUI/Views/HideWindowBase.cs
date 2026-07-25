using AdonisUI.Controls;

using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DivinityModManager.Views;

public class HideWindowBase<TViewModel> : AdonisWindow, IViewFor<TViewModel> where TViewModel : class
{
	private bool _hideAnimationRunning;
	/// <summary>
	/// The view model dependency property.
	/// </summary>
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register("ViewModel",
		typeof(TViewModel),
		typeof(HideWindowBase<TViewModel>),
		new PropertyMetadata(null));

	/// <summary>
	/// Gets the binding root view model.
	/// </summary>
	public TViewModel BindingRoot => ViewModel;

	/// <inheritdoc/>
	public TViewModel ViewModel
	{
		get => (TViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	/// <inheritdoc/>
	object IViewFor.ViewModel
	{
		get => ViewModel;
		set => ViewModel = (TViewModel)value;
	}

	public HideWindowBase()
	{
		Closing += HideWindow_Closing;
		ReduxWindowBehavior.AttachAdaptiveSizing(this);
		KeyDown += (o, e) =>
		{
			if (!e.Handled && e.Key == System.Windows.Input.Key.Escape)
			{
				if (Keyboard.FocusedElement == null || Keyboard.FocusedElement.GetType() != typeof(TextBox))
				{
					HideWithTransition();
				}
			}
		};
	}

	public virtual void HideWindow_Closing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		HideWithTransition();
	}

	public void HideWithTransition()
	{
		if (_hideAnimationRunning || !IsVisible) return;
		_hideAnimationRunning = true;
		ReduxWindowBehavior.AnimateExit(this, () =>
		{
			Hide();
			_hideAnimationRunning = false;
		});
	}

	public void ShowWithTransition()
	{
		if (IsVisible)
		{
			Activate();
			return;
		}

		ReduxWindowBehavior.PrepareEntrance(this);
		Show();
		Dispatcher.BeginInvoke(
			() => ReduxWindowBehavior.AnimateEntrance(this, 0),
			System.Windows.Threading.DispatcherPriority.Render);
	}
}
