# Safe exit for updates

Run the installed executable with `--exit-for-update` from the signed-in user's account.
It checks the live app state itself. It never kills the process, saves drafts, changes
preferences, or launches a remapper instance.

Exit codes:

- `0`: app was not running, or hidden clean app exited normally.
- `10`: window is visible (including minimized); leave it alone and coordinate with user.
- `11`: unsaved edits; do not discard them.
- `12`: a dialog is open or the main form is disabled; leave it alone.
- `13`: request timed out; don't assume it closed. Recheck process/state.
- `14`: running build doesn't expose the handler yet, or it is starting/exiting.
- `15`: another update-exit request is already in progress.

The normal Exit button still offers save/discard/cancel for unsaved changes.
The control is scoped to the same Windows account and session. Do not run a second
copy under another account to bypass a refusal. After code 0, verify no live remapper
process remains before replacing its executable.
