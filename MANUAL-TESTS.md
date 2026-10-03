# Physical keyboard acceptance checklist

## KiWeave 1.0 additions

- [ ] With **Allow network access** off, verify Check for updates refuses locally and a disposable HTTP mapping reports that network access is blocked without contacting the endpoint. Confirm ordinary remapping, profiles, layers, backups, and diagnostics still work.
- [ ] Enable network access, repeat the update check, and trigger a disposable loopback-only HTTP action. Turn network access off again afterward unless it is wanted.
- [ ] Open Privacy Center and verify the listed storage sizes are plausible. Preview and export safe diagnostics, then inspect the file for mapping targets, custom profile names, commands, arguments, paths, URLs, request bodies, typed text, or key history; none should appear.
- [ ] Create a harmless log entry through a failed action, use **Clear local logs**, and verify mappings, profiles, preferences, and backups remain unchanged.
- [ ] Add a modifier layer using Caps Lock, map Layer F5 to Mute, save, then hold Caps Lock and press F5. Verify mute fires, neither Caps Lock nor F5 reaches the foreground app, and plain F5 still uses the base mapping after release.
- [ ] While a layer key is held, disable shortcuts with the mouse and then release the layer key. Verify no stuck or unmatched key remains. Also press a second configured layer key while the first is held; the first layer should remain active and the second key should pass through normally.
- [ ] Export and re-import a configuration with two layers, then copy it into a named profile. Verify layer names, activation keys, and all mappings survive without activating or executing anything during import.
- [ ] Open the bottom-left gear Settings page and verify startup, tray, automatic-profile, and update-check toggles persist immediately after reopening KiWeave. Confirm mapping edits still require Save changes.
- [ ] Turn launch update checks off, restart KiWeave, and confirm startup remains quiet; use Check for updates to verify the manual result appears without downloading anything.
- [ ] Create a `.keyweave` full backup, inspect its summary, restore it after a harmless temporary change, and verify the rollback folder was created. Confirm PowerToys settings were not changed.
- [ ] Create two profiles, assign a different test application to each, hide KiWeave, and verify the active profile changes only when the matching app is foreground.
- [ ] Confirm automatic switching pauses while the editor is visible and while it has unsaved changes.
- [ ] Switch profiles from the tray and verify the selected function-key and custom-hotkey mappings become active.
- [ ] Click the active-profile badge and verify the explanation matches a manual selection or foreground-app rule. Pin the profile, foreground a differently matched app, and verify the pinned profile remains active; resume automatic switching and verify matching resumes after KiWeave is hidden.
- [ ] Assign **Activate and pin profile** to a harmless F-key or custom hotkey. Trigger it and verify the selected profile becomes active and remains pinned only until resume or app exit.
- [ ] Create a profile inheriting from Default and override only F2. Change Default F1, reopen the inherited profile, and verify F1 follows Default while F2 keeps its override. Verify an inheritance loop is refused.
- [ ] Open **Settings → Live key tester**, press a physical F key and an ordinary typing key, and verify only the latest event is shown with modifiers, source, decision, profile/layer, and resolved action. Close/reopen it and verify no prior event or key history remains.
- [ ] Open **Settings → Conflict center** with a harmless duplicate shortcut or profile rule. Verify the result explains what wins, exposes no action target/path/URL, and **Open relevant area** navigates without changing or executing anything.
- [ ] Choose **Record** beside a custom-hotkey shortcut, physically press Ctrl+Alt+K, release it, and verify the dialog shows exactly `Ctrl+Alt+K` without activating another app. Accept it, then repeat for a Send key/Send shortcut action field. Verify a plain letter is rejected for the global hotkey, Ctrl+Alt+Delete is rejected, Escape cancels, and reopening the recorder shows no prior key.
- [ ] Save a harmless mapping change, open **Settings → Undo and history**, and verify a new local snapshot describes the changed key without showing its target/path/URL. Restore it, confirm the setup returns to the earlier value, and verify the pre-restore state was itself preserved as a newer history entry. Do not use a destructive action for this test.
- [ ] Use Diagnostics and verify its copied report contains no action targets, command arguments, URLs, or personal paths.
- [ ] Test **Switch to next audio output** with at least two active playback devices, then restore the preferred default device.
- [ ] Test center-window and always-on-top against ordinary, non-elevated windows.
- [ ] Test Discord, Spotify, OBS, and PowerToys actions only after reviewing the relevant app's own shortcut/settings state.
- [ ] Point an HTTP action at a disposable local endpoint and verify GET/JSON POST behavior and the eight-second timeout. Do not use secrets in exported configurations.
- [ ] Build a conditional action on a harmless F key: **Foreground application is Notepad.exe**, matching action **Mute / unmute**, fallback **Do nothing**. Save, focus Notepad, press the key, then focus another app and press it again. Verify only the matching press mutes.
- [ ] Change the same rule to **Application is running**, leave Notepad open in the background, and verify the matching branch runs. Close Notepad and verify the fallback runs. Open Action info and an exported/re-imported review to confirm both outcomes and their permission labels are shown without executing either one.
- [ ] Fully exit ordinary KiWeave, hold Shift while launching, and verify **KiWeave Safe Mode** opens visibly. Repeat through the separate Start-menu shortcut. Confirm no tray icon appears and ordinary mappings/custom hotkeys do not run.
- [ ] In Safe Mode, review recovery status, redacted diagnostics, Privacy Center, and the data folder. Export a private backup, open Undo and history without restoring, then close Safe Mode. Verify the active configuration files are byte-for-byte unchanged.
- [ ] With a disposable test backup, confirm Safe Mode shows its summary and asks before restore. Cancel once and verify no changes. If deliberately testing restore, verify a rollback folder is created and mappings remain inactive until **Restart normally** is separately confirmed.

Use the installed Start menu/Search entry or `bin/KiWeave.exe`. The **Keep running in tray** setting controls close behavior; **Exit** always quits. Test close/reopen with tray on, then close with tray off; startup with tray off must remain visible.

For the DDC update, use `bin/KiWeave.exe`. Detection and real monitor write/restore tests passed on the SE2426HG; see [DDC-CI.md](DDC-CI.md). Also verify:

- [ ] Map a spare key to Monitor (DDC/CI) / monitor-volume down with a 1% step, Save, and press it physically. Verify hardware volume changes and the key's original action is suppressed.
- [ ] Hold the key briefly; adjustments should repeat without a long queue of changes after release. Restore the desired volume afterward.
- [ ] Repeat with monitor brightness and contrast if desired. Check global off and the emergency chord with a DDC mapping.
- [ ] Disconnect/reconnect or switch off the monitor and use Detect monitors. Saved mappings must remain attached to the selected monitor and show an error when unavailable, never target a different display.
- [ ] Export/import a DDC configuration and confirm version-2 fields survive; import should not run an adjustment.

Keep settings visible during the first test. Start with remapping off. Save mappings before enabling them. These checks require the user's actual keyboard and applications; they are not claimed as completed by synthetic-input tests.

- [ ] **F5 volume and suppression:** Map F5 to Media / Volume up and Save. Open a harmless browser page; turn remapping on, then physically press F5. Verify volume rises and the page does not refresh. Hold F5 briefly to verify repeat. Restore the desired volume afterward. If necessary compare F5 and Fn+F5 on the Apple/Boot Camp keyboard.
- [ ] **Plain key:** Map F4 to Send a key / A. In a blank unsaved Notepad document, press F4 once and hold it briefly. Exactly one A should appear for each separate press. No F4 behavior should leak through.
- [ ] **Shortcut:** Map F6 to Ctrl+Shift+S. In a harmless blank document, verify one Save As dialog opens per press; cancel it. Try Alt+Tab if desired. Confirm modifier keys still behave normally afterward.
- [ ] **No recursion:** Map F4 to F5 and F5 to Volume up. Press F4 once. It should emit a normal F5, not activate the F5 mapping. Choose a harmless foreground application for this check.
- [ ] **Application launch:** Map F7 to the Notepad executable using the file picker. Press and hold F7. Only one launch should occur. Release and press again to verify a second launch.
- [ ] **File/folder/shortcut:** In turn, map F8 to a harmless .txt file, a local folder, and a .lnk pointing to Notepad. Verify the expected associated app, Explorer folder, and shortcut open. Close test windows afterward.
- [ ] **Command/script:** Choose a harmless existing test script or command and verify its expected local result. Try arguments containing spaces and a working directory. Do not test commands you do not understand.
- [ ] **Unbound:** Map F5 to Unbound. Press it in a browser; nothing should happen.
- [ ] **Pass through:** Map F5 to Pass through. Its normal refresh behavior should return.
- [ ] **Global off:** With F5 mapped to a media action, turn remapping off in the window, then test again using the tray toggle. F5 should behave normally.
- [ ] **Emergency off:** Enable a mapping, hold Ctrl+Alt+Shift together for at least 1.5 seconds, and release. Confirm the status changes to off and new F-key presses pass through. Repeat once with right-side modifiers. It should never toggle on by itself.
- [ ] **Mid-press transitions:** Hold a mapped key, turn remapping off with the mouse, then release the key. New presses should pass normally. No modifier or destination key should remain stuck.
- [ ] **Exit:** Enable a mapping, exit the app, and verify immediate restoration of normal keys. Reopen it to continue.
- [ ] **Persistence:** Save several mappings, exit, and reopen. Verify the mappings and enabled preference are restored.
- [ ] **Export/import:** Export, change the editor, then import the exported file. Verify all twelve entries match. Changes should become active only after Save; import should not turn remapping on.
- [ ] **Invalid import:** Import a copy with invalid JSON, a duplicate key, an unknown field, an invalid shortcut, or a non-local target. Verify an error and no replacement of valid live/editor configuration.
- [ ] **Missing target:** Save a mapping to a disposable test file, then move that file. Trigger the mapping and verify a visible warning with no crash.
- [ ] **Tray and close:** With Keep running in tray on, close the window and reopen it from the blue F icon or Search. Remapping should keep running. With tray use off, closing must exit. The Exit button must quit in either mode.
- [ ] **Startup (optional):** Only if desired, tick Start with Windows in Settings. Sign out/in and verify tray startup. Untick it afterward if not wanted.
- [ ] **Elevated app limitation:** Compare a standard and elevated harmless app, if needed. Record limitations without changing Windows security settings.

The source audit and automated tests verify that the program does not write pressed keys to logs. Optional additional inspection: the local configuration directory should contain only JSON configuration/backup files after normal use.
