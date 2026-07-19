using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using NotePadMod.UI;
using TownOfUs.Utilities;
using TownOfUs.Modules.Localization;
using System;

namespace NotePadMod.Patches;

[HarmonyPatch(typeof(FakeChatHistory), nameof(FakeChatHistory.Record))]
public static class RoleInfoPatch
{
    private static readonly Regex ColorTagRegex = new(@"<color=#?[0-9A-Fa-f]+>(.*?)</color>", RegexOptions.Compiled);
    private static readonly HashSet<string> IncludedTitles = new()
    {
        TouLocale.GetParsed("TouRoleLookoutFeedbackTitle"),
        "Cleric Feedback",
        TouLocale.Get("TouRoleForensicMessageTitle"),
        TouLocale.GetParsed("TouRoleOracleConfessionTitle"),
        TouLocale.Get("TouRoleDoomsayerMessageTitle"),
        TouLocale.Get("TouRoleTrapperMessageTitle"),
        TouLocale.Get("TouRoleInquisitorMessageTitle"),
    };

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

    private static readonly HashSet<string> EmptyResultTexts = new()
    {
        TouLocale.GetParsed("TouRoleOracleConfessorDied"),
        TouLocale.GetParsed("TouRoleOracleTooFew"),
        TouLocale.GetParsed("TouRoleOracleNoMoreEvil"),
        TouLocale.GetParsed("TouRoleTrapperNoPlayers"),
        TouLocale.GetParsed("TouRoleTrapperNotEnoughPLayers"),
    };

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
    private static readonly List<Regex> EmptyResultTemplates = new()
    {
        BuildTemplateRegex(TouLocale.GetParsed("TouRoleInquisitorInquiredNonHeretic")),
        BuildTemplateRegex(TouLocale.GetParsed("TouRoleLookoutNoInteractionFeedback")),
        BuildTemplateRegex("No negative effects were found on <player>."),
    };

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

    [HarmonyPostfix]
    public static void RecordPatch(string title, string message)
    {
        message = message.Replace("#", "");
        message = message.Replace("-", " ");
        if (FakeChatHistory.IsReplaying) return;
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