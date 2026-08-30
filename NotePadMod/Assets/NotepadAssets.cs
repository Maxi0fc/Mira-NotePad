using System.Reflection;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using UnityEngine;

namespace NotePadMod.Assets;

/// <summary>
/// Loads the notepad's Unity prefab and HUD-button sprites from the embedded
/// AssetBundle at Resources/notepad.bundle.
///
/// That bundle is a trimmed copy of LaunchpadReloaded's
/// "launchpad-assets-win.bundle" — stripped down (via UnityPy) to just the
/// "Notepad" prefab and the two "NotepadButton"/"NotepadButtonActive"
/// sprites, so this mod ships only what it actually uses.
/// </summary>
public static class NotepadAssets
{
    private static AssetBundle? _bundle;

    /// <summary>
    /// The loaded AssetBundle, lazily loaded on first access.
    /// Reactor's AssetBundleManager looks for an embedded resource whose name
    /// ends with "notepad.bundle" (see Resources/notepad.bundle in this
    /// project, added as an EmbeddedResource in the .csproj).
    /// </summary>
    private static AssetBundle Bundle => AssetBundleManager.Load("notepad");

    /// <summary>The root "Notepad" GameObject (Background, CloseButton, Title, Textbox, Lines).</summary>
    public static LoadableAsset<GameObject> Notepad { get; } = new LoadableBundleAsset<GameObject>("Notepad", Bundle);

    /// <summary>The HUD toolbar button's inactive-state sprite.</summary>
    public static LoadableAsset<Sprite> NotepadButtonSprite { get; } = new LoadableBundleAsset<Sprite>("NotepadButton", Bundle);

    /// <summary>The HUD toolbar button's active/pressed-state sprite.</summary>
    public static LoadableAsset<Sprite> NotepadButtonActiveSprite { get; } = new LoadableBundleAsset<Sprite>("NotepadButtonActive", Bundle);
}
