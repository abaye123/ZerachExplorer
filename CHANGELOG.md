# Changelog

All notable changes to ZerachExplorer will be documented in this file.

## [1.7.0] - Stage 7: Kiosk Lockdown, Session Privacy, Audit Logging & Grid/Tile Views

### Features & Security (אבטחה ותכונות עמדה)
- **Kiosk Lockdown & Exit Interception (מצב קיוסק ויירוט יציאה):** Added KioskController managing borderless fullscreen mode and intercepting application exit requests (Closing event), requiring administrator password authentication when active.
- **Audit & Activity Logging (יומן פעילות וביקורת):** Implemented AuditService with secure auto-rotating log storage (`%ProgramData%\ZerachExplorer\audit.log`) tracking file opens, copy/move/rename/delete operations, drive connections/ejections, and security violations.
- **Session Reset & Privacy Wipe (איפוס סשן ומחיקת פרטיות):** Added SessionService to automatically clean temporary files, wipe OS and internal clipboards, and return navigation to home upon USB disconnection or manual reset button.
- **Grid / Large Icons View Mode (תצוגת גריד ואייקונים גדולים):** Added ViewMode (Details vs Tiles) allowing seamless switching between detailed column lists and 48x48 icon grids with ribbon toggle button and keyboard shortcut (Ctrl+Shift+V).
- **Quick USB Detection Modal (דיאלוג חיבור התקן USB מהיר):** Created QuickUsbDialog offering instant direct folder navigation upon removable media insertion.

### Testing (בדיקות)
- **Stage 7 Verification:** Added automated unit tests verifying audit logging, log parsing, log rotation and clearing, session reset events, kiosk exit protection logic, and view mode switching.

## [1.7.0] - Stage 5: Modernization, CI/CD, CommunityToolkit.Mvvm & Single-File Distribution

### Modernization & Architecture (מודרניזציה ותשתית)
- **.NET 8 (LTS) Upgrade (שדרוג סביבת הריצה ל-.NET 8):** Fully migrated project and test solutions to `net8.0-windows` leveraging modern C# 12 language features, enhanced runtime performance, and long-term support.
- **Centralized Build & Nullable Context (ניהול גרסה מרכזי ובטיחות Nullable):** Configured `Directory.Build.props` managing central project metadata, versioning (1.7.0), LangVersion 12.0, ImplicitUsings, and enabled `<Nullable>enable</Nullable>` across the codebase.
- **CommunityToolkit.Mvvm Architecture (אימוץ ארכיטקטורת MVVM מודרנית):** Integrated official `CommunityToolkit.Mvvm` (8.3.2) foundation. Base `ViewModel` now inherits directly from `ObservableObject` with full backward compatibility for `SetValue` / `SetProperty`.
- **Automated CI/CD Workflow (אוטומציית GitHub Actions):** Added `.github/workflows/ci.yml` providing automated dependency restore, solution building, unit test execution, and single-file packaging on every push and pull request.
- **Self-Contained Single-File Release (הפצה כקובץ הפעלה בודד ועצמאי):** Created automated build profile and `scripts/build-standalone.ps1` to produce a completely self-contained, single-file executable (`publish-standalone/LiteExplorer.exe`) for Windows x64 that runs on kiosk workstations without requiring any pre-installed .NET runtimes.

### Testing (בדיקות)
- **Stage 5 Verification:** Added automated unit tests verifying runtime environment version (>= 8.0), `ObservableObject` inheritance, property change notifications, and zero build/test regressions.

## [1.6.0] - Stage 4: UX, Navigation, Search, Column Sorting, Auto Refresh & Quick Preview

### Features & UI (תכונות וממשק משתמש)
- **Interactive Breadcrumbs (סרגל פירורי לחם אינטראקטיבי):** Added breadcrumb navigation with clickable directory segments to quickly jump to parent folders, along with smooth toggle between breadcrumb chips and direct text editing.
- **Instant Search & Filter (חיפוש וסינון מהיר):** Implemented in-folder filtering by file name or extension with instant responsive filtering via CollectionViewSource and quick clear button.
- **Multi-Column Sorting (מיון לפי עמודות):** Transformed ListView into a modern GridView supporting column header clicks for Name, Date Modified, Type, and Size with ascending/descending toggle and custom directory-first ordering (FileSystemObjectComparer).
- **Real-Time Auto Refresh (רענון אוטומטי בזמן אמת):** Integrated debounced FileSystemWatcher to detect file system creations, modifications, and deletions in real-time, plus Windows WM_DEVICECHANGE detection via DeviceNotificationService to automatically refresh connected drives and safely handle unplugged USB devices.
- **Sandboxed Quick Preview Pane (חלונית תצוגה מקדימה מהירה ובטוחה):** Added toggleable preview side-pane (toolbar button + Alt+P) supporting text files (.txt, .csv, .json, etc.), image files (.png, .jpg, .bmp, etc.) with frozen memory bitmaps, and rich document metadata cards without invoking external applications.
- **Keyboard Shortcuts (קיצורי מקלדת תקניים):** Added and unified shortcuts for F5 (refresh), F2 (rename), Delete (delete), Alt+P (toggle preview), and Enter/Backspace for navigation.

### Testing (בדיקות)
- **Stage 4 Verification:** Added automated unit tests verifying breadcrumb path generation, collection filtering, multi-column sorting comparisons, device notification events, and preview model integrity.

## [1.6.0] - Stage 3: Admin Profiles & Policy Configuration Engine

### Features & Security (תכונות חדשות ואבטחה)
- **Kiosk Operational Profiles (פרופילי עמדה מובנים):** Introduced built-in presets: Standard Documents Kiosk, Read-Only Kiosk (blocks writing, deleting, renaming, and folder creation), Controlled Workstation (restricts operations to explicitly allowed directories), and Custom profiles.
- **Granular Action Permissions (מודל הרשאות פעולה מפורט):** Added granular permission flags (`CanList`, `CanOpen`, `CanCopyFrom`, `CanCopyTo`, `CanRename`, `CanDelete`, `CanCreateDirectory`) enforced throughout `PathPolicy` and reflected dynamically in the UI command bar (`CanExecute`).
- **Protected Configuration Service (שירות תצורה מוגן ב-ProgramData):** Implemented `IConfigurationService` managing JSON configuration under `%ProgramData%\ZerachExplorer\config.json` with fallback handling and secure ACL preservation for kiosk environments.
- **PBKDF2 Password Security (אבטחת סיסמה בהצפנת תקן):** Added `SecurityHelper` utilizing PBKDF2 (SHA-256 with 10,000 iterations and cryptographic salts) to secure administrator access.
- **Admin Password Authentication Dialog (דיאלוג אימות סיסמת מנהל):** Created modern modal password prompt protecting administrative operations.
- **Admin Settings Management Window (מסך ניהול והגדרות מנהל):** Added full-featured administration dialog accessible via toolbar and keyboard shortcut (`Ctrl+Shift+A`) allowing real-time editing of active profiles, permissions, allowed file extensions, explicit directories, and password changes.

### Testing (בדיקות)
- **Stage 3 Verification:** Added unit tests verifying password hashing and verification, configuration JSON persistence and round-trip, profile policy enforcement (ReadOnly, Controlled, Custom), and dynamic policy application.

## [1.5.1] - Tabs UX & Color Palette Improvements

### Features & UI (תכונות וממשק משתמש)
- **Tab Close Button (איקס לסגירת כרטיסיות):** Added a close ('X') button to explorer tabs matching modern browser and Windows Explorer behaviors with proper RTL alignment, hover cursor, and graceful exit upon closing the last active tab.
- **Color Palette Brightening (הבהרת גווני כתום):** Brightened the dark rust-orange warning and danger palette in PercentToColor converter to clear, vibrant tones (RGB(255, 140, 20) and RGB(255, 185, 60)).

## [1.5.0] - Stage 2: File Operations & Device Management

### Features (תכונות חדשות)
- **File Copy & Paste (העתקה והדבקה):** Added clipboard copy and paste (Ctrl+C / Ctrl+V) with double `PathPolicy` security validation across both source and destination paths.
- **File Cut & Move (גזירה והעברה):** Added clipboard cut and move (Ctrl+X / Ctrl+V) with permission checks for reading, destination writing, and source deletion.
- **Multi-Item Selection (בחירה מרובה):** Extended selection support allowing batch copy, cut, and deletion across multiple selected items.
- **New Folder Creation (יצירת תיקייה חדשה):** Added toolbar button, context menu item, hotkey (Ctrl+Shift+N), and modal input dialog with validation against reserved DOS names and illegal characters.
- **Conflict Resolution Dialog (פתרון התנגשויות קבצים):** Added interactive resolution dialog (Replace, Skip, Keep Both / Auto-rename) with batch "Apply to all" support.
- **Safe USB Ejection (הוצאה בטוחה של התקני USB):** Implemented `DriveEjectService` using Windows Storage IOCTLs to lock, dismount, and safely eject removable media with user notifications in Hebrew.

### Testing (בדיקות)
- **Stage 2 Verification:** Added unit tests covering copy and move policies, directory creation checks, device ejection restrictions, and conflict resolution models.
