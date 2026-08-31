using BepInEx.Logging;
using NotePadMod.Compatibility;
using NotePadMod.MeetingAbilities;

namespace NotePadMod.Patches;

public static class JottedLabelPatch
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("JottedLabelPatch");

    /// <summary>
    /// Drops any jotted label whose role has since become genuinely
    /// known - e.g. the local player turned into a teammate (vampire
    /// conversion), the target got a reveal modifier applied (mayor
    /// reveal, etc.), or the local player died. Runs every time the
    /// real name-text panel refreshes, so a stale/redundant guess
    /// never lingers next to (or in place of) the real answer.
    /// </summary>
    private static void SweepKnownRoles()
    {
        foreach (var playerId in JotedRoleLabels.GetJottedPlayerIdsSnapshot())
        {
            var target = GameData.Instance?.GetPlayerById(playerId)?.Object;
            if (target == null) continue;

            if (TouIntegration.IsRoleAlreadyKnown(target))
            {
                RemoveJotedLabel(playerId);
            }
        }
    }

    public static void AppendJotedLabels()
    {
        if (MeetingHud.Instance == null) return;

        SweepKnownRoles();

        try
        {
            foreach (var playerVA in MeetingHud.Instance.playerStates)
            {
                if (playerVA == null) continue;

                if (!JotedRoleLabels.TryGetLabel(playerVA.PlayerId.Value, out var label))
                    continue;

                playerVA.NameText.text = $"{playerVA.NameText.text}\n{label}";
                playerVA.NameText.ForceMeshUpdate();
            }
        }
        catch (System.Exception ex)
        {
            Log.LogError($"AppendJotedLabels failed: {ex}");
        }
    }

    /// <summary>
    /// Removes a player's jotted role label and immediately restores
    /// their name panel to what it looked like before the label was
    /// appended, rather than waiting for the next natural
    /// UpdateRoleNameText refresh (which may not come again soon).
    /// </summary>
    public static bool RemoveJotedLabel(byte playerId)
    {
        if (!JotedRoleLabels.TryRemoveLabel(playerId, out var label))
            return false;

        try
        {
            if (MeetingHud.Instance == null)
                return true;

            foreach (var playerVA in MeetingHud.Instance.playerStates)
            {
                if (playerVA == null || playerVA.PlayerId.Value != playerId)
                    continue;

                var suffix = $"\n{label}";

                if (playerVA.NameText.text.EndsWith(suffix))
                {
                    playerVA.NameText.text =
                        playerVA.NameText.text[..^suffix.Length];

                    playerVA.NameText.ForceMeshUpdate();
                }

                break;
            }
        }
        catch (System.Exception ex)
        {
            Log.LogError($"RemoveJotedLabel restore failed: {ex}");
        }

        return true;
    }
}
