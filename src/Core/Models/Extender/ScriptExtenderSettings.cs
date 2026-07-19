using DivinityModManager.Extensions;

using Newtonsoft.Json;

using System.ComponentModel;
using System.Runtime.Serialization;

namespace DivinityModManager.Models.Extender;

[DataContract]
public class ScriptExtenderSettings : ReactiveObject
{
	[Reactive] public bool ExtenderIsAvailable { get; set; }
	[Reactive] public string ExtenderVersion { get; set; }
	[Reactive] public int ExtenderMajorVersion { get; set; }

	[DefaultValue(false)]
	[JsonIgnore] // This isn't an actual extender setting, so omit it from the exported json
	[SettingsEntry("Export settings with default values", "Include every setting when writing ScriptExtenderSettings.json, even when its value matches the extender default.")]
	[DataMember, Reactive]
	public bool ExportDefaultExtenderSettings { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Enable developer mode", "Enable Script Extender debugging features. Mods can also detect this setting and produce additional diagnostic output.")]
	[DataMember, Reactive]
	public bool DeveloperMode { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Custom profile folder", "Use a profile other than Public. Enter the profile folder name, not a full path.")]
	[DataMember, Reactive]
	public string CustomProfile { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Open extender console", "Open a console window that displays Script Extender diagnostic output while the game runs.")]
	[DataMember, Reactive]
	public bool CreateConsole { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Log story compilation errors", "Write Osiris story compilation errors to the LogFailedCompile file.")]
	[DataMember, Reactive]
	public bool LogFailedCompile { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Log Osiris activity", "Log Osiris rule evaluation, queries, and related activity.")]
	[DataMember, Reactive]
	public bool EnableLogging { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Log story compilation", "Write Osiris story compilation activity to a log file.")]
	[DataMember, Reactive]
	public bool LogCompile { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Log output folder", "Folder where Script Extender and Osiris logs are written. Default: Documents\\OsirisLogs.")]
	[DataMember, Reactive]
	public string LogDirectory { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Log console and script output", "Write Script Extender console and script output to a log file.")]
	[DataMember, Reactive]
	public bool LogRuntime { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable Launcher", "Prevents the exe from force-opening the launcher\nMay not work correctly if extender auto-updating is enabled, or the --skip-launcher launch param is set", true)]
	[DataMember, Reactive]
	public bool DisableLauncher { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable Story Merge", "Prevents story.div.osi merging, which automatically happens when mods are present\nMay only occur when loading a save", true)]
	[DataMember, Reactive]
	public bool DisableStoryMerge { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Disable Story Patching", "Prevents patching story.bin with story.div.osi when loading saves, effectively preventing the Osiris scripts in the save from updating", true)]
	[DataMember, Reactive]
	public bool DisableStoryPatching { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Skip mod validation", "Skip module hashing when loading mods. This reduces loading time without changing the load order.")]
	[DataMember, Reactive]
	public bool DisableModValidation { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Enable achievements", "Allow achievements while playing with mods.")]
	[DataMember, Reactive]
	public bool EnableAchievements { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Enable Extensions", "Enables or disables extender API functionality", true)]
	[DataMember, Reactive]
	public bool EnableExtensions { get; set; }

	[DefaultValue(true)]
	[SettingsEntry("Send Script Extender crash reports", "Upload Script Extender minidumps to its crash-report server after a game crash.")]
	[DataMember, Reactive]
	public bool SendCrashReports { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Enable Osiris Debugger", "Enables the Osiris debugger interface (vscode extension)", true)]
	[DataMember, Reactive]
	public bool EnableDebugger { get; set; }

	[DefaultValue(9999)]
	[SettingsEntry("Osiris Debugger Port", "Port number the Osiris debugger will listen on\nDefault: 9999", true)]
	[DataMember, Reactive]
	public int DebuggerPort { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Dump Network Strings", "Dumps the NetworkFixedString table to LogDirectory\nMainly useful for debugging desync issues", true)]
	[DataMember, Reactive]
	public bool DumpNetworkStrings { get; set; }

	[DefaultValue(0)]
	[SettingsEntry("Osiris debugger flags", "Advanced debugger flags passed to the Osiris debugger. Default: 0.")]
	[DataMember, Reactive]
	public int DebuggerFlags { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Enable Lua Debugger", "Enables the Lua debugger interface (vscode extension)", true)]
	[DataMember, Reactive]
	public bool EnableLuaDebugger { get; set; }

	[DefaultValue("")]
	[SettingsEntry("Lua Builtin Directory", "An additional directory where the Script Extender will check for builtin scripts\nThis setting is meant for developers, to make it easier to test builtin script changes", true)]
	[DataMember, Reactive]
	public string LuaBuiltinResourceDirectory { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Clear Console On Reset", "Clears the extender console when the reset command is used", true)]
	[DataMember, Reactive]
	public bool ClearOnReset { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Default to Client Side", "Defaults the extender console to the client-side\nThis is setting is intended for developers", true)]
	[DataMember, Reactive]
	public bool DefaultToClientConsole { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Show Performance Warnings", "Print warnings to the extender console window, which indicates when the server-side part of the game lags behind (a.k.a. warnings about ticks taking too long).", true)]
	[DataMember, Reactive]
	public bool ShowPerfWarnings { get; set; }

	[DefaultValue(false)]
	[SettingsEntry("Prevent ModCrashSanityCheck", "Ask Script Extender to prevent BG3's ModCrashSanityCheck from disabling mods on the next launch.")]
	[DataMember, Reactive]
	public bool InsanityCheck { get; set; }

	public ScriptExtenderSettings()
	{
		this.SetToDefault();
		ExtenderVersion = String.Empty;
		ExtenderMajorVersion = -1;
	}
}
