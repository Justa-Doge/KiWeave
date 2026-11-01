using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FunctionRowRemapper
{
    // The function-key and custom-hotkey editors share this ordered catalog in
    // both UI hosts. Keep category order and choice order in one place so the
    // WPF migration cannot silently diverge from the WinForms picker.
    internal static class ActionPickerCatalog
    {
        internal static readonly string[] FunctionGroups = { "Normal behavior", "Disable this key", "Keyboard input", "Media and sound", "Open or run something", "Monitor controls", "Custom action" };
        internal static readonly string[] CustomGroups = { "Keyboard input", "Media and sound", "Open or run something", "Custom action", "Monitor controls" };
        internal static readonly ActionPickerChoice ChooseAction = Choice("Choose an action", ActionKind.Unbound);

        static ActionPickerChoice Choice(string label, ActionKind kind) { return new ActionPickerChoice(label, new Mapping { Kind = kind }); }
        static ActionPickerChoice Preset(string label, ActionKind kind, string target, string arguments) { return new ActionPickerChoice(label, new Mapping { Kind = kind, Target = target, Arguments = arguments }); }
        static ActionPickerChoice Shortcut(string label, string target) { return Preset(label, ActionKind.SendShortcut, target, ""); }

        internal static ActionPickerChoice[] ChoicesFor(int group, bool custom)
        {
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (custom && group == 4) return DdcChoices();
            if (!custom)
            {
                if (group == 0) return new[] { Choice("Pass through normally", ActionKind.PassThrough) };
                if (group == 1) return new[] { Choice("Do nothing", ActionKind.Unbound) };
                if (group == 5) return DdcChoices();
            }

            int normalized = custom ? group : group - 2;
            if (normalized == 0) return new[] { Choice("Send one key", ActionKind.SendKey), Choice("Send a keyboard shortcut", ActionKind.SendShortcut) };
            if (normalized == 1) return new[] {
                Preset("Volume up", ActionKind.Media, "VolumeUp", ""),
                Preset("Volume down", ActionKind.Media, "VolumeDown", ""),
                Preset("Mute or unmute", ActionKind.Media, "VolumeMute", ""),
                Preset("Play or pause", ActionKind.Media, "MediaPlayPause", ""),
                Preset("Next track", ActionKind.Media, "MediaNextTrack", ""),
                Preset("Previous track", ActionKind.Media, "MediaPreviousTrack", ""),
                Preset("Eject", ActionKind.Media, "MediaEject", "")
            };
            if (normalized == 2) return new[] {
                Choice("Open an application", ActionKind.Application),
                Choice("Open a file or folder", ActionKind.FileOrFolder),
                Choice("Run a Windows shortcut", ActionKind.WindowsShortcut),
                Choice("Run a command or script", ActionKind.Command),
                Choice("Run a Python script", ActionKind.Python),
                Choice("Call an HTTP endpoint", ActionKind.HttpRequest)
            };

            var presets = new List<ActionPickerChoice> {
                Choice("Lock Windows, then sleep", ActionKind.LockThenSleep),
                Shortcut("Lock Windows", "Win+L"), Shortcut("Open file explorer", "Win+E"), Shortcut("Open Windows settings", "Win+I"),
                Shortcut("Open task manager", "Ctrl+Shift+Escape"), Shortcut("Open clipboard history", "Win+V"),
                Shortcut("Open notification center", "Win+N"), Shortcut("Open quick settings", "Win+A"),
                Shortcut("Open emoji picker", "Win+OemPeriod"), Shortcut("Open task view", "Win+Tab"),
                Shortcut("Open run dialog", "Win+R"), Shortcut("Open power-user menu", "Win+X"),
                Shortcut("Take a screen snip", "Win+Shift+S"), Shortcut("Show desktop", "Win+D"),
                Shortcut("Switch apps", "Alt+Tab"), Shortcut("Snap window left", "Win+Left"),
                Shortcut("Snap window right", "Win+Right"), Shortcut("Maximize window", "Win+Up"),
                Shortcut("Minimize window", "Win+Down"), Shortcut("New virtual desktop", "Win+Ctrl+D"),
                Shortcut("Close virtual desktop", "Win+Ctrl+F4"), Shortcut("Next virtual desktop", "Win+Ctrl+Right"),
                Shortcut("Previous virtual desktop", "Win+Ctrl+Left"), Shortcut("Copy", "Ctrl+C"),
                Shortcut("Paste", "Ctrl+V"), Shortcut("Cut", "Ctrl+X"), Shortcut("Undo", "Ctrl+Z"),
                Shortcut("Redo", "Ctrl+Y"), Shortcut("Select all", "Ctrl+A"), Shortcut("Save", "Ctrl+S"),
                Shortcut("Open", "Ctrl+O"), Shortcut("New", "Ctrl+N"), Shortcut("Find", "Ctrl+F"),
                Shortcut("Print", "Ctrl+P"), Shortcut("Refresh", "F5"), Shortcut("Close current window", "Alt+F4"),
                Shortcut("Toggle full screen", "F11"),
                Preset("Open calculator", ActionKind.Application, Path.Combine(sys, "calc.exe"), ""),
                Preset("Open notepad", ActionKind.Application, Path.Combine(sys, "notepad.exe"), ""),
                Preset("Open paint", ActionKind.Application, Path.Combine(sys, "mspaint.exe"), ""),
                Preset("Open control panel", ActionKind.Application, Path.Combine(sys, "control.exe"), ""),
                Preset("Open device manager", ActionKind.Application, Path.Combine(sys, "mmc.exe"), "devmgmt.msc"),
                Preset("Sleep", ActionKind.Application, Path.Combine(sys, "rundll32.exe"), "powrprof.dll,SetSuspendState 0,1,0"),
                Preset("Sign out", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/l"),
                Preset("Restart", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/r /t 0"),
                Preset("Shut down", ActionKind.Application, Path.Combine(sys, "shutdown.exe"), "/s /t 0")
            };
            presets.AddRange(ExpandedActions.All);
            presets.Insert(0, ChooseAction);
            presets.Insert(1, Choice("Build a conditional action...", ActionKind.Conditional));
            if (custom) presets.Insert(2, Choice("Build a step-by-step sequence...", ActionKind.Sequence));
            return presets.ToArray();
        }

        internal static ActionPickerChoice[] LibraryChoices(bool allowSequence = true, bool allowConditional = true)
        {
            return Enumerable.Range(0, 5).SelectMany(group => ChoicesFor(group, true))
                .Where(choice => choice.Label != ChooseAction.Label &&
                    (allowSequence || choice.Mapping.Kind != ActionKind.Sequence) &&
                    (allowConditional || choice.Mapping.Kind != ActionKind.Conditional))
                .GroupBy(choice => choice.Label)
                .Select(group => group.First())
                .OrderBy(choice => choice.Label)
                .ToArray();
        }

        static ActionPickerChoice[] DdcChoices()
        {
            return DdcOperation.All.Select(operation =>
                new ActionPickerChoice(operation.Label,
                    new Mapping { Kind = ActionKind.Monitor, MonitorControl = operation.Id, MonitorStep = 5 },
                    "Integrations and hardware", "Detect a DDC/CI monitor, then choose it in Monitor.")).ToArray();
        }
    }
}
