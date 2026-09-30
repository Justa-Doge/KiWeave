# Physical keyboard acceptance checklist

Use the installed Start menu/Search entry or `bin/FunctionRowRemapper.exe`. The **Use system tray** setting controls close behavior; **Exit** always quits. Test close/reopen with tray on, then close with tray off; startup with tray off must remain visible.

For the DDC update, use `bin/FunctionRowRemapper.exe`. Detection and real monitor write/restore tests passed on the SE2426HG; see [DDC-CI.md](DDC-CI.md). Also verify:

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
- [ ] **Tray and close:** With Use system tray on, close the window and reopen it from the blue F icon or Search. Remapping should keep running. With tray use off, closing must exit. The Exit button must quit in either mode.
- [ ] **Startup (optional):** Only if desired, tick Start with Windows and Save. Sign out/in and verify tray startup. Untick and Save afterward if not wanted.
- [ ] **Elevated app limitation:** Compare a standard and elevated harmless app, if needed. Record limitations without changing Windows security settings.

The source audit and automated tests verify that the program does not write pressed keys to logs. Optional additional inspection: the local configuration directory should contain only JSON configuration/backup files after normal use.
