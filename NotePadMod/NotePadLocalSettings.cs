using BepInEx.Configuration;
using MiraAPI.LocalSettings;
using MiraAPI.LocalSettings.Attributes;
using TownOfUs.Assets;
using UnityEngine;


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
    Black = 0,
    White = 1,
    Red = 2,
    Yellow = 3,
    Green = 4,
    Cyan = 5,
    Grey = 6,
}

/// <summary>
/// Visual skin of the notepad window background.
/// </summary>
public enum NotepadWindowSkin
{
    Grey = 0,
    Black = 1,
}
public sealed class NotePadLocalSettings(ConfigFile config) : LocalSettingsTab(config)
{
    public override string TabName => "Notepad";
    protected override bool ShouldCreateLabels => true;
    public static LoadableResourceAsset NotepadIcon { get; } = new("NotePadMod.Resources.notepad_logo.png");
    public override LocalSettingTabAppearance TabAppearance => new()
    {
        TabButtonHoverColor = Color.yellow,
        TabColor = Color.yellow,
        TabIcon = NotepadIcon
    };

    /// <summary>
    /// Which HUD row the Notepad button is placed in.
    /// Change takes effect after the HUD is next rebuilt (map/lobby transition).
    /// </summary>
    [LocalEnumSetting]
    public ConfigEntry<NotepadButtonRow> ButtonRow { get; private set; } =
        config.Bind(
            "Button",
            "Button Row",
            NotepadButtonRow.SecondRow);

    /// <summary>
    /// Color used for plain (non-role) notepad text.
    /// </summary>
    [LocalEnumSetting]
    public ConfigEntry<NotepadTextColor> TextColor { get; private set; } =
        config.Bind(
            "Appearance",
            "Text Color",
            NotepadTextColor.Black);

    /// <summary>
    /// Visual skin for the notepad window background sprite.
    /// </summary>
    [LocalEnumSetting]
    public ConfigEntry<NotepadWindowSkin> WindowSkin { get; private set; } =
        config.Bind(
            "Appearance",
            "Window Skin",
            NotepadWindowSkin.Grey);
}