using System;
using System.Reflection;
using BepInEx.Logging;
using MiraAPI.Modifiers;

namespace NotePadMod.Compatibility;

public static class JottingIntegration
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("JottingIntegration");

    private static MethodInfo? _createMethod;
    private static MethodInfo? _beginMethod;

    public static bool IsAvailable => _createMethod != null && _beginMethod != null;

    public static void CacheTypes(Assembly touAssembly)
    {
        var JotMenuType = touAssembly.GetType("TownOfUs.Modules.Components.GuesserMenu");

        if (JotMenuType == null)
        {
            Log.LogWarning("JotMenu type not found; role guessing button will stay disabled.");
            return;
        }

        _createMethod = JotMenuType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);

        _beginMethod = JotMenuType.GetMethod(
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

        if (_createMethod == null || _beginMethod == null)
        {
            Log.LogWarning("JotMenu.Create/Begin not found; role guessing button will stay disabled.");
        }
        else
        {
            Log.LogInfo("JotMenu.Create/Begin successfully hooked.");
        }
    }

    public static void Open(Func<RoleBehaviour, bool> roleMatch, Action<RoleBehaviour> onRoleClick)
    {
        if (!IsAvailable)
        {
            Log.LogWarning("Open() called but JotMenu.Create/Begin were never resolved.");
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

        if (menuObj is not Minigame minigame)
        {
            Log.LogWarning("JotMenu.Create() did not return a Minigame; cannot open.");
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

            minigame.Close();
        }

        try
        {
            _beginMethod!.Invoke(
                menuObj,
                new object?[] { roleMatch, (Action<RoleBehaviour>)WrappedClick, null, null });
        }
        catch (Exception ex)
        {
            Log.LogError($"JotMenu.Begin() threw: {ex.InnerException ?? ex}");
        }
    }
}
