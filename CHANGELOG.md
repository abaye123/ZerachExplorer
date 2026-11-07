# Changelog

All notable changes to ZerachExplorer will be documented in this file.

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
