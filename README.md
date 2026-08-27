# Quick_Sab

WPF (.NET 6) quick launcher: a large filter box, actions defined in a JSON file, a global hotkey to show / hide the window, and named panels of pinned shortcuts.

## Run

```powershell
dotnet build
.\bin\Debug\net6.0-windows\Quick_Sab.exe
```

The app lives in the notification area (tray icon). Left click = show / hide, right click = menu (Settings, Open config.json, Exit).

## Usage

| Key                  | Effect                                                     |
|----------------------|------------------------------------------------------------|
| `Ctrl + Alt + Space` | Show / hide the window (configurable)                      |
| Type text            | Filter on the key (then description / value)               |
| `Enter` / click      | Run the selected action, then hide the window              |
| `Tab`                | Complete the selected key                                  |
| `Up` / `Down`        | Navigate the suggestions                                   |
| `Ctrl + P`           | Pin / unpin the selected suggestion in the first panel     |
| `Esc`                | Clear the filter, then hide the window                     |
| ⚙ button             | Open the settings window                                   |

### Pinned panels

- Each suggestion row has a 📌 button: it opens a menu listing the panels (checked = already pinned there). Click a panel to pin / unpin, or *Pin to a new panel...*.
- Every pinned chip has a ✕ to unpin it.
- *＋ New panel* creates a named panel. Each panel header has ✎ (rename) and ✕ (delete); right-click the header for *Move up / Move down*.
- Panels can also be managed in *Settings → Panels*.

## Action types

| Type      | Value                                  | Behaviour                                                        |
|-----------|----------------------------------------|------------------------------------------------------------------|
| `Share`   | Local or UNC path (`\\server\share`)   | Opens in Explorer                                                |
| `Web`     | URL                                    | Opens in the default browser                                     |
| `Command` | Command line                           | Runs through `cmd.exe /c` (or `/k` with "Keep cmd open"), in the given working directory |

Each type has its own colour, editable in the **Colors** tab.

## Variables in values

| Syntax                     | Replaced by                                     |
|----------------------------|-------------------------------------------------|
| `{{git_path_1}}`           | Path of the 1st git repository (2, 3, ...)      |
| `{{git_name_1}}`           | Name of the 1st git repository                  |
| `{{git:name}}` / `{{name}}`| Path of the git repository named `name`         |
| `{{my_variable}}`          | Free variable (**Variables** tab)               |
| `{{env:USERPROFILE}}`      | Environment variable                            |

## Configuration file

`%AppData%\Quick_Sab\config.json` (path shown in *Settings → Hotkey & options*).

```json
{
  "Hotkey": { "Ctrl": true, "Alt": true, "Shift": false, "Win": false, "Key": "Space" },
  "HideAfterAction": true,
  "HideOnFocusLost": true,
  "Colors": { "Share": "#3B82F6", "Web": "#22C55E", "Command": "#F59E0B" },
  "GitRepos": [ { "Name": "quick_sab", "Path": "C:\\_Projects\\Quick_Sab" } ],
  "Variables": [ { "Name": "branch", "Value": "main" } ],
  "Panels": [
    { "Name": "Favorites", "Keys": [ "google", "projects" ] },
    { "Name": "Git",       "Keys": [ "git status" ] }
  ],
  "Items": [
    { "Key": "google",     "Value": "https://www.google.com", "Type": "Web" },
    { "Key": "share",      "Value": "\\\\server\\share",       "Type": "Share" },
    { "Key": "git status", "Value": "git status", "Type": "Command",
      "WorkingDirectory": "{{git_path_1}}", "KeepWindowOpen": true }
  ]
}
```

Older files using a flat `"Pinned": [ ... ]` list are migrated automatically into a panel named *Pinned*.

`Key` of the hotkey = name of a `System.Windows.Input.Key` value (`Space`, `Q`, `F12`, `OemTilde`...).

## Structure

- `Models/AppConfig.cs` — configuration model
- `Services/ConfigService.cs` — JSON load / save, legacy migration
- `Services/VariableResolver.cs` — `{{variable}}` substitution
- `Services/ActionExecutor.cs` — Share / Web / Command execution
- `Services/HotkeyManager.cs` — global hotkey (RegisterHotKey)
- `Views/MainWindow.xaml` — launcher (filter, autocomplete, panels, tray)
- `Views/ConfigWindow.xaml` — settings window
- `Views/InputDialog.xaml` — small text prompt (panel names)
