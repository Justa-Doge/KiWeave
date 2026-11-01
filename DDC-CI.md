# DDC/CI monitor controls

Use the current beta-5 candidate listed in `TEST-REPORT.md`. Select a function key or custom hotkey, choose **Monitor (DDC/CI)**, select the detected monitor and action, set the adjustment step when shown, and Save. The monitor actions are available to both function-key mappings and custom hotkeys. Existing media actions still control Windows audio; monitor-volume actions control the display's hardware audio level.

Supported adjustable actions are brightness up/down (VCP 0x10), contrast up/down (0x12), monitor-volume up/down (0x62), and **Backlight off/on** (VCP 0x6B). Backlight off sends a direct 0x6B write with value 0; backlight on sends a 0x6B write with the monitor's reported maximum. These are one-shot writes, not repeated adjustments. The monitor determines how a zero backlight level appears; maximum does not restore a previous level.

The action picker separately exposes monitor power-mode requests using VCP 0xD6. “Monitor power: request standby (may turn display off)” sends value 4; “Monitor power: request normal operation (wake)” sends value 1. These are power requests, not backlight controls. The old `BacklightLevelOff`/`BacklightLevelOn` IDs remain readable aliases for the new `BacklightOff`/`BacklightOn` VCP 0x6B actions. Older `BacklightOff`/`BacklightOn` mappings that previously meant D6 power mode now correctly mean backlight off/on; select the explicitly named Monitor power actions to request standby/wake.

## Detection

The app scans on launch. **Detect monitors** refreshes the list after cable, power, or topology changes. Detection only reads settings. Enable DDC/CI in the monitor's own menu if necessary; the cable/adapter must carry it.

When a valid capabilities list is available, a control must be advertised and respond with the expected value type. Continuous controls (including 0x6B) must report a usable current/maximum range; discrete power mode (0xD6) is checked as a discrete setting rather than a slider. When that list is unavailable or malformed, the app probes only the supported codes and requires a successful read. Only detected controls are offered in the editor. Some monitors may still reject writes or disable controls in particular picture modes; errors are reported in the app.

Saved selections use an opaque identifier derived from the Windows monitor device interface. A disconnected monitor never causes a mapping to target a different screen. Moving ports can require selecting the monitor again. Unavailable saved mappings are preserved and visibly labeled; dispatch fails with a message until the selected control is available. Ambiguous duplicate identities and multiple physical monitors sharing one logical handle are skipped.

Monitor I/O runs away from the UI and keyboard-hook threads, on a separate action worker. Only one pending DDC action is queued; excess held-key repeats are dropped to avoid a long delayed ramp. One pending adjustment may finish after key release. Disable/emergency-off cancels queued work and checks again immediately before a write, although an already-submitted driver call cannot be recalled. Monitor handles are released after scans and adjustments; the app never requests a write to the monitor's nonvolatile settings storage.

## Configuration and upgrade

Monitor mappings use version 2 JSON with `monitorId`, `control`, and `step`. Original-action-only configurations remain version 1. This release accepts both. Older app builds reject version 2, so use the new executable once you save a DDC mapping. Import remains an editor-only operation and never executes a monitor command or changes enabled/startup preferences.

Save updates an already-enabled startup entry to the current executable's path. It does not enable startup unless the checkbox is checked. Installation preserves the user's existing mappings and enabled preference and updates the existing startup entry to the installed executable.

## Verified on this computer

The **Dell SE2426HG (DISPLAY1)** was detected through the existing adapter. All three monitor controls were tested with actual writes and read-back, then restored with restoration verified:

| Control | Original | Test | Restored |
| --- | ---: | ---: | ---: |
| Brightness | 100 | 99 | 100 |
| Contrast | 75 | 74 | 75 |
| Monitor volume | 100 | 99 | 100 |

The original native editor was visually inspected with the detected model, supported actions, step control, and detection status. A temporary F1 preview was reverted before saving; no DDC mapping was imposed on the user's keyboard. These historical hardware checks covered only brightness, contrast, and monitor volume; the newly added backlight-level and power-mode actions have not been physically verified on that monitor.

The current shared regression suite has **208 passing tests**, including DDC power/backlight semantics, capability probing, public picker choices, saved custom-hotkey round trips, repeat suppression for fixed-level operations, and monitor action queue backpressure. The self-contained WPF candidate's isolated package self-test also passes. Automated tests do not prove the monitor accepts 0x6B writes or that its 0% level actually extinguishes the panel; complete the physical check below before relying on those actions.

The physical F-key and custom-hotkey DDC paths for Backlight off/on need a hands-on test. Use a spare key and monitor advertising VCP 0x6B; verify off writes value 0 and on writes the reported maximum. Test D6 standby/wake only when you can safely recover from standby using the monitor's own controls. For adjustment actions, map monitor-volume down with a 1% step and verify hardware volume changes and the key's old action is suppressed. Windows volume and monitor volume are separate, so a Windows volume overlay is not proof of monitor adjustment.

## Diagnostics

```powershell
.\bin\KiWeave.Tests.exe --ddc-detect
```

This command only reads monitors. The optional `--ddc-hardware` test deliberately decreases each supported control by one raw unit, reads it back, restores its original value in a finally block, and verifies restoration. Run it only when a brief settings change is appropriate. It does not replace a physical-key acceptance check.

Implementation reference: [Microsoft low-level monitor configuration functions](https://learn.microsoft.com/en-us/windows/win32/monitor/using-the-low-level-monitor-configuration-functions).
