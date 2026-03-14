using HarmonyLib;
using NotePadMod.UI;
using TownOfUs.Patches;
namespace NotePadMod.Patches;
[HarmonyPatch]
public static class ZoomPatch
{
    [HarmonyPatch(typeof(HudManagerPatches), nameof(HudManagerPatches.CanZoom), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool CanZoomPatch(ref bool __result)
    {
        if (NotePadWindow.IsOpen)
        {
            __result = false;
            return false;
        }
        return true;
    }
}
