using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI;
using MiraAPI.PluginLoading;
using NotePadMod.Compatibility;
using Reactor;
using Reactor.Utilities;

namespace NotePadMod;

[BepInPlugin("maxi.notepad", "Notepad", "1.4.1")]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency(MiraApiPlugin.Id)]
[BepInDependency("auavengers.tou.mira", BepInDependency.DependencyFlags.SoftDependency)]
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
        ReactorCredits.Register("NotePad", "1.4.1", false, ReactorCredits.AlwaysShow);

        var harmony = new Harmony("maxi.notepad");
        harmony.PatchAll();

        // Initialize soft dependency integration with Town of Us Mira if present
        TouIntegration.Initialize(harmony);

        Log.LogInfo("[NotepadPlugin] Loaded as MiraAPI mod!");
    }
}