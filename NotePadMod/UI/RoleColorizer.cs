using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MiraAPI.Roles;
using UnityEngine;

namespace NotePadMod.UI;

/// <summary>
/// Scans notepad text for role names and wraps them in bold + color TMP tags.
/// </summary>
public static class RoleColorizer
{
    private static Dictionary<string, string>? _roleColors;
    private static Regex? _roleRegex;

    public static void Refresh()
    {
        _roleColors = new Dictionary<string, string>();

        foreach (var role in CustomRoleManager.AllRoles)
        {
            if (role is not ICustomRole customRole) continue;

            string name = customRole.RoleName?.Trim() ?? "";
            if (name.Length == 0) continue;

            string hex = ColorUtility.ToHtmlStringRGB(customRole.RoleColor);
            _roleColors[name.ToLowerInvariant()] = hex;
        }

        BuildRegex();
    }

    private static void BuildRegex()
    {
        if (_roleColors == null || _roleColors.Count == 0)
        {
            _roleRegex = null;
            return;
        }

        // Longest names first so "Serial Killer" matches before "Killer"
        var names = new List<string>(_roleColors.Keys);
        names.Sort((a, b) => b.Length.CompareTo(a.Length));

        var sb = new StringBuilder(@"(?i)\b(");
        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(Regex.Escape(names[i]));
        }
        sb.Append(@")\b");

        _roleRegex = new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public static string Apply(string raw)
    {
        if (_roleColors == null || _roleRegex == null || raw.Length == 0)
            return raw;

        return _roleRegex.Replace(raw, m =>
        {
            string key = m.Value.ToLowerInvariant();
            if (_roleColors.TryGetValue(key, out string? hex))
                return $"<b><color=#{hex}>{m.Value}</color></b>";
            return m.Value;
        });
    }

    public static bool IsReady => _roleColors != null && _roleColors.Count > 0;
}