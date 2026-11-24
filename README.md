# ZerachExplorer | זרח אקספלורר

[עברית](#עברית) | [English](#english)

## עברית

<div dir="rtl">

סייר קבצים מודרני ומוגן לעמדות מחשב ציבוריות, בעיצוב ובהתנהגות של סייר Windows 11.

### הורדה

- [מתקין](https://github.com/abaye123/ZerachExplorer/releases/latest/download/ZerachExplorer_setup.exe) - מומלץ לעמדות ציבוריות, משדרג גם גרסאות קודמות
- [קובץ נייד](https://github.com/abaye123/ZerachExplorer/releases/latest/download/ZerachExplorer_portable.exe) - ללא התקנה
- [כל הגרסאות](https://github.com/abaye123/ZerachExplorer/releases)

Windows 10 ומעלה, 64 סיביות. אין צורך להתקין ‎.NET.

### תכונות

**הגנה**

- אין גישה לכונן המערכת, לתיקיות ולקבצים מוסתרים ולכונני "אוצר החכמה".
- נפתחים ומועתקים רק קבצים מהסוגים המאושרים.
- הרשאות נפרדות לעיון, פתיחה, העתקה, הדבקה, שינוי שם, מחיקה ויצירת תיקיות.
- חסימת כוננים, תיקיות מאושרות, מצב קיוסק ויציאה בסיסמה.
- ההגדרות מוגנות בסיסמה ובאישור מנהל Windows, ויש יומן פעולות.

**קבצים**

- העתקה, גזירה והדבקה, גם מול סייר Windows, עם חלון התקדמות, קצב, השהיה וביטול.
- טיפול בהתנגשויות, סל מיחזור, תיקיות חדשות ושינוי שם (ללא שינוי סיומת).
- הוצאה בטוחה של USB.

**ממשק**

- כרטיסיות, היסטוריה, שורת כתובת, חיפוש, מיון, תצוגת פרטים וסמלים.
- בחירה במלבן ובתיבות סימון, ותצוגה מקדימה.
- ייבוא וייצוא הגדרות להגדרה מהירה של מחשבים רבים.

### מדריך

[התקנה, הגדרה ושימוש](docs/guide.he.md) | [אבטחה והקשחת העמדה](SECURITY.md) | [יומן שינויים](CHANGELOG.md)

### קרדיט

- [המלאך (Amlaach)](https://github.com/Amlaach) פיתח את רוב החידושים בגרסה 2.0.0.
- התוכנה מבוססת על [LiteExplorer](https://github.com/tempous/LiteExplorer) מאת tempous.

### רישיון

[BSD 3-Clause](LICENSE)

</div>

---

## English

A modern, protected file explorer for public computer stations, with the look and behavior of the Windows 11 file explorer.

### Download

- [Installer](https://github.com/abaye123/ZerachExplorer/releases/latest/download/ZerachExplorer_setup.exe) - recommended for public stations, also upgrades earlier versions
- [Portable file](https://github.com/abaye123/ZerachExplorer/releases/latest/download/ZerachExplorer_portable.exe) - no installation
- [All releases](https://github.com/abaye123/ZerachExplorer/releases)

64-bit Windows 10 or later. .NET does not need to be installed.

### Features

**Protection**

- No access to the system drive, hidden files and folders, or "Otzar HaHochma" drives.
- Only approved file types can be opened and copied.
- Separate permissions for browsing, opening, copying, pasting, renaming, deleting and creating folders.
- Drive blocking, approved folders, kiosk mode and password-protected exit.
- Settings protected by a password and Windows administrator approval, and an audit log.

**Files**

- Copy, cut and paste, also with Windows Explorer, with progress, speed, pause and cancel.
- Conflict handling, recycle bin, new folders and renaming (the extension is kept).
- Safe USB eject.

**Interface**

- Tabs, history, address bar, search, sorting, details and large icons.
- Rectangle and check box selection, and a preview pane.
- Import and export of settings for quick setup of many computers.

### Guide

[Installation, setup and usage](docs/guide.en.md) | [Security and station hardening](SECURITY.md) | [Changelog](CHANGELOG.md)

### Credits

- [Amlaach](https://github.com/Amlaach) developed most of version 2.0.0.
- Based on [LiteExplorer](https://github.com/tempous/LiteExplorer) by tempous.

### License

[BSD 3-Clause](LICENSE)

### Building

Requires the .NET 10 SDK, and Inno Setup 6 for the installer.

```
dotnet test ZerachExplorer.sln
powershell -File scripts/build-release.ps1
```

A release is published by changing the version in `Directory.Build.props` on `main`, with notes in `release-notes`.
