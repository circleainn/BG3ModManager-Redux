using DivinityModManager.Util;

using System.Windows.Input;

namespace DivinityModManager.Controls;

/// <summary>Opens web links from Markdown documents in the default browser.</summary>
public sealed class MarkdownLinkCommand : ICommand
{
	private readonly Action<string> _openUrl;

	public MarkdownLinkCommand(Action<string>? openUrl = null)
	{
		_openUrl = openUrl ?? (url => ProcessHelper.TryOpenUrl(url));
	}

	// Availability depends only on the link parameter, never on application state.
	public event EventHandler? CanExecuteChanged { add { } remove { } }

	public bool CanExecute(object? parameter) => TryGetWebUri(parameter, out _);

	public void Execute(object? parameter)
	{
		if (!TryGetWebUri(parameter, out var uri)) return;
		try
		{
			_openUrl(uri.AbsoluteUri);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error opening Markdown link:\n{ex}");
		}
	}

	private static bool TryGetWebUri(object? parameter, out Uri? uri)
	{
		uri = null;
		return parameter is string url
			&& Uri.TryCreate(url, UriKind.Absolute, out uri)
			&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
	}
}
