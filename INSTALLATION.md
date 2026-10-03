# Installation and system tray

The current build is **bin/KiWeave.exe**. It includes the dark sidebar design, searchable action library, global mapping search, sequence builder, custom global hotkeys, Python actions, DDC/CI, recovery drafts, and tray controls. Earlier named build folders and portable ZIPs are historical versions.

## Installed on this computer

Installed executable:

`%LOCALAPPDATA%\Programs\KiWeave\KiWeave.exe`

Start menu shortcut:

`%APPDATA%\Microsoft\Windows\Start Menu\Programs\KiWeave.lnk`

Recovery shortcut:

`%APPDATA%\Microsoft\Windows\Start Menu\Programs\KiWeave Safe Mode.lnk`

Open Windows Search and type **KiWeave**. Launching it while it is already running brings that copy's settings window forward instead of starting a second remapping engine. The shortcut always opens settings, even if startup is configured to begin in the tray.

Search for **KiWeave Safe Mode**, or hold Shift while launching, to open the recovery-only window. Exit ordinary KiWeave first. Safe Mode does not load hooks, hotkeys, actions, automatic profiles, integrations, tray behavior, or network features.

The existing per-user Windows startup entry has been updated to the installed executable. Installation preserves mappings and enabled/startup preferences; it does not newly enable startup. No administrator privileges or installer service are used.

## Keep running in tray

The bottom-left gear opens **Settings**, where **Keep running in tray** sits with **Start with Windows**, automatic profile switching, and update checks. These changes save immediately, independently of unsaved mapping edits.

- **On:** the notification icon is visible, closing the window hides it to the tray, and remapping keeps running. Unsaved editor changes remain in memory. Use **More → Exit app** or the tray's Exit command to fully quit. Hide to tray is also available.
- **Off:** the notification icon is removed, Hide to tray is unavailable, and closing the window exits the app. Windows startup opens a visible window even if the startup command contains `--tray`.

Tray use defaults to on. It is stored separately in `%LOCALAPPDATA%\KiWeave\preferences.json`, with an atomic previous-save `.bak`. Mapping imports/exports do not change this preference. A malformed preferences file is preserved, an error is shown, and the app starts visibly with tray use off so it cannot become inaccessible.

## Build or install again

From PowerShell 7 in the source folder:

```powershell
.\install.ps1
```

`install.ps1` is the single PowerShell install entry point. If `bin\KiWeave.exe` is missing, it builds KiWeave and the red `UninstallKiWeave.exe` first, then installs both and creates the normal and Safe Mode Start Menu shortcuts. The uninstaller stays in the install folder instead of appearing in Windows search. You can still pass `-SourceExe` to install a different local build (such as a private test build).

Exit the running remapper before installing an update. The installer script copies the compiled executable to the per-user Programs folder, verifies its hash, creates/verifies both the normal and Safe Mode Start-menu shortcuts, and migrates this app's existing startup entry if one exists. It does not overwrite mapping or preference files. A previous installed executable is backed up when applicable. On first launch, legacy Function Row Remapper data is copied into `%LOCALAPPDATA%\KiWeave` without deleting the old recovery copy.

## Remove

Run the red `%LOCALAPPDATA%\Programs\KiWeave\UninstallKiWeave.exe` directly from the install folder. It removes the app, shortcuts, and startup entry while preserving mappings, profiles, backups, and preferences. To remove saved data too, delete `%LOCALAPPDATA%\KiWeave` separately.
