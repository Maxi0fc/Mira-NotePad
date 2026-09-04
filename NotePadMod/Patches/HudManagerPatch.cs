using System;
using System.Reflection;
using HarmonyLib;
using NotePadMod.Assets;
using NotePadMod.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NotePadMod.Patches;

[HarmonyPatch]
public static class HudManagerPatch
{
    public static GameObject? NotePadButtonObj;
    private static NotepadButtonRow _currentRow = (NotepadButtonRow)(-1);
    private static Sprite? _inactiveSprite;
    private static Sprite? _activeSprite;

    private static Type? _miraHudHelperType;
    private static FieldInfo? _uiTopRightField;
    private static FieldInfo? _extraUiTopRightField;
    private static FieldInfo? _uiGridField;
    private static FieldInfo? _extraUiGridField;
    private static bool _lookedUpHelper;

    public static void InvalidateButton()
    {
        if (NotePadButtonObj)
            UnityEngine.Object.Destroy(NotePadButtonObj);
        NotePadButtonObj = null;
        _currentRow = (NotepadButtonRow)(-1);
    }

    private static void EnsureHelperLookup()
    {
        if (_lookedUpHelper) return;
        _lookedUpHelper = true;

        _miraHudHelperType = AccessTools.TypeByName("MiraAPI.Hud.MiraHudHelper");

        if (_miraHudHelperType != null)
        {
            /*
             * These are plain public static FIELDS on MiraHudHelper,
             * not C# properties - GetProperty() here would always
             * silently return null, permanently forcing every row
             * lookup down to the fallback paths below regardless of
             * timing or patch order. Use GetField().
             */
            _uiTopRightField = _miraHudHelperType.GetField("UiTopRight", BindingFlags.Public | BindingFlags.Static);
            _extraUiTopRightField = _miraHudHelperType.GetField("ExtraUiTopRight", BindingFlags.Public | BindingFlags.Static);
            _uiGridField = _miraHudHelperType.GetField("UiGrid", BindingFlags.Public | BindingFlags.Static);
            _extraUiGridField = _miraHudHelperType.GetField("ExtraUiGrid", BindingFlags.Public | BindingFlags.Static);
        }
    }

    public static Transform? GetTopRightRow(NotepadButtonRow row)
    {
        EnsureHelperLookup();

        if (row == NotepadButtonRow.TopRow)
        {
            if (_uiTopRightField?.GetValue(null) is GameObject go && go != null)
                return go.transform;
        }
        else
        {
            if (_extraUiTopRightField?.GetValue(null) is GameObject go && go != null)
                return go.transform;
        }

        // Direct GameObject search - ExtraUiTopRight is a sibling of
        // UiTopRight (both live one level up, under UiTopRight's own
        // parent), not a direct child of HudManager, so search from
        // there instead of from HudManager.Instance.transform.
        if (HudManager.Instance != null && HudManager.Instance.MapButton != null)
        {
            var uiTopRight = HudManager.Instance.MapButton.transform.parent;

            if (row == NotepadButtonRow.TopRow)
                return uiTopRight;

            var searchRoot = uiTopRight != null ? uiTopRight.parent : HudManager.Instance.transform;
            if (searchRoot != null)
            {
                var child = searchRoot.Find("ExtraUiTopRight");
                if (child != null) return child;
            }

            // Last resort: no ExtraUiTopRight exists yet at all.
            return uiTopRight;
        }

        return HudManager.Instance != null ? HudManager.Instance.transform : null;
    }

    private static void ArrangeGrids()
    {
        try
        {
            if (_uiGridField?.GetValue(null) is GridArrange uiGrid && uiGrid != null)
                uiGrid.ArrangeChilds();
            if (_extraUiGridField?.GetValue(null) is GridArrange extraGrid && extraGrid != null)
                extraGrid.ArrangeChilds();
        }
        catch { }
    }

    private static bool IsButtonStale()
    {
        if (!NotePadButtonObj) return true;

        var expectedParent = GetTopRightRow(_currentRow);
        return expectedParent == null || NotePadButtonObj!.transform.parent != expectedParent;
    }

    public static void CreateOrUpdateNotePadButton(HudManager instance)
    {
        var desiredRow = NotePadPlugin.Settings.ButtonRow.Value;
        var targetParent = GetTopRightRow(desiredRow);
        if (targetParent == null) return;

        if (IsButtonStale())
            InvalidateButton();

        if (!NotePadButtonObj)
        {
            _currentRow = (NotepadButtonRow)(-1);

            NotePadButtonObj = UnityEngine.Object.Instantiate(
                instance.MapButton.gameObject,
                targetParent
            );
            NotePadButtonObj.name = "NotePadButton";

            var btn = NotePadButtonObj.GetComponent<PassiveButton>();
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityAction)NotePadWindow.Toggle);

            var ap = NotePadButtonObj.GetComponentInChildren<AspectPosition>();
            if (ap != null) UnityEngine.Object.Destroy(ap);

            if (_inactiveSprite == null)
                _inactiveSprite = NotepadAssets.NotepadButtonSprite.LoadAsset();
            if (_activeSprite == null)
                _activeSprite = NotepadAssets.NotepadButtonActiveSprite.LoadAsset();

            var inactive = NotePadButtonObj.transform.Find("Inactive");
            var active = NotePadButtonObj.transform.Find("Active");

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

        if (_currentRow != desiredRow)
        {
            NotePadButtonObj!.transform.SetParent(targetParent, false);
            NotePadButtonObj.transform.localPosition = new Vector3(0f, 0.021f, -80f);

            if (desiredRow == NotepadButtonRow.TopRow)
                NotePadButtonObj.transform.SetAsLastSibling();
            else
                NotePadButtonObj.transform.SetAsFirstSibling();

            _currentRow = desiredRow;

            ArrangeGrids();
        }

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