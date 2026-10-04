# Changelog

## 1.0.0-beta.2.5

- Applied the dark native theme consistently to list, text, combo, and scrollable controls so scrollbars match KiWeave's dark panels.
- Kept the private KiWeave-only state checker out of the release source.

## 1.0.0-beta.3 (local development)

- Added the first-party Extensions page with installed/featured browsing, local search, per-extension enable/disable controls, Windhawk-inspired layout attribution, and detailed status/permission/compatibility dialogs.
- Added stable first-party integration IDs so Discord, Spotify, OBS, PowerToys, Stream Deck, and Controller + MIDI settings remain local and existing mappings are preserved when an extension is disabled.
- Discord authorization can persist across KiWeave launches through an encrypted Windows-account refresh token. Access tokens remain memory-only; Disconnect/Forget authorization removes the local token.
- KiWeave silently refreshes authorization and reconnects Discord IPC when network access is allowed and Discord is open. No token is included in backups, diagnostics, logs, or Git.

## 1.0.0-beta.2

- Renamed the actual application identity to KiWeave: `KiWeave.exe`, KiWeave install/data folders, Start-menu entries, startup value, mutexes, manifest, project, tests, and release packaging.
- Added non-destructive migration from the legacy Function Row Remapper data folder. Existing KiWeave files always win and the legacy folder remains untouched as a recovery copy.
- Added global mapping search across Default and custom profiles, base and modifier layers, function keys, custom hotkeys, conditions, sequences, apps, paths, and actions, with direct editor navigation and private targets hidden from result labels.
- Added crash-safe private drafts after unsaved edits, with local preview, restore-to-editor, discard, export, and no automatic activation.
- Added a last-known-good private backup after a mapping save validates and successfully reaches the running engine, plus reviewed restoration from Safe Mode with rollback.
- Automatic update checks now run at launch and every 12 hours while KiWeave remains open, still respecting both the master network switch and update-check preference and never downloading automatically.
- Added experimental Discord OAuth/IPC voice controls with session-only authorization, automatic reconnect, and focused-app-independent mute/deafen actions. Discord voice control may remain unavailable until the KiWeave Discord application is public/approved; the existing Discord shortcut fallback remains available.

## 1.0.0-beta.1

- Added privacy-first Safe Mode through Shift-at-launch and a separate installed recovery shortcut. It loads no keyboard hook, global hotkeys, mapped actions, automatic profiles, integrations, tray mode, or network features.
- Added recovery status, redacted diagnostics, Privacy Center access, readable-setup backup, reviewed full-backup/history restore with rollback, and warned normal restart.
- Added conditional actions for foreground or running applications with a reviewed matching action, explicit fallback, trigger-time-only checks, and no hidden background monitoring.
- Conditional branches now participate in Action info, maturity labels, sequence safety summaries, and import-quarantine risk counts so network or executable effects cannot be hidden behind the rule.
- Added a privacy-first master network switch. New installations start blocked; local remapping, profiles, layers, backups, and diagnostics remain available offline.
- Added a Privacy Center that inventories local data sizes, lists the only network-capable KiWeave features, clears bounded logs, and previews/exports redacted diagnostics.
- HTTP actions and GitHub checks now enforce the master network policy at execution time.
- Safe diagnostics no longer expose custom profile names and exclude targets, commands, arguments, paths, URLs, request bodies, typed text, and key history.
- Added strict version 3 preferences with backward-compatible migration and included network state in full backup summaries.
- Added import quarantine with a complete local review of non-default actions, targets, commands, URLs, bodies, and risk labels before anything reaches the editor.
- Added a compact, non-executing Action info view for permission labels and sequence size/wait summaries.
- Added up to four named modifier layers per profile, each with a dedicated hold key and its own F1–F12 mappings.
- Added backward-compatible version 4 configuration support for layers, including imports, exports, profiles, and full backups.
- Layer activation stores only the current hold key, ignores injected input, permits only one active layer, and safely pairs suppressed releases across disable or profile changes.
- Added named mapping profiles with manual tray selection and automatic foreground-app switching.
- Added Windows window controls and default audio-output cycling.
- Added Discord, Spotify, OBS Studio, PowerToys, and configurable HTTP actions.
- Moved PowerToys management into the Custom hotkeys workspace, showing active shortcuts first and off or unassigned functions when adding one.
- Added a privacy-conscious diagnostics report that excludes action targets, arguments, URLs, and personal paths.
- Added clearer duplicate and operating-system shortcut conflict messages.
- Added background GitHub release checks and update notifications without automatic downloading or installation.
- Preserved existing configuration files and kept all new profile data out of the repository.
- Expanded the searchable action library to 441 actions across 16 categories.
- Licensed KiWeave under Apache 2.0 with a required redistribution attribution notice for Justa-Doge.
- Added versioned `.keyweave` full backups with strict validation, content summaries, atomic saves, rollback snapshots, and read-only PowerToys inventories.
- Added an About window with the version, Justa-Doge credit, Apache license, and project link.
- Added an explicit, confirmation-gated Test action button for function keys and custom hotkeys.
- Added an Inno Setup installer definition and tagged GitHub release workflow with tests, portable packaging, checksums, and release publishing.
- Added a first-launch welcome guide for genuinely new installations without interrupting existing users.
- Added one-click profile duplication and a visible automatic-profile status indicator.
- Added a bounded local diagnostic log designed to exclude mappings, URLs, paths, arguments, typed text, and raw exception messages.
- Added a bottom-left Settings page for startup, tray behavior, automatic profiles, update checks, backups, diagnostics, logs, and app information.
- 2026-10-03: Added read-only keyboard-layout drift detection to Integration Health. KiWeave records only the active layout name/identifier fingerprint and reports changes since the last local observation. Commit `6b5f662`; build passed 107 checks.
- 2026-10-03: Import review now supports selective staging of individual non-default base-key, modifier-layer, and custom-hotkey actions. Unchecked actions become pass-through or are omitted before the reviewed import reaches the editor; build passed 107 checks.
