using HarmonyLib;
using MiraAPI.MeetingAbilities;

namespace NotePadMod.Patches;

[HarmonyPatch(typeof(MeetingAbilityBehaviour), "OnEnable")]
public static class MeetingButtonLayoutPatch
{
    [HarmonyPostfix]
    public static void Postfix(MeetingAbilityBehaviour __instance)
    {
        if (__instance.Renderer != null && !__instance.Renderer.enabled)
            __instance.gameObject.SetActive(false);
    }
}
