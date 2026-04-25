using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.PluginLoading;

namespace NotePadMod;

[BepInPlugin("maxi.notepad", "Notepad", "1.0.0")]
[BepInDependency("gg.reactor.api")]
[BepInDependency("mira.api")]
public class NotePadPlugin : BasePlugin, IMiraPlugin
{
    public static NotePadPlugin Instance { get; private set; } = null!;

    /// <summary>
    /// Direct reference to the settings tab, safe to use at any time.
    /// </summary>
    public static NotePadLocalSettings Settings { get; private set; } = null!;

    // IMiraPlugin — confirmed from TownOfUsPlugin.cs
    public string OptionsTitleText => "Notepad";
    public ConfigFile GetConfigFile() => Config;

    public override void Load()
    {
        Instance = this;
        Settings = new NotePadLocalSettings(Config);

        new Harmony("maxi.notepad").PatchAll();
        Log.LogInfo("[NotepadPlugin] Loaded!");
    }
}