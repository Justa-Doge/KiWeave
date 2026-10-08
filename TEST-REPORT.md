# Verification report

## Release-readiness cleanup, October 7, 2026

- Removed duplicate **About KiWeave** and **Open log folder** entries from the global **More** menu; both remain available in Settings.
- `bin-release-cleanup-compile\\KiWeave.exe` and its uninstaller compile successfully with the current source and no compiler diagnostics.
- The most recent completed full local build remains `bin-diff-fix-final`, with 139 checks passing. A later full build attempt entered the native-hook runner without producing output and was stopped; the user-owned KiWeave process was not touched.
- No public version, tag, release package, or checksum was changed.

## Safe Mode and recovery local build, October 2, 2026

The non-hook suite passes 89 checks with zero failures and the real Windows-hook suite passes 7 checks with zero failures. New checks cover explicit `--safe-mode`, Shift-at-launch, the deliberate normal-restart override, forced network blocking, absence-of-hook/hotkey diagnostics, redaction of local paths and account identity, and dark-title consistency. Existing action, conditional, privacy, history, backup, profile, layer, and dispatcher coverage remains green.

The recovery window was rendered and inspected at normal and supported minimum sizes, including its scrolled minimum state. Recovery status, backup/history controls, redacted diagnostics, Privacy Center, data-folder access, Close, and Restart normally remain reachable. A real process launch opened **KiWeave Safe Mode**, stayed responsive, owned zero TCP connections, and exited cleanly. The installed Safe Mode shortcut was verified to target the installed executable with only `--safe-mode`; the installed executable hash matches the tested private-helper build. Configuration, preferences, and startup state were preserved. An ordinary post-install launch caught Shift held and entered Safe Mode, live-verifying the Shift path; the deliberate `--normal-mode` recovery restart then opened responsive normal KiWeave.

## Conditional actions local build, October 2, 2026

The non-hook suite passes 87 checks with zero failures and the real Windows-hook suite passes 7 checks with zero failures. New coverage verifies strict conditional encoding/round-trip behavior, invalid field/path/nesting rejection, foreground and running-process matching, exactly-one-branch dispatch, editor availability for both F keys and custom hotkeys, nested maturity labels, and import-quarantine counts that expose network, launch, and hardware effects inside either possible branch. The searchable catalog now contains 442 unique actions across 16 categories.

The conditional builder was rendered and inspected at normal and supported minimum sizes. Its application field, condition selector, matching action, explicit fallback, edit controls, and save/cancel controls remain visible. The preview harness also verified that opening editors did not modify the saved configuration. Physical app focus/running transitions remain a manual acceptance check.

## KiWeave 1.0.0-alpha.1 privacy pass, October 2, 2026

The non-hook suite passes 80 checks with zero failures. New coverage verifies import-quarantine risk summaries, privacy-first network defaults, strict preference migration, master-policy blocking before an HTTP request, profile persistence and case-insensitive foreground-app matching, profile-action validation/routing and both-editor availability, version 1 profile migration, chained inheritance resolution, explicit override masks, loop rejection, duplicate app ownership rejection, transient live-key decisions, read-only conflict scanning with private target/path redaction, privacy-safe shortcut-capture normalization and reserved-combination rejection, bounded local configuration history with target-redacted comparisons, `.keyweave` bundle parsing and atomic replacement, private-safe logs without raw exception messages, and system/integration validation without changing an audio device. Conflict coverage includes common Windows ownership, active PowerToys shortcuts, layer activation overlap, and duplicate automatic-profile rules with explicit winner explanations. Modifier-layer coverage includes version 4 round trips, backward compatibility, duplicate activation/name rejection, action resolution, injected-key isolation, safe release pairing across disable, and competing layer keys. The allowed-network HTTP path is verified against a temporary loopback-only endpoint. The searchable action catalog contains 441 unique actions across 16 categories.

The native-hook suite passes 7 checks with zero failures. Its layer test installs the real low-level Windows hook, holds a test-tagged Caps Lock layer, routes F5 to a single F6 down/up pair, and verifies both physical-source events are suppressed. A separate test verifies delivery of one transient live-tester event with the correct resolved action. These test-tagged synthetic events exercise the hook path without claiming physical-key or firmware validation.

Rendered previews were checked for the main function-key editor with its active-profile badge, profile-status explanation and pin controls, modifier-layer manager, custom-hotkey editor, shortcut-capture dialog, Undo and History, bottom-left Settings page, Privacy Center, Conflict Center, live key tester, inherited-profile manager, action library, sequence builder, PowerToys active/add views, and diagnostics dialog, including supported minimum sizes. Profile status, History, Shortcut Capture, Conflict Center, inheritance controls, and the tester remain readable at normal and minimum sizes. The Settings render verifies its dedicated gear navigation, master network switch, immediate-save explanation, grouped controls, and non-mapping footer state. Existing version 1–3 mapping files, version 1 profile files, and version 1–2 preferences remain supported; configuration version 4 is emitted only after a layer is added, profile version 2 adds inheritance metadata, and preferences version 3 records the explicit master network state. Actual history restoration, automatic app switching, physical shortcut capture, physical tester input, audio-output changes, OBS controls, Discord shortcuts, externally triggered HTTP calls, physical keys, and alternate DPI remain manual acceptance checks.

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
