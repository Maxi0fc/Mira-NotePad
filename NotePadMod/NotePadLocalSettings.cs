using BepInEx.Configuration;
using MiraAPI.LocalSettings;
using MiraAPI.LocalSettings.Attributes;
using MiraAPI.Utilities.Assets;
using TownOfUs.Assets;

namespace NotePadMod;

/// <summary>
/// Which HUD row the notepad button should live in.
/// </summary>
public enum NotepadButtonRow
{
    /// <summary>Top row — same row as Map, Settings, Chat.</summary>
    TopRow = 0,

    /// <summary>Second row — same row as Wiki and Zoom.</summary>
    SecondRow = 1,
}

/// <summary>
/// Color of the notepad text.
/// </summary>
public enum NotepadTextColor
{
    Black  = 0,
    White  = 1,
    Red    = 2,
    Yellow = 3,
    Green  = 4,
    Cyan   = 5,
    Grey   = 6,
}

/// <summary>
/// Visual skin of the notepad window background.
/// </summary>
public enum NotepadWindowSkin
{
    Grey  = 0,
    Black = 1,
}

public sealed class NotePadLocalSettings(ConfigFile config) : LocalSettingsTab(config)
{
    public override string TabName => "Mira Notepad";
    protected override bool ShouldCreateLabels => true;

    public override LocalSettingTabAppearance TabAppearance => new()
    {
        // LoadableResourceAsset is MiraAPI's built-in wrapper for embedded sprites.
        // Pass the short resource name without namespace prefix or file extension.
        TabIcon = TouRoleIcons.Ambassador
    };

    [LocalEnumSetting]
    public ConfigEntry<NotepadButtonRow> ButtonRow { get; private set; } =
        config.Bind(
            "Button",
            "Button Row",
            NotepadButtonRow.SecondRow,
            "Which HUD row to place the Notepad button in. TopRow = same row as Map/Settings/Chat. SecondRow = same row as Wiki/Zoom.");

    /// <summary>
    /// Color used for plain (non-role) notepad text.
    /// </summary>
    [LocalEnumSetting]
    public ConfigEntry<NotepadTextColor> TextColor { get; private set; } =
        config.Bind(
            "Appearance",
            "Text Color",
            NotepadTextColor.Black,
            "Color of the notepad text. Role names are always shown in their faction color regardless of this setting.");

    /// <summary>
    /// Visual skin for the notepad window background sprite.
    /// </summary>
    [LocalEnumSetting]
    public ConfigEntry<NotepadWindowSkin> WindowSkin { get; private set; } =
        config.Bind(
            "Appearance",
            "Window Skin",
            NotepadWindowSkin.Grey,
            "Background skin for the notepad window.");
}