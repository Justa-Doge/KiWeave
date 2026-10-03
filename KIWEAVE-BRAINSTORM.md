# KiWeave brainstorm

Working product direction: KiWeave should grow from a function-row remapper into a configurable desktop control layer while staying predictable, safe, and easy to understand.

This is a planning document, not a promise that every idea will ship. Features should be added in small, testable slices and remain local-first by default.

Privacy is a product requirement, not an optional mode. KiWeave should work fully offline for core remapping, collect no analytics or usage telemetry, and make every network-capable feature visible and opt-in.

## Already available in the current 1.0 build

- Up to four named modifier layers per profile with dedicated hold keys, safe release pairing, and backward-compatible import/export.
- Function-row mappings with safe Pass through defaults.
- Custom modifier-based hotkeys and step-by-step sequences.
- Profiles with manual tray switching and foreground-app auto-switching.
- PowerToys browsing and supported shortcut editing.
- Window, audio, media, Discord, Spotify, OBS, monitor, HTTP, and Windows actions.
- Settings page, diagnostics, privacy-safe logs, backups, restore rollback, and update checks.
- A privacy-first live key tester that retains only the latest in-memory event while open.
- Profile inheritance with explicit override masks, chained resolution, and loop protection.
- A read-only Conflict Center with winner explanations and safe editor navigation.
- Temporary, reviewed shortcut capture for custom hotkeys and key/shortcut actions.
- Bounded local undo history with redacted comparisons and deliberate restore.
- Readable conditional actions for foreground/running applications, with an explicit fallback and no background monitoring.
- Global mapping search across profiles, layers, keys, hotkeys, sequences, conditions, and private target text, with redacted result labels and direct editor navigation.
- Crash-safe local draft recovery with preview, restore-to-editor, discard, and private export.
- A Safe Mode-accessible last-known-good recovery backup after a verified successful save.
- Portable ZIP packaging that keeps KiWeave self-contained without an installer.

## Highest-priority next features

### 1. Modifier layers (implemented locally)

Let users hold a chosen layer key, such as Caps Lock, Alt, a spare function key, or a custom modifier, to access another mapping set.

Examples:

- Plain F1: volume down.
- Layer + F1: previous browser tab.
- Layer + F2: next browser tab.
- Layer + F3: launch a work app.

Requirements:

- [x] Multiple named layers per profile.
- [x] Clear, single-active-layer activation rules.
- [x] Duplicate name and activation-key validation.
- [x] Safe behavior when the layer key is released mid-action or after disabling.
- [x] A visible layer selector in the function-key editor.
- [x] Import/export support with backward-compatible config migration.
- [ ] Active-layer indication and switching from the tray menu.
- [ ] Cross-profile action conflict explanations beyond duplicate activation keys.

### 2. Live key tester (implemented locally)

Add a diagnostics view that shows what KiWeave sees when a physical key is pressed.

Display only temporary diagnostic data:

- Physical key name and virtual-key code.
- Modifier state.
- Whether the event was injected or physical.
- Whether KiWeave suppressed it.
- The selected profile and layer.
- The resolved action name.

Do not record general typing, save key history, or write pressed keys to logs. Never do this

### 3. Better profile switching (core controls implemented locally)

- Optional custom profile images stored locally. Custom images are deliberately excluded from configuration exports and `.keyweave` backups; missing images fall back to the built-in profile appearance.
- [x] A clear active-profile badge.
- [x] “Why is this profile active?” explanation.
- [x] Faster tray profile selection and status controls.
- Profile priority when multiple app rules match.
- [x] A temporary session-only “pin this profile” mode.
- [x] Profile activation from F-key mappings or custom hotkeys.

### 4. Visual macro builder (core builder already available)

Expand the current sequence editor into a timeline-style builder with:

- Actions, waits, shortcuts, launches, and media controls.
- Reordering by drag and drop.
- Duplicate step.
- Inline editing.
- Validation before saving.
- Per-step test with confirmation.
- Maximum duration and step-count limits.
- A readable summary of what the macro will do.

## Strong follow-up features

### Conflict and safety center

- Detect Windows-reserved shortcuts.
- Detect duplicate KiWeave mappings.
- Detect conflicts between layers and profiles.
- Warn when another app or PowerToys owns a shortcut.
- Explain exactly which rule wins.
- Provide undo for recent mapping/profile changes.
- Add a safe recovery mode when a configuration fails to load.

### Privacy center

- Show exactly which features can access the network.
- Keep update checks optional and easy to disable.
- Never upload mappings, profiles, action targets, URLs, arguments, typed text, or key history.
- Keep diagnostics redacted by default, with a preview before copying or sharing.
- Make logs local-only, rotating, bounded, and free of raw exception messages or personal paths.
- Warn when an action pack, plugin, webhook, or imported backup contains URLs, commands, scripts, or sensitive-looking fields.
- Provide “Export safe diagnostics” separately from full private backups.
- Keep cloud sync out of the default architecture; if ever added, make it explicit, encrypted, and disabled by default.
- Give users a one-click way to open the data folder and remove local logs, caches, backups, and profiles.

### Action packs

Create optional local action packs for:

- Windows productivity.
- Gaming.
- Streaming and OBS.
- Discord and music.
- VR.
- Coding.
- Adobe and creative tools.
- Accessibility.

Packs should be reviewable before import, must never contain hidden executable commands, and must declare every network endpoint or external integration they use.

### Customization

- User-defined key labels.
- Profile-specific themes.
- Custom sidebar icons.
- Compact and expanded layouts.
- Searchable command palette.
- Better tray menu organization.
- First-run setup based on the user’s main use case.

### Integrations

- More OBS scene controls.
- Discord status and channel actions.
- More audio-device routing.
- MIDI/controller input.
- Stream Deck-style local webhooks.
- WebSocket actions for local tools.
- Smart-home actions only through explicit user-configured endpoints.

### Long-term architecture

- Separate action engine from the Windows keyboard backend.
- Keep profiles, layers, actions, and validation platform-neutral.
- Add native macOS and Linux input backends later.
- Preserve the same config and backup concepts across platforms.
- Keep platform-specific actions clearly labeled.
- Keep the action engine usable without an account, cloud service, or telemetry backend.

## Approved roadmap additions

### 1. Undo and configuration history (implemented locally)

- Create bounded, local snapshots before every successful save, import, restore, profile edit, or PowerToys edit.
- Provide one-click preview and rollback without silently activating an older configuration.
- Clearly show what changed, when it changed, and whether the snapshot contains private action data.
- Automatically remove only the oldest snapshots after a configurable storage limit.
- Current local implementation snapshots before mapping/enable/profile/restore changes, retains the newest 20 entries, provides a target-redacted comparison and deliberate restore flow, preserves the pre-restore state, and inventories history storage in the Privacy Center. A user-configurable entry limit and PowerToys-backup unification remain future work.

### 2. Conflict center (implemented locally)

- Detect duplicate KiWeave shortcuts, Windows-reserved combinations, PowerToys ownership, profile overlap, layer activation conflicts, and ambiguous automatic-profile rules.
- Explain which rule currently wins and why.
- Link each result directly to the relevant mapping editor.
- Never resolve a conflict or rewrite another application's settings without explicit approval.
- Current local implementation covers Windows-owned shortcuts, active PowerToys shortcuts, modifier-layer activation overlap, invalid/ambiguous profile rules, explicit winner explanations, and safe editor navigation without exposing action targets. Deeper cross-profile mapping comparisons remain future work.

### 3. Shortcut capture wizard (implemented locally)

- Add a Record shortcut control that captures one reviewed key combination instead of requiring typed key names.
- Show the captured keys before accepting them.
- Reject ordinary unmodified typing keys, reserved security combinations, and unsupported input.
- Keep capture temporary and never save or log unrelated keys.
- Current local implementation adds Record beside custom-hotkey and action shortcut fields, suppresses physical activation while the modal recorder is open, requires explicit review, rejects unsafe/unsupported combinations, and clears transient capture state when closed.

### 4. Conditional actions (first local slice implemented)

- Allow explicit local conditions such as foreground application, whether a selected application is running, and other predictable system state.
- Show conditions as readable rules rather than scripts.
- Define a clear fallback when no condition matches.
- Do not add hidden background monitoring, arbitrary expressions, or cloud-dependent conditions.
- [x] Foreground-application conditions using an executable name.
- [x] Application-running conditions using an executable name.
- [x] Explicit matching and fallback outcomes for F keys and custom hotkeys.
- [x] Nested privacy/risk review, maturity labels, strict format validation, and non-executing Action info.
- [ ] Additional predictable local conditions only after each receives a clear privacy and conflict model.

### 5. Profile inheritance (implemented locally)

- Let profiles inherit unchanged mappings and layers from Default or another chosen base profile.
- Store only intentional overrides where practical.
- Preview inherited versus overridden values and prevent inheritance loops.
- Make base-profile changes visible before they affect derived profiles.

### 6. Action permissions and risk labels

- Label actions that send input, launch local files, change hardware, alter Windows state, or contact the network.
- Show the labels in editors, imports, backups, action packs, and the action information view.
- Keep imported high-risk actions disabled until the user reviews and approves them.
- Never treat a familiar-looking name or icon as proof that an action is safe.

### 7. Private import review

- Preview every profile, layer, mapping, command, local path, URL, request body, and external integration before import.
- Support selective import instead of requiring all-or-nothing replacement.
- Highlight newly introduced network access and executable behavior.
- Importing must never execute, register, enable, or contact anything by itself.

### 8. Portable mode (packaging implemented)

- Allow an explicit portable mode that keeps configuration beside the executable rather than in AppData.
- Make the active storage mode and folder obvious.
- Preserve atomic saves, backups, privacy controls, and safe-mode recovery.
- Never switch storage locations or copy private configuration automatically.

### 9. Recovery and safe mode (implemented locally)

- Allow holding Shift during launch to open KiWeave in safe mode.
- Also install or provide a separate, clearly named **KiWeave Safe Mode** launcher for easy access.
- Safe mode must start visibly with keyboard hooks, automatic profiles, integrations, imported actions, and network features disabled.
- Let the user inspect, repair, export, roll back, or reset configuration before deliberately returning to normal mode.
- Safe mode must not overwrite the active configuration merely by opening.
- [x] Shift-at-launch and a separate installed **KiWeave Safe Mode** shortcut.
- [x] A completely separate recovery form that does not construct the normal editor or keyboard engine.
- [x] No hooks, registered global hotkeys, automatic profiles, integrations, tray hiding, mapped actions, or network access.
- [x] Readable configuration health, redacted diagnostics, Privacy Center, private backup export, full-backup restore, and undo-history restore.
- [x] Rollback snapshots before replacement and a warned, deliberate restart into normal mode.
- [ ] A future known-good recovery snapshot kept beside the existing Backups area.

### 10. Mapping search in the existing interface (implemented locally)

- Integrate global mapping search into the existing action library, profile manager, or another current workspace instead of creating an unnecessary standalone page.
- Search across base mappings, layers, custom hotkeys, profiles, conditions, commands, paths, applications, URLs, and PowerToys references.
- Open the selected result directly in its existing editor.
- Redact private target details from copied search results unless the user deliberately reveals them.
- [x] Search across Default and custom profiles, base and modifier layers, function keys, custom hotkeys, conditions, sequences, apps, paths, and action text.
- [x] Keep private targets searchable locally without displaying them in result labels.
- [x] Open the selected result in the existing function-key or custom-hotkey editor.

### 11. Tray quick actions

- Show the active profile and paused state in the tray menu.
- Allow pausing, resuming, switching profiles, and temporarily pinning a profile.
- Explain automatic versus manually pinned profile state.
- Keep destructive maintenance actions out of the quick menu.

### 12. Local profile schedules

- Optionally activate profiles during user-chosen local times and days.
- Keep schedules offline and stored locally.
- Define precedence between schedules, foreground-app rules, manual pins, and the Default profile.
- Provide a visible explanation of why a scheduled profile is active.

### 13. Accessibility controls

- Add larger-interface sizing, reduced motion, high contrast, complete keyboard navigation, clear focus indicators, and screen-reader-friendly labels and summaries.
- Keep every function available without precise pointer input.
- Respect relevant Windows accessibility settings where practical without silently changing them.
- Test dialogs and editors at supported scaling and minimum sizes.

### 14. Privacy center

Privacy is a core product boundary, not a cosmetic settings section.

- Provide a single dashboard listing every feature capable of network access, what triggers it, what data it can send, and whether it is enabled.
- Include a master network-off control that disables update checks, HTTP actions, network integrations, and future online features without disabling local remapping.
- Show exactly where configurations, profiles, histories, drafts, backups, caches, and logs are stored and how much space they use.
- Provide separate one-click controls to clear logs, caches, drafts, histories, and other disposable data without deleting active mappings.
- Keep analytics, telemetry, advertising identifiers, cloud accounts, and background uploads entirely absent.
- Never collect or retain general typing, clipboard contents, window titles, browsing history, or a timeline of pressed keys.
- Keep logs bounded, rotating, local-only, and free of raw paths, commands, arguments, URLs, request bodies, typed text, and secret-looking values.
- Make safe diagnostics export a separate redacted format with a complete preview before saving or copying.
- Treat full backups and ordinary configuration exports as private because they may contain commands, paths, applications, URLs, and personal workflow details.
- Require explicit review before imported data can introduce executable actions, hardware changes, persistent behavior, or network access.
- Make every privacy-affecting default conservative and explain changes in plain language.
- Keep all core remapping, profiles, layers, search, history, scheduling, and recovery features fully usable offline.

### 15. Compact action information button

- Add a small information button beside the selected action rather than a separate dry-run system.
- Show a concise, non-executing explanation of what the action does, its risk labels, required application or hardware, and whether it launches or contacts anything.
- For sequences, summarize each step and total configured wait time.
- The information view must never execute, validate through side effects, open a URL, or contact an endpoint.

### 16. Crash-safe draft recovery (implemented locally)

- Save bounded local recovery drafts separately from the active configuration.
- After an unexpected exit, offer to preview, restore, discard, or export the draft.
- Never activate a recovered draft until the user reviews and saves it.
- Exclude raw key history and unrelated application state from recovery files.
- [x] Save a bounded private recovery draft only after an unsaved edit.
- [x] Preview, restore into the editor without activation, discard, export, or decide later.
- [x] Remove the draft after a deliberate save or discard.

### 17. Verifiable community action packs

- Support only declarative packs built from KiWeave's own validated actions, conditions, sequences, profiles, and current custom-hotkey functions.
- Do not allow embedded scripts, executable code, binaries, plugins, shell fragments, dynamic downloads, or hidden post-install behavior.
- Validate the complete pack against KiWeave's strict schema and safety limits before showing it.
- Present a full manifest of mappings, required applications, local paths, permissions, hardware effects, and network endpoints before import.
- Keep all imported actions disabled until the user chooses what to trust and enable.
- Display publisher identity or signatures when available, but never treat signing alone as proof that a pack is harmless.
- Make packs reproducible and inspectable so another KiWeave installation can verify that the visible manifest matches the imported content.
- Allow reporting or blocklisting known malicious packs without transmitting the user's mappings or usage history.
- If a pack cannot be represented entirely through KiWeave's verified built-in action model, reject it.

### 18. Configuration health checks

- Run a lightweight local health check with the daily official-release check and expose the same scan from the Privacy Center.
- Check for missing programs, moved files, unavailable monitors and audio devices, inactive PowerToys modules, invalid local configuration references, and outdated profile rules without executing actions.
- Keep the health scan local. Only the separate release check may contact GitHub, and only when network checks are enabled.
- Summarize actionable problems in the Privacy Center and show a concise warning at the bottom of the main window when attention is needed.

### 19. Configuration comparison viewer

- Show a readable before-and-after comparison for history rollback, backup restore, imports, action packs, inherited-profile changes, and format migrations.
- Group changes by profile, layer, hotkey, action, condition, permission, and network behavior.
- Redact private targets by default while allowing deliberate reveal during local review.
- Comparison must remain non-executing and must not activate the proposed configuration.

### 20. Import quarantine

- Keep imported profiles, backups, and action packs isolated from the live configuration until review finishes.
- Require explicit approval for executable actions, hardware changes, persistence, and network access.
- Allow selective acceptance or rejection of individual profiles, layers, mappings, and permissions.
- Deleting a quarantined import must not affect the active configuration.

### 21. Resilient dependency and integration health

- Fold dependency status into the Privacy Center and the main-window attention message rather than adding another permanent page.
- Detect integrations through stable capabilities, documented interfaces, executable identity, and user-configured locations where possible, not one fragile internal process or file name.
- Treat renamed or unknown application versions as **Needs review**, not broken or malicious.
- Cover PowerToys, OBS, Discord, Spotify, Python, DDC/CI monitors, audio devices, and user-configured local tools without installing or repairing them automatically.

### 22. Sequence safety summary and limits

- Before saving, show configured duration, step count, waits, launches, network requests, hardware operations, and repeated actions.
- Enforce conservative limits on total steps, waiting time, launches, and network operations.
- Highlight sequences whose effect depends on focus, external applications, or hardware.
- Keep cancellation behavior separate until an interaction can be designed that is difficult to trigger accidentally.

### 23. Emergency recovery backup (implemented locally)

- Maintain one known-good local recovery configuration independently of ordinary undo history.
- Store it inside the existing Backups folder or directly beside that folder, never in a hidden unrelated location.
- Let Safe Mode preview and restore it without first loading active mappings or installing hooks.
- Replace the known-good copy only after a verified successful save and startup, with clear retention rules.
- [x] Keep `known-good.keyweave` in the existing Backups folder.
- [x] Replace it only after validation, persistence, live-engine apply, hotkey registration, and startup-setting completion.
- [x] Preview and restore it from Safe Mode with a rollback folder and no action activation.

### 24. Official-release authenticity and self-integrity

- Protect real published releases, not ordinary repository commits or metadata-only version changes.
- Sign release executables and installers with a code-signing identity when available.
- Publish a signed release manifest containing the version, supported files, cryptographic hashes, release channel, and minimum compatible configuration version.
- Embed only the public verification key in KiWeave. Keep the private release-signing key outside the repository and build output.
- At startup or on demand, verify the installed executable and packaged files against the locally stored signed manifest without requiring a network connection.
- When checking GitHub for an official update, accept a release only if its manifest signature and downloaded-file hash validate. A matching version string or repository file alone is not proof.
- If integrity verification fails, normal KiWeave initialization must fail closed before installing hooks, loading profiles, registering hotkeys, starting integrations, or enabling network actions.
- Show only a minimal integrity-recovery screen explaining that the installation may be damaged, incomplete, or modified by malware and recommending a fresh installation of the latest official published release.
- Preserve read-only access to integrity details, safe diagnostics, the Backups folder, and Safe Mode so failure cannot destroy or trap private user configuration.
- Never auto-download or silently repair from the failed executable. Direct the user to the official release source and require the replacement package to pass signature and manifest verification.
- Clearly identify locally compiled development builds as **Development build — integrity not certified** so contributors are not given a false malware warning merely because their binary is not an official published release.
- Do not claim the checker code is literally unremovable. Malware capable of rewriting the executable could patch an internal check; authenticity must ultimately be anchored in Windows signature validation and a private signing key the attacker does not possess.
- Provide a separate small verification or repair entry point so the main executable is not the only component deciding whether it has been modified.

### 25. Private mapping notes

- Add an optional Notes item in the extra mapping/profile menu.
- Allow notes on mappings, sequences, layers, conditions, and profiles to explain purpose, expected application state, or setup requirements.
- Keep notes local, exclude them from logs and safe diagnostics, and clearly include them only in private/full exports and backups.
- Never interpret notes as commands, templates, variables, or executable content.

### 26. Duplicate and template actions

- Allow deliberate duplication of mappings, sequences, layers, conditions, and profiles from their existing menus.
- Give every copy a new identity and a clear **Copy** name until the user renames it.
- Re-run conflict and safety validation before the copy can be saved or activated.

### 27. Read-only configuration mode

- Add a deliberate editing lock that leaves active mappings running while preventing accidental configuration changes.
- Require a clear confirmation to unlock, but no password or false security claim.
- Show the locked state throughout every editor and reject imports, restores, and profile edits until unlocked.

### 28. Migration preview

- Before changing an older configuration format, show what will be added, changed, preserved, or dropped.
- Preserve the untouched original beside the normal backups before writing the migrated copy.
- Opening or previewing an old file must not migrate, activate, or overwrite it.
- Use the configuration comparison viewer for the migration explanation.

### 29. Manual redacted diagnostic bundle

- Create a user-triggered support bundle containing version, integrity state, health results, feature maturity, and optionally user-approved screenshots.
- Exclude mappings, notes, key history, commands, arguments, paths, URLs, request bodies, credentials, window titles, and unrelated machine identifiers.
- Show a complete local preview before saving or sharing the bundle.
- Never upload the bundle automatically.

### 30. Safe localization system

- Separate interface strings from action definitions so the UI can be translated without changing behavior.
- Translation files may contain text only and must never define actions, URLs, commands, paths, scripts, or formatting capable of execution.
- Fall back safely to the built-in language when a translation is missing, malformed, or for an incompatible version.
- Keep security, permission, privacy, and recovery terminology unambiguous in every supported language.

### 31. Feature maturity labels

- Mark relevant features as Stable, Experimental, Hardware-dependent, App-dependent, or Deprecated.
- Explain what each label means and which limitations or manual checks remain.
- Show maturity in the action information view, Gallery, integrations, diagnostics, and health results.
- A maturity label must never replace a permission warning or safety check.

## Deferred after review

- **Action rate limits and per-action cooldowns:** Useful for preventing repeated launches or requests, but deferred until a simpler rule model is justified. Cooldowns block rapid repeats; they are different from scheduled tasks, which start actions at chosen times.
- **Sequence cancellation controls:** Deferred until there is a deliberate interaction that cannot easily be triggered by a fat-fingered shortcut. Sequence safety limits remain approved.
- **Encrypted remapping backups:** Deferred because ordinary remapping backups usually do not justify password-management complexity. Full backups remain clearly labeled private and can be protected using normal encrypted storage if needed.



# Explicitly dropped for now

### Custom OSD replacement

Dropped because replacing Windows’ native OSD reliably requires complicated interception and rendering behavior. KiWeave can revisit a small status overlay later if it can be implemented without fighting the operating system.

### Tap/hold/double-tap timing

Dropped because timing-based behavior can feel inconsistent with modifiers, games, keyboard firmware, and accessibility settings. Modifier layers provide much of the same power with more predictable behavior.

## Suggested order

1. Finish modifier-layer tray polish and physical keyboard acceptance testing.
2. Profile colors, icons, and active-profile explanation.
3. Stronger conflict/safety center.
4. Finish visual macro-builder polish only where it improves the existing builder.
5. Action packs and reviewed imports.
6. Additional integrations.
7. Cross-platform backend.


## Product principles

- Local-first and privacy-conscious by default.
- Privacy is the default posture: no telemetry, no key logging, no hidden network calls, and no cloud dependency for core features.
- Nothing executes merely because it was imported or previewed.
- Every dangerous action has a clear confirmation or visible warning.
- Existing configurations remain migratable.
- Background behavior should be explainable.
- The UI should make the safe action the obvious action.

## Expanded roadmap backlog

These are approved ideas for future KiWeave updates. They are roadmap entries, not promises that every item is already implemented.

1. Per-application profiles with automatic foreground-app switching.
2. Modifier layers with user-selected layer keys.
3. Profile colors and icons, excluding personal images from exports by default.
4. Crash-safe drafts with clearer recovery prompts.
5. Global mapping search across profiles, layers, and nested sequences.
6. Dependency explanations for every app, device, or integration action.
7. Local profile schedules by time and day, with no network service.
8. Read-only configuration mode that keeps active mappings running.
9. A readable configuration diff before restores, imports, profile switches, or migrations.
10. Migration preview that preserves the untouched original.
11. Import quarantine with permission and risk review before activation.
12. Emergency known-good backup beside the normal local backup folder.
13. Backup rotation and retention controls.
14. Optional encrypted full backups only if the privacy tradeoff is justified.
15. A local secrets vault for webhook and API credentials, never included in exports.
16. Deliberate sequence cancellation with an unmistakable emergency interaction.
17. Sequence limits for runtime, repeats, launches, network requests, and hardware operations.
18. Integration health dashboard with refreshable read-only status and repair guidance.
19. Manually created redacted diagnostic bundles with a complete preview.
20. Release checksum and package-integrity verification.
21. Stable, beta, and alpha update-channel preferences.
22. Safe localization using text-only translation files.
23. Accessibility and keyboard-only navigation pass.
24. Live key tester improvements for suppression, translation, and layer state.
25. Conflict explanations with suggested fixes and affected integrations.
26. Mapping, sequence, layer, condition, and profile duplication templates.
27. Declarative local action-pack gallery with reviewed imports.
28. Action-pack trust labels and optional signature verification.
29. Portable-mode improvements with explicit data-location controls.
30. Windows notification preferences for updates, health, and safety warnings.
31. Custom tray-menu profile switching.
32. Local “why did this action run?” explanations without key-history logging.
33. Dry-run mode for sequences and imported packs.
34. Per-integration permission toggles beneath the master network policy.
35. Backup privacy scan before export.
36. One-click recovery after a failed startup.
37. Admin-only Settings controls visible only in an elevated KiWeave session.
38. Restricted elevated local-`.exe` launches, subject to a separate privacy and safety review.
39. Read-only Windows admin-console shortcuts with explicit UAC prompts.
40. A release-readiness checklist before the eventual 1.0.0 launch.

## Further roadmap backlog

41. Per-profile backup snapshots.
42. An “Explain this shortcut” hover panel.
43. Conflict auto-resolution suggestions.
44. Temporary shortcut suspension by focused app.
45. Game mode with anti-cheat-safe restrictions.
46. Hardware keyboard detection and layout profiles.
47. Import/export redaction presets.
48. Backup expiration reminders.
49. Restore-point labels and private notes.
50. First-use action execution preview.
51. Test-without-saving editor mode.
52. A visual profile-inheritance tree.
53. Modifier-layer conflict visualization.
54. Shortcut collision simulation.
55. App-path migration assistant.
56. Offline documentation browser.
57. Built-in keyboard shortcut reference.
58. Integration-specific reconnect controls.
59. Discord authorization-expiry warnings.
60. PowerToys configuration drift detection.
61. Monitor capability-change alerts.
62. Audio-device availability alerts.
63. Safe startup after repeated crashes.
64. Automatic rollback after failed configuration activation.
65. User-selectable notification severity.
66. Privacy-dashboard export history.
67. Local audit trail with automatic redaction.
68. Experimental-feature kill switch.
69. Separate developer mode for test actions.
70. Signed community action-pack repository.
71. Action-pack version compatibility checks.
72. Profile import merge mode.
73. Profile conflict-resolution wizard.
74. Keyboard-layout change detection.
75. Per-Windows-user profiles.
76. Multi-monitor-specific mappings.
77. Remote-desktop-aware profiles.
78. VM-aware profile switching.
79. Controller and MIDI input support: read physical controllers through Windows.Gaming.Input/Game Input, read MIDI through Windows MIDI Services, and keep virtual-gamepad output as a separately reviewed optional backend rather than making the archived ViGEmBus driver a default dependency.
80. Final release-readiness and security checklist before 1.0.0.
81. Stream Deck integration: a KiWeave plugin with declarative action buttons, profile switching, shortcut status, and safe local IPC. Use Elgato's official SDK/WebSocket plugin model, keep secrets out of the plugin, and never execute arbitrary plugin-provided code.
82. Script workspace: open selected `.py`, `.ps1`, `.cmd`, `.bat`, `.js`, `.lua`, and other explicitly supported script files in VS Code when available, with a safe fallback to the user-selected editor. Opening a script never executes it; execution remains opt-in, visibly labeled, and subject to language/path validation and import quarantine.
83. First-party Extensions page: browse KiWeave-maintained integrations and declarative action packs from the official release source, showing version, permissions, supported KiWeave version, maturity, integrity state, and local-data impact before installation. No arbitrary extension code, silent downloads, or hidden network permissions.
84. Modular first-party integrations: move Discord, Spotify, OBS, PowerToys, Stream Deck, MIDI, controller, and future app-specific support out of the default core experience and expose them through the first-party Extensions page. Existing mappings must migrate by stable integration IDs, disabled extensions must fail safely, and uninstalling an extension must preserve the user's mappings and notes.
85. Extensions browsing polish: use an installed-versus-featured card layout, local search, details views, and clear permission/maturity labels. The browsing pattern is inspired by Windhawk's mod pages; KiWeave's extension model, safety rules, and attribution are its own.
