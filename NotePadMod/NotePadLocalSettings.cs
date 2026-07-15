using BepInEx.Configuration;
using MiraAPI.LocalSettings;
using MiraAPI.LocalSettings.Attributes;
using MiraAPI.Utilities.Assets;
using UnityEngine;


namespace NotePadMod;

public enum NotepadButtonRow
{
    TopRow = 0,
    SecondRow = 1,
}

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

    [LocalEnumSetting]
    public ConfigEntry<NotepadButtonRow> ButtonRow { get; private set; } =
        config.Bind(
            "Button",
            "Button Row",
            NotepadButtonRow.SecondRow);

    [LocalEnumSetting]
    public ConfigEntry<NotepadTextColor> TextColor { get; private set; } =
        config.Bind(
            "Appearance",
            "Text Color",
            NotepadTextColor.White);

    [LocalEnumSetting]
    public ConfigEntry<NotepadWindowSkin> WindowSkin { get; private set; } =
        config.Bind(
            "Appearance",
            "Window Skin",
            NotepadWindowSkin.Grey);

    [LocalToggleSetting]
    public ConfigEntry<bool> AutoAddRoleInfo { get; private set; } =
        config.Bind(
            "Behavior",
            "Auto-Add Role Info",
            true);
}