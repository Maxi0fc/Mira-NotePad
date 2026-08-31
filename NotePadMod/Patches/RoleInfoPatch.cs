using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NotePadMod.Compatibility;
using NotePadMod.UI;

namespace NotePadMod.Patches;

public static class RoleInfoPatch
{
    private static readonly Regex ColorTagRegex = new(@"<color=#?[0-9A-Fa-f]+>(.*?)</color>", RegexOptions.Compiled);
    private static readonly HashSet<string> IncludedTitles = new();
    private static readonly HashSet<string> EmptyResultTexts = new();
    private static readonly List<Regex> EmptyResultTemplates = new();
    private static bool _initialized;

    public static void InitializeTouStrings()
    {
        if (_initialized) return;
        _initialized = true;

        RegisterTitle(TouIntegration.GetTouLocaleParsed("TouRoleLookoutFeedbackTitle", "Lookout Feedback"));
        RegisterTitle("Cleric Feedback");
        RegisterTitle(TouIntegration.GetTouLocale("TouRoleForensicMessageTitle", "Forensic Report"));
        RegisterTitle(TouIntegration.GetTouLocaleParsed("TouRoleOracleConfessionTitle", "Oracle Confession"));
        RegisterTitle(TouIntegration.GetTouLocale("TouRoleDoomsayerMessageTitle", "Doomsayer Feedback"));
        RegisterTitle(TouIntegration.GetTouLocale("TouRoleTrapperMessageTitle", "Trapper Feedback"));
        RegisterTitle(TouIntegration.GetTouLocale("TouRoleInquisitorMessageTitle", "Inquisitor Feedback"));

        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed("TouRoleOracleConfessorDied", "The confessor died before confessing."));
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed("TouRoleOracleTooFew", "There were too few players to confess."));
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed("TouRoleOracleNoMoreEvil", "There are no more evil players to confess."));
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed("TouRoleTrapperNoPlayers", "No players triggered your trap."));
        RegisterEmptyResultText(TouIntegration.GetTouLocaleParsed("TouRoleTrapperNotEnoughPLayers", "Not enough players triggered your trap."));

        RegisterEmptyResultTemplate(TouIntegration.GetTouLocaleParsed("TouRoleInquisitorInquiredNonHeretic", "<player> is not a Heretic."));
        RegisterEmptyResultTemplate(TouIntegration.GetTouLocaleParsed("TouRoleLookoutNoInteractionFeedback", "Nobody visited <player>."));
        RegisterEmptyResultTemplate("No negative effects were found on <player>.");
    }

    public static void RegisterTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return;
        IncludedTitles.Add(title);
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

    public static void RecordPatch(string title, string message)
    {
        message = message.Replace("#", "");
        message = message.Replace("-", " ");
        if (TouIntegration.IsFakeChatHistoryReplaying()) return;
        if (!NotePadPlugin.Settings.AutoAddRoleInfo.Value) return;

        var plainTitle = StripColorTag(title);
        if (!IncludedTitles.Contains(plainTitle)) return;

        var plainMessage = StripColorTag(message);
        if (string.IsNullOrWhiteSpace(plainMessage)) return;
        if (EmptyResultTexts.Contains(plainMessage)) return;
        if (EmptyResultTemplates.Any(template => template.IsMatch(plainMessage))) return;

        NotePadWindow.AppendText($"{message}\n");
    }
}