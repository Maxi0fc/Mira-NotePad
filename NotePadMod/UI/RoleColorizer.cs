using System;
using System.Collections.Generic;
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
    private static Dictionary<string, string>? _roleColors;
    private static Dictionary<string, string>? _roleIcons;
    private static Regex? _roleRegex;

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

                string name = customRole.RoleName?.Trim() ?? "";
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
                        name = cr.RoleName?.Trim() ?? "";
                        color = cr.RoleColor;
                    }
                    else
                    {
                        name = TranslationController.Instance?.GetString(role.StringName)?.Trim() ?? role.Role.ToString();
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
        Log.LogInfo($"[RoleColorizer] Regex built with {names.Count} role names");
    }

    public static string Apply(string raw)
    {
        if (!IsReady)
        {
            Refresh();
        }

        if (_roleColors == null || _roleRegex == null || raw.Length == 0)
            return raw;

        return _roleRegex.Replace(raw, m =>
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