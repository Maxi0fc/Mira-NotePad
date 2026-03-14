using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using NotePadMod.UI;

namespace NotePadMod;

[BepInPlugin("maxi.notepad", "Notepad", "1.0.0")]
[BepInDependency("gg.reactor.api")]
[BepInDependency("mira.api")]
public class NotePadPlugin : BasePlugin
{
    public override void Load()
    {
        NotePadWindow.InitConfig(Config);
        new Harmony("maxi.notepad").PatchAll();
        Log.LogInfo("[NotepadPlugin] Loaded!");
    }
}
