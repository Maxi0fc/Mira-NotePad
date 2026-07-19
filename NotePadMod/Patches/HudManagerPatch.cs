using HarmonyLib;
using NotePadMod.UI;
using TownOfUs.Patches;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Reflection;

namespace NotePadMod.Patches;

[HarmonyPatch]
public static class HudManagerPatch
{
    public static GameObject? NotePadButtonObj;
    private static NotepadButtonRow _currentRow = (NotepadButtonRow)(-1);
    private static Sprite? _inactiveSprite;
    private static Sprite? _activeSprite;

    private static Sprite? LoadEmbeddedSprite(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(tex, bytes);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 115f);
    }

    public static void InvalidateButton()
    {
        if (NotePadButtonObj)
            Object.Destroy(NotePadButtonObj);
        NotePadButtonObj = null;
        _currentRow = (NotepadButtonRow)(-1);
    }

    private static bool IsButtonStale()
    {
        if (!NotePadButtonObj) return true;

        var expectedParent = _currentRow == NotepadButtonRow.TopRow
            ? HudManagerPatches.UiTopRight?.transform
            : HudManagerPatches.ExtraUiTopRight?.transform;

        return expectedParent == null || NotePadButtonObj!.transform.parent != expectedParent;
    }

    public static void CreateOrUpdateNotePadButton(HudManager instance)
    {
        if (HudManagerPatches.UiTopRight == null || HudManagerPatches.ExtraUiTopRight == null)
            return;

        if (IsButtonStale())
            InvalidateButton();

        var desiredRow = NotePadPlugin.Settings.ButtonRow.Value;

        // ── Create ────────────────────────────────────────────────────────────
        if (!NotePadButtonObj)
        {
            _currentRow = (NotepadButtonRow)(-1);

            NotePadButtonObj = Object.Instantiate(
                instance.MapButton.gameObject,
                HudManagerPatches.ExtraUiTopRight.transform
            );
            NotePadButtonObj.name = "NotePadButton";

            var btn = NotePadButtonObj.GetComponent<PassiveButton>();
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityAction)NotePadWindow.Toggle);

            var ap = NotePadButtonObj.GetComponentInChildren<AspectPosition>();
            if (ap != null) Object.Destroy(ap);

            if (_inactiveSprite == null)
                _inactiveSprite = LoadEmbeddedSprite("NotePadMod.Resources.notepad_inactive.png");
            if (_activeSprite == null)
                _activeSprite = LoadEmbeddedSprite("NotePadMod.Resources.notepad_active.png");

            var inactive = NotePadButtonObj.transform.Find("Inactive");
            var active   = NotePadButtonObj.transform.Find("Active");

            if (inactive != null && _inactiveSprite != null)
            {
                inactive.GetComponent<SpriteRenderer>().sprite = _inactiveSprite;
                inactive.localPosition = new Vector3(0f, 0.021f, -80f);
                NotePadButtonObj.transform.localPosition = new Vector3(0f, 0.021f, -80f);
            }
            if (active != null && _activeSprite != null)
            {
                active.GetComponent<SpriteRenderer>().sprite = _activeSprite;
                active.localPosition = new Vector3(0f, 0.021f, -80f);
                NotePadButtonObj.transform.localPosition = new Vector3(0f, 0.021f, -80f);
            }
        }

        // ── Row placement ─────────────────────────────────────────────────────
        if (_currentRow != desiredRow)
        {
            Transform targetParent = desiredRow == NotepadButtonRow.TopRow
                ? HudManagerPatches.UiTopRight.transform
                : HudManagerPatches.ExtraUiTopRight.transform;

            NotePadButtonObj!.transform.SetParent(targetParent, false);
            NotePadButtonObj.transform.localPosition = new Vector3(0f, 0.021f, -80f);

            if (desiredRow == NotepadButtonRow.TopRow)
                NotePadButtonObj.transform.SetAsLastSibling();
            else
                NotePadButtonObj.transform.SetAsFirstSibling();

            _currentRow = desiredRow;

            HudManagerPatches.UiGrid?.ArrangeChilds();
            HudManagerPatches.ExtraUiGrid?.ArrangeChilds();
        }

        // Hide during meeting/exile animations by matching the MapButton's visibility.
        // The game hides the top-right buttons during meeting intro and exile sequences;
        // we mirror that so the notepad doesn't stay visible when everything else is hidden.
        bool show = true;
        if (ExileController.Instance)
        {
            show = false;
        }
        else if (instance.MapButton != null && !instance.MapButton.gameObject.activeSelf)
        {
            show = false;
        }
        NotePadButtonObj!.SetActive(show);
    }

    // ── Harmony patches ───────────────────────────────────────────────────────

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePatch(HudManager __instance)
    {
        if (PlayerControl.LocalPlayer?.Data == null) return;
        CreateOrUpdateNotePadButton(__instance);
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    [HarmonyPostfix]
    public static void HudManagerStartPatch(HudManager __instance)
    {
        InvalidateButton();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingHudStartPatch(MeetingHud __instance)
    {
        if (NotePadButtonObj == null) return;
        NotePadButtonObj.SetActive(false);
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    [HarmonyPostfix]
    public static void MeetingHudClosePatch()
    {
        if (NotePadButtonObj == null) return;
        NotePadButtonObj.SetActive(false);
        NotePadButtonObj.transform.localPosition = new Vector3(0f, 0.021f, 1f);

    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    [HarmonyPostfix]
    public static void ChatUpdatePatch()
    {
        if (NotePadWindow.IsOpen)
            NotePadWindow.ForceToFront();
    }

    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    [HarmonyPrefix]
    public static bool KeyboardJoystickUpdatePatch() => !NotePadWindow.IsOpen;

    [HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
    [HarmonyPrefix]
    public static bool FollowerCameraUpdatePatch() => !NotePadWindow.IsOpen;

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    [HarmonyPrefix]
    public static bool LobbyBehaviourUpdatePatch() => !NotePadWindow.IsOpen;

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
    [HarmonyPrefix]
    public static bool PlayerPhysicsFixedUpdatePatch(PlayerPhysics __instance)
    {
        if (!NotePadWindow.IsOpen) return true;
        if (__instance.myPlayer != PlayerControl.LocalPlayer) return true;
        __instance.body.velocity = Vector2.zero;
        __instance.body.angularVelocity = 0f;
        return false;
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.WalkPlayerTo))]
    [HarmonyPrefix]
    public static bool WalkPlayerToPatch(PlayerPhysics __instance)
    {
        if (!NotePadWindow.IsOpen) return true;
        if (__instance.myPlayer != PlayerControl.LocalPlayer) return true;
        return false;
    }
}