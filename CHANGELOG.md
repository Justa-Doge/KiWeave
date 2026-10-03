# Changelog

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
