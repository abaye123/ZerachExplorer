# ZerachExplorer - installation, setup and user guide

[עברית](guide.he.md)

## Contents

1. [Requirements](#requirements)
2. [Installation](#installation)
3. [First setup](#first-setup)
4. [Administrator settings](#administrator-settings)
5. [Copying settings to other computers](#copying-settings-to-other-computers)
6. [Usage](#usage)
7. [Keyboard and mouse shortcuts](#keyboard-and-mouse-shortcuts)
8. [Hiding a drive](#hiding-a-drive)
9. [Troubleshooting](#troubleshooting)
10. [File locations and removal](#file-locations-and-removal)

## Requirements

- 64-bit Windows 10 or 11.
- .NET does not need to be installed.
- Installing and saving settings require Windows administrator approval.

## Installation

### Installer (recommended for public stations)

1. Download [`ZerachExplorer_setup.exe`](https://github.com/abaye123/ZerachExplorer/releases/latest/download/ZerachExplorer_setup.exe).
2. Run it, approve the Windows prompt and follow the instructions.
3. The installer also upgrades earlier versions in place.

The application is signed with a self-signed certificate, so Windows may show a SmartScreen warning: choose **More info**, then **Run anyway**. Download only from the official links.

Silent installation for scripts:

```bat
ZerachExplorer_setup.exe /VERYSILENT /NORESTART
```

### Portable file

[`ZerachExplorer_portable.exe`](https://github.com/abaye123/ZerachExplorer/releases/latest/download/ZerachExplorer_portable.exe) runs without installation. It unpacks libraries into a temporary folder of the user, so the installer is the better choice for a public station.

## First setup

1. Open the settings with the gear button or `Ctrl+Shift+A`.
2. Set an administrator password of at least 8 characters. **There is no default password.**
3. Approve the Windows prompt.

Every save requires Windows administrator approval. After 5 wrong passwords there is a growing delay of up to 15 minutes.

**Important:** set the password right after installing, and run the station as a standard Windows user rather than an administrator. See also [SECURITY.md](../SECURITY.md).

## Administrator settings

| Page | Purpose |
|---|---|
| Profile and permissions | Standard, read-only, controlled or custom, and every single permission |
| Kiosk and USB drives | Full screen, exit by password only, offer to open a connected USB drive. All off by default |
| File types | Extensions that may be opened and copied |
| Approved folders | Folders shown on the home screen. In the controlled profile only approved locations are available |
| Drives | Blocking additional drives |
| Log | Days to keep and maximum size |
| Administrator password | Changing the password |
| Import and export | Copying settings to other computers |

Allowed by default: `doc`, `docx`, `xls`, `xlsx`, `pdf`, `txt`, `csv`, `one`, `onetoc2`. All files are listed, but only these can be opened and copied.

The system drive and "Otzar HaHochma" drives are always blocked, and an approved folder does not override this.

## Copying settings to other computers

### Inside the application

1. On the configured computer: **Settings > Import and export > Export**.
2. On the new computer: **Settings > Import and export > Import**, then **Save**.

The password is not included unless the switch is turned on. Without a password in the file, the new computer keeps its own. A file with a password contains only a hash, but should still be kept safe.

### By script, for many computers

Run as administrator:

```bat
ZerachExplorer_setup.exe /VERYSILENT /NORESTART
start /wait "" "%ProgramFiles%\ZerachExplorer\ZerachExplorer.exe" --import-settings "\\server\share\ZerachExplorer-settings.json"
echo %errorlevel%
```

| Code | Meaning |
|---|---|
| 0 | Imported |
| 2 | Not run as administrator |
| 3 | Missing or invalid file |
| 4 | Saving failed |

To set the password as well, export the file with the password.

## Usage

- The **home screen** shows the available drives and approved folders.
- **Copy and move** work inside the application and with Windows Explorer. Long operations show progress, speed, pause and cancel.
- **Existing files:** the application asks whether to replace, skip or keep both.
- **Deleting** uses the recycle bin. On drives without one (USB, network) deletion is **permanent**, after a warning.
- **Ejecting USB:** right-click the drive and choose safe eject.
- **Selecting:** draw a rectangle, use the check boxes, or `Ctrl` and `Shift`. The check boxes can be hidden in the View menu.
- **Preview:** `Alt+P`. Shows text and the details of a file or folder.
- **Search** filters the current folder only, without subfolders.

## Keyboard and mouse shortcuts

| Action | Shortcut |
|---|---|
| New / close tab | `Ctrl+T` / `Ctrl+W` or middle click |
| Switch tabs | `Ctrl+Tab`, `Ctrl+1` to `Ctrl+9` |
| Folder in a background tab | Middle click |
| Back / forward / up | `Alt+Left` / `Alt+Right` / `Alt+Up`, mouse buttons |
| Address / search | `Ctrl+L` / `Ctrl+F` |
| Copy, cut, paste | `Ctrl+C`, `Ctrl+X`, `Ctrl+V` |
| Rename / delete / refresh | `F2` / `Delete` / `F5` |
| New folder | `Ctrl+Shift+N` |
| Switch view | `Ctrl+Shift+V` or `Ctrl`+wheel |
| Preview | `Alt+P` |
| Administrator settings | `Ctrl+Shift+A` |
| Exit | `Ctrl+Q` |

## Hiding a drive

A drive is blocked when any of these applies:

- It is marked on the **Drives** page of the settings.
- Its root contains a file named `otzar.exe` (even empty) or a folder named `Otzar`.
- Its label contains `Otzar` or "אוצר החוכמה".

## Troubleshooting

- **A drive is missing:** check that it is not blocked in the settings, has no `otzar.exe` or `Otzar` folder, and is visible in Windows Explorer.
- **A file does not open:** if a message appeared, its extension is not approved. If it opens and closes, the protection software of the station probably blocks Word or the PDF reader, which must be allowed there.
- **Settings are locked:** the configuration file is damaged or not owned by an administrator. Setting a new password with Windows administrator approval resets it.
- **Forgotten password:** a Windows administrator deletes `C:\ProgramData\ZerachExplorer\config.json` and a new password is set. Settings are reset too.
- **Unexpected error:** details are in `%LocalAppData%\ZerachExplorer\logs`.

Support: cs@abaye.co

## File locations and removal

| What | Where |
|---|---|
| Application | `C:\Program Files\ZerachExplorer` |
| Administrator settings | `C:\ProgramData\ZerachExplorer\config.json` |
| Log and view preferences | `%LocalAppData%\ZerachExplorer`, per user |

Uninstall through Windows settings. Settings and logs remain and can be deleted manually.
