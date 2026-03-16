using HarmonyLib;
using NotePadMod.UI;

namespace NotePadMod.Patches;

[HarmonyPatch]
public static class GameFlowPatch
{
    // Build role color lookup once when the lobby loads (roles are registered by then).
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    [HarmonyPostfix]
    public static void OnLobbyStart()
    {
        RoleColorizer.Refresh();
        NotePadWindow.ClearText();
        NotePadWindow.Close();
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ReallyBegin))]
    [HarmonyPostfix]
    public static void OnGameStart()
    {
        RoleColorizer.Refresh(); // renames may have been finalised in lobby options
        NotePadWindow.ClearText();
        NotePadWindow.Close();
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    [HarmonyPostfix]
    public static void OnShipStart()
    {
        NotePadWindow.Close();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void OnMeetingStart() => NotePadWindow.Close();

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    [HarmonyPostfix]
    public static void OnMeetingEnd() => NotePadWindow.Close();

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
    [HarmonyPostfix]
    public static void OnEndGame() => NotePadWindow.Close();
}
