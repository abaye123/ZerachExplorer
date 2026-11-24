# ZerachExplorer | זרח אקספלורר

זרח אקספלורר הוא סייר קבצים מודרני ומוגן עבור עמדות מחשבים ציבוריות.

## תכונות:

* אין גישה לכונן C, ולתיקיות וקבצים מוסתרים.  
* אין גישה לכוננים של תוכנת "אוצר החוכמה".  
* פתיחת והעתקת קבצים מהסוגים המאושרים בלבד.  
* סיומות הקבצים המאושרות הם: `doc`, `docx`, `xls`, `xlsx`, `pdf`, `txt`, `csv`, `one`, `onetoc2`  (וורד, אקסל, וואן נוט).  
* העתקה, גזירה, הדבקה, מחיקה, תיקייה חדשה ושינוי שם (ללא שינוי סיומת הקובץ).
* עיצוב בסגנון Windows 11, לשוניות, תצוגת אריחים, חיפוש, מיון ותצוגה מקדימה.
* הוצאה בטוחה של כונן USB.
* פרופילי עמדה, הרשאות, חסימת כוננים, מצב קיוסק ויומן פעולות.

## התקנה:

* `ZerachExplorer_setup_x.y.z.exe` - מתקין, מומלץ לעמדות ציבוריות. משדרג גם גרסאות קודמות.
* `ZerachExplorer_portable_x.y.z.exe` - קובץ אחד ללא התקנה.
* דרוש Windows 10 ומעלה (64 סיביות). אין צורך להתקין .NET.

## הגדרות מנהל וסיסמה:

* לפתיחת מסך הגדרות המנהל (ניהול פרופילי קיוסק, הרשאות וסיומות מאושרות): לחיצה על כפתור ההגדרות בסרגל או קיצור המקלדת `Ctrl+Shift+A`.
* **סיסמת מנהל:** אין סיסמת ברירת מחדל. בכניסה הראשונה להגדרות קובעים סיסמה, באישור מנהל Windows. כל שמירת הגדרות דורשת אישור כזה, והקובץ `%ProgramData%\ZerachExplorer\config.json` ניתן לשינוי רק על ידי מנהלים.

## קרדיט:

התוכנה הזו קיימת בזכות הפרויקט הנהדר הזה:  
https://github.com/tempous/LiteExplorer

רוב החידושים בגרסה 2.0.0 פותחו על ידי [המלאך (Amlaach)](https://github.com/Amlaach).

## רישיון:

קוד המקור של התוכנה זמין בכתובת:  
https://github.com/abaye123/ZerachExplorer  
תחת רישיון BSD 3-Clause License.

---

# ZerachExplorer

ZerachExplorer is a modern and secure file explorer designed for public computer stations.

## Features:

* No access to the C drive, hidden folders, or files.  
* No access to the "Otzar HaHochma" software drives.  
* Opening and copying files only of approved types.  
* Approved file extensions: `doc`, `docx`, `xls`, `xlsx`, `pdf`, `txt`, `csv`, `one`, `onetoc2` (Word, Excel, OneNote).  
* Copy, cut, paste, delete, new folder and rename (without changing the file extension).
* Windows 11 style interface with tabs, tiles, search, sorting and quick preview.
* Safe USB eject.
* Station profiles, permissions, drive blocking, kiosk mode and an audit log.

## Installation:

* `ZerachExplorer_setup_x.y.z.exe` - installer, recommended for public stations. Also upgrades earlier versions.
* `ZerachExplorer_portable_x.y.z.exe` - a single file that needs no installation.
* Requires 64-bit Windows 10 or later. .NET does not need to be installed.

## Admin Settings & Password:

* To open the Admin Settings window (kiosk profiles, granular permissions, and allowed extensions): click the Admin Settings button in the toolbar or press `Ctrl+Shift+A`.
* **Admin password:** there is no default password. It is set on the first visit to the settings and requires Windows administrator approval, as does every later save. `%ProgramData%\ZerachExplorer\config.json` is writable by administrators only.

## Credit:

This software exists thanks to this fantastic project:  
https://github.com/tempous/LiteExplorer

Most of version 2.0.0 was developed by [Amlaach](https://github.com/Amlaach).

## License:

The source code is available at:  
https://github.com/abaye123/ZerachExplorer  
under the BSD 3-Clause License.

## Building:

`dotnet test LiteExplorer.sln` runs the tests. `scripts/build-release.ps1` builds both packages into `dist` and needs Inno Setup 6. A release is published by changing the version in `Directory.Build.props` on `main`, with notes in `release-notes`.
