# KiWeave

Current development build: **1.0.0-beta.3**. This is a beta checkpoint, not the final 1.0 release. Optional update checks run at launch and every 12 hours while KiWeave stays open; they remain blocked by the master network switch and only notify.

A Windows tray app for turning F1 through F12 and custom global hotkeys into personal actions, profiles, app integrations, and automations. Remapping works offline; an optional background check looks for newer versions on GitHub.

Previously called Function Row Remapper. The application is now fully installed as **KiWeave**, including `KiWeave.exe`, its Start-menu entries, startup identity, install folder, and `%LOCALAPPDATA%\KiWeave` data folder. Existing Function Row Remapper data is copied forward without overwriting newer KiWeave files; the old folder is left untouched as a recovery copy.

## License and credit

KiWeave is licensed under the [Apache License 2.0](LICENSE). Redistributed copies and derivative works must preserve the license and the attribution in [NOTICE](NOTICE): **KiWeave © 2026 Justa-Doge**.

The build and installer place both files beside the executable so binary distributions retain the same license and attribution.

Copyright does not cover a general software idea. If KiWeave inspires a separate implementation that does not reuse its protected code or artwork, credit is still warmly requested: **Inspired by KiWeave by Justa-Doge**.

The interface uses a charcoal dark theme with purple accents, a sidebar for switching editors, rounded controls, and keycap-style mapping rows. Action labels use sentence case. The library and sequence windows share the same theme; standard Windows file/message dialogs still follow Windows' own appearance.

The **Custom hotkeys** sidebar page also lets you assign up to 32 global Ctrl, Alt, Shift, or Win shortcuts to the same actions. It includes **Run a Python script** for a local `.py` file. Python runs with the signed-in Windows account only, never changes execution/security policy, and needs the normal Python launcher (`py.exe`) already installed.

Use **Record** beside a shortcut field to capture a key combination instead of typing its name. Capture exists only while the review dialog is open, suppresses the physical keys so the shortcut does not fire during recording, keeps no history, and writes no log. Nothing is applied until the exact captured combination is reviewed and accepted; custom global hotkeys still require Ctrl, Alt, Shift, or Win, and reserved security combinations are rejected.

Both editors start with a short category menu instead of a long raw action list. Choose Keyboard input, Media and sound, Open or run something, or Custom action, then select the specific action beneath it. Custom action includes ready-made lock, sleep, Explorer, Settings, Task Manager, clipboard history, screenshot, desktop, app-switching, editing, Calculator, sign-out, restart, and shut-down actions. System-changing presets only run if the assigned key is pressed.

The Function keys page also supports up to four named **modifier layers**. Choose **Manage layers**, give each layer a hold key, then select it from the Editing dropdown and assign its F1–F12 actions. While that activation key is physically held, KiWeave suppresses it and uses the layer mappings. Releasing it restores the base layer. Only one layer can be active at a time, injected keys cannot activate one, and disabling KiWeave safely pairs any key release already in progress.

Use **Browse action library** on Function keys or **Custom action** on Custom hotkeys to open the searchable library. Search any part of an action name, such as `clipboard`, `snap`, `device`, `sleep`, or `paint`, then choose **Use action**. The short dropdown menus still provide direct access by category. Only the fields needed by the selected action are shown.

Use **Test action** to validate and run the currently edited action once. KiWeave always shows a final summary and confirmation first because tests can send keys, launch programs, contact configured endpoints, or change hardware.

Use the compact **Action info** button to inspect an action's permission label and effect without executing, launching, sending, or contacting anything. Sequence information includes its step count and configured wait time.

Choose **Build a conditional action...** under Custom action to make an F key or custom hotkey react to local application state. The current builder can check whether a named `.exe` is the foreground application or is running, then choose one reviewed action when it matches and an explicit fallback otherwise. KiWeave checks only when the assigned key or hotkey is pressed. It does not continuously monitor window titles, run condition scripts, or contact a server. Action info and import quarantine show the permissions of both possible outcomes.

For a Custom hotkey, choose **Build sequence...** to open the separate sequence builder. Search the full action library on the left; add actions and waits to the numbered blocks on the right. Reorder, edit, or remove steps, then choose **Use sequence**. Opening the builder does not execute steps. A sequence runs its steps in order when the saved shortcut is pressed. It supports up to 20 steps, each wait is limited to 60 seconds, and total waiting time is limited to five minutes.

Both editor pages share **Shortcuts enabled** and **Save changes**. Use the gear button at the bottom-left for **Settings**, including Windows startup, tray behavior, automatic profile switching, the master network switch, update checks, the Privacy Center, full backups, diagnostics, data/log folders, the welcome guide, and About. Background-behavior settings apply immediately; mapping edits still wait for **Save changes**. **More** retains quick access to profiles, import/export, function-key reset/disable, and **Exit app**. The editor panels scroll when needed at smaller window sizes.

Open **Settings → Undo and history** to review the last 20 local snapshots created before mapping saves, enable-state changes, profile edits, backup restores, and history restores. The comparison names changed controls without revealing action targets or URLs. Restoring requires selecting an entry and confirming it; KiWeave first preserves the current setup in history and also creates its normal rollback folder. History never syncs or restores automatically, but its `.keyweave` files can contain private mappings and remain visible in the Privacy Center's local-data inventory.

**More → Back up everything** creates a versioned `.keyweave` file containing saved mappings, profiles, preferences, startup preference, and a read-only inventory of discovered PowerToys shortcuts. Restore validates the entire bundle, summarizes its contents, asks before replacing anything, and creates a local rollback copy. PowerToys settings are never changed by restore. Because action targets, arguments, URLs, or HTTP bodies may be private, treat backup files as sensitive.

## Profiles and automatic switching

Open **More → Profiles** to copy the current mappings into a named profile. A profile contains its own twelve base function keys, modifier layers, custom hotkeys, sequences, and enabled state. A profile may remain independent or inherit unchanged behavior from Default or another profile; KiWeave stores an explicit override mask, follows later base-profile changes for everything else, and rejects missing bases or inheritance loops. Add process names such as `obs64.exe`, `Discord.exe`, or `Spotify.exe` to switch automatically while that application is in front. An application can belong to only one profile. Automatic switching pauses while the KiWeave window is open or has unsaved edits.

Profiles can also be selected manually from the tray menu. The header badge and tray menu show the active profile; clicking the badge or **Active: ... · Why?** explains whether it was selected manually, matched an app rule, restored, or pinned. **Pin current profile for this session** pauses automatic switching until it is resumed or KiWeave exits, without changing saved preferences. Profile actions appear in both mapping editors and activate plus pin the chosen profile, so an F-key or custom hotkey can switch modes without immediately being overridden by the foreground-app rule. The original `config.json` remains the **Default** profile. Additional profiles are stored in `%LOCALAPPDATA%\KiWeave\profiles.json`, with an atomic-save backup beside it.

## Windows and app integrations

The action library includes window centering, always-on-top, switching to the next active Windows audio output, Discord mute/deafen, Spotify media control, OBS recording/streaming controls, and opening PowerToys settings. Existing snap, monitor-move, media, DDC/CI, Windows Settings, and PowerToys shortcut actions remain available. Discord voice control is experimental in beta 3 and may not work until the KiWeave Discord application is public/approved; the normal Discord shortcut fallback remains available.

**Call an HTTP endpoint** performs an eight-second GET when its body is empty or a JSON POST when a body is supplied. It is intended for local dashboards, webhooks, and Stream Deck-style tools. The URL cannot contain embedded credentials. HTTP actions contact the configured server only when their assigned key is pressed and the master network switch is on; KiWeave does not send them automatically.

Open **More → Diagnostics** for a copyable, read-only report containing engine/profile/integration counts and status. It deliberately excludes action targets, command arguments, configured URLs, and personal file paths.

Open **Settings → Live key tester** to inspect the latest key event, modifiers, physical/injected source, suppression decision, active profile/layer, and resolved action. The tester subscribes only while its window is open, keeps only the latest event in memory, and never logs or saves key history. Existing mappings still run normally during observation.

Open **Settings → Conflict center** for a read-only scan of overlapping KiWeave hotkeys, common Windows-owned shortcuts, active PowerToys shortcuts, modifier-layer activation keys, invalid profile rules, and ambiguous automatic-profile matches. Each finding explains what currently wins and can open the relevant editor. The scan never executes an action, reveals private action targets, changes a mapping, or rewrites another application's settings.

KiWeave also keeps a small rotating local error log. Entries contain only a UTC timestamp, component name, exception type, and numeric error code. Raw error messages, keys, targets, arguments, URLs, and typed text are never written. Use **More → Open log folder** when troubleshooting.

## PowerToys shortcuts

Choose **Custom hotkeys → PowerToys**. The left side shows only shortcuts that are assigned and whose PowerToys module is enabled; select one to edit its details on the right. Choose **Add shortcut** to browse off or unassigned PowerToys functions using module and specific-function dropdowns. After an editable function is enabled, assigned, and saved, it moves into the active list. **Refresh** picks up changes made directly in PowerToys. Keyboard Manager's existing shortcut remaps are shown when its active profile exists.

PowerToys remains the owner of these shortcuts; KiWeave does not register them a second time. PowerToys must be running for its actions to work. KiWeave's **Save changes** button applies only to KiWeave's own hotkeys, while **Save in PowerToys** applies a supported PowerToys shortcut edit.

For modules supported by the installed `PowerToys.DSC.exe`, you can edit the key combination and optionally enable an off module using **Save in PowerToys**. KiWeave first backs up the original PowerToys settings under `%LOCALAPPDATA%\KiWeave\PowerToysBackups`, rejects a stale or conflicting edit, applies it through PowerToys' configuration tool, then checks the saved result. If PowerToys does not activate it immediately, restart PowerToys yourself; KiWeave does not restart it. Modules without supported configuration, and Keyboard Manager's remap rules, appear read-only; use **Open PowerToys Settings** to edit those. This integration does not claim to control every PowerToys action or bypass Windows-reserved shortcuts.

Run **bin/KiWeave.exe**, or use the installed **KiWeave** entry in Windows Search. See [INSTALLATION.md](INSTALLATION.md) for installation, the **Keep running in tray** setting, and removal instructions.

## Update notifications

When both **Allow network access** and update checks are enabled in Settings, KiWeave quietly checks public, stable `vX.Y.Z` releases through GitHub's HTTPS API at launch. **Check for updates** runs the same check on demand. If a release is newer than the installed version, it shows a Windows notification linking to that release. It never downloads or installs an update, needs no Git installation or GitHub account, and uses an eight-second limit. Alpha and beta GitHub prereleases are ignored by the stable update checker. If the repository is private, GitHub is unavailable, or the PC is offline, the launch check is skipped without delaying startup. A release needs a matching version bump in `src/UpdateChecker.cs` and a published GitHub release. Ordinary commits and tags alone do not trigger notifications.

DDC/CI supports detected monitor brightness, contrast, and hardware-volume actions. See [DDC-CI.md](DDC-CI.md) for setup, compatibility, and verification.

## Run it

Brand-new installations open with a short welcome guide explaining safe defaults, testing, saving, profiles, backups, and the emergency pause chord. Existing users with saved configuration are not interrupted by it.

## Safe Mode and recovery

Hold **Shift while launching KiWeave**, or open the separately installed **KiWeave Safe Mode** Start-menu shortcut. Safe Mode uses a separate visible recovery window and never constructs the normal editor or keyboard engine. Keyboard hooks, global hotkeys, mapped actions, automatic profiles, integrations, tray hiding, update checks, HTTP actions, and all other network access remain inactive.

Safe Mode validates the saved configuration, profiles, and preferences without activating them. It can open redacted diagnostics and the Privacy Center, open the local data folder, export a private backup when the saved setup is readable, restore a reviewed `.keyweave` backup, or restore a local undo-history snapshot. Restores create a rollback folder first. Merely opening Safe Mode never repairs, deletes, overwrites, or enables anything. **Restart normally** warns before leaving recovery mode because saved enabled mappings may become active after normal startup.

Safe Mode will not run beside an existing normal KiWeave process. Exit the running app first so recovery cannot coexist with active hooks or registered hotkeys.

Run `./build.ps1` from PowerShell 7 first, then open **bin/KiWeave.exe**. No installer, account, scripting runtime, or administrator access is needed. You can copy that one executable to a permanent local folder. This x64 build uses the Windows .NET Framework 4.x runtime; it is not a bundled .NET runtime or an ARM64 build. Intended for Windows 10/11 x64; tested on one Windows 11 machine only.

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
| System or integration action | Window controls, audio-device switching, Discord, Spotify, OBS, and PowerToys actions. |
| HTTP request | GET an HTTP(S) URL, or POST an optional JSON body, when the assigned key is pressed. |
| Conditional action | Check whether a selected app is foreground/running at trigger time, then run the matching action or explicit fallback. |

Fields use full local drive paths, such as `C:\Tools\app.exe`. Do not add quotes around the target path; use argument quoting where needed. Environment-variable paths, network shares, URLs, device paths, alternate data streams, and wildcard paths are rejected. Key names use Windows virtual-key names; keyboard layouts can affect punctuation.

Commands run with your account's permissions. Only use commands and shortcuts you trust. For shell built-ins, choose `C:\Windows\System32\cmd.exe` and supply arguments, for example `/d /c echo Hello` (a hidden window means its output will not be displayed). PowerShell scripts run using Windows PowerShell with `-NoProfile -NonInteractive -File`, respecting the machine's existing execution policy. The app never changes that policy. Applications use their normal window behavior; command/script windows are requested hidden. **Test action** always summarizes the action and asks for confirmation before executing it once.

**More → Reset function keys** stages twelve Pass-through mappings. **More → Disable all function keys** stages twelve swallowed keys. Both need Save changes. They leave the global switch and startup preference as they are.

## Key behavior and emergency off

- Hold **Ctrl + Alt + Shift together for 1.5 seconds** to turn remapping off. Left or right modifiers work. Release the keys afterward. This only turns remapping off; it never re-enables it.
- You can also switch it off in the app or from the tray menu. Click **Exit** in the app or tray to quit and remove the hook. With **Keep running in tray** on, closing the window hides it and keeps remapping running; with it off, closing exits.
- A press already in progress keeps its original suppression decision through release. After disabling, release any held F key; new presses pass through. This avoids mismatched key-down/key-up events.
- Windows volume up/down and DDC monitor adjustments repeat while held. All other actions, including shortcuts, key taps, launches, and mute, fire once per physical press. Sent shortcuts are complete down/up taps, not held keys or macros.
- Held Ctrl/Alt/Shift/Win modifiers also apply to the destination action. The app does not release modifiers you are holding. For example, holding Shift while triggering a mapped A can produce uppercase A. Release a physically held destination key before triggering its mapping.
- Synthetic input is ignored, including this app's output and other automation tools' injected function keys. This prevents recursive remapping. Only physical standard F1–F12 input is remapped in the production app.
- Disabling cancels pending queued actions; a process that already began launching may still open. The action queue is bounded; extremely rapid input can drop excess queued actions.

## Save, import, and export

Configuration is human-readable JSON: version 1 for original actions, version 2 when it contains monitor actions, version 3 when it contains custom hotkeys, and version 4 when it contains modifier layers. All four versions import. It is stored at:

Saving at least one custom hotkey uses version 3 and adds a `customHotkeys` section. Adding a modifier layer uses version 4 and adds a `layers` section while preserving the base mappings and custom hotkeys. A custom hotkey must include at least one modifier so ordinary typing cannot be captured. Windows-reserved or already-used combinations are reported when you save.

`%LOCALAPPDATA%\KiWeave\config.json`

Saves write and flush a temporary file in the same directory, then atomically replace the current file. `config.json.bak` retains the previous saved version. A corrupt existing file is not overwritten just by opening the app; the app opens with safe defaults and an error. You can inspect or restore the backup while the app is closed.

**Export configuration** writes the current editor draft, including unsaved edits, without activating it. **Import configuration** checks size, JSON syntax, duplicate/unknown fields, version, exactly twelve unique function keys, actions, paths, and shortcut syntax, then keeps the result quarantined in a private review dialog. The review lists every non-default mapping, target, command, URL/body, and permission label before the user can stage it in the editor. Cancelling or rejecting an invalid import leaves both the editor and live mappings intact. The maximum file size is 64 KB.

Imports never execute commands, enable remapping, or change startup. Review all twelve mappings, especially launch targets and commands, then Save. A missing target may be imported for review, but Save and dispatch report an error until it is fixed. A target removed after saving is checked again when invoked.

`sample-config.json` gives examples: F1 volume down, F2 volume up, F3 mute, F5 volume up, F6 Ctrl+Shift+S; the rest pass through. The sample has remapping off and contains no launches. `defaults.json` contains the original all-Pass-through configuration.

## Start with Windows

Open the bottom-left **Settings** page and tick **Start with Windows**. The change applies immediately and creates only the per-user startup value:

`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run\KiWeave`

The value is the quoted executable path followed by `--tray`. Untick it to remove the value immediately. Startup is separate from the imported/exported mapping JSON so an import cannot enable persistence. Place the executable in its permanent folder before enabling startup. If you move it later, toggle the setting off and on from the new location to update the entry.

## Windows and keyboard limitations

The app captures standard Windows F1–F12 virtual-key events. Apple/Boot Camp keyboards and laptop Fn modes may generate media events instead, or handle brightness/media functions in firmware. Try Fn+F5 if plain F5 produces a hardware action. The app does not intercept dedicated media keys, change Fn mode, alter firmware, install drivers, or intercept actions handled entirely by keyboard firmware. It cannot reliably identify the source hardware when firmware handles an event entirely outside Windows.

Windows integrity boundaries can block a normal-privilege remapper from sending input into elevated apps. UAC secure-desktop prompts and Ctrl+Alt+Delete are outside its scope. Games, anti-cheat systems, raw-input apps, and other keyboard hooks may behave differently. No security boundary is bypassed.

The status shows **Active in background**, **Shortcuts paused**, or **Unavailable**. This reflects the app's current engine state, not a continuous operating-system health probe. Windows can silently remove a low-level hook if its callback times out; it does not provide reliable removal notification. If remapping stops unexpectedly, exit and reopen the app. The hook runs on a dedicated message-loop thread and queues action execution to a separate worker to keep callbacks short. Windows removes a process's hook when that process terminates.

No account, analytics, telemetry, advertising identifier, cloud sync, or key logs are used. New installations begin with the master network switch off. When allowed, KiWeave's own network access is limited to the optional GitHub update check and user-created HTTP actions. HTTP action URLs and bodies stay in the user's local configuration; they are transmitted only to the configured endpoint when the assigned action runs. Layer names, activation keys, profiles, inheritance masks, mappings, and backups stay local. The hook examines event metadata to identify function keys and injected events; normally it retains only twelve active press states and the currently held layer key. The optional live tester temporarily exposes only the latest event to its open window and clears it on close. Emergency polling reads only Ctrl/Alt/Shift key state. No general typed text or key history is persisted.

The **Privacy Center** lists every network-capable KiWeave feature, the current master-network state, local storage categories and sizes, and whether a cache exists. It can open the data folder, delete only the bounded local diagnostic logs, preview safe diagnostics, and export that redacted report. Safe diagnostics exclude mapping targets, commands, arguments, paths, URLs, request bodies, typed text, key history, and custom profile names. Full `.keyweave` backups remain private and are never safe-diagnostic exports.

## Build and test

Tagged `vX.Y.Z` pushes run the Windows release workflow, repeat the normal and native-hook tests, build a per-user installer with uninstall support, create a portable ZIP, generate SHA-256 checksums, and publish the files as a GitHub release. The installer preserves mappings and profiles when updating or uninstalling. Release binaries are currently unsigned, so Windows may show an unfamiliar-publisher warning.

### Expanded action library (2026-09-22)

The searchable library now contains 442 entries across 16 categories, including Windows/window management, audio devices, Discord, Spotify, OBS Studio, PowerToys, text editing, File Explorer, Chrome, VS Code, taskbar, Settings pages, folders, and Windows tools. Filter by category or search by name, category, context, shortcut, or target. The sequence builder shares the expanded catalog. Existing saved hotkeys are unchanged.

App-specific shortcuts require that app to be focused and use its default key bindings. Settings pages depend on Windows version and hardware; presets open pages rather than automatically changing settings. Tools are included only when their executable exists. Catalog definitions live in `src/ExpandedActions.cs`.

References: [Windows shortcuts](https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows), [Settings URIs](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings), [Chrome shortcuts](https://support.google.com/chrome/answer/157179?hl=en), and [VS Code defaults](https://code.visualstudio.com/shortcuts/keyboard-shortcuts-windows.pdf).


From this project directory in PowerShell 7 (`pwsh`, already available on this machine):

```powershell
.\build.ps1
.\bin\KiWeave.Tests.exe --native
.\bin\KiWeave.Tests.exe --ddc-detect
```

Close the running remapper before rebuilding. The script uses the built-in 64-bit C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, without downloading dependencies or changing script execution policy. It compiles the application and tests and runs the non-hook suite. Windows PowerShell 5 on this computer blocks .ps1 files under its existing policy; the verified build used PowerShell 7. No policy change is required to run the compiled app. `KiWeave.csproj` is also supplied for Visual Studio/MSBuild with the .NET Framework 4.8 targeting pack.

The build defaults to `bin`. `Ddc.cs` implements detection and the Windows dxva2 monitor operations, with scoped physical-monitor handles and a separate monitor dispatcher. `UserPreferences.cs` stores tray, update-check, and automatic-profile preferences separately from mappings, while the Windows startup choice remains in the per-user Run key.

The implementation uses C# WinForms and Win32 `WH_KEYBOARD_LL`/`SendInput`. This was chosen because it produces a compact native Windows UI and a single executable with the toolchain already present here. `Configuration.cs` and `JsonSyntax.cs` handle persistence/validation; `Engine.cs` contains key state, dispatch, and native interop; `MainForm.cs` contains the editor/tray; `tests/Tests.cs` exercises the core and native hook.

The native tests use a test-only constructor that accepts a particular injected-event tag, plus an observation hook that consumes the test keys so they do not reach other applications. **They verify the Win32 hook path, not physical hardware input.** The shipping app has no switch or configuration field for enabling that test mode.

See `TEST-REPORT.md` for completed verification and `MANUAL-TESTS.md` for the physical acceptance checklist.

Microsoft references: [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc) and [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).

## Remove it

1. Untick Start with Windows in Settings.
2. Exit the app from its window or tray menu.
3. Delete the executable/project folder when no longer needed.
4. Optionally delete `%LOCALAPPDATA%\KiWeave` to remove configuration and backup files. A migrated legacy `%LOCALAPPDATA%\FunctionRowRemapper` folder may remain as a recovery copy until you deliberately remove it.

If the executable has already been removed, delete only the named `FunctionRowRemapper` value from the per-user Run key above. Do not delete the Run key itself. There is no service, driver, scheduled task, or system-wide keyboard setting to uninstall.

This locally built executable is not digitally signed. No installer or trusted-publisher certificate is included.
