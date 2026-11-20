# Changelog

All notable changes to ZerachExplorer will be documented in this file.

## [1.7.0] - Stage 7: Kiosk Lockdown, Session Privacy, Audit Logging & Grid/Tile Views

### Features & Security (������ ����� ������)
- **Kiosk Lockdown & Exit Interception (��� ����� ���� ����� �����):** Added KioskController managing borderless fullscreen mode and intercepting application exit requests (Closing event), requiring administrator password authentication when active.
- **Audit & Activity Logging (���� ������ ����� ������):** Implemented AuditService with secure auto-rotating log storage (%ProgramData%\ZerachExplorer\audit.log) tracking file opens, copy/move/rename/delete operations, drive connections/ejections, and security violations.
- **Session Reset & Privacy Wipe (����� ����� ����� ������):** Added SessionService to automatically clean temporary files, wipe OS and internal clipboards, and return navigation to home upon USB disconnection or manual reset button.
- **Grid / Large Icons View Mode (����� ��� ��������� �������):** Added ViewMode (Details vs Tiles) allowing seamless switching between detailed column lists and 48x48 icon grids with ribbon toggle button and keyboard shortcut (Ctrl+Shift+V).
- **Quick USB Detection Modal (���� ����� ���� �-USB):** Created QuickUsbDialog offering instant direct folder navigation upon removable media insertion.

### Testing (������)
- **Stage 7 Verification:** Added automated unit tests verifying audit logging, log parsing, log rotation and clearing, session reset events, kiosk exit protection logic, and view mode switching (91 total passing tests).

## [1.6.0] - Stage 4: UX, Navigation, Search, Column Sorting, Auto Refresh & Quick Preview

### Features & UI (תכונות וממשק משתמש)
- **Interactive Breadcrumbs (סרגל פירורי לחם אינטראקטיבי):** Added breadcrumb navigation with clickable directory segments to quickly jump to parent folders, along with smooth toggle between breadcrumb chips and direct text editing.
- **Instant Search & Filter (חיפוש וסינון מהיר):** Implemented in-folder filtering by file name or extension with instant responsive filtering via CollectionViewSource and quick clear button.
- **Multi-Column Sorting (מיון לפי עמודות):** Transformed ListView into a modern GridView supporting column header clicks for Name, Date Modified, Type, and Size with ascending/descending toggle and custom directory-first ordering (FileSystemObjectComparer).
- **Real-Time Auto Refresh (רענון אוטומטי בזמן אמת):** Integrated debounced FileSystemWatcher to detect file system creations, modifications, and deletions in real-time, plus Windows WM_DEVICECHANGE detection via DeviceNotificationService to automatically refresh connected drives and safely handle unplugged USB devices.
- **Sandboxed Quick Preview Pane (חלונית תצוגה מקדימה מהירה ובטוחה):** Added toggleable preview side-pane (toolbar button + Alt+P) supporting text files (.txt, .csv, .json, etc.), image files (.png, .jpg, .bmp, etc.) with frozen memory bitmaps, and rich document metadata cards without invoking external applications.
- **Keyboard Shortcuts (קיצורי מקלדת תקניים):** Added and unified shortcuts for F5 (refresh), F2 (rename), Delete (delete), Alt+P (toggle preview), and Enter/Backspace for navigation.

### Testing (בדיקות)
- **Stage 4 Verification:** Added automated unit tests verifying breadcrumb path generation, collection filtering, multi-column sorting comparisons, device notification events, and preview model integrity (75 total passing tests).

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
