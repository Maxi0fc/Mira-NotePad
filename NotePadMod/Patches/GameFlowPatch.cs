using HarmonyLib;
using NotePadMod.MeetingAbilities;
using NotePadMod.UI;
namespace NotePadMod.Patches;
[HarmonyPatch]
public static class GameFlowPatch
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.ReallyBegin))]
    [HarmonyPostfix]
    public static void OnGameStart()
    {
        NotePadWindow.ClearText();
        NotePadWindow.CloseWindow();
        JotedRoleLabels.Clear();
    }
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    [HarmonyPostfix]
    public static void OnShipStart()
    {
        RoleColorizer.Refresh();
        ModifierColorizer.Refresh();
        NotePadWindow.CloseWindow();
    }
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    [HarmonyPostfix]
    public static void OnLobbyStart()
    {
        NotePadWindow.ClearText();
        NotePadWindow.CloseWindow();
    }
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void OnMeetingStart() => NotePadWindow.CloseWindow();
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    [HarmonyPostfix]
    public static void OnMeetingEnd() => NotePadWindow.CloseWindow();
    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
    [HarmonyPostfix]
    public static void OnEndGame() => NotePadWindow.CloseWindow();
}
