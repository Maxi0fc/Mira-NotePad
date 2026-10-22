using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using MiraAPI.Modifiers;
using UnityEngine;

namespace NotePadMod.Compatibility;

public static class JottingIntegration
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("JottingIntegration");

    private static MethodInfo? _createMethod;
    private static MethodInfo? _beginMethod;
    private static MethodInfo? _closeMethod;
    private static readonly List<(GameObject GameObject, bool WasActive)> HiddenPlayerAreas = new();

    public static bool IsAvailable => _createMethod != null && _beginMethod != null && _closeMethod != null;

    public static void CacheTypes(Assembly touAssembly)
    {
        var jotMenuType = touAssembly.GetType("TownOfUs.Modules.Components.GuesserMenu");

        if (jotMenuType == null)
        {
            Log.LogWarning("JotMenu type not found; role guessing button will stay disabled.");
            return;
        }

        _createMethod = jotMenuType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);

        _beginMethod = jotMenuType.GetMethod(
            "Begin",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[]
            {
                typeof(Func<RoleBehaviour, bool>),
                typeof(Action<RoleBehaviour>),
                typeof(Func<BaseModifier, bool>),
                typeof(Action<BaseModifier>),
            },
            null);

        _closeMethod = jotMenuType.GetMethod(
            "Close",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (_createMethod == null || _beginMethod == null || _closeMethod == null)
        {
            Log.LogWarning("JotMenu.Create/Begin/Close not found; role guessing button will stay disabled.");
        }
        else
        {
            Log.LogInfo("JotMenu.Create/Begin/Close successfully hooked.");
        }
    }

    public static void Open(Func<RoleBehaviour, bool> roleMatch, Action<RoleBehaviour> onRoleClick)
    {
        if (!IsAvailable)
        {
            Log.LogWarning("Open() called but JotMenu.Create/Begin/Close were never resolved.");
            return;
        }

        object? menuObj;
        try
        {
            menuObj = _createMethod!.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Log.LogError($"JotMenu.Create() threw: {ex.InnerException ?? ex}");
            return;
        }

        if (menuObj == null)
        {
            Log.LogWarning("JotMenu.Create() returned null; cannot open.");
            return;
        }

        var menuType = _beginMethod!.DeclaringType;
        if (menuType == null || !menuType.IsInstanceOfType(menuObj))
        {
            Log.LogWarning("JotMenu.Create() returned an object that cannot begin the menu.");
            return;
        }

        void WrappedClick(RoleBehaviour role)
        {
            try
            {
                onRoleClick(role);
            }
            catch (Exception ex)
            {
                Log.LogError($"Role click handler threw: {ex}");
            }
            finally
            {
                CloseMenu(menuObj);
            }
        }

        try
        {
            _beginMethod!.Invoke(
                menuObj,
                new object?[] { roleMatch, (Action<RoleBehaviour>)WrappedClick, null, null });
            HideMeetingPlayerAreas();
        }
        catch (Exception ex)
        {
            Log.LogError($"JotMenu.Begin() threw: {ex.InnerException ?? ex}");
            CloseMenu(menuObj);
        }
    }

    private static void HideMeetingPlayerAreas()
    {
        RestoreMeetingPlayerAreas();
        if (MeetingHud.Instance == null) return;

        foreach (var playerVoteArea in MeetingHud.Instance.playerStates)
        {
            if (playerVoteArea == null) continue;

            var playerArea = playerVoteArea.gameObject;
            HiddenPlayerAreas.Add((playerArea, playerArea.activeSelf));
            playerArea.SetActive(false);
        }
    }

    internal static void RestoreMeetingPlayerAreas()
    {
        foreach (var (gameObject, wasActive) in HiddenPlayerAreas)
        {
            if (gameObject != null) gameObject.SetActive(wasActive);
        }

        HiddenPlayerAreas.Clear();
    }

    private static void CloseMenu(object menuObj)
    {
        try
        {
            _closeMethod!.Invoke(menuObj, null);
        }
        catch (Exception ex)
        {
            Log.LogError($"JotMenu.Close() threw: {ex.InnerException ?? ex}");
        }
    }
}

[HarmonyPatch(typeof(Minigame), nameof(Minigame.Close))]
public static class JotMenuMeetingVisibilityPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        JottingIntegration.RestoreMeetingPlayerAreas();
    }
}
