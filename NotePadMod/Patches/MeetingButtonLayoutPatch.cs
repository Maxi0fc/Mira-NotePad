using HarmonyLib;
using MiraAPI.MeetingAbilities;
using NotePadMod.MeetingAbilities;
using UnityEngine;

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

[HarmonyPatch(typeof(MeetingAbilityBehaviour), nameof(MeetingAbilityBehaviour.Initialize))]
public static class JotButtonPlacementPatch
{
    [HarmonyPostfix]
    public static void Postfix(MeetingAbilityBehaviour __instance)
    {
        if (__instance.gameObject.name != RoleJotButton.JotButtonGameObjectName) return;

        var voteArea = __instance.VoteArea;
        if (voteArea == null || voteArea.Buttons == null ||
            voteArea.ConfirmButton == null || voteArea.CancelButton == null ||
            __instance.Renderer == null)
            return;

        var confirmTransform = voteArea.ConfirmButton.transform;
        var cancelTransform = voteArea.CancelButton.transform;
        var confirmRenderer = voteArea.ConfirmButton.GetComponent<SpriteRenderer>();
        var cancelRenderer = voteArea.CancelButton.GetComponent<SpriteRenderer>();
        if (confirmRenderer == null || cancelRenderer == null) return;

        float leftEdge = Mathf.Min(
            confirmTransform.position.x - confirmRenderer.bounds.extents.x,
            cancelTransform.position.x - cancelRenderer.bounds.extents.x);

        var position = __instance.transform.position;
        position.x = leftEdge - __instance.Renderer.bounds.extents.x - 0.12f;
        position.y = (confirmTransform.position.y + cancelTransform.position.y) * 0.5f;
        position.z = confirmTransform.position.z;
        __instance.transform.position = position;
    }
}
