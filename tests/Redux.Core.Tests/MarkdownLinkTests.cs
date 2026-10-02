using DivinityModManager.Controls;

using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

namespace Redux.Core.Tests;

public sealed class MarkdownLinkTests
{
	public void ReleaseNotesLinkIsEnabledWithoutANavigationHost()
	{
		const string url = "https://github.com/circleainn/BG3ModManager-Redux/releases/tag/v0.1.0-alpha.16.5.4";
		var document = new Markdown().Transform($"[Read the official release notes]({url})");
		var link = document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Hyperlink>().Single();

		RegressionAssert.True(link.Command is MarkdownLinkCommand);
		RegressionAssert.True(link.Command.CanExecute(link.CommandParameter));
		RegressionAssert.True(link.IsEnabled);
		RegressionAssert.Equal(url, link.CommandParameter);
	}

	public void MarkdownWebLinksDispatchToTheBrowserCommand()
	{
		var opened = new List<string>();
		var markdown = new Markdown { HyperlinkCommand = new MarkdownLinkCommand(opened.Add) };
		foreach (var url in new[] { "https://example.com/release?version=16&channel=alpha#notes", "http://example.com/help" })
		{
			var document = markdown.Transform($"[Details]({url})");
			var link = document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Hyperlink>().Single();
			RegressionAssert.True(link.IsEnabled);
			link.Command.Execute(link.CommandParameter);
			RegressionAssert.Equal(url, opened.Last());
		}
		RegressionAssert.Equal(2, opened.Count);
	}

	public void MarkdownLinksRejectNonWebTargetsEvenWhenExecutedDirectly()
	{
		var opened = new List<string>();
		var command = new MarkdownLinkCommand(opened.Add);
		foreach (var value in new object?[]
		{
			null, 42, "", "not a URL", "https://", "../release-notes.md", "//example.com/notes",
			"file:///C:/Windows/notepad.exe", "C:\\Windows\\notepad.exe", "javascript:alert(1)",
			"nxm://baldursgate3/mods/1/files/2", "mailto:example@example.com"
		})
		{
			RegressionAssert.False(command.CanExecute(value));
			command.Execute(value);
		}

		var document = new Markdown().Transform("[Local file](file:///C:/Windows/notepad.exe)");
		var link = document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Hyperlink>().Single();
		RegressionAssert.False(link.IsEnabled);
		RegressionAssert.Equal(0, opened.Count);
	}
}
