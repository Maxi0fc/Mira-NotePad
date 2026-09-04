using System.Collections.Generic;

namespace NotePadMod.MeetingAbilities;

public static class JotedRoleLabels
{
    private static readonly Dictionary<byte, string> Labels = new();

    public static void SetLabel(byte playerId, string styledText)
    {
        Labels[playerId] = styledText;
    }

    public static bool TryGetLabel(byte playerId, out string styledText)
    {
        return Labels.TryGetValue(playerId, out styledText!);
    }

    public static bool TryRemoveLabel(byte playerId, out string removedText)
    {
        return Labels.Remove(playerId, out removedText!);
    }

    /// <summary>
    /// A snapshot copy of currently-jotted player IDs, safe to
    /// iterate while removing entries from the live dictionary.
    /// </summary>
    public static List<byte> GetJottedPlayerIdsSnapshot()
    {
        return new List<byte>(Labels.Keys);
    }

    public static void Clear()
    {
        Labels.Clear();
    }
}
