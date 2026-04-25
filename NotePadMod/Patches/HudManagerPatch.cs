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

    // Whether we are currently in a meeting — used to manage button parenting.
    private static bool _inMeeting = false;

    // The HUD parent we reparent to during meetings so the button stays visible
    // even when TOU:M deactivates the row GameObjects.
    private static Transform? _hudRoot = null;

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

        // During a meeting the button is intentionally parented to _hudRoot,
        // so don't treat that as stale.
        if (_inMeeting) return false;

        var expectedParent = _currentRow == NotepadButtonRow.TopRow
            ? HudManagerPatches.UiTopRight?.transform
            : HudManagerPatches.ExtraUiTopRight?.transform;

        return expectedParent == null ||
               NotePadButtonObj!.transform.parent != expectedParent;
    }

    public static void CreateOrUpdateNotePadButton(HudManager instance)
    {
        if (HudManagerPatches.UiTopRight == null || HudManagerPatches.ExtraUiTopRight == null)
            return;

        // Cache the HUD root once — it's the parent of the row containers.
        if (_hudRoot == null)
            _hudRoot = HudManagerPatches.UiTopRight.transform.parent;

        if (IsButtonStale())
            InvalidateButton();

        var desiredRow = NotePadPlugin.Settings.ButtonRow.Value;

        // ── First-time creation ───────────────────────────────────────────────
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
                inactive.localPosition = new Vector3(0f, 0.021f, -0.1f);
            }
            if (active != null && _activeSprite != null)
            {
                active.GetComponent<SpriteRenderer>().sprite = _activeSprite;
                active.localPosition = new Vector3(0f, 0.021f, -0.1f);
            }
        }

        // ── Meeting visibility ────────────────────────────────────────────────
        // During meetings TOU:M deactivates the row GameObjects, making anything
        // parented inside them invisible even if SetActive(true) is called on
        // the button itself.  Fix: reparent directly to the HUD root so it is
        // always in an active hierarchy.
        if (_inMeeting)
        {
            if (_hudRoot != null && NotePadButtonObj!.transform.parent != _hudRoot)
            {
                NotePadButtonObj.transform.SetParent(_hudRoot, true);
            }
            NotePadButtonObj!.SetActive(true);
            return; // skip normal row-placement logic during meetings
        }

        // ── Row placement (outside meetings) ──────────────────────────────────
        if (_currentRow != desiredRow)
        {
            Transform targetParent = desiredRow == NotepadButtonRow.TopRow
                ? HudManagerPatches.UiTopRight.transform
                : HudManagerPatches.ExtraUiTopRight.transform;

            NotePadButtonObj!.transform.SetParent(targetParent, false);
            NotePadButtonObj.transform.localPosition = Vector3.zero;

            // Both rows use GridArrange with StartAlign.Right.
            // Right-aligned grids lay out children left-to-right from first→last sibling,
            // so position in the row depends on sibling index:
            //   TopRow:    we want the notepad at the FAR LEFT  → SetAsLastSibling
            //              (last = pushed furthest left in a right-anchored layout)
            //   SecondRow: we want the notepad at the FAR RIGHT → SetAsFirstSibling
            //              (first = closest to the right anchor)
            if (desiredRow == NotepadButtonRow.TopRow)
                NotePadButtonObj.transform.SetAsLastSibling();
            else
                NotePadButtonObj.transform.SetAsFirstSibling();

            _currentRow = desiredRow;

            HudManagerPatches.UiGrid?.ArrangeChilds();
            HudManagerPatches.ExtraUiGrid?.ArrangeChilds();
        }

        NotePadButtonObj!.SetActive(true);
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
        _inMeeting = false;
        InvalidateButton();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingHudStartPatch()
    {
        // Mark that we are in a meeting so Update logic reparents the button
        // to the HUD root instead of the (now-hidden) row containers.
        _inMeeting = true;
        // Don't invalidate — we keep the button object and just reparent it.
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    [HarmonyPostfix]
    public static void MeetingHudClosePatch()
    {
        // Meeting is ending — force a full re-parent back into the correct row
        // on the next Update tick by invalidating _currentRow only (keeps the
        // button object alive to avoid a flash of missing button).
        _inMeeting = false;
        _currentRow = (NotepadButtonRow)(-1);
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
}