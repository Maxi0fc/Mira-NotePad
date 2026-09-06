using System;
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

    private const string BundleName = "notepad-mod";

    public static readonly AssetBundle Bundle = LoadBundle();

    private static AssetBundle LoadBundle()
    {
        try
        {
            return AssetBundleManager.Load(BundleName);
        }
        catch (Exception ex)
        {
            DebugLog.LogError($"Unable to load {BundleName} asset bundle: {ex}");
            return null!;
        }
    }
    public static readonly LoadableAsset<GameObject> Notepad = new LoadableBundleAsset<GameObject>("Notepad", Bundle);
    public static readonly LoadableAsset<Sprite> NotepadButtonSprite = new LoadableBundleAsset<Sprite>("NotepadButton", Bundle);
    public static readonly LoadableAsset<Sprite> NotepadButtonActiveSprite = new LoadableBundleAsset<Sprite>("NotepadButtonActive", Bundle);
    public static readonly LoadableAsset<Sprite> JotButtonSprite = new LoadableResourceAsset("NotePadMod.Resources.JotButton.png");

}
