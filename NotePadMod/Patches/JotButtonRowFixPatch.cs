using HarmonyLib;

namespace NotePadMod.Patches;

/// <summary>
/// The Jot Role button is a MiraAPI TargetedMeetingButton, which
/// MeetingButtonManager.OnMeetingStart parents under each
/// PlayerVoteArea's own "Buttons" container - the same container
/// holding the vanilla vote/cancel button graphics. Once the local
/// player casts their vote, the base game disables that whole
/// container, taking the Jot button down with it even though it
/// isn't actually a vote action.
///
/// Move it out into the row itself (a sibling of Buttons, not a
/// child) so it survives voting, preserving its exact on-screen
/// position via worldPositionStays.
///
/// This runs on both MeetingHud.Start (low priority, so it runs
/// after MiraAPI's own button-creation postfix) and every
/// MeetingHud.Update, so it self-heals regardless of exact Harmony
/// patch ordering between mods rather than depending on winning a
/// one-shot timing race.
/// </summary>
[HarmonyPatch]
public static class JotButtonRowFixPatch
{
    private const string JotButtonName = "Jot RoleButton";

    private static void FixJotButtonParents(MeetingHud instance)
    {
        if (instance == null || instance.playerStates == null) return;

        foreach (var playerVoteArea in instance.playerStates)
        {
            if (playerVoteArea == null || playerVoteArea.Buttons == null) continue;

            var jotButton = playerVoteArea.Buttons.transform.Find(JotButtonName);
            if (jotButton == null) continue;

            // worldPositionStays: true keeps the exact same on-screen
            // spot, it just escapes the container that gets disabled
            // once the local player votes.
            jotButton.SetParent(playerVoteArea.transform, true);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    public static void MeetingHudStartPostfix(MeetingHud __instance)
    {
        FixJotButtonParents(__instance);
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingHudUpdatePostfix(MeetingHud __instance)
    {
        FixJotButtonParents(__instance);
    }
}
