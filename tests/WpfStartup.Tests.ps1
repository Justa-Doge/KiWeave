$ErrorActionPreference = 'Stop'
$appSource = Join-Path $PSScriptRoot '..\wpf-rewrite\App.xaml.cs'
$sourceText = Get-Content -LiteralPath $appSource -Raw
$inspectAt = $sourceText.IndexOf('"--inspect-state"', [StringComparison]::Ordinal)
$exitAt = $sourceText.IndexOf('"--exit-for-update"', [StringComparison]::Ordinal)
$migrationAt = $sourceText.IndexOf('AppStorage.MigrateLegacy()', [StringComparison]::Ordinal)
$languageAt = $sourceText.IndexOf('WpfLanguageCatalog.Load()', [StringComparison]::Ordinal)
if ($inspectAt -lt 0 -or $exitAt -lt 0 -or $migrationAt -lt 0 -or $languageAt -lt 0) {
    throw 'WPF startup is missing an update-check, update-exit, migration, or language initialization path.'
}
if ($inspectAt -ge $migrationAt -or $exitAt -ge $migrationAt -or $inspectAt -ge $languageAt -or $exitAt -ge $languageAt) {
    throw 'WPF update-checker launches must return before legacy migration or language initialization can touch user data.'
}
$runtimeCompatPath = Join-Path $PSScriptRoot '..\wpf-rewrite\WpfRuntimeCompat.cs'
$runtimeCompat = Get-Content -LiteralPath $runtimeCompatPath -Raw
if ($runtimeCompat -notmatch 'protected override void OnClosed\(EventArgs e\)\s*\{\s*closing = true;\s*timer\.Stop\(\);\s*base\.OnClosed\(e\);\s*\}') {
    throw 'Closing a WPF status flyout must stop its toast timer before the window is released.'
}
$motionPath = Join-Path $PSScriptRoot '..\wpf-rewrite\WpfMotion.cs'
$motion = Get-Content -LiteralPath $motionPath -Raw
if ($motion -notmatch 'CompositionTarget\.Rendering\s*\+=\s*state\.Tick' -or
    $motion -notmatch 'state\.Tick\s*=\s*delegate\s*\(object\s+renderingSender,\s*EventArgs\s+renderingArgs\)' -or
    $motion -notmatch 'var rendering = renderingArgs as RenderingEventArgs;' -or
    $motion -match 'var rendering = sender as RenderingEventArgs;' -or
    $motion -notmatch 'rendering\?\.RenderingTime' -or
    $motion -notmatch 'CompositionTarget\.Rendering\s*-=' -or
    $motion -notmatch 'viewer\.Unloaded\s*\+=\s*state\.Unloaded' -or
    $motion -notmatch 'state\.Unloaded\s*=\s*delegate\s*\{\s*state\.Running\s*=\s*false;\s*state\.LastRenderingTime\s*=\s*TimeSpan\.Zero;\s*CompositionTarget\.Rendering\s*-=' -or
    $motion -match 'DispatcherTimer|DesiredFrameRate|TargetFrameRate') {
    throw 'WPF smooth scrolling must follow compositor render timestamps, unsubscribe on unload, and avoid a fixed frame-rate timer or cap.'
}
$mainWindowCode = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\MainWindow.xaml.cs') -Raw
$automaticProfileStart = $mainWindowCode.IndexOf('void CheckAutomaticProfile()', [StringComparison]::Ordinal)
$automaticProfileEnd = $mainWindowCode.IndexOf('void SelectAutomaticProfile(', $automaticProfileStart, [StringComparison]::Ordinal)
if ($automaticProfileStart -lt 0 -or $automaticProfileEnd -le $automaticProfileStart) { throw 'WPF automatic profile-check handler could not be located.' }
$automaticProfileCheck = $mainWindowCode.Substring($automaticProfileStart, $automaticProfileEnd - $automaticProfileStart)
if ($automaticProfileCheck -notmatch 'Native\.TryForegroundProcessName\(out string process\);\s*if\s*\(!String\.IsNullOrWhiteSpace\(process\)\)' -or
    $automaticProfileCheck -notmatch 'AppLog\.Record\("Automatic profile selection", error\)' -or
    $automaticProfileCheck -notmatch 'nextAutomaticProfileErrorLogTick = now \+ 60_000') {
    throw 'WPF automatic profile selection must use the shared race-safe foreground lookup and rate-limit private-safe error logging.'
}
$discordWindow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\DiscordConnectionWindow.cs') -Raw
$extensionDetailsWindow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\FirstPartyExtensionsWindow.cs') -Raw
$mainWindowXaml = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\MainWindow.xaml') -Raw
$accessibilityControls = @(
    'AutomationProperties.Name="Minimize window"',
    'AutomationProperties.Name="Maximize or restore window"',
    'AutomationProperties.Name="Close KiWeave window"',
    'AutomationProperties.Name="Function keys"',
    'AutomationProperties.Name="Custom hotkeys"',
    'AutomationProperties.Name="Extensions"',
    'AutomationProperties.Name="Integration health"',
    'AutomationProperties.Name="Settings"'
)
if (@($accessibilityControls | Where-Object { $mainWindowXaml.IndexOf($_, [StringComparison]::Ordinal) -lt 0 }).Count -gt 0 -or
    $mainWindowXaml -notmatch 'MinWidth="960" MinHeight="640"' -or
    $mainWindowXaml -notmatch 'VerticalScrollBarVisibility="Auto"') {
    throw 'WPF main-window navigation and title-bar controls must remain screen-reader named, keyboard reachable, and usable at the supported minimum size.'
}
$streamDeckBridge = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\WpfStreamDeckBridge.cs') -Raw
$discordIntegration = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DiscordIntegration.cs') -Raw
$systemActions = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\SystemActions.cs') -Raw
$reconnectStart = $discordIntegration.IndexOf('static void ReconnectIfNeeded()', [StringComparison]::Ordinal)
$reconnectEnd = $discordIntegration.IndexOf('static bool DiscordDesktopIsRunning()', $reconnectStart, [StringComparison]::Ordinal)
if ($reconnectStart -lt 0 -or $reconnectEnd -le $reconnectStart) { throw 'Discord reconnect-monitor handler could not be located.' }
$reconnectHandler = $discordIntegration.Substring($reconnectStart, $reconnectEnd - $reconnectStart)
$reconnectRefreshAt = $reconnectHandler.IndexOf('RefreshAccessToken(observedRefreshToken, attemptCancellation.Token)', [StringComparison]::Ordinal)
$reconnectConnectAt = $reconnectHandler.IndexOf('next.Connect(1200)', [StringComparison]::Ordinal)
$reconnectWorkAt = $reconnectHandler.IndexOf('TokenSet refreshed = null;', [StringComparison]::Ordinal)
if ($reconnectRefreshAt -le $reconnectWorkAt -or $reconnectConnectAt -le $reconnectWorkAt -or
    $reconnectHandler -notmatch 'ShouldCommitReconnect\(generation, reconnectGeneration, networkRequirementSatisfied, tokensUnchanged, alreadyConnected\)' -or
    $reconnectHandler -notmatch 'ShouldPersistRefreshedToken\(refreshed != null, tokensUnchanged\)' -or
    $reconnectHandler -notmatch 'ReferenceEquals\(reconnectAttemptCancellation, attemptCancellation\)' -or
    $discordIntegration -notmatch 'if \(reconnectAttemptCancellation != null\) reconnectAttemptCancellation\.Cancel\(\)' -or
    $discordIntegration -notmatch 'ShouldCommitReconnect\(long attemptGeneration, long currentGeneration, bool networkRequirementSatisfied, bool tokensUnchanged, bool alreadyConnected\)' -or
    $discordIntegration -notmatch 'networkPolicyGeneration\+\+; if \(reconnectTimer != null\)') {
    throw 'Discord reconnect network/IPC work must run outside the shared status lock, cancel on shutdown, and discard stale connection results.'
}
$voiceToggleStart = $discordIntegration.IndexOf('internal static bool TryToggleVoice(string field, out bool enabled)', [StringComparison]::Ordinal)
$voiceToggleEnd = $discordIntegration.IndexOf('static void StartReconnectMonitorLocked()', $voiceToggleStart, [StringComparison]::Ordinal)
if ($voiceToggleStart -lt 0 -or $voiceToggleEnd -le $voiceToggleStart) { throw 'Discord voice hotkey handler could not be located.' }
$voiceToggle = $discordIntegration.Substring($voiceToggleStart, $voiceToggleEnd - $voiceToggleStart)
$voiceClientStart = $discordIntegration.IndexOf('internal bool ToggleVoice(string field)', [StringComparison]::Ordinal)
$voiceClientEnd = $discordIntegration.IndexOf('void RefreshVoiceState()', $voiceClientStart, [StringComparison]::Ordinal)
if ($voiceClientStart -lt 0 -or $voiceClientEnd -le $voiceClientStart) { throw 'Discord IPC voice toggle could not be located.' }
$voiceClientToggle = $discordIntegration.Substring($voiceClientStart, $voiceClientEnd - $voiceClientStart)
$discordConnectStart = $discordIntegration.IndexOf('internal void Connect(int timeoutMilliseconds)', [StringComparison]::Ordinal)
$discordConnectEnd = $discordIntegration.IndexOf('internal void SetVoice(', $discordConnectStart, [StringComparison]::Ordinal)
if ($discordConnectStart -lt 0 -or $discordConnectEnd -le $discordConnectStart) { throw 'Discord IPC connection handler could not be located.' }
$discordConnect = $discordIntegration.Substring($discordConnectStart, $discordConnectEnd - $discordConnectStart)
if ($voiceToggle -match 'RefreshAccessToken\(' -or
    $voiceToggle -notmatch 'ShouldAttemptImmediateVoiceReconnect\(accessToken\.Length > 0, reconnecting, restoring\)' -or
    $voiceToggle -notmatch 'reconnecting = true;[\s\S]*?\}\s*if \(previous != null\)[\s\S]*?candidate\.Connect\(250\);' -or
    $voiceToggle -notmatch 'current\.ToggleVoice\(field\)' -or
    $voiceClientToggle -notmatch 'lock \(voiceGate\)' -or
    $discordConnect -notmatch 'candidate\.Dispose\(\)' -or
    $discordConnect -notmatch 'catch \{ Dispose\(\); throw; \}' -or
    $discordIntegration -notmatch 'internal DiscordIpcClient\(string token, Func<int, string> pipeNameAt\)' -or
    $discordIntegration -match 'ReconnectLocked\(' -or
    $systemActions -notmatch 'ShouldSendDiscordShortcutFallback\(bool discordFocused\) \{ return discordFocused; \}' -or
    $systemActions -notmatch 'else if \(ShouldSendDiscordShortcutFallback\(discordFocused\)\) sink\.Send\(Shortcuts\.Parse\("Ctrl\+Shift\+[MD]", false\)\)') {
    throw 'Discord voice hotkeys must connect and toggle outside the shared status lock, serialize pipe use, and only fall back to a shortcut when Discord owns focus.'
}
$mainWindow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\MainWindow.xaml.cs') -Raw
$moreStart = $mainWindow.IndexOf('void MoreClick(', [StringComparison]::Ordinal)
$bulkStart = $mainWindow.IndexOf('void BulkFunctionMappings(', $moreStart, [StringComparison]::Ordinal)
$bulkEnd = $mainWindow.IndexOf('void LiveKeyTesterClick(', $bulkStart, [StringComparison]::Ordinal)
if ($moreStart -lt 0 -or $bulkStart -le $moreStart -or $bulkEnd -le $bulkStart) { throw 'The WPF More-menu bulk function-key handlers could not be located.' }
$moreHandler = $mainWindow.Substring($moreStart, $bulkStart - $moreStart)
$bulkHandler = $mainWindow.Substring($bulkStart, $bulkEnd - $bulkStart)
if ($moreHandler -notmatch 'BulkFunctionMappings\(ActionKind\.PassThrough' -or
    $moreHandler -notmatch 'BulkFunctionMappings\(ActionKind\.Unbound' -or
    $bulkHandler -notmatch 'WithBulkFunctionMappings\(configuration,\s*selectedLayer,\s*kind\)' -or
    $bulkHandler -notmatch 'bool wasCustom = showingCustom;' -or
    $bulkHandler -notmatch 'bool wasPowerToys = showingPowerToys;' -or
    $bulkHandler -notmatch 'ShowFunctionKeys\(wasCustom\);' -or
    $bulkHandler -match 'ShowFunctionKeys\(false\);' -or
    $bulkHandler -notmatch 'FooterFeedback\.Text = success; FooterFeedback\.Visibility = Visibility\.Visible;' -or
    $bulkHandler -notmatch 'FooterFeedback\.Text = message; FooterFeedback\.Visibility = Visibility\.Visible;' -or
    $mainWindowXaml -notmatch 'x:Name="FooterFeedback" Grid\.Column="0"') {
    throw 'WPF bulk function-key actions must update the selected layer and preserve the current editor section, matching WinForms.'
}
$engineSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\Engine.cs') -Raw
$startupTimeoutStart = $engineSource.IndexOf('if (!ready.WaitOne(hookStartupTimeoutMilliseconds))', [StringComparison]::Ordinal)
$startupTimeoutEnd = $engineSource.IndexOf('if (startupError != null)', $startupTimeoutStart, [StringComparison]::Ordinal)
$engineDisposeStart = $engineSource.IndexOf('public void Dispose()', $startupTimeoutEnd, [StringComparison]::Ordinal)
$engineDisposeEnd = $engineSource.IndexOf('internal static class Native', $engineDisposeStart, [StringComparison]::Ordinal)
if ($startupTimeoutStart -lt 0 -or $startupTimeoutEnd -le $startupTimeoutStart -or $engineDisposeStart -lt 0 -or $engineDisposeEnd -le $engineDisposeStart) {
    throw 'Keyboard engine startup timeout or cleanup block could not be located.'
}
$startupTimeout = $engineSource.Substring($startupTimeoutStart, $startupTimeoutEnd - $startupTimeoutStart)
$engineDispose = $engineSource.Substring($engineDisposeStart, $engineDisposeEnd - $engineDisposeStart)
if ($startupTimeout -notmatch 'Dispose\(\);\s*throw new InvalidOperationException\("The keyboard hook did not start within five seconds\."\)' -or
    $engineDispose -notmatch 'stopping = true;\s*if \(Interlocked\.Exchange\(ref stopSignaled, 1\) == 0\) \{ Interlocked\.Increment\(ref generation\); queue\.CompleteAdding\(\); monitorQueue\.CompleteAdding\(\); \}' -or
    $engineDispose -notmatch 'control\.BeginInvoke\(' -or
    $engineDispose -notmatch 'if \(!hookThread\.IsAlive && Interlocked\.Exchange\(ref readyDisposed, 1\) == 0\) ready\.Dispose\(\);') {
    throw 'A keyboard-hook startup timeout must request full worker/hook cleanup, and the readiness handle must remain alive while the hook thread is still unwinding.'
}
$runtimeStartupStart = $mainWindow.IndexOf('if (!App.DisableEngine)', [StringComparison]::Ordinal)
$runtimeStartupEnd = $mainWindow.IndexOf('SelectItem(ThemeChoice', $runtimeStartupStart, [StringComparison]::Ordinal)
if ($runtimeStartupStart -lt 0 -or $runtimeStartupEnd -le $runtimeStartupStart) { throw 'WPF keyboard runtime startup block could not be located.' }
$runtimeStartup = $mainWindow.Substring($runtimeStartupStart, $runtimeStartupEnd - $runtimeStartupStart)
$runtimeErrorSubscribeAt = $runtimeStartup.IndexOf('runtime.Error += RuntimeError;', [StringComparison]::Ordinal)
$runtimeApplyAt = $runtimeStartup.IndexOf('runtime.Apply(configuration);', [StringComparison]::Ordinal)
if ($runtimeErrorSubscribeAt -lt 0 -or $runtimeApplyAt -le $runtimeErrorSubscribeAt -or
    $runtimeStartup -notmatch 'var failedRuntime = runtime;\s*runtime = null;\s*if \(failedRuntime != null\)\s*\{\s*try \{ failedRuntime.Dispose\(\); \}\s*catch \(Exception cleanupError\) \{ AppLog.Record\("Failed keyboard runtime cleanup", cleanupError\); \}') {
    throw 'WPF runtime action errors must be subscribed before applying mappings, and a partially initialized keyboard runtime must be disposed if startup fails.'
}
$profileManager = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\ProfileManagerWindow.cs') -Raw
$mainForm = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\MainForm.cs') -Raw
$inputEngine = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\Engine.cs') -Raw
$hookStart = $inputEngine.IndexOf('IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)', [StringComparison]::Ordinal)
$hookEnd = $inputEngine.IndexOf('void NotifyEmergencyDisabled()', $hookStart, [StringComparison]::Ordinal)
if ($hookStart -lt 0 -or $hookEnd -le $hookStart) { throw 'The low-level keyboard hook callback could not be located.' }
$hookCallback = $inputEngine.Substring($hookStart, $hookEnd - $hookStart)
if ($hookCallback -notmatch 'ShortcutSuspension\.IsSuspendedCached\(\)' -or
    $hookCallback -notmatch 'GameMode\.EnabledCached' -or
    $hookCallback -notmatch 'GameMode\.IsFullscreenForegroundCached' -or
    $hookCallback -match 'ShortcutSuspension\.IsSuspended\(\)|GameMode\.RefreshEnabledState\(\)|GameMode\.IsFullscreenForeground\(\)') {
    throw 'The low-level keyboard callback must use cached policy only and perform no disk or foreground-process refresh.'
}
$networkElevation = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\WpfNetworkPreferenceElevation.cs') -Raw
$updateExit = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\WpfUpdateExit.cs') -Raw
if ($updateExit -notmatch 'Codes = \{ 0, 10, 11, 12, 14 \}' -or
    $updateExit -notmatch 'code = 14;' -or
    $updateExit -notmatch '14 => "KiWeave could not verify a safe update\. Close it manually and retry\."') {
    throw 'Every WPF update-state response, including an internal checker error, must have a response event and an accurate diagnostic.'
}
if ($networkElevation -notmatch 'enum RequestResult \{ Approved, RejectedOrFailed, OutcomeUnknown \}' -or
    $networkElevation -notmatch 'if \(wait == WaitResult\.StillRunning\) return RequestResult\.OutcomeUnknown;' -or
    $mainWindow -notmatch 'if \(result == WpfNetworkPreferenceElevation\.RequestResult\.OutcomeUnknown\)' -or
    $mainWindow -notmatch 'NetworkChoice\.IsChecked = preferences\.NetworkAccess; NetworkChoice\.IsEnabled = false;' -or
    $mainWindow -notmatch 'The administrator helper may still be running') {
    throw 'An unconfirmed elevated network helper must not be reported as a declined/no-change request or allow a second network toggle before restart.'
}
$ddcScanStart = $mainWindow.IndexOf('async void DetectDdcMonitorsClick(', [StringComparison]::Ordinal)
$ddcScanEnd = $mainWindow.IndexOf('void DdcMonitorChoiceChanged(', $ddcScanStart, [StringComparison]::Ordinal)
if ($ddcScanStart -lt 0 -or $ddcScanEnd -le $ddcScanStart) { throw 'WPF DDC monitor detection handler could not be located.' }
$ddcScan = $mainWindow.Substring($ddcScanStart, $ddcScanEnd - $ddcScanStart)
$ddcAwaitAt = $ddcScan.IndexOf('await Task.Run(() => DdcService.Shared.Scan());', [StringComparison]::Ordinal)
$ddcClosedGuardAt = $ddcScan.IndexOf('if (windowClosed || !IsLoaded) return;', [StringComparison]::Ordinal)
if ($ddcAwaitAt -lt 0 -or $ddcClosedGuardAt -le $ddcAwaitAt -or
    $ddcScan.IndexOf('LoadDdcEditor(', $ddcClosedGuardAt, [StringComparison]::Ordinal) -lt 0) {
    throw 'A completed DDC monitor scan must stop before updating editor controls when its WPF window has closed.'
}
$closedStart = $mainWindow.IndexOf('protected override void OnClosed(EventArgs e)', [StringComparison]::Ordinal)
$closedEnd = $mainWindow.IndexOf('base.OnClosed(e);', $closedStart, [StringComparison]::Ordinal)
if ($closedStart -lt 0 -or $closedEnd -le $closedStart) { throw 'WPF main-window shutdown handler could not be located.' }
$closedHandler = $mainWindow.Substring($closedStart, $closedEnd - $closedStart)
$cleanupRequirements = @(
    'windowClosed = true;',
    'FirstPartyExtensionCatalog.EnabledChanged -= ExtensionStateChanged;',
    '"MIDI trigger shutdown"',
    '"Controller trigger shutdown"',
    '"Custom hotkey shutdown"',
    '"Keyboard runtime shutdown"',
    '"Discord integration shutdown", DiscordIntegration.Disconnect',
    '"Stream Deck bridge shutdown"',
    '"Profile schedule timer shutdown"',
    '"Automatic profile timer shutdown"',
    '"Hotkey suspension timer shutdown"',
    '"Foreground-change hook shutdown"',
    '"Recovery draft timer shutdown"',
    '"Raw consumer input shutdown"',
    '"Window hook shutdown"',
    '"Tray shutdown"'
)
if ($closedHandler.IndexOf('windowClosed = true;', [StringComparison]::Ordinal) -gt $closedHandler.IndexOf('RunCleanupActions(', [StringComparison]::Ordinal) -or
    @($cleanupRequirements | Where-Object { $closedHandler.IndexOf($_, [StringComparison]::Ordinal) -lt 0 }).Count -gt 0) {
    throw 'WPF main-window shutdown must mark itself closed first and dispose every owned hook, timer, worker, and tray resource even if one cleanup fails.'
}
if ($profileManager -notmatch 'ProfileManagerWindow\(ProfileCollection source, Configuration currentConfiguration, Configuration defaultProfile\)' -or
    $profileManager -notmatch 'current = currentConfiguration\.Copy\(\); defaultConfiguration = defaultProfile\.Copy\(\);' -or
    $mainWindow -notmatch 'new ProfileManagerWindow\(profiles, configuration, ConfigStore\.Load\(ConfigStore\.DefaultPath\)\)') {
    throw 'WPF Add current must snapshot the active profile configuration while inheritance resolves against the separate saved Default configuration.'
}
$profileChangedStart = $mainWindow.IndexOf('void ProfileChanged(', [StringComparison]::Ordinal)
$profileChangedEnd = $mainWindow.IndexOf('void HideAllPages(', $profileChangedStart, [StringComparison]::Ordinal)
$profileManagerClickStart = $mainWindow.IndexOf('void ProfileManagerClick(', [StringComparison]::Ordinal)
$profileManagerClickEnd = $mainWindow.IndexOf('bool ApplyProfileAfterManager(', $profileManagerClickStart, [StringComparison]::Ordinal)
$applyManagedProfileStart = $profileManagerClickEnd
$applyManagedProfileEnd = $mainWindow.IndexOf('void ProfileSchedulesClick(', $applyManagedProfileStart, [StringComparison]::Ordinal)
if ($profileChangedStart -lt 0 -or $profileChangedEnd -le $profileChangedStart -or
    $profileManagerClickStart -lt 0 -or $profileManagerClickEnd -le $profileManagerClickStart -or
    $applyManagedProfileEnd -le $applyManagedProfileStart) {
    throw 'WPF profile-switch handlers could not be located for state-preservation checks.'
}
$profileChanged = $mainWindow.Substring($profileChangedStart, $profileChangedEnd - $profileChangedStart)
$profileManagerClick = $mainWindow.Substring($profileManagerClickStart, $profileManagerClickEnd - $profileManagerClickStart)
$applyManagedProfile = $mainWindow.Substring($applyManagedProfileStart, $applyManagedProfileEnd - $applyManagedProfileStart)
if ($profileChanged -notmatch 'int previousLayer = selectedLayer;' -or
    $profileChanged -notmatch 'currentProfile = name; selectedLayer = -1;' -or
    $profileChanged -notmatch 'selectedLayer = previousLayer;' -or
    $profileChanged -notmatch 'int previousFunctionSelection = FunctionKeyList\.SelectedIndex;' -or
    ([regex]::Matches($profileChanged, 'FunctionKeyList\.SelectedIndex = previousFunctionSelection;').Count -lt 2) -or
    ([regex]::Matches($profileChanged, 'ShowFunctionKeys\(showingCustom\);').Count -lt 2)) {
    throw 'Switching profiles must select Base, preserve the selected function key on success, and restore the previous editor/list state after activation failure.'
}
if ($applyManagedProfile -notmatch 'currentProfile = name; selectedLayer = -1;' -or
    $applyManagedProfile -notmatch 'if \(manualSelection\)\s*\{\s*automaticProfileActive = false;\s*if \(pinnedProfile\.Length > 0\) pinnedProfile = currentProfile;\s*profileReason = pinnedProfile\.Length > 0 \? "Selected manually while profile pinning is active\." : "Selected manually\.";' -or
    $applyManagedProfile -notmatch 'selectedLayer = previousLayer;' -or
    $applyManagedProfile -notmatch 'int previousFunctionSelection = FunctionKeyList\.SelectedIndex;' -or
    ([regex]::Matches($applyManagedProfile, 'FunctionKeyList\.SelectedIndex = previousFunctionSelection;').Count -lt 2) -or
    ([regex]::Matches($applyManagedProfile, 'ShowFunctionKeys\(showingCustom\);').Count -lt 2)) {
    throw 'Applying a profile from the manager must reset to Base, preserve the selected mapping, handle manual pin state, and restore the entire visible editor after failure.'
}
if ($profileManagerClick -notmatch 'ApplyProfileAfterManager\(target, true\)' -or
    $profileManagerClick -notmatch 'ApplyProfileAfterManager\(target, false\)') {
    throw 'Choosing a profile and refreshing edits to the active profile must be distinguished so refresh does not masquerade as manual selection.'
}
if ($profileManagerClick -notmatch 'dialog\.ResolveRenamedProfile\(pinnedProfile\)' -or
    $profileManagerClick -notmatch 'profiles\.Find\(resolvedPinnedProfile\) == null') {
    throw 'Renaming a pinned profile must move the pin to its renamed profile, while deleting it clears the stale pin.'
}
if ($mainWindow -notmatch 'error => WpfDispatcher\.TryPost\(Dispatcher, new Action\(\(\) => \{ if \(!windowClosed && IsLoaded\) CurrentSetupStatus\.Text \+= " Controller trigger polling stopped: " \+ error\.Message; \}\)\)') {
    throw 'The asynchronous controller-poll error callback must ignore a main window that has already closed.'
}
if ($mainWindow -notmatch 'streamDeckBridge = new WpfStreamDeckBridge\(command => WpfDispatcher\.TryPost\(Dispatcher, new Action\(delegate\s*\{\s*if \(!CanDispatchExternalCommandForState\(windowClosed, IsLoaded\)\) return;') {
    throw 'Queued Stream Deck commands must not reopen or mutate a WPF main window after it has closed.'
}
if ($streamDeckBridge -notmatch 'PipeOptions\.Asynchronous\s*\|\s*PipeOptions\.CurrentUserOnly') {
    throw 'The WPF Stream Deck named pipe must restrict clients to the current Windows user and elevation level.'
}
if ($streamDeckBridge -notmatch 'WaitForConnectionAsync\(lifetime\.Token\)' -or
    $streamDeckBridge -notmatch 'ReadBoundedLineWithTimeout\(reader,\s*2048,\s*clientReadTimeoutMilliseconds,\s*lifetime\.Token\)' -or
    $streamDeckBridge -notmatch 'lifetime\.Cancel\(\)' -or
    $streamDeckBridge -match 'pipe\.WaitForConnection\(\)' -or
    $streamDeckBridge -match 'for \(int attempt = 0; attempt < 10 && thread\.IsAlive; attempt\+\+\)') {
    throw 'Disabling the WPF Stream Deck bridge must cancel both client acceptance and stalled reads without retry-based wakeups.'
}
if ($mainWindow -notmatch 'runtime\?\.SetCustomChordPrecedence\(HotkeyRegistration\.ActiveModifierChordKeys\(configuration, registeredCustomHotkeys\)\);' -or
    $mainForm -notmatch 'engine != null\) engine\.SetCustomChordPrecedence\(HotkeyRegistration\.ActiveModifierChordKeys\(config, registeredHotkeys\)\);') {
    throw 'Both UI shells must tell the shared input engine which F-key chords were actually registered so failed bindings cannot shadow base mappings.'
}
if ($extensionDetailsWindow -notmatch 'if \(item\.Id == "discord"\)' -or
    $extensionDetailsWindow -notmatch 'Button\("Reconnect"\)' -or
    $extensionDetailsWindow -notmatch 'await Task\.Run\(\(\) => DiscordIntegration\.Reconnect\(\)\)' -or
    $extensionDetailsWindow -notmatch 'WpfMessageBox\.Show\(this, DiscordIntegration\.Status, "Discord"') {
    throw 'WPF Discord extension details must retain the WinForms reconnect action without blocking the UI.'
}
if ($mainWindow -notmatch 'Task\.Run\(\(\)\s*=>\s*CollectIntegrationHealth\(configurationSnapshot,\s*profileSnapshot\)\)' -or
    $mainWindow -notmatch 'ConfigurationHealth\.TryScan\(\(\)\s*=>\s*ConfigurationHealth\.Scan\(configuration,\s*profiles,\s*monitors\)' -or
    $mainWindow -notmatch 'AudioDevices\.Snapshot\(\)' -or
    $mainWindow -notmatch 'PowerToysIntegration\.Load\(\)' -or
    $mainWindow -notmatch 'MonitorMappingScope\.Describe\(configuration\)' -or
    $mainWindow -notmatch 'version != integrationHealthScanVersion \|\| !IsLoaded \|\| Dispatcher\.HasShutdownStarted' -or
    $mainWindow -notmatch 'new IntegrationHealthEntry\("DDC/CI monitors"' -or
    $mainWindow -notmatch 'new IntegrationHealthEntry\("Audio devices"' -or
    $mainWindow -notmatch 'new IntegrationHealthEntry\("Enabled extensions"' -or
    $mainWindow -match 'AddIntegrationHealthRow\("Configuration",\s*"Healthy"') {
    throw 'WPF Integration Health must asynchronously report live shared local checks rather than hard-coded healthy/read-only statuses.'
}
$healthClickStart = $mainWindow.IndexOf('void IntegrationHealthClick(', [StringComparison]::Ordinal)
$healthClickEnd = $mainWindow.IndexOf('static IntegrationHealthEntry[] CollectIntegrationHealth(', $healthClickStart, [StringComparison]::Ordinal)
if ($healthClickStart -lt 0 -or $healthClickEnd -le $healthClickStart) { throw 'WPF Integration Health refresh handler could not be located.' }
$healthClick = $mainWindow.Substring($healthClickStart, $healthClickEnd - $healthClickStart)
$healthReenableAt = $healthClick.IndexOf('IntegrationHealthRefreshButton.IsEnabled = true;', [StringComparison]::Ordinal)
$healthStaleCheckAt = $healthClick.IndexOf('if (version != integrationHealthScanVersion', [StringComparison]::Ordinal)
if ($healthClick -notmatch 'if \(integrationHealthScanInProgress\) return;' -or
    $healthClick -notmatch 'integrationHealthScanInProgress = true;\s*IntegrationHealthRefreshButton.IsEnabled = false;' -or
    $healthReenableAt -lt 0 -or $healthStaleCheckAt -le $healthReenableAt -or
    $mainWindowXaml -notmatch 'x:Name="IntegrationHealthRefreshButton" Content="Refresh" Click="IntegrationHealthClick"') {
    throw 'Integration Health must disable duplicate refreshes while read-only device scans are pending and always re-enable after completion.'
}
if ($mainWindow -notmatch 'if \(ShouldHideToTrayForState\(exitRequested, tray != null\)\) \{ e\.Cancel = true; WpfMotion\.HideWindow\(this\); return; \}' -or
    $mainWindow -notmatch 'if \(File\.Exists\(ProfileStore\.DefaultPath\)\) File\.Copy\(ProfileStore\.DefaultPath, Path\.Combine\(backup, "profiles\.json"\), true\);' -or
    $discordWindow -notmatch 'connectionLifetime\.Cancel\(\);\s*connectionLifetime\.Dispose\(\);\s*base\.OnClosed\(e\);' -or
    $discordWindow -notmatch 'ConnectInteractive\(new WindowOwner\(this\), PostStatus, connectionLifetime\.Token\)' -or
    $discordWindow -notmatch 'if \(IsLoaded && IsVisible\) status\.Text = text;' -or
    $discordIntegration -notmatch 'CreateLinkedTokenSource\(NetworkPolicy\.BeginRequest\(\), callerCancellation\)' -or
    $discordIntegration -notmatch 'ConnectInteractive\(owner, PostStatus, connectionLifetime\.Token\)' -or
    $discordIntegration -notmatch 'protected override void OnFormClosed\(FormClosedEventArgs e\)\s*\{\s*connectionLifetime\.Cancel\(\);\s*connectionLifetime\.Dispose\(\);' -or
    $mainWindow -notmatch 'if \(!windowClosed && IsLoaded\) SettingsScroll\.IsEnabled = true;' -or
    $mainWindow -notmatch 'if \(windowClosed \|\| !IsLoaded\) return;') {
    throw 'Closing either shell must cancel pending OAuth work and guard async UI completions against closed windows; path migration must back up the profile file it can update.'
}
$actionPickerStart = $mainWindow.IndexOf('void ActionChoiceChanged(', [StringComparison]::Ordinal)
$actionPickerEnd = $mainWindow.IndexOf('void StageActionInputs(', $actionPickerStart, [StringComparison]::Ordinal)
if ($actionPickerStart -lt 0 -or $actionPickerEnd -le $actionPickerStart) { throw 'WPF action-picker change handler could not be located.' }
$actionPickerHandler = $mainWindow.Substring($actionPickerStart, $actionPickerEnd - $actionPickerStart)
if ($actionPickerHandler -notmatch 'kind == ActionKind\.PassThrough\s*\?\s*"This key keeps its normal Windows and app behavior\."' -or
    $actionPickerHandler -notmatch 'kind == ActionKind\.Unbound\s*\?\s*"This key is consumed without running an action\."') {
    throw 'WPF action selection must explain Pass Through and Unbound in the visible editor status.'
}
$actionBuilder = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\ActionBuilderWindow.cs') -Raw
$duplicateStepStart = $actionBuilder.IndexOf('void DuplicateStep()', [StringComparison]::Ordinal)
$duplicateStepEnd = $actionBuilder.IndexOf('void RefreshSteps(', $duplicateStepStart, [StringComparison]::Ordinal)
if ($duplicateStepStart -lt 0 -or $duplicateStepEnd -le $duplicateStepStart) { throw 'The WPF sequence duplicate handler could not be located.' }
$duplicateStep = $actionBuilder.Substring($duplicateStepStart, $duplicateStepEnd - $duplicateStepStart)
if ($duplicateStep -notmatch 'sequenceSteps\.Count >= SequenceCodec\.MaxSteps' -or
    $duplicateStep -notmatch 'A sequence supports up to " \+ SequenceCodec\.MaxSteps \+ " steps\.' -or
    $duplicateStep -notmatch 'WpfColor\.FromRgb\(255, 151, 153\)' -or
    $actionBuilder -notmatch 'feedback\.Foreground = BrushFor\("MutedBrush", "#AAA5C0"\); feedback\.Text = sequenceSteps\.Count == 0') {
    throw 'WPF sequence duplication must report the step limit and reset feedback after a valid edit.'
}
$buildScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\build.ps1') -Raw
if ($buildScript -notmatch 'wpf-rewrite\\KiWeave\.Wpf\.csproj' -or
    $buildScript -notmatch 'WpfAssemblyName=KiWeave' -or
    $buildScript -notmatch 'DebugSymbols=false' -or
    $buildScript -notmatch 'DebugType=None') {
    throw 'The public build must publish the WPF app as KiWeave.exe without debug-symbol files.'
}
$localReleaseScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\BuildBeta5LocalRelease.ps1') -Raw
if ($localReleaseScript -notmatch '(?m)& \(Join-Path \$repoRoot ''build\.ps1''\)\s+-OutputFolder\s+\(Split-Path -Leaf \$validationOutput\)\s+-DotnetPath \$DotnetPath') {
    throw 'The local Beta 5 release gate must pass its selected .NET SDK path into the shared build.'
}
$recoveryWindow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\RecoveryWindow.cs') -Raw
if ($recoveryWindow -notmatch 'ConfigurationHistoryComparison\.Compare\(draft, current, profiles, currentPreferences, startup\)' -or $recoveryWindow -match 'static string BuildDetails\(') {
    throw 'The WPF crash-recovery review must use the shared, privacy-safe configuration comparison.'
}
if ($mainWindow -notmatch 'if \(!ConfigurationHealth\.TryScan\(\(\) => ConfigurationHealth\.Scan\(configuration, profiles, monitors\), out ConfigurationHealthReport health\)\) health = ConfigurationHealthReport\.Unavailable;') {
    throw 'The WPF privacy health page must show a safe fallback if local configuration inspection fails.'
}
$hotkeySource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\Hotkeys.cs') -Raw
if ($hotkeySource -notmatch 'ShouldPollForeground\(bool notificationsAvailable, bool registrationNeedsRetry\)\s*\{\s*return !notificationsAvailable \|\| registrationNeedsRetry;\s*\}' -or
    $mainWindow -notmatch 'void UpdateCustomHotkeySuspensionPolling\(\)' -or
    $mainWindow -notmatch 'if \(foregroundChangeMonitor != null\) UpdateCustomHotkeySuspensionPolling\(\);' -or
    $mainWindow -notmatch 'UpdateAutomaticProfileTimers\(\); UpdateCustomHotkeySuspensionPolling\(\); ApplyGlassBackdrop\(\);' -or
    $mainWindow -notmatch 'customHotkeySuspensionTimer\.Interval = TimeSpan\.FromMilliseconds\(notificationsAvailable \? 1000 : 100\);') {
    throw 'WPF foreground-state polling must stop when notifications work, except for bounded hotkey-registration retries, and keep polling fallback when notifications are unavailable.'
}
$popupWindows = @(
    'FirstPartyExtensionsWindow.cs',
    'SafeModeWindow.cs',
    'WelcomeWindow.cs'
)
foreach ($popupWindowName in $popupWindows) {
    $popupWindow = Get-Content -LiteralPath (Join-Path $PSScriptRoot ('..\wpf-rewrite\' + $popupWindowName)) -Raw
    if ($popupWindow -notmatch 'WindowStyle\s*=\s*WindowStyle\.None' -or
        $popupWindow -notmatch 'WpfWindowParity\.EnableCaptionCloseFallback\(this\)' -or
        $popupWindow -notmatch 'WpfWindowParity\.EnableEscapeClose\(this\)' -or
        $popupWindow -notmatch '(?s)close\.Click\s*\+=\s*delegate\s*\{[^}]*?(?:Close\(\)|DialogResult\s*=\s*false)') {
        throw "Borderless WPF dialog '$popupWindowName' must retain its close-button, caption fallback, and Escape behavior."
    }
}
$actionLibraryWindow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\ActionLibraryWindow.cs') -Raw
if ($actionLibraryWindow -notmatch 'WindowStyle\s*=\s*WindowStyle\.SingleBorderWindow' -or
    $actionLibraryWindow -notmatch 'ResizeMode\s*=\s*ResizeMode\.CanResize' -or
    $actionLibraryWindow -notmatch 'WpfWindowParity\.EnableEscapeClose\(this\)' -or
    $actionLibraryWindow -notmatch 'search\.PreviewKeyDown\s*\+=\s*delegate\(object sender, System\.Windows\.Input\.KeyEventArgs e\)\s*\{\s*if \(e\.Key == Key\.Down && list\.Items\.Count > 0\) \{ list\.Focus\(\); e\.Handled = true; \} \}' -or
    $actionLibraryWindow -notmatch 'readonly Button choose = new Button\(\);' -or
    $actionLibraryWindow -notmatch 'choose\.IsEnabled = selected != null;' -or
    $actionLibraryWindow -match 'EnableCaptionCloseFallback|WindowStyle\.None|WindowChrome') {
    throw 'WPF action library must retain the previous shell native title bar, standard system controls, and Escape-to-close behavior.'
}
$appSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\wpf-rewrite\App.xaml.cs') -Raw
if ($appSource -notmatch 'startupWindow\.Opacity\s*=\s*0' -or
    $appSource -notmatch 'startupWindow\.ContentRendered\s*\+=' -or
    $appSource -notmatch 'startupWindow\.Opacity\s*=\s*1') {
    throw 'WPF startup must hide the native host until the first composed frame to prevent a black launch surface.'
}
Write-Output 'PASS WPF action library uses the previous shell native title bar and standard system controls'
Write-Output 'PASS WPF update-checker launches return before migration and user-language loading'
Write-Output 'PASS WPF update-checker signals its internal-error response without misreporting a timeout'
Write-Output 'PASS elevated network timeout distinguishes an unconfirmed helper from a declined request'
Write-Output 'PASS WPF DDC monitor scans stop before touching a closed editor window'
Write-Output 'PASS WPF main-window shutdown marks closed and releases every owned runtime resource'
Write-Output 'PASS WPF status flyout shutdown stops its hold timer'
Write-Output 'PASS WPF smooth scrolling follows compositor render timestamps without a fixed frame-rate cap'
Write-Output 'PASS WPF close hides to tray while explicit Exit bypasses tray hiding'
Write-Output 'PASS WPF path migration backs up the profile file before updating a non-default profile'
Write-Output 'PASS closed windows in both shells ignore pending OAuth, monitor-scan, and preference-elevation completions'
Write-Output 'PASS WPF controller-trigger errors ignore a closed main window'
Write-Output 'PASS queued Stream Deck commands ignore a closed main window'
Write-Output 'PASS WPF Stream Deck named pipe accepts only the current user/elevation level'
Write-Output 'PASS WPF action selection explains Pass Through and Unbound'
Write-Output 'PASS WPF sequence duplication reports the step limit and resets feedback after valid edits'
Write-Output 'PASS WPF More-menu bulk actions target the selected layer and preserve the current editor section'
Write-Output 'PASS both shells sync active F-key chord registrations with the shared engine'
Write-Output 'PASS WPF Integration Health performs responsive live checks, serializes refresh requests, and ignores stale/closed-window results'
Write-Output 'PASS WPF Discord extension details retain the responsive reconnect action'
Write-Output 'PASS the public build publishes WPF as the canonical KiWeave app without debug symbols'
Write-Output 'PASS the local Beta 5 release gate propagates its selected .NET SDK path'
Write-Output 'PASS WPF and WinForms recovery review use the shared configuration comparison'
Write-Output 'PASS WPF privacy health scan failures use the redacted fallback'
Write-Output 'PASS WPF profile manager snapshots active mappings separately from Default'
Write-Output 'PASS borderless WPF dialogs retain working X, caption fallback, and Escape close behavior'
Write-Output 'PASS WPF foreground polling stops when event notifications work but retains fallback and registration retries'
Write-Output 'PASS WPF automatic profile selection uses the shared foreground-process lookup and rate-limits error logging'
Write-Output 'PASS Discord reconnect and voice IPC avoid the status lock, serialize pipe use, and reject unsafe fallback/stale results'
Write-Output 'PASS WPF profile switches reset to Base and restore the previous layer on failure'
Write-Output 'PASS WPF profile switches preserve the selected function key and roll back the visible editor'
Write-Output 'PASS WPF profile-manager rename keeps a valid profile pin and activation updates it'
Write-Output 'PASS WPF profile-manager manual activation clears automatic state without changing refresh semantics'
Write-Output 'PASS low-level keyboard callback uses cached policy and performs no disk or foreground-process refresh'
Write-Output 'PASS WPF startup hides the native host until the first composed frame'
