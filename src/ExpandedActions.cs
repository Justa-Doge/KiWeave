using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FunctionRowRemapper
{
    // Adding catalog entries never runs them. These mappings use the existing validated dispatcher.
    internal static class ExpandedActions
    {
        internal static readonly MainForm.SpecificChoice[] All = Build();
        static MainForm.SpecificChoice[] Build()
        {
            var items = new List<MainForm.SpecificChoice>();
            AddKeys(items, "Windows and windows", "Windows shortcut; behavior can vary by Windows version.", @"
Open start menu|Ctrl+Escape
Open Windows search|Win+S
Open accessibility settings|Win+U
Open projection menu|Win+P
Open cast menu|Win+K
Open widgets|Win+W
Open voice typing|Win+H
Open snap layouts|Win+Z
Minimize all windows|Win+M
Restore minimized windows|Win+Shift+M
Minimize other windows|Win+Home
Move window to left monitor|Win+Shift+Left
Move window to right monitor|Win+Shift+Right
Open window menu|Alt+Space
Switch to previous app|Alt+Shift+Tab
Open persistent app switcher|Ctrl+Alt+Tab
Cycle windows in launch order|Alt+Escape
Focus notification area|Win+B
Cycle taskbar apps forward|Win+T
Cycle taskbar apps backward|Win+Shift+T
Switch input language|Win+Space
Copy active window screenshot|Alt+PrintScreen
Save full-screen screenshot|Win+PrintScreen
Open context menu|Shift+F10
Cycle interface areas|F6
Activate menu bar|F10
Open game bar|Win+G
Take game bar screenshot|Win+Alt+PrintScreen
Toggle game recording|Win+Alt+R
Toggle game recording microphone|Win+Alt+M
Open magnifier|Win+Oemplus
Zoom magnifier out|Win+OemMinus
Close magnifier|Win+Escape
Toggle narrator|Win+Ctrl+Enter
Toggle color filters|Win+Ctrl+C
Open live captions|Win+Ctrl+L");
            AddKeys(items, "Text and editing", "Acts on the focused control; support depends on the app.", @"
Paste without formatting|Ctrl+Shift+V
Save as|Ctrl+Shift+S
Replace text|Ctrl+H
Find next result|F3
Find previous result|Shift+F3
Move one word left|Ctrl+Left
Move one word right|Ctrl+Right
Move one paragraph up|Ctrl+Up
Move one paragraph down|Ctrl+Down
Move to line start|Home
Move to line end|End
Move to document start|Ctrl+Home
Move to document end|Ctrl+End
Select character left|Shift+Left
Select character right|Shift+Right
Select line above|Shift+Up
Select line below|Shift+Down
Select word left|Ctrl+Shift+Left
Select word right|Ctrl+Shift+Right
Select paragraph above|Ctrl+Shift+Up
Select paragraph below|Ctrl+Shift+Down
Select to line start|Shift+Home
Select to line end|Shift+End
Select to document start|Ctrl+Shift+Home
Select to document end|Ctrl+Shift+End
Select page upward|Shift+PageUp
Select page downward|Shift+PageDown
Delete previous word|Ctrl+Back
Delete next word|Ctrl+Delete
Toggle bold text|Ctrl+B
Toggle italic text|Ctrl+I
Toggle underline text|Ctrl+U
Insert hyperlink|Ctrl+K
Move to next field|Tab
Move to previous field|Shift+Tab
Cancel current operation|Escape
Confirm current selection|Enter
Insert a line break|Shift+Enter
Scroll page upward|PageUp
Scroll page downward|PageDown");
            AddKeys(items, "File explorer", "File explorer must be focused. File actions apply to the selected items.", @"
Explorer: new window|Ctrl+N
Explorer: close window or tab|Ctrl+W
Explorer: new tab|Ctrl+T
Explorer: next tab|Ctrl+Tab
Explorer: previous tab|Ctrl+Shift+Tab
Explorer: create folder|Ctrl+Shift+N
Explorer: rename selection|F2
Explorer: show properties|Alt+Enter
Explorer: parent folder|Alt+Up
Explorer: go back|Alt+Left
Explorer: go forward|Alt+Right
Explorer: focus address bar|Alt+D
Explorer: search folder|Ctrl+E
Explorer: refresh folder|F5
Explorer: toggle preview pane|Alt+P
Explorer: toggle details pane|Alt+Shift+P
Explorer: extra-large icons|Ctrl+Shift+1
Explorer: large icons|Ctrl+Shift+2
Explorer: medium icons|Ctrl+Shift+3
Explorer: small icons|Ctrl+Shift+4
Explorer: list view|Ctrl+Shift+5
Explorer: details view|Ctrl+Shift+6
Explorer: tiles view|Ctrl+Shift+7
Explorer: content view|Ctrl+Shift+8
Explorer: expand folder tree|Multiply
Explorer: expand selected folder|Add
Explorer: collapse selected folder|Subtract");
            AddKeys(items, "Chrome browser", "Chrome must be focused. Many also work in Edge; app or site overrides can differ.", @"
Browser: new tab|Ctrl+T
Browser: reopen closed tab|Ctrl+Shift+T
Browser: next tab|Ctrl+Tab
Browser: previous tab|Ctrl+Shift+Tab
Browser: close tab|Ctrl+W
Browser: new window|Ctrl+N
Browser: private window|Ctrl+Shift+N
Browser: close window|Ctrl+Shift+W
Browser: last tab|Ctrl+9
Browser: move tab left|Ctrl+Shift+PageUp
Browser: move tab right|Ctrl+Shift+PageDown
Browser: address bar|Ctrl+L
Browser: history|Ctrl+H
Browser: downloads|Ctrl+J
Browser: bookmark page|Ctrl+D
Browser: bookmark all tabs|Ctrl+Shift+D
Browser: bookmark manager|Ctrl+Shift+O
Browser: toggle bookmarks bar|Ctrl+Shift+B
Browser: reload page|Ctrl+R
Browser: reload without cache|Ctrl+Shift+R
Browser: stop loading|Escape
Browser: back|Alt+Left
Browser: forward|Alt+Right
Browser: home page|Alt+Home
Browser: find in page|Ctrl+F
Browser: next match|Ctrl+G
Browser: previous match|Ctrl+Shift+G
Browser: zoom in|Ctrl+Oemplus
Browser: zoom out|Ctrl+OemMinus
Browser: reset zoom|Ctrl+0
Browser: full screen|F11
Browser: print page|Ctrl+P
Browser: save page|Ctrl+S
Browser: open local file|Ctrl+O
Browser: view page source|Ctrl+U
Browser: developer tools|F12
Browser: developer console|Ctrl+Shift+J
Browser: inspect element|Ctrl+Shift+C
Chrome: task manager|Shift+Escape
Chrome: open menu|Alt+F
Chrome: switch profile menu|Ctrl+Shift+M
Chrome: focus toolbar|Alt+Shift+T
Chrome: toggle caret browsing|F7");
            AddKeys(items, "VS Code", "VS Code must be focused with default Windows keybindings. Extensions can override shortcuts.", @"
Code: command palette|Ctrl+Shift+P
Code: quick open file|Ctrl+P
Code: new file|Ctrl+N
Code: open file|Ctrl+O
Code: save file|Ctrl+S
Code: save as|Ctrl+Shift+S
Code: new window|Ctrl+Shift+N
Code: close window|Ctrl+Shift+W
Code: user settings|Ctrl+Oemcomma
Code: move line up|Alt+Up
Code: move line down|Alt+Down
Code: copy line up|Alt+Shift+Up
Code: copy line down|Alt+Shift+Down
Code: delete line|Ctrl+Shift+K
Code: insert line below|Ctrl+Enter
Code: insert line above|Ctrl+Shift+Enter
Code: jump to matching bracket|Ctrl+Shift+OemPipe
Code: indent line|Ctrl+OemCloseBrackets
Code: outdent line|Ctrl+OemOpenBrackets
Code: fold region|Ctrl+Shift+OemOpenBrackets
Code: unfold region|Ctrl+Shift+OemCloseBrackets
Code: toggle line comment|Ctrl+OemQuestion
Code: toggle block comment|Alt+Shift+A
Code: toggle word wrap|Alt+Z
Code: go to line|Ctrl+G
Code: go to symbol|Ctrl+Shift+O
Code: workspace symbols|Ctrl+T
Code: problems panel|Ctrl+Shift+M
Code: next problem|F8
Code: previous problem|Shift+F8
Code: select next matching word|Ctrl+D
Code: select current line|Ctrl+L
Code: select all matching selections|Ctrl+Shift+L
Code: select all matching words|Ctrl+F2
Code: expand selection|Alt+Shift+Right
Code: shrink selection|Alt+Shift+Left
Code: undo cursor operation|Ctrl+U
Code: show suggestions|Ctrl+Space
Code: parameter hints|Ctrl+Shift+Space
Code: format document|Alt+Shift+F
Code: go to definition|F12
Code: peek definition|Alt+F12
Code: quick fix|Ctrl+OemPeriod
Code: show references|Shift+F12
Code: rename symbol|F2
Code: split editor|Ctrl+OemPipe
Code: move editor left|Ctrl+Shift+PageUp
Code: move editor right|Ctrl+Shift+PageDown
Code: close editor|Ctrl+W
Code: reopen closed editor|Ctrl+Shift+T
Code: toggle sidebar|Ctrl+B
Code: files sidebar|Ctrl+Shift+E
Code: search files|Ctrl+Shift+F
Code: source control|Ctrl+Shift+G
Code: debug sidebar|Ctrl+Shift+D
Code: extensions sidebar|Ctrl+Shift+X
Code: replace in files|Ctrl+Shift+H
Code: output panel|Ctrl+Shift+U
Code: Markdown preview|Ctrl+Shift+V
Code: toggle breakpoint|F9
Code: start or continue debugging|F5
Code: stop debugging|Shift+F5
Code: step into|F11
Code: step out|Shift+F11
Code: step over|F10
Code: toggle terminal|Ctrl+Oemtilde
Code: new terminal|Ctrl+Shift+Oemtilde");
            for (int i = 1; i <= 9; i++) {
                Key(items, "Taskbar: open app " + i, "Win+" + i, "Taskbar", "Uses the pinned app at this taskbar position.");
                Key(items, "Taskbar: new instance of app " + i, "Win+Shift+" + i, "Taskbar", "The app must support multiple instances.");
                Key(items, "Taskbar: jump list for app " + i, "Win+Alt+" + i, "Taskbar", "Opens the pinned app's jump list.");
                if (i < 9) Key(items, "Browser: tab " + i, "Ctrl+" + i, "Chrome browser", "Chrome must be focused; selects this tab position.");
            }
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            foreach (var row in Rows(@"
Display|display
Graphics preferences|display-advancedgraphics
Default graphics options|display-advancedgraphics-default
Night light|nightlight
Sound|sound
Volume mixer|apps-volume
Sound devices|sound-devices
Default microphone|sound-defaultinputproperties
Default audio output|sound-defaultoutputproperties
Notifications|notifications
Focus assist|quiethours
Power and sleep|powersleep
Energy recommendations|energyrecommendations
Storage|storagesense
Storage sense|storagepolicies
Storage recommendations|storagerecommendations
Disks and volumes|disksandvolumes
Save locations|savelocations
Multitasking|multitasking
Projecting to this PC|project
Shared experiences|crossdevice
Clipboard|clipboard
About this PC|about
Bluetooth|bluetooth
Connected devices|connecteddevices
Printers and scanners|printers
Mouse|mousetouchpad
Touchpad|devices-touchpad
Typing|typing
Hardware typing suggestions|devicestyping-hwkbtextsuggestions
Pen and Windows Ink|pen
AutoPlay|autoplay
USB|usb
Cameras|camera
Mobile devices|mobile-devices
Network status|network-status
Advanced network settings|network-advancedsettings
Wi-Fi|network-wifi
Known Wi-Fi networks|network-wifisettings
Ethernet|network-ethernet
VPN|network-vpn
Mobile hotspot|network-mobilehotspot
Proxy|network-proxy
Airplane mode|network-airplanemode
Background|personalization-background
Colors|personalization-colors
Lock screen|lockscreen
Themes|themes
Fonts|fonts
Start menu|personalization-start
Taskbar|taskbar
Text input|personalization-textinput
Touch keyboard|personalization-touchkeyboard
Dynamic lighting|personalization-lighting
Installed apps|appsfeatures
Default apps|defaultapps
Optional features|optionalfeatures
Startup apps|startupapps
Video playback|videoplayback
Your account info|yourinfo
Email and app accounts|emailandaccounts
Sign-in options|signinoptions
Other users|otherusers
Work or school accounts|workplace
Date and time|dateandtime
Region|regionformatting
Language|regionlanguage
Advanced keyboard settings|keyboard-advanced
Speech|speech
Game bar|gaming-gamebar
Game captures|gaming-gamedvr
Game mode|gaming-gamemode
Accessibility display|easeofaccess-display
Accessibility audio|easeofaccess-audio
Captions|easeofaccess-closedcaptioning
Color filters|easeofaccess-colorfilter
Contrast themes|easeofaccess-highcontrast
Accessibility keyboard|easeofaccess-keyboard
Magnifier|easeofaccess-magnifier
Accessibility mouse|easeofaccess-mouse
Mouse pointer|easeofaccess-mousepointer
Narrator|easeofaccess-narrator
Text cursor|easeofaccess-cursor
Visual effects|easeofaccess-visualeffects
Search permissions|search-permissions
Windows search settings|search
Camera permissions|privacy-webcam
Microphone permissions|privacy-microphone
Location permissions|privacy-location
File system permissions|privacy-broadfilesystemaccess
Voice activation permissions|privacy-voiceactivation
Diagnostics and feedback|privacy-feedback
Activation|activation
Troubleshooting|troubleshoot
Recovery options|recovery
Windows update|windowsupdate
Update history|windowsupdate-history
Update active hours|windowsupdate-activehours
Advanced update options|windowsupdate-options
Optional updates|windowsupdate-optionalupdates
Delivery optimization|delivery-optimization
Developer settings|developers")) {
                bool proper = row[0].StartsWith("Windows") || row[0].StartsWith("Wi-Fi") || row[0] == "USB" || row[0] == "VPN";
                string label = "Settings: " + (proper ? row[0] : Char.ToLowerInvariant(row[0][0]) + row[0].Substring(1));
                items.Add(new MainForm.SpecificChoice(label, new Mapping { Kind = ActionKind.Application, Target = explorer, Arguments = "ms-settings:" + row[1] }, "Windows settings", "Opens the page only; availability depends on Windows version and hardware."));
            }
            Folder(items, "Open desktop folder", Environment.SpecialFolder.DesktopDirectory);
            Folder(items, "Open documents folder", Environment.SpecialFolder.MyDocuments);
            Folder(items, "Open pictures folder", Environment.SpecialFolder.MyPictures);
            Folder(items, "Open music folder", Environment.SpecialFolder.MyMusic);
            Folder(items, "Open videos folder", Environment.SpecialFolder.MyVideos);
            Folder(items, "Open user profile folder", Environment.SpecialFolder.UserProfile);
            Folder(items, "Open roaming app data folder", Environment.SpecialFolder.ApplicationData);
            Folder(items, "Open local app data folder", Environment.SpecialFolder.LocalApplicationData);
            Folder(items, "Open startup shortcuts folder", Environment.SpecialFolder.Startup);
            Folder(items, "Open start menu shortcuts folder", Environment.SpecialFolder.StartMenu);
            Folder(items, "Open Windows folder", Environment.SpecialFolder.Windows);
            Folder(items, "Open temporary files folder", Path.GetTempPath());
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            foreach (var row in Rows(@"
Open system information|msinfo32.exe
Open resource monitor|resmon.exe
Open DirectX diagnostics|dxdiag.exe
Open on-screen keyboard|osk.exe
Open character map|charmap.exe
Open Windows version|winver.exe
Open command prompt|cmd.exe")) {
                string path = Path.Combine(sys, row[1]);
                if (File.Exists(path)) items.Add(new MainForm.SpecificChoice(row[0], new Mapping { Kind = ActionKind.Application, Target = path }, "Windows tools", "Opens the tool with your normal account; no command is pre-run."));
            }
            return items.ToArray();
        }
        static IEnumerable<string[]> Rows(string text) { return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().Split('|')); }
        static void AddKeys(List<MainForm.SpecificChoice> items, string category, string context, string data) { foreach (var row in Rows(data)) Key(items, row[0], row[1], category, context); }
        static void Key(List<MainForm.SpecificChoice> items, string label, string chord, string category, string context) { items.Add(new MainForm.SpecificChoice(label, new Mapping { Kind = ActionKind.SendShortcut, Target = chord }, category, context)); }
        static void Folder(List<MainForm.SpecificChoice> items, string label, Environment.SpecialFolder folder) { Folder(items, label, Environment.GetFolderPath(folder)); }
        static void Folder(List<MainForm.SpecificChoice> items, string label, string path) { if (!String.IsNullOrEmpty(path) && Directory.Exists(path)) items.Add(new MainForm.SpecificChoice(label, new Mapping { Kind = ActionKind.FileOrFolder, Target = path }, "Folders", "Opens this local folder without changing its contents.")); }
    }
}
