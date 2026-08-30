using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor.Utilities;
using MiraAPI.PluginLoading;

namespace NotePadMod;

[BepInPlugin("maxi.notepad", "Notepad", "1.4.0")]
[BepInDependency("gg.reactor.api")]
[BepInDependency("mira.api")]
public class NotePadPlugin : BasePlugin, IMiraPlugin
{
    public static NotePadPlugin Instance { get; private set; } = null!;

    public static NotePadLocalSettings Settings { get; private set; } = null!;
    public string OptionsTitleText => "Notepad";
    public ConfigFile GetConfigFile() => Config;

    public override void Load()
    {
        Instance = this;
        Settings = new NotePadLocalSettings(Config);
        ReactorCredits.Register("NotePad", "1.4.0", false, ReactorCredits.AlwaysShow);

        new Harmony("maxi.notepad").PatchAll();
        Log.LogInfo("[NotepadPlugin] Loaded!");
    }
}