using HarmonyLib;
using NotePadMod.UI;
using UnityEngine;

namespace NotePadMod.Patches;

[HarmonyPatch]
public static class MovementPatch
{
    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
    [HarmonyPrefix]
    public static bool StopMovement(PlayerPhysics __instance)
    {
        if (!NotePadWindow.IsOpen) return true;

        // Only freeze the local player
        if (__instance.myPlayer == null) return true;
        if (__instance.myPlayer != PlayerControl.LocalPlayer) return true;

        if (__instance.body != null)
            __instance.body.velocity = Vector2.zero;

        return false;
    }
}