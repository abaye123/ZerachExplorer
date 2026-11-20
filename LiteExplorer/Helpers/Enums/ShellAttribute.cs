using System;

namespace LiteExplorer.Helpers.Enums;

[Flags]
public enum ShellAttribute : uint
{
    LargeIcon = 0x00000000,
    SmallIcon = 0x00000001,
    OpenIcon = 0x00000002,
    ShellIconSize = 0x00000004,
    Pidl = 0x00000008,
    UseFileAttributes = 0x00000010,
    AddOverlays = 0x00000020,
    OverlayIndex = 0x00000040,
    Others = 0x00000080,
    Icon = 0x00000100,
    DisplayName = 0x00000200,
    TypeName = 0x00000400,
    Attributes = 0x00000800,
    IconLocation = 0x00001000,
    ExeType = 0x00002000,
    SystemIconIndex = 0x00004000,
    LinkOverlay = 0x00008000,
    Selected = 0x00010000,
    AttributeSpecified = 0x00020000
}
