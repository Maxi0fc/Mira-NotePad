using HarmonyLib;
using NotePadMod.UI;
using UnityEngine;

namespace NotePadMod.Patches;

[HarmonyPatch]
public static class InputBlockPatch
{
    private static readonly KeyCode[] AllowedKeys =
    {
        KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
        KeyCode.Home, KeyCode.End, KeyCode.Backspace, KeyCode.Delete,
        KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Escape,
    };

    private static bool IsAllowedKey(KeyCode key)
    {
        foreach (var allowed in AllowedKeys)
            if (allowed == key)
                return true;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
    [HarmonyPrefix]
    public static bool GetKeyDownPatch(KeyCode key, ref bool __result)
    {
        if (!NotePadWindow.IsOpen || IsAllowedKey(key)) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
    [HarmonyPrefix]
    public static bool GetKeyPatch(KeyCode key, ref bool __result)
    {
        if (!NotePadWindow.IsOpen || IsAllowedKey(key)) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(KeyCode))]
    [HarmonyPrefix]
    public static bool GetKeyUpPatch(KeyCode key, ref bool __result)
    {
        if (!NotePadWindow.IsOpen || IsAllowedKey(key)) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButtonDown), typeof(string))]
    [HarmonyPrefix]
    public static bool GetButtonDownPatch(ref bool __result)
    {
        if (!NotePadWindow.IsOpen) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButton), typeof(string))]
    [HarmonyPrefix]
    public static bool GetButtonPatch(ref bool __result)
    {
        if (!NotePadWindow.IsOpen) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButtonUp), typeof(string))]
    [HarmonyPrefix]
    public static bool GetButtonUpPatch(ref bool __result)
    {
        if (!NotePadWindow.IsOpen) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetAxis), typeof(string))]
    [HarmonyPrefix]
    public static bool GetAxisPatch(ref float __result)
    {
        if (!NotePadWindow.IsOpen) return true;
        __result = 0f;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetAxisRaw), typeof(string))]
    [HarmonyPrefix]
    public static bool GetAxisRawPatch(ref float __result)
    {
        if (!NotePadWindow.IsOpen) return true;
        __result = 0f;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown), typeof(int))]
    [HarmonyPrefix]
    public static bool GetMouseButtonDownPatch(int button, ref bool __result)
    {
        if (!NotePadWindow.IsOpen || button == 0) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton), typeof(int))]
    [HarmonyPrefix]
    public static bool GetMouseButtonPatch(int button, ref bool __result)
    {
        if (!NotePadWindow.IsOpen || button == 0) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp), typeof(int))]
    [HarmonyPrefix]
    public static bool GetMouseButtonUpPatch(int button, ref bool __result)
    {
        if (!NotePadWindow.IsOpen || button == 0) return true;
        __result = false;
        return false;
    }
}
