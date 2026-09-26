using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AmongUs.GameOptions;
using BepInEx.Logging;
using MiraAPI.Roles;
using UnityEngine;

namespace NotePadMod.UI;

public static class RoleColorizer
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("RoleColorizer");
    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static Dictionary<string, string>? _roleColors;
    private static Dictionary<string, string>? _roleIcons;
    private static Regex? _roleRegex;

    private static string StripTags(string text) => string.IsNullOrEmpty(text) ? text : TagRegex.Replace(text, "").Trim();

    public static void Refresh()
    {
        _roleColors = new Dictionary<string, string>();
        _roleIcons = new Dictionary<string, string>();
        int count = 0;

        try
        {
            // 1. Process custom roles registered in MiraAPI
            foreach (var customRole in CustomRoleManager.CustomMiraRoles)
            {
                if (customRole == null) continue;

                string name = StripTags(customRole.RoleName?.Trim() ?? "");
                if (name.Length == 0) continue;

                string hex = ColorUtility.ToHtmlStringRGB(customRole.RoleColor);
                string key = name.ToLowerInvariant();
                _roleColors[key] = hex;

                string? icon = GetRoleTmpIcon(customRole);
                if (!string.IsNullOrEmpty(icon))
                {
                    _roleIcons[key] = icon;
                }

                count++;
            }

            // 2. Also check RoleManager.Instance.AllRoles for vanilla or other registered roles
            if (RoleManager.Instance?.AllRoles != null)
            {
                foreach (var role in RoleManager.Instance.AllRoles)
                {
                    if (role == null) continue;

                    string name;
                    Color color;

                    if (role is ICustomRole cr)
                    {
                        name = StripTags(cr.RoleName?.Trim() ?? "");
                        color = cr.RoleColor;
                    }
                    else
                    {
                        name = StripTags(TranslationController.Instance?.GetString(role.StringName)?.Trim() ?? role.Role.ToString());
                        color = role.TeamType == RoleTeamTypes.Impostor ? Palette.ImpostorRed : Palette.CrewmateBlue;
                    }

                    if (string.IsNullOrEmpty(name)) continue;

                    string key = name.ToLowerInvariant();
                    if (!_roleColors.ContainsKey(key))
                    {
                        _roleColors[key] = ColorUtility.ToHtmlStringRGB(color);
                        count++;
                    }

                    if (!_roleIcons.ContainsKey(key))
                    {
                        string? icon = GetRoleTmpIcon(role);
                        if (!string.IsNullOrEmpty(icon))
                        {
                            _roleIcons[key] = icon;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[RoleColorizer] Exception during Refresh: {ex}");
        }

        Log.LogInfo($"[RoleColorizer] Loaded {count} roles");
        BuildRegex();
    }

    public static string GetRoleTmpIcon(ICustomRole role)
    {
        return role.Configuration.IconTmp ? $"<sprite name=\"{role.Configuration.IconTmp.name}\">" : $"<sprite name=\"AmongUs.Role.{role.Team}\">";
    }

    public static string GetRoleTmpIcon(RoleBehaviour role)
    {
        if (role is ICustomRole custom)
        {
            return custom.Configuration.IconTmp ? $"<sprite name=\"{custom.Configuration.IconTmp.name}\">" : $"<sprite name=\"AmongUs.Role.{custom.Team}\">";
        }
        return $"<sprite name=\"AmongUs.Role.{role.Role}\">";
    }

    private static void BuildRegex()
    {
        if (_roleColors == null || _roleColors.Count == 0)
        {
            _roleRegex = null;
            Log.LogWarning("[RoleColorizer] No roles found, regex not built");
            return;
        }

        var names = _roleColors.Keys
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ToList();

        if (names.Count == 0)
        {
            _roleRegex = null;
            return;
        }

        var pattern = "(?i)(?<![A-Za-z0-9])(?:" + string.Join("|", names.Select(Regex.Escape)) + ")(?![A-Za-z0-9])";
        _roleRegex = new Regex(pattern, RegexOptions.Compiled);
        Log.LogInfo($"[RoleColorizer] Regex built with {names.Count} role names");
    }

    private static string ApplyPattern(string raw, MatchEvaluator replacer)
    {
        if (string.IsNullOrEmpty(raw) || _roleRegex == null)
            return raw;

        var result = new StringBuilder(raw.Length);
        int lastIndex = 0;

        foreach (Match tagMatch in TagRegex.Matches(raw))
        {
            if (tagMatch.Index > lastIndex)
            {
                string plain = raw.Substring(lastIndex, tagMatch.Index - lastIndex);
                result.Append(_roleRegex.Replace(plain, replacer));
            }

            result.Append(tagMatch.Value);
            lastIndex = tagMatch.Index + tagMatch.Length;
        }

        if (lastIndex < raw.Length)
        {
            result.Append(_roleRegex.Replace(raw.Substring(lastIndex), replacer));
        }

        return result.ToString();
    }

    public static string Apply(string raw)
    {
        if (!IsReady)
        {
            Refresh();
        }

        if (_roleColors == null || _roleRegex == null || raw.Length == 0)
            return raw;

        if (raw.Contains("<color=", StringComparison.OrdinalIgnoreCase))
            return raw;

        return ApplyPattern(raw, m =>
        {
            string key = m.Value.ToLowerInvariant();
            if (_roleColors.TryGetValue(key, out string? hex))
            {
                string icon = "";
                if (NotePadPlugin.Settings.ShowRoleIcons.Value &&
                    _roleIcons != null && _roleIcons.TryGetValue(key, out string? iconTmp))
                {
                    icon = iconTmp;
                }
                return $"{icon}<b><color=#{hex}>{m.Value}</color></b>";
            }
            return m.Value;
        });
    }

    public static bool IsReady => _roleColors != null && _roleColors.Count > 0;
}