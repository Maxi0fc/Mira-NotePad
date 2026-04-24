using HarmonyLib;
using MiraAPI.LocalSettings;
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
<<<<<<< Updated upstream
    public static AspectPosition? NotePadAspectPos;
=======

    // Track which row we're currently parented into so we can reparent when the
    // setting changes without waiting for a full HUD rebuild.
    private static NotepadButtonRow _currentRow = (NotepadButtonRow)(-1);

>>>>>>> Stashed changes
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
            NotePadButtonObj!.transform.SetParent(null, false);

        NotePadButtonObj = null;
        _currentRow = (NotepadButtonRow)(-1);
    }

    private static bool IsButtonStale()
    {
        if (!NotePadButtonObj) return true;

        var expectedParent = _currentRow == NotepadButtonRow.TopRow
            ? HudManagerPatches.UiTopRight?.transform
            : HudManagerPatches.ExtraUiTopRight?.transform;

        return expectedParent == null ||
               NotePadButtonObj!.transform.parent != expectedParent;
    }

    public static void CreateOrUpdateNotePadButton(HudManager instance)
>>>>>>> Stashed changes
    {
        // Both TOU:M grid rows must exist before we can do anything.
        if (HudManagerPatches.UiTopRight == null || HudManagerPatches.ExtraUiTopRight == null)
            return;

        if (IsButtonStale())
            InvalidateButton();

        var desiredRow = NotePadPlugin.Settings.ButtonRow.Value;

        // ── First-time creation ───────────────────────────────────────────────
        if (!NotePadButtonObj)
        {
<<<<<<< Updated upstream
=======
            _currentRow = (NotepadButtonRow)(-1); // force reparent after creation

>>>>>>> Stashed changes
            NotePadButtonObj = Object.Instantiate(
                instance.MapButton.gameObject,
                // Temporary parent — will be corrected below.
                HudManagerPatches.ExtraUiTopRight.transform
            );
            NotePadButtonObj.name = "NotePadButton";
<<<<<<< Updated upstream
            var btn = NotePadButtonObj.GetComponent<PassiveButton>();
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityAction)NotePadWindow.Toggle);
            NotePadButtonObj.transform.Find("Background").localPosition = Vector3.zero;
=======

            // Wire the click
            var btn = NotePadButtonObj.GetComponent<PassiveButton>();
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityAction)NotePadWindow.Toggle);

            // TOU:M grid buttons must NOT have their own AspectPosition —
            // the GridArrange component handles all placement.
            var ap = NotePadButtonObj.GetComponentInChildren<AspectPosition>();
            if (ap != null) UnityEngine.Object.Destroy(ap);

            // Sprites
>>>>>>> Stashed changes
            if (_inactiveSprite == null)
                _inactiveSprite = LoadEmbeddedSprite("NotePadMod.Resources.notepad_inactive.png");
            if (_activeSprite == null)
                _activeSprite = LoadEmbeddedSprite("NotePadMod.Resources.notepad_active.png");
<<<<<<< Updated upstream
            if (_inactiveSprite != null)
                NotePadButtonObj.transform.Find("Inactive").GetComponent<SpriteRenderer>().sprite = _inactiveSprite;
            if (_activeSprite != null)
                NotePadButtonObj.transform.Find("Active").GetComponent<SpriteRenderer>().sprite = _activeSprite;
            NotePadAspectPos = NotePadButtonObj.GetComponentInChildren<AspectPosition>();
        }
        if (NotePadButtonObj && NotePadAspectPos != null && HudManagerPatches.WikiAspectPos != null)
        {
            var dist = HudManagerPatches.WikiAspectPos.DistanceFromEdge;
            dist.x += 0.84f;
            NotePadAspectPos.DistanceFromEdge = dist;
            NotePadAspectPos.Alignment = HudManagerPatches.WikiAspectPos.Alignment;
            NotePadAspectPos.AdjustPosition();
        }
        if (NotePadButtonObj)
        {
            bool wikiVisible = HudManagerPatches.WikiButton != null && HudManagerPatches.WikiButton.activeSelf;
            NotePadButtonObj.SetActive(wikiVisible);
        }
    }
=======

            var inactive = NotePadButtonObj.transform.Find("Inactive");
            var active   = NotePadButtonObj.transform.Find("Active");

            if (inactive != null && _inactiveSprite != null)
            {
                inactive.GetComponent<SpriteRenderer>().sprite = _inactiveSprite;
            }
            if (active != null && _activeSprite != null)
            {
                active.GetComponent<SpriteRenderer>().sprite = _activeSprite;
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
            NotePadButtonObj.transform.SetAsFirstSibling();
            _currentRow = desiredRow;

            // Let TOU:M re-arrange whichever row we just modified.
            HudManagerPatches.UiGrid?.ArrangeChilds();
            HudManagerPatches.ExtraUiGrid?.ArrangeChilds();
        }

        NotePadButtonObj!.SetActive(true);
    }

    // ── Harmony patches ───────────────────────────────────────────────────────

>>>>>>> Stashed changes
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePatch(HudManager __instance)
    {
        if (PlayerControl.LocalPlayer?.Data == null) return;
        CreateOrUpdateNotePadButton(__instance);
    }

    /// <summary>
    /// When TOU:M destroys / rebuilds the HUD rows (e.g. scene transitions) our
    /// cached button reference becomes stale.  Nulling it out here forces a clean
    /// recreation on the next Update tick.
    /// </summary>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    [HarmonyPostfix]
    public static void HudManagerStartPatch(HudManager __instance)
    {
        NotePadButtonObj!.SetActive(true);
    }

    /// <summary>
    /// On meeting start TOU:M rebuilds the HUD rows, staling our button.
    /// Invalidate so it gets cleanly re-parented. We do NOT invalidate on
    /// MeetingHud.Close — the button survives the meeting fine and the next
    /// Update tick keeps it visible.
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingHudStartPatch()
    {
        InvalidateButton();
    }
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    [HarmonyPostfix]
    public static void ChatUpdatePatch()
    {
        if (NotePadWindow.IsOpen)
            NotePadWindow.ForceToFront();
    }
<<<<<<< Updated upstream
    // Blockera tangentbordsrörelse
    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    [HarmonyPrefix]
    public static bool KeyboardJoystickUpdatePatch()
    {
        return !NotePadWindow.IsOpen;
    }
    // Blockera zoom via FollowerCamera
    [HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
    [HarmonyPrefix]
    public static bool FollowerCameraUpdatePatch()
    {
        return !NotePadWindow.IsOpen;
    }
    // Blockera lobbyn
=======

    // Block keyboard input reaching PlayerPhysics while notepad is open
    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    [HarmonyPrefix]
    public static bool KeyboardJoystickUpdatePatch() => !NotePadWindow.IsOpen;

    // Block camera zoom while notepad is open
    [HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
    [HarmonyPrefix]
    public static bool FollowerCameraUpdatePatch() => !NotePadWindow.IsOpen;

    // Block lobby Update while notepad is open
>>>>>>> Stashed changes
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    [HarmonyPrefix]
    public static bool LobbyBehaviourUpdatePatch() => !NotePadWindow.IsOpen;
}