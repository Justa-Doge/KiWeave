# KeyWeave

A small Windows tray app for giving F1 through F12 your own actions. Remapping works offline; an optional background check looks for newer versions on GitHub.

Previously called Function Row Remapper. The current executable and internal project names still use `FunctionRowRemapper` until the app rebrand is finished; this does not affect saved mappings.

The interface uses a charcoal dark theme with purple accents, a sidebar for switching editors, rounded controls, and keycap-style mapping rows. Action labels use sentence case. The library and sequence windows share the same theme; standard Windows file/message dialogs still follow Windows' own appearance.

The **Custom hotkeys** sidebar page also lets you assign up to 32 global Ctrl, Alt, Shift, or Win shortcuts to the same actions. It includes **Run a Python script** for a local `.py` file. Python runs with the signed-in Windows account only, never changes execution/security policy, and needs the normal Python launcher (`py.exe`) already installed.

Both editors start with a short category menu instead of a long raw action list. Choose Keyboard input, Media and sound, Open or run something, or Custom action, then select the specific action beneath it. Custom action includes ready-made lock, sleep, Explorer, Settings, Task Manager, clipboard history, screenshot, desktop, app-switching, editing, Calculator, sign-out, restart, and shut-down actions. System-changing presets only run if the assigned key is pressed.

Use **Browse action library** on Function keys or **Custom action** on Custom hotkeys to open the searchable library. Search any part of an action name, such as `clipboard`, `snap`, `device`, `sleep`, or `paint`, then choose **Use action**. The short dropdown menus still provide direct access by category. Only the fields needed by the selected action are shown.

For a Custom hotkey, choose **Build sequence...** to open the separate sequence builder. Search the full action library on the left; add actions and waits to the numbered blocks on the right. Reorder, edit, or remove steps, then choose **Use sequence**. Opening the builder does not execute steps. A sequence runs its steps in order when the saved shortcut is pressed. It supports up to 20 steps, each wait is limited to 60 seconds, and total waiting time is limited to five minutes.

Both pages share **Shortcuts enabled**, **Start with Windows**, **Keep running in tray**, and **Save changes**. **More** contains import/export, function-key reset/disable, and **Exit app**. The editor panels scroll when needed at smaller window sizes.

## PowerToys shortcuts

Choose **Custom hotkeys → PowerToys**. The left side shows only shortcuts that are assigned and whose PowerToys module is enabled; select one to edit its details on the right. Choose **Add shortcut** to browse off or unassigned PowerToys functions using module and specific-function dropdowns. After an editable function is enabled, assigned, and saved, it moves into the active list. **Refresh** picks up changes made directly in PowerToys. Keyboard Manager's existing shortcut remaps are shown when its active profile exists.

PowerToys remains the owner of these shortcuts; KeyWeave does not register them a second time. PowerToys must be running for its actions to work. KeyWeave's **Save changes** button applies only to KeyWeave's own hotkeys, while **Save in PowerToys** applies a supported PowerToys shortcut edit.

For modules supported by the installed `PowerToys.DSC.exe`, you can edit the key combination and optionally enable an off module using **Save in PowerToys**. KeyWeave first backs up the original PowerToys settings under `%LOCALAPPDATA%\KeyWeave\PowerToysBackups`, rejects a stale or conflicting edit, applies it through PowerToys' configuration tool, then checks the saved result. If PowerToys does not activate it immediately, restart PowerToys yourself; KeyWeave does not restart it. Modules without supported configuration, and Keyboard Manager's remap rules, appear read-only; use **Open PowerToys Settings** to edit those. This integration does not claim to control every PowerToys action or bypass Windows-reserved shortcuts.

Run **bin/FunctionRowRemapper.exe**, or use the installed **Function Row Remapper** entry in Windows Search. See [INSTALLATION.md](INSTALLATION.md) for installation, the **Use system tray** setting, and removal instructions.

## Update notifications

On launch, KeyWeave quietly checks stable `vX.Y.Z` tags in its private GitHub repository. If a tag is newer than the installed version, it shows a Windows notification linking to that tag. It never downloads or installs an update. The check uses the PC's existing Git credential manager, with interaction disabled and an eight-second limit; no credential is stored in KeyWeave. If Git or private-repo access is unavailable, it skips the check without delaying startup. A release needs a matching version bump in `src/UpdateChecker.cs` and a pushed Git tag. Ordinary commits to `main` do not trigger notifications.

DDC/CI supports detected monitor brightness, contrast, and hardware-volume actions. See [DDC-CI.md](DDC-CI.md) for setup, compatibility, and verification.

## Run it

Run `./build.ps1` from PowerShell 7 first, then open **bin/FunctionRowRemapper.exe**. No installer, account, scripting runtime, or administrator access is needed. You can copy that one executable to a permanent local folder. This x64 build uses the Windows .NET Framework 4.x runtime; it is not a bundled .NET runtime or an ARM64 build. Intended for Windows 10/11 x64; tested on one Windows 11 machine only.

First-run defaults are **all keys Pass through, remapping off, and Windows startup off**. A blue F icon appears in the notification area. If another copy is running, use that icon to open it.

## Configure a key

1. Select F1 through F12 in the left-hand list.
2. Choose an action and fill in its fields. Changes remain in the editor as you select other keys.
3. Click **Save changes** to validate, persist, and apply the edited mappings.
4. Turn on **Enable shortcuts**. This switch takes effect immediately and uses the last saved mappings. Its preference is saved immediately too.

For example, select **F5**, choose **Media and sound**, select **Volume up**, and Save changes. Turn on Enable shortcuts, then physically press F5. It should adjust volume and suppress F5's original action.

| Action | Details |
| --- | --- |
| Pass through | Normal F-key behavior, including modifier combinations. |
| Unbound | Suppress the function key and do nothing. |
| Send a key | Key name such as A, 7, Enter, Escape, Space, Left, F8, Home, Oemplus. |
| Send a shortcut | Modifiers plus one final key: Ctrl+Shift+S, Alt+Tab, Win+E. Media key names also work. Ctrl+Alt+Delete is rejected. |
| Media / system | Volume up/down, mute, play/pause, next track, previous track. |
| Monitor (DDC/CI) | Select a detected monitor, brightness/contrast/monitor-volume up or down, and a 1–20% step. |
| Open an application | Browse to an .exe. Arguments and working directory are optional. |
| Open a file or folder | Browse to a file or folder; Windows opens it using its association or File Explorer. |
| Run a Windows shortcut | Browse to a .lnk; its saved target, arguments, and working directory are used. |
| Run a command or script | Choose a local .exe, .cmd, .bat, or .ps1. Arguments and working directory are optional. |
| Run a Python script | Choose a local `.py` file. Python must already be installed; arguments and working directory are optional. |

Fields use full local drive paths, such as `C:\Tools\app.exe`. Do not add quotes around the target path; use argument quoting where needed. Environment-variable paths, network shares, URLs, device paths, alternate data streams, and wildcard paths are rejected. Key names use Windows virtual-key names; keyboard layouts can affect punctuation.

Commands run with your account's permissions. Only use commands and shortcuts you trust. For shell built-ins, choose `C:\Windows\System32\cmd.exe` and supply arguments, for example `/d /c echo Hello` (a hidden window means its output will not be displayed). PowerShell scripts run using Windows PowerShell with `-NoProfile -NonInteractive -File`, respecting the machine's existing execution policy. The app never changes that policy. Applications use their normal window behavior; command/script windows are requested hidden. There is no preview/test button that executes an action while editing.

**More → Reset function keys** stages twelve Pass-through mappings. **More → Disable all function keys** stages twelve swallowed keys. Both need Save changes. They leave the global switch and startup preference as they are.

## Key behavior and emergency off

- Hold **Ctrl + Alt + Shift together for 1.5 seconds** to turn remapping off. Left or right modifiers work. Release the keys afterward. This only turns remapping off; it never re-enables it.
- You can also switch it off in the app or from the tray menu. Click **Exit** in the app or tray to quit and remove the hook. With **Use system tray** on, closing the window hides it and keeps remapping running; with it off, closing exits.
- A press already in progress keeps its original suppression decision through release. After disabling, release any held F key; new presses pass through. This avoids mismatched key-down/key-up events.
- Windows volume up/down and DDC monitor adjustments repeat while held. All other actions, including shortcuts, key taps, launches, and mute, fire once per physical press. Sent shortcuts are complete down/up taps, not held keys or macros.
- Held Ctrl/Alt/Shift/Win modifiers also apply to the destination action. The app does not release modifiers you are holding. For example, holding Shift while triggering a mapped A can produce uppercase A. Release a physically held destination key before triggering its mapping.
- Synthetic input is ignored, including this app's output and other automation tools' injected function keys. This prevents recursive remapping. Only physical standard F1–F12 input is remapped in the production app.
- Disabling cancels pending queued actions; a process that already began launching may still open. The action queue is bounded; extremely rapid input can drop excess queued actions.

## Save, import, and export

Configuration is human-readable JSON: version 1 for original actions, version 2 when it contains monitor actions. Both versions import. It is stored at:

Saving at least one custom hotkey uses version 3 and adds a `customHotkeys` section. Versions 1 and 2 remain supported. A custom hotkey must include at least one modifier so ordinary typing cannot be captured. Windows-reserved or already-used combinations are reported when you save. The default **Win+Shift+L** row is an editable **Lock Windows, then sleep** action in the Custom hotkeys tab.

`%LOCALAPPDATA%\FunctionRowRemapper\config.json`

Saves write and flush a temporary file in the same directory, then atomically replace the current file. `config.json.bak` retains the previous saved version. A corrupt existing file is not overwritten just by opening the app; the app opens with safe defaults and an error. You can inspect or restore the backup while the app is closed.

**Export configuration** writes the current editor draft, including unsaved edits, without activating it. **Import configuration** checks size, JSON syntax, duplicate/unknown fields, version, exactly twelve unique function keys, actions, paths, and shortcut syntax before replacing the editor draft. An invalid import leaves both the editor and live mappings intact. The maximum file size is 64 KB.

Imports never execute commands, enable remapping, or change startup. Review all twelve mappings, especially launch targets and commands, then Save. A missing target may be imported for review, but Save and dispatch report an error until it is fixed. A target removed after saving is checked again when invoked.

`sample-config.json` gives examples: F1 volume down, F2 volume up, F3 mute, F5 volume up, F6 Ctrl+Shift+S; the rest pass through. The sample has remapping off and contains no launches. `defaults.json` contains the original all-Pass-through configuration.

## Start with Windows

Tick **Start with Windows**, then Save. This explicitly creates only the per-user startup value:

`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\FunctionRowRemapper`

The value is the quoted executable path followed by `--tray`. Untick it and Save to remove the value. Startup is separate from the imported/exported JSON so an import cannot enable persistence. Place the executable in its permanent folder before enabling startup. If you move it later, Save from the new location with Start with Windows still checked to update the existing entry.

## Windows and keyboard limitations

The app captures standard Windows F1–F12 virtual-key events. Apple/Boot Camp keyboards and laptop Fn modes may generate media events instead, or handle brightness/media functions in firmware. Try Fn+F5 if plain F5 produces a hardware action. The app does not intercept dedicated media keys, change Fn mode, alter firmware, install drivers, or intercept actions handled entirely by keyboard firmware. It cannot reliably identify the source hardware when firmware handles an event entirely outside Windows.

Windows integrity boundaries can block a normal-privilege remapper from sending input into elevated apps. UAC secure-desktop prompts and Ctrl+Alt+Delete are outside its scope. Games, anti-cheat systems, raw-input apps, and other keyboard hooks may behave differently. No security boundary is bypassed.

The status shows **Active in background**, **Shortcuts paused**, or **Unavailable**. This reflects the app's current engine state, not a continuous operating-system health probe. Windows can silently remove a low-level hook if its callback times out; it does not provide reliable removal notification. If remapping stops unexpectedly, exit and reopen the app. The hook runs on a dedicated message-loop thread and queues action execution to a separate worker to keep callbacks short. Windows removes a process's hook when that process terminates.

No network access, account, analytics, telemetry, or key logs are used. The hook examines event metadata to identify function keys and injected events; it retains only twelve active press states. Emergency polling reads only Ctrl/Alt/Shift key state. No general typed text is captured or persisted.

## Build and test

### Expanded action library (2026-09-22)

The searchable library now contains 429 entries across 10 categories, including Windows/window management, text editing, File Explorer, Chrome, VS Code, taskbar, Settings pages, folders, and Windows tools. Filter by category or search by name, category, context, shortcut, or target. The sequence builder shares the expanded catalog. Existing saved hotkeys are unchanged.

App-specific shortcuts require that app to be focused and use its default key bindings. Settings pages depend on Windows version and hardware; presets open pages rather than automatically changing settings. Tools are included only when their executable exists. Catalog definitions live in `src/ExpandedActions.cs`.

References: [Windows shortcuts](https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows), [Settings URIs](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings), [Chrome shortcuts](https://support.google.com/chrome/answer/157179?hl=en), and [VS Code defaults](https://code.visualstudio.com/shortcuts/keyboard-shortcuts-windows.pdf).


From this project directory in PowerShell 7 (`pwsh`, already available on this machine):

```powershell
.\build.ps1
.\bin\FunctionRowRemapper.Tests.exe --native
.\bin\FunctionRowRemapper.Tests.exe --ddc-detect
```

Close the running remapper before rebuilding. The script uses the built-in 64-bit C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, without downloading dependencies or changing script execution policy. It compiles the application and tests and runs the non-hook suite. Windows PowerShell 5 on this computer blocks .ps1 files under its existing policy; the verified build used PowerShell 7. No policy change is required to run the compiled app. `FunctionRowRemapper.csproj` is also supplied for Visual Studio/MSBuild with the .NET Framework 4.8 targeting pack.

The build defaults to `bin`. `Ddc.cs` implements detection and the Windows dxva2 monitor operations, with scoped physical-monitor handles and a separate monitor dispatcher. `UserPreferences.cs` stores the tray preference separately from mappings.

The implementation uses C# WinForms and Win32 `WH_KEYBOARD_LL`/`SendInput`. This was chosen because it produces a compact native Windows UI and a single executable with the toolchain already present here. `Configuration.cs` and `JsonSyntax.cs` handle persistence/validation; `Engine.cs` contains key state, dispatch, and native interop; `MainForm.cs` contains the editor/tray; `tests/Tests.cs` exercises the core and native hook.

The native tests use a test-only constructor that accepts a particular injected-event tag, plus an observation hook that consumes the test keys so they do not reach other applications. **They verify the Win32 hook path, not physical hardware input.** The shipping app has no switch or configuration field for enabling that test mode.

See `TEST-REPORT.md` for completed verification and `MANUAL-TESTS.md` for the physical acceptance checklist.

Microsoft references: [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc) and [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).

## Remove it

1. Untick Start with Windows and Save.
2. Exit the app from its window or tray menu.
3. Delete the executable/project folder when no longer needed.
4. Optionally delete `%LOCALAPPDATA%\FunctionRowRemapper` to remove configuration and backup files.

If the executable has already been removed, delete only the named `FunctionRowRemapper` value from the per-user Run key above. Do not delete the Run key itself. There is no service, driver, scheduled task, or system-wide keyboard setting to uninstall.

This locally built executable is not digitally signed. No installer or trusted-publisher certificate is included.
