using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using NotePadMod.Compatibility;
using NotePadMod.UI;

namespace NotePadMod.Patches;

public static class RoleInfoPatch
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("RoleInfoPatch");
    private static readonly Regex ColorTagRegex = new(@"<color=#?[0-9A-Fa-f]+>(.*?)</color>", RegexOptions.Compiled);
    private static readonly HashSet<string> IncludedTitles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> EmptyResultTexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Regex> EmptyResultTemplates = new();
    private static bool _initialized;

    public static void InitializeTouStrings()
    {
        if (_initialized) return;
        _initialized = true;

        RegisterTitle(TouIntegration.GetTouLocaleParsed("TownOfUsMira.Role.LookoutFeedbackTitle", "Lookout Feedback"));
        RegisterTitle("Cleric Feedback");
        RegisterTitle(TouIntegration.GetTouLocaleParsed("TownOfUsMira.Role.ForensicMessageTitle", "Forensic Report"));
        RegisterTitle(TouIntegration.GetTouLocaleParsed("TownOfUsMira.Role.OracleConfessionTitle", "Oracle Confession"));
        RegisterTitle(TouIntegration.GetTouLocaleParsed("TownOfUsMira.Role.DoomsayerMessageTitle", "Doomsayer Report"));
        RegisterTitle(TouIntegration.GetTouLocaleParsed("TownOfUsMira.Role.TrapperMessageTitle", "Trapper Report"));
        RegisterTitle(TouIntegration.GetTouLocaleParsed("TownOfUsMira.Role.InquisitorMessageTitle", "Inquisitor Report"));

        RegisterTitle("Doomsayer Feedback");
        RegisterTitle("Trapper Feedback");
        RegisterTitle("Inquisitor Feedback");

        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.OracleConfessorDied",
            "Your confessor failed to survive so you received no confession"));
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.OracleTooFew",
            "Too few people alive to receive a confessional"));
        RegisterEmptyResultTemplate(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.OracleNoMoreEvil",
            "[player] confesses to knowing that there are no more evil players!"),
            "[player]");
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.TrapperNoPlayers",
            "No players entered any of your traps"));
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.TrapperNotEnoughPLayers",
            "Not enough players triggered your traps"));

        RegisterEmptyResultTemplate(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.InquisitorInquiredNonHeretic",
            "Your inquiry reveals that [player] is not a heretic!"),
            "[player]");
        RegisterEmptyResultTemplate(TouIntegration.GetTouLocaleParsed(
            "TownOfUsMira.Role.LookoutNoInteractionFeedback",
            "No players interacted with [player]"),
            "[player]");
        RegisterEmptyResultTemplate("No negative effects were found on <player>.");
    }

    public static void RegisterTitle(string title)
    {
        var normalizedTitle = NormalizeTitle(title);
        if (normalizedTitle.Length > 0)
        {
            IncludedTitles.Add(normalizedTitle);
        }
    }

    public static void RegisterTitles(IEnumerable<string> titles)
    {
        foreach (var title in titles)
        {
            RegisterTitle(title);
        }
    }

    public static void RegisterEmptyResultText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        EmptyResultTexts.Add(text);
    }

    public static void RegisterEmptyResultTexts(IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            RegisterEmptyResultText(text);
        }
    }

    private static Regex BuildTemplateRegex(string template, string placeholder = "<player>")
    {
        var parts = template.Split(new[] { placeholder }, StringSplitOptions.None);
        var pattern = "^" + string.Join(".*", parts.Select(Regex.Escape)) + "$";
        return new Regex(pattern, RegexOptions.Compiled);
    }

    public static void RegisterEmptyResultTemplate(string template, string placeholder = "<player>")
    {
        if (string.IsNullOrEmpty(template)) return;
        EmptyResultTemplates.Add(BuildTemplateRegex(template, placeholder));
    }

    private static string StripColorTag(string text)
    {
        var match = ColorTagRegex.Match(text);
        return match.Success ? match.Groups[1].Value : text;
    }

    public static void RecordPatch(object[] __args)
    {
        if (__args.Length < 3 ||
            __args[1] is not string title ||
            __args[2] is not string message)
        {
            Log.LogWarning(
                $"Ignored FakeChatHistory.Record with unexpected arguments ({__args.Length} arguments).");
            return;
        }

        if (TouIntegration.IsFakeChatHistoryReplaying()) return;
        if (!NotePadPlugin.Settings.AutoAddRoleInfo.Value) return;

        var plainTitle = NormalizeTitle(title);
        if (!IncludedTitles.Contains(plainTitle)) return;

        var plainMessage = StripColorTag(message);
        if (string.IsNullOrWhiteSpace(plainMessage)) return;
        if (EmptyResultTexts.Contains(plainMessage)) return;
        if (EmptyResultTemplates.Any(template => template.IsMatch(plainMessage))) return;

        NotePadWindow.AppendRoleInfoText($"<b>{title}</b>", message);
        Log.LogInfo($"Added whitelisted role info to Notepad: {plainTitle}.");
    }

    private static string NormalizeTitle(string title)
    {
        var plainTitle = StripColorTag(title).Trim();
        return plainTitle.TrimEnd(':').Trim();
    }
}