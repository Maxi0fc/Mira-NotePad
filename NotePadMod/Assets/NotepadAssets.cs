using BepInEx.Logging;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using UnityEngine;

namespace NotePadMod.Assets;

public static class NotepadAssets
{
    private static readonly ManualLogSource DebugLog = BepInEx.Logging.Logger.CreateLogSource("NotepadAssets");
    private static readonly bool LoggedResources = LogResources();

    private static bool LogResources()
    {
        DebugLog.LogInfo($"NotepadAssets assembly: {typeof(NotepadAssets).Assembly.FullName}");
        DebugLog.LogInfo($"NotepadAssets assembly location: {typeof(NotepadAssets).Assembly.Location}");
        foreach (var n in typeof(NotepadAssets).Assembly.GetManifestResourceNames())
        {
            DebugLog.LogInfo($"NotepadAssets resource: {n}");
        }
        return true;
    }

    public static readonly AssetBundle Bundle = AssetBundleManager.Load("notepad-mod");
    public static readonly LoadableAsset<GameObject> Notepad = new LoadableBundleAsset<GameObject>("Notepad", Bundle);
    public static readonly LoadableAsset<Sprite> NotepadButtonSprite = new LoadableBundleAsset<Sprite>("NotepadButton", Bundle);
    public static readonly LoadableAsset<Sprite> NotepadButtonActiveSprite = new LoadableBundleAsset<Sprite>("NotepadButtonActive", Bundle);
    public static readonly LoadableAsset<Sprite> JotButtonSprite = new LoadableResourceAsset("NotePadMod.Resources.JotButton.png");

}
