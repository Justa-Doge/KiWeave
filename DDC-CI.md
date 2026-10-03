# DDC/CI monitor controls

Run **bin/KiWeave.exe**. Select a function key, choose **Monitor (DDC/CI)**, select the detected monitor and action, set the adjustment step, and Save. Existing media actions still control Windows audio; monitor-volume actions control the display's hardware audio level.

Supported actions are brightness up/down (VCP 0x10), contrast up/down (0x12), and monitor-volume up/down (0x62). The step is 1–20% of the control's reported maximum, rounded to at least one unit and clamped to 0–maximum. Hold the function key to repeat. No input switching, monitor power-off, raw VCP commands, or firmware changes are exposed.

## Detection

The app scans on launch. **Detect monitors** refreshes the list after cable, power, or topology changes. Detection only reads settings. Enable DDC/CI in the monitor's own menu if necessary; the cable/adapter must carry it.

When a valid capabilities list is available, a control must be advertised and respond with a usable current/maximum value. When that list is unavailable or malformed, the app probes only the three supported codes and requires a successful read. Only detected controls are offered in the editor. Some monitors may still reject writes or disable controls in particular picture modes; errors are reported in the app.

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

The native editor was visually inspected with the detected model, supported actions, step control, and detection status. A temporary F1 preview was reverted before saving; no DDC mapping was imposed on the user's keyboard.

Eight new automated tests cover the configuration schema, invalid controls/identities/steps, capability parsing, range calculations, dispatch separation and cancellation, held-key behavior, and refusal to write to an undetected monitor. The full suite has **42 core tests**, **5 Windows-hook tests**, and **3 hardware monitor tests**, all passing.

The physical F-key-to-DDC path still needs a hands-on test. Choose a spare key, map it to monitor-volume down with a 1% step, Save, and press the physical key once. Verify the display's hardware volume changes and the key's old action does not occur. Also try global off and the emergency chord. Windows volume and monitor volume are separate, so a Windows volume overlay is not proof of monitor adjustment.

## Diagnostics

```powershell
.\bin\KiWeave.Tests.exe --ddc-detect
```

This command only reads monitors. The optional `--ddc-hardware` test deliberately decreases each supported control by one raw unit, reads it back, restores its original value in a finally block, and verifies restoration. Run it only when a brief settings change is appropriate. It does not replace a physical-key acceptance check.

Implementation reference: [Microsoft low-level monitor configuration functions](https://learn.microsoft.com/en-us/windows/win32/monitor/using-the-low-level-monitor-configuration-functions).
