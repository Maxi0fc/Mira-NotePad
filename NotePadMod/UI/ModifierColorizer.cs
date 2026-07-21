using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Game;
using TownOfUs.Utilities;
using UnityEngine;
namespace NotePadMod.UI;

public static class ModifierColorizer
{
    private static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("ModifierColorizer");
    private static Dictionary<string, string>? _modifierColors;
    private static Dictionary<string, string>? _modifierIcons;
    private static Regex? _modifierRegex;
    public static void Refresh()
    {
        _modifierColors = new Dictionary<string, string>();
        _modifierIcons = new Dictionary<string, string>();
        int count = 0;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray()!;
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(TouGameModifier).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                TouGameModifier? instance;
                try
                {
                    instance = Activator.CreateInstance(type) as TouGameModifier;
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[ModifierColorizer] Failed to instantiate {type.Name}: {ex.Message}");
                    continue;
                }

                if (instance == null) continue;

                string name = instance.ModifierName?.Trim() ?? "";
                if (name.Length == 0) continue;

                ModifierUiConfiguration config;
                try
                {
                    config = instance.Configuration;
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[ModifierColorizer] {type.Name}.Configuration threw: {ex.Message}");
                    continue;
                }

                string key = name.ToLowerInvariant();
                _modifierColors[key] = ColorUtility.ToHtmlStringRGB(MiscUtils.GetModifierColour(instance));

                if (config.PopUpIconTmp != null)
                {
                    _modifierIcons[key] = $"<sprite name=\"{config.PopUpIconTmp.name}\">";
                }

                count++;
            }
        }

        Log.LogInfo($"[ModifierColorizer] Loaded {count} modifiers");
        BuildRegex();
    }

    private static void BuildRegex()
    {
        if (_modifierColors == null || _modifierColors.Count == 0)
        {
            _modifierRegex = null;
            Log.LogWarning("[ModifierColorizer] No modifiers found, regex not built");
            return;
        }
        var names = new List<string>(_modifierColors.Keys);
        names.Sort((a, b) => b.Length.CompareTo(a.Length));
        var sb = new StringBuilder(@"(?i)\b(");
        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(Regex.Escape(names[i]));
        }
        sb.Append(@")\b");
        _modifierRegex = new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Log.LogInfo($"[ModifierColorizer] Regex built with {names.Count} modifier names");
    }

    public static string Apply(string raw)
    {
        if (!IsReady)
        {
            Refresh();
        }

        if (_modifierColors == null || _modifierRegex == null || raw.Length == 0)
            return raw;
        return _modifierRegex.Replace(raw, m =>
        {
            string key = m.Value.ToLowerInvariant();
            if (_modifierColors.TryGetValue(key, out string? hex))
            {
                string icon = "";
                if (NotePadPlugin.Settings.ShowModifierIcons.Value &&
                    _modifierIcons != null && _modifierIcons.TryGetValue(key, out string? iconTmp))
                {
                    icon = iconTmp;
                }
                return $"{icon}<b><color=#{hex}>{m.Value}</color></b>";
            }
            return m.Value;
        });
    }

    public static bool IsReady => _modifierColors != null && _modifierColors.Count > 0;
}