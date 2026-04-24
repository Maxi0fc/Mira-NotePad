using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.LocalSettings;

namespace NotePadMod;

[BepInPlugin("maxi.notepad", "Notepad", "1.2.0")]
[BepInDependency("gg.reactor.api")]
[BepInDependency("mira.api")]
public class NotePadPlugin : BasePlugin
{
    public override void Load()
    {
        // Register the MiraAPI local-settings tab so it appears in the in-game
        // settings UI alongside TOU:M's own tabs.
        _ = new NotePadLocalSettings(Config);

        new Harmony("maxi.notepad").PatchAll();
        Log.LogInfo("[NotepadPlugin] Loaded!");
    }
}
