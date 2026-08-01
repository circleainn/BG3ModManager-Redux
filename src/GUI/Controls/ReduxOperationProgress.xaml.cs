using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DivinityModManager.Controls;

public partial class ReduxOperationProgress : UserControl
{
	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
		nameof(Title), typeof(string), typeof(ReduxOperationProgress), new PropertyMetadata(String.Empty));

	public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
		nameof(Status), typeof(string), typeof(ReduxOperationProgress), new PropertyMetadata(String.Empty));

	public static readonly DependencyProperty ProgressValueProperty = DependencyProperty.Register(
		nameof(ProgressValue), typeof(double), typeof(ReduxOperationProgress), new PropertyMetadata(0d));

	public static readonly DependencyProperty CanCancelProperty = DependencyProperty.Register(
		nameof(CanCancel), typeof(bool), typeof(ReduxOperationProgress), new PropertyMetadata(true));

	public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(
		nameof(CancelCommand), typeof(ICommand), typeof(ReduxOperationProgress), new PropertyMetadata(null));

	public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(
		nameof(IconKey), typeof(string), typeof(ReduxOperationProgress), new PropertyMetadata("package"));

	public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
		nameof(IconBrush), typeof(Brush), typeof(ReduxOperationProgress), new PropertyMetadata(null));

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public string Status
	{
		get => (string)GetValue(StatusProperty);
		set => SetValue(StatusProperty, value);
	}

	public double ProgressValue
	{
		get => (double)GetValue(ProgressValueProperty);
		set => SetValue(ProgressValueProperty, value);
	}

	public bool CanCancel
	{
		get => (bool)GetValue(CanCancelProperty);
		set => SetValue(CanCancelProperty, value);
	}

	public ICommand? CancelCommand
	{
		get => (ICommand?)GetValue(CancelCommandProperty);
		set => SetValue(CancelCommandProperty, value);
	}

	public string IconKey
	{
		get => (string)GetValue(IconKeyProperty);
		set => SetValue(IconKeyProperty, value);
	}

	public Brush? IconBrush
	{
		get => (Brush?)GetValue(IconBrushProperty);
		set => SetValue(IconBrushProperty, value);
	}

	public ReduxOperationProgress()
	{
		InitializeComponent();
	}
}
