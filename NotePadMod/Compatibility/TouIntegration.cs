using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.Modifiers;
using NotePadMod.Patches;
using UnityEngine;

namespace NotePadMod.Compatibility;

public static class TouIntegration
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("TouIntegration");

    public static bool IsTouPresent { get; private set; }

    private static Assembly? _touAssembly;
    private static Type? _touLocaleType;
    private static MethodInfo? _getParsedMethod;
    private static MethodInfo? _getMethod;
    private static MethodInfo? _getModifierColourMethod;
    private static Func<object, UnityEngine.Color>? _getModifierColourFunc;
    private static Type? _baseRevealModifierType;
    private static PropertyInfo? _revealVisibleProp;
    private static PropertyInfo? _revealRoleProp;
    private static MethodInfo? _areTeammatesMethod;

    public static void Initialize(Harmony harmony)
    {
        try
        {
            CheckTouPresence();

            if (!IsTouPresent)
            {
                Log.LogInfo("[TouIntegration] Town of Us Mira is not detected. Running as standalone MiraAPI mod.");
                return;
            }

            Log.LogInfo("[TouIntegration] Town of Us Mira detected! Initializing soft-dependency features...");

            CacheTouTypes();
            RoleInfoPatch.InitializeTouStrings();
            ApplyDynamicPatches(harmony);

            Log.LogInfo("[TouIntegration] Town of Us Mira features initialized successfully.");
        }
        catch (Exception ex)
        {
            Log.LogError($"[TouIntegration] Error initializing Town of Us integration: {ex}");
        }
    }

    private static void CheckTouPresence()
    {
        if (IL2CPPChainloader.Instance?.Plugins != null)
        {
            if (IL2CPPChainloader.Instance.Plugins.ContainsKey("auavengers.tou.mira"))
            {
                IsTouPresent = true;
            }
        }

        if (!IsTouPresent)
        {
            _touAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "TownOfUsMira", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(a.GetName().Name, "TownOfUs", StringComparison.OrdinalIgnoreCase));

            if (_touAssembly != null)
            {
                IsTouPresent = true;
            }
        }
    }

    private static void CacheTouTypes()
    {
        if (_touAssembly == null)
        {
            _touAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "TownOfUsMira", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(a.GetName().Name, "TownOfUs", StringComparison.OrdinalIgnoreCase));
        }

        if (_touAssembly == null) return;

        _touLocaleType = _touAssembly.GetType("TownOfUs.Modules.Localization.TouLocale");
        if (_touLocaleType != null)
        {
            _getParsedMethod = _touLocaleType.GetMethod("GetParsed", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            _getMethod = _touLocaleType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        }

        var miscUtilsType = _touAssembly.GetType("TownOfUs.Utilities.MiscUtils");
        if (miscUtilsType != null)
        {
            _getModifierColourMethod = miscUtilsType.GetMethod("GetModifierColour", BindingFlags.Public | BindingFlags.Static);
            if (_getModifierColourMethod != null)
            {
                _getModifierColourFunc = mod => (UnityEngine.Color)_getModifierColourMethod.Invoke(null, new[] { mod })!;
            }
        }

        JottingIntegration.CacheTypes(_touAssembly);

        _baseRevealModifierType = _touAssembly.GetType("TownOfUs.Modifiers.BaseRevealModifier");
        if (_baseRevealModifierType != null)
        {
            _revealVisibleProp = _baseRevealModifierType.GetProperty("Visible", BindingFlags.Public | BindingFlags.Instance);
            _revealRoleProp = _baseRevealModifierType.GetProperty("RevealRole", BindingFlags.Public | BindingFlags.Instance);
        }

        var touRoleUtilsType = _touAssembly.GetType("TownOfUs.Utilities.TouRoleUtils");
        if (touRoleUtilsType != null)
        {
            _areTeammatesMethod = touRoleUtilsType.GetMethod("AreTeammates", BindingFlags.Public | BindingFlags.Static);
        }
    }

    private static void ApplyDynamicPatches(Harmony harmony)
    {
        if (_touAssembly == null) return;

        var fakeChatHistoryType = _touAssembly.GetType("TownOfUs.Utilities.FakeChatHistory");
        if (fakeChatHistoryType != null)
        {
            var recordMethod = fakeChatHistoryType.GetMethod("Record", BindingFlags.Public | BindingFlags.Static);
            if (recordMethod != null)
            {
                var postfixMethod = typeof(RoleInfoPatch).GetMethod(nameof(RoleInfoPatch.RecordPatch), BindingFlags.Public | BindingFlags.Static);
                if (postfixMethod != null)
                {
                    harmony.Patch(recordMethod, postfix: new HarmonyMethod(postfixMethod));
                    Log.LogInfo("[TouIntegration] Successfully hooked FakeChatHistory.Record for Role Info Jotting.");
                }
            }
        }

        var hudPatchesType = _touAssembly.GetType("TownOfUs.Patches.HudManagerPatches");
        if (hudPatchesType != null)
        {
            var canZoomProp = hudPatchesType.GetProperty("CanZoom", BindingFlags.Public | BindingFlags.Static);
            var canZoomGetter = canZoomProp?.GetGetMethod();
            if (canZoomGetter != null)
            {
                var prefixMethod = typeof(ZoomPatch).GetMethod(nameof(ZoomPatch.CanZoomPatch), BindingFlags.Public | BindingFlags.Static);
                if (prefixMethod != null)
                {
                    harmony.Patch(canZoomGetter, prefix: new HarmonyMethod(prefixMethod));
                    Log.LogInfo("[TouIntegration] Successfully hooked HudManagerPatches.CanZoom.");
                }
            }
        }

        var hudHelperType = _touAssembly.GetType("TownOfUs.Modules.Components.HudManagerHelper");
        if (hudHelperType != null)
        {
            var updateRoleNameTextMethod = hudHelperType.GetMethod("UpdateRoleNameText", BindingFlags.Public | BindingFlags.Static);
            if (updateRoleNameTextMethod != null)
            {
                var postfixMethod = typeof(JottedLabelPatch).GetMethod(nameof(JottedLabelPatch.AppendJotedLabels), BindingFlags.Public | BindingFlags.Static);
                if (postfixMethod != null)
                {
                    harmony.Patch(updateRoleNameTextMethod, postfix: new HarmonyMethod(postfixMethod));
                    Log.LogInfo("[TouIntegration] Successfully hooked HudManagerHelper.UpdateRoleNameText for guessed role labels.");
                }
                else
                {
                    Log.LogWarning("[TouIntegration] JottedLabelPatch.AppendJotedLabels not found via reflection.");
                }
            }
            else
            {
                Log.LogWarning("[TouIntegration] HudManagerHelper.UpdateRoleNameText not found; guessed role labels will not display.");
            }
        }
        else
        {
            Log.LogWarning("[TouIntegration] TownOfUs.Modules.Components.HudManagerHelper type not found.");
        }
    }

    public static string GetTouLocaleParsed(string key, string fallback = "")
    {
        if (_getParsedMethod != null)
        {
            try
            {
                var res = (string?)_getParsedMethod.Invoke(null, new object[] { key });
                if (!string.IsNullOrEmpty(res)) return res;
            }
            catch { }
        }
        return fallback;
    }

    public static string GetTouLocale(string key, string fallback = "")
    {
        if (_getMethod != null)
        {
            try
            {
                var res = (string?)_getMethod.Invoke(null, new object[] { key });
                if (!string.IsNullOrEmpty(res)) return res;
            }
            catch { }
        }
        return fallback;
    }

    public static UnityEngine.Color? GetModifierColour(object modifier)
    {
        if (_getModifierColourFunc == null) return null;
        try
        {
            return _getModifierColourFunc(modifier);
        }
        catch
        {
            return null;
        }
    }

    public static Type? GetTouBaseGameModifierType()
    {
        return _touAssembly?.GetType("TownOfUs.Modifiers.TouBaseGameModifier");
    }

    public static bool IsFakeChatHistoryReplaying()
    {
        if (_touAssembly == null) return false;
        var fakeChatHistoryType = _touAssembly.GetType("TownOfUs.Utilities.FakeChatHistory");
        if (fakeChatHistoryType == null) return false;

        var prop = fakeChatHistoryType.GetProperty("IsReplaying", BindingFlags.Public | BindingFlags.Static);
        if (prop != null)
        {
            try
            {
                return (bool)prop.GetValue(null)!;
            }
            catch { }
        }
        return false;
    }

    public static bool IsRoleAlreadyKnown(PlayerControl target)
    {
        if (target == null || PlayerControl.LocalPlayer == null) return false;
        if (target == PlayerControl.LocalPlayer) return false;

        try
        {
            /*
             * Once the local player is dead, dead players typically
             * see everyone's real role - so any jotted guess becomes
             * redundant/stale the moment the local player dies,
             * regardless of teammate status or reveal modifiers.
             */
            if (PlayerControl.LocalPlayer.Data != null &&
                PlayerControl.LocalPlayer.Data.IsDead)
            {
                return true;
            }

            if (_areTeammatesMethod != null &&
                (bool)_areTeammatesMethod.Invoke(null, new object[] { PlayerControl.LocalPlayer, target })!)
            {
                return true;
            }

            if (_baseRevealModifierType != null && _revealVisibleProp != null && _revealRoleProp != null)
            {
                foreach (var modifier in target.GetModifiers(_baseRevealModifierType))
                {
                    var visible = (bool)(_revealVisibleProp.GetValue(modifier) ?? false);
                    var revealRole = (bool)(_revealRoleProp.GetValue(modifier) ?? false);
                    if (visible && revealRole) return true;
                }
            }
        }
        catch { }

        return false;
    }
}
