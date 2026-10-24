using DivinityModManager.Localization;
using System.ComponentModel;
using System.Windows;
using Newtonsoft.Json.Converters;

using System.Runtime.Serialization;
using System.Text;
using System.Windows.Input;

namespace DivinityModManager.Models.App;

public interface IHotkey
{
	Key Key { get; set; }
	ModifierKeys Modifiers { get; set; }
	ICommand Command { get; }
	bool Enabled { get; set; }
	string DisplayName { get; set; }
}

[DataContract]
public class Hotkey : ReactiveObject, IHotkey
{
	public string ID { get; set; }

	[Reactive] public string DisplayName { get; set; }
	[Reactive] public string Category { get; set; }
	[Reactive] public string Description { get; set; }

	public string LocalizedDisplayName => ReduxLanguageService.Current.Translate(DisplayName);
	public string LocalizedCategory => ReduxLanguageService.Current.Translate(Category);
	public string LocalizedDescription => ReduxLanguageService.Current.Translate(Description);
	public string ToolTip => IsDefault ? LocalizedDisplayName
		: ReduxLanguageService.Current.FormatEnglish("{0} (Modified)", LocalizedDisplayName);
	public string CommandToolTip
	{
		get
		{
			var title = Key == Key.None
				? LocalizedDisplayName
				: $"{LocalizedDisplayName} ({DisplayBindingText})";
			return String.IsNullOrWhiteSpace(Description)
				? title
				: $"{title}\n{LocalizedDescription}";
		}
	}

	[Reactive] public string DisplayBindingText { get; private set; }
	public string MenuDisplayBindingText => Key == Key.None ? String.Empty : DisplayBindingText;

	[DataMember]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[Reactive] public Key Key { get; set; }

	[DataMember]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[Reactive] public ModifierKeys Modifiers { get; set; }

	public RxCommandUnit Command { get; set; }
	ICommand IHotkey.Command => this.Command;

	[Reactive] public ICommand ResetCommand { get; private set; }
	[Reactive] public ICommand ClearCommand { get; private set; }

	[Reactive] public bool Enabled { get; set; }
	[Reactive] public bool CanEdit { get; set; }

	private readonly ObservableAsPropertyHelper<bool> _isDefault;
	public bool IsDefault => _isDefault.Value;
	[Reactive] public bool IsSelected { get; set; }

	private readonly ObservableAsPropertyHelper<string> _modifiedText;
	public string ModifiedText => _modifiedText.Value;

	private readonly Key _defaultKey = Key.None;
	private readonly ModifierKeys _defaultModifiers = ModifierKeys.None;

	public Key DefaultKey => _defaultKey;
	public ModifierKeys DefaultModifiers => _defaultModifiers;

	private readonly ObservableAsPropertyHelper<bool> _canExecuteCommand;
	public bool CanExecuteCommand => _canExecuteCommand.Value;

	private IObservable<bool> _canExecuteConditions;

	private readonly List<Action> _actions;
	public bool HasActions => _actions.Count > 0;

	public void AddAction(Action action, IObservable<bool> actionCanExecute = null)
	{
		_actions.Add(action);

		if (actionCanExecute != null)
		{
			AddCanExecuteCondition(actionCanExecute);
		}
	}

	public void AddCanExecuteCondition(IObservable<bool> canExecute)
	{
		_canExecuteConditions = _canExecuteConditions.CombineLatest(canExecute, (b1, b2) => b1 && b2);
		this.RaisePropertyChanged("_canExecuteConditions");
	}

	public void Invoke()
	{
		_actions.ForEach(a => a.Invoke());
	}

	public void ResetToDefault()
	{
		Key = _defaultKey;
		Modifiers = _defaultModifiers;
		UpdateDisplayBindingText();
	}

	public void Clear()
	{
		Key = Key.None;
		Modifiers = ModifierKeys.None;
		UpdateDisplayBindingText();
	}

	public void UpdateDisplayBindingText()
	{
		DisplayBindingText = ToString();
		this.RaisePropertyChanged(nameof(MenuDisplayBindingText));
	}

	public Hotkey(Key key = Key.None, ModifierKeys modifiers = ModifierKeys.None)
	{
		DisplayName = "";
		Category = "";
		Description = "";
		Key = key;
		Modifiers = modifiers;
		_defaultKey = key;
		_defaultModifiers = modifiers;

		Enabled = true;
		CanEdit = true;

		_actions = new List<Action>();

		DisplayBindingText = ToString();

		_canExecuteConditions = this.WhenAnyValue(x => x.Enabled);
		_canExecuteCommand = this.WhenAnyObservable(x => x._canExecuteConditions).ToProperty(this, nameof(CanExecuteCommand), false, RxApp.MainThreadScheduler);
		Command = ReactiveCommand.Create(Invoke, this.WhenAnyValue(x => x.CanExecuteCommand));

		_isDefault = this.WhenAnyValue(x => x.Key, x => x.Modifiers)
			.Select(x => x.Item1 == _defaultKey && x.Item2 == _defaultModifiers)
			.ToProperty(this, nameof(IsDefault), initialValue: true);

		var isDefaultObservable = this.WhenAnyValue(x => x.IsDefault);

		_modifiedText = isDefaultObservable.Select(b => !b ? "*" : "")
			.ToProperty(this, nameof(ModifiedText), "", scheduler: RxApp.MainThreadScheduler);

		this.WhenAnyValue(x => x.DisplayName, x => x.Category, x => x.Description, x => x.IsDefault)
			.Subscribe(_ => RefreshLocalizedText());
		PropertyChangedEventManager.AddHandler(ReduxLanguageService.Current, OnLanguageChanged, nameof(ReduxLanguageService.Language));

		this.WhenAnyValue(
				x => x.DisplayName,
				x => x.Description,
				x => x.DisplayBindingText,
				x => x.Key)
			.Subscribe(_ => this.RaisePropertyChanged(nameof(CommandToolTip)));

		var canReset = isDefaultObservable.Select(b => !b);
		var canClear = this.WhenAnyValue(x => x.Key, x => x.Modifiers, (k, m) => k != Key.None);

		ResetCommand = ReactiveCommand.Create(ResetToDefault, canReset);
		ClearCommand = ReactiveCommand.Create(Clear, canClear);
	}

	private void OnLanguageChanged(object sender, PropertyChangedEventArgs e) => RefreshLocalizedText();

	private void RefreshLocalizedText()
	{
		this.RaisePropertyChanged(nameof(LocalizedDisplayName));
		this.RaisePropertyChanged(nameof(LocalizedCategory));
		this.RaisePropertyChanged(nameof(LocalizedDescription));
		this.RaisePropertyChanged(nameof(ToolTip));
		this.RaisePropertyChanged(nameof(CommandToolTip));
	}

	public override string ToString()
	{
		var str = new StringBuilder();

		if (Modifiers.HasFlag(ModifierKeys.Control))
			str.Append("Ctrl + ");
		if (Modifiers.HasFlag(ModifierKeys.Shift))
			str.Append("Shift + ");
		if (Modifiers.HasFlag(ModifierKeys.Alt))
			str.Append("Alt + ");
		if (Modifiers.HasFlag(ModifierKeys.Windows))
			str.Append("Win + ");

		str.Append(Key.GetKeyName());

		return str.ToString();
	}
}
