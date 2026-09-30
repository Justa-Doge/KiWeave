# Verification report

## Dark design update, September 22, 2026

- 48 core/dispatch tests passed in the final build, with no compiler warnings.
- Editor preview checks cover saved Python/preset selection, editor-load preservation, search/no results, sequence reorder, empty/add-hotkey state, and saved configuration preservation. Preview Save is explicitly non-persisting.
- Dropdown containment checks pass in normal, minimum-size, and manually 1.5x-scaled test layouts. The height reserved for each dropdown includes its complete native height plus bottom clearance; popup menus use integral item heights.
- Rendered previews inspected for the main workspace, custom hotkeys, action library, and sequence builder. Computer Use confirmed the live dark main window and dark title bar. Attempts to open its dropdown were interrupted by detected user input, so live popup interaction is not claimed as verified.
- Installed executable hash matched the build. Mappings, tray preferences, and the existing startup entry were preserved. Prior executable/source and configuration backups are under `backups`.
- Physical hotkey actions, actual alternate display DPI, logon, and power/audio actions were not retested during this visual-only update.

**Search/tray update (September 12, 2026):** 45 core tests pass, including preference persistence/backup, malformed preference rejection, and preservation of corrupt files. The installed executable hash and Start menu shortcut target were verified. Windows `Get-StartApps` lists Function Row Remapper at the per-user installed path. See INSTALLATION.md for current behavior and locations. Earlier sections record earlier releases.

Live application checks: closing with tray enabled hid the window while its process remained running; launching the actual Start menu shortcut reopened the same window in the same process. Disabling tray use persisted immediately and disabled Hide to tray. Closing then terminated the process. Launching with `--tray` while the preference was off produced a visible window, confirming startup cannot hide an app with tray use disabled. The default-on preference was restored after testing. The saved mapping file hash was unchanged throughout, and the already-enabled startup entry points to the installed executable.

**DDC/CI update:** 42 core tests, 5 native-hook tests, and 3 actual monitor write/read/restore tests pass. The new eight core tests and the SE2426HG hardware results are recorded in [DDC-CI.md](DDC-CI.md). The user's subsequently saved mappings and enabled preference were preserved byte-for-byte during this update, and the existing enabled startup entry now points to the new executable. The initial-release details below describe the earlier delivery state.

Date: September 11, 2026. Machine: Windows x64, with .NET Framework release value 533509. No SDK or third-party package was installed.

## Automated results

### Expanded library update, 2026-09-22

Current build: `bin-expanded-library`. 51 automated checks passed, zero failed. Catalog has 429 entries in 10 categories; unique labels, every added preset's validation/serialization, and Settings launch routing passed. UI preview harness verified category/search filters, no-results state, sequence ordering, editor loading, layout bounds, and saved-config preservation. Dark category picker render was visually inspected. Individual app shortcuts and all Settings pages were not executed; availability depends on app focus, bindings, Windows version, and hardware. Results below describe earlier releases.


**34 core/dispatch tests passed; 0 failed.**

Coverage includes safe defaults; all nine action types; JSON round-trip; unsupported versions; unknown/duplicate fields and keys; malformed/oversized input; strict JSON syntax; 1,500 random malformed inputs; reserved and malformed shortcuts; path restrictions; missing target handling; atomic replacement and backup; failed-save preservation; all twelve function keys; injected-event isolation; down/up pairing across enable/disable; repeat rules; emergency hold timing/rearm; keyboard/media dispatch; process launch settings; script quoting; x64 native structure layout; and a real harmless command with spaced arguments and an explicit working directory.

**5 native-hook tests passed; 0 failed.**

1. Production-mode hook ignores injected F5 events.
2. Test-marked F5 is suppressed; one F6 down/up pair is emitted despite repeated source key-downs.
3. Unbound suppresses both edges; global off restores pass-through.
4. Holding a test-marked function key starts exactly one real probe process.
5. Pass-through works, and disposing the hook restores both edges.

Native tests install real `WH_KEYBOARD_LL` hooks and use `SendInput`, with a test-only injected-event acceptance path. An observation hook consumes test keys before they reach foreground apps. These are integration tests of Windows interop and dispatch, not proof of physical keyboard/Fn behavior.

## Interface and persistence

- Launched the built executable on this machine and inspected the actual window visually and through accessibility.
- Confirmed all twelve labeled key rows, action selector, media editor, global switch, status, startup opt-in, Save, reset, Unbound, export/import, and Hide to tray controls render.
- Selected F5, changed it to a media action, and saved through the UI while global remapping remained off.
- Confirmed the JSON contained the selected mapping and the off preference; closed the app cleanly and reopened the final build to check restoration.
- Restored all keys to Pass through and kept remapping off for delivery. Windows startup was not enabled.
- The displayed layout was inspected at the current desktop scaling. Other DPI/scaling and Windows 10 are not claimed as tested.

## Source inspection

No keystroke log, typed-text collection, telemetry client, network call, driver installation, firmware change, or system-wide keyboard setting is present. The only registry mutation implemented is the explicitly enabled/disabled per-user Run value. Shell execution is reached only by action dispatch from a mapped press. Import and edit paths only validate and update the draft. The test executable separately creates short-lived fixtures under a unique temporary directory and removes that directory afterward.

## Not verified hands-on

Physical keyboard presses; Apple/Boot Camp Fn handling; audible/system-volume change coupled with browser refresh suppression; real held emergency chord; actual file associations and .lnk launching; elevated applications; logon startup; games/raw-input apps; and arbitrary user scripts. File/folder/.lnk launch routing is verified with a fake launch sink, not by opening user files. The precise remaining steps are in `MANUAL-TESTS.md`.

The executable is a locally built, unsigned Windows application, not a security-audited or broadly hardware-certified release.
