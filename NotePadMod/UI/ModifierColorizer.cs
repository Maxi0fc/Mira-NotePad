using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using MiraAPI.Modifiers;
using NotePadMod.Compatibility;
using UnityEngine;

namespace NotePadMod.UI;

public static class ModifierColorizer
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("ModifierColorizer");
    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static Dictionary<string, string>? _modifierColors;
    private static Dictionary<string, string>? _modifierIcons;
    private static Regex? _modifierRegex;

    private static string StripTags(string text) => string.IsNullOrEmpty(text) ? text : TagRegex.Replace(text, "").Trim();

    public static void Refresh()
    {
        _modifierColors = new Dictionary<string, string>();
        _modifierIcons = new Dictionary<string, string>();
        int count = 0;

        try
        {
            // 1. Load all registered modifiers from MiraAPI ModifierManager
            if (ModifierManager.Modifiers != null)
            {
                foreach (var mod in ModifierManager.Modifiers)
                {
                    if (mod == null) continue;

                    string name = GetModifierName(mod);
                    if (string.IsNullOrEmpty(name)) continue;

                    string key = name.ToLowerInvariant();
                    Color? col = TouIntegration.IsTouPresent ? TouIntegration.GetModifierColour(mod) : null;
                    string hex = ColorUtility.ToHtmlStringRGB(col ?? Color.magenta);

                    _modifierColors[key] = hex;

                    string? icon = TryGetModifierIcon(mod);
                    if (!string.IsNullOrEmpty(icon))
                    {
                        _modifierIcons[key] = icon;
                    }

                    count++;
                }
            }

            // 2. Scan assemblies for any additional BaseModifier or TouBaseGameModifier implementations
            Type? touBaseModType = TouIntegration.IsTouPresent ? TouIntegration.GetTouBaseGameModifierType() : null;

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

                    bool isBaseMod = typeof(BaseModifier).IsAssignableFrom(type);
                    bool isTouMod = touBaseModType != null && touBaseModType.IsAssignableFrom(type);

                    if (!isBaseMod && !isTouMod) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    object? instance;
                    try
                    {
                        instance = Activator.CreateInstance(type);
                    }
                    catch
                    {
                        continue;
                    }

                    if (instance == null) continue;

                    string name = GetModifierName(instance);
                    if (string.IsNullOrEmpty(name)) continue;

                    string key = name.ToLowerInvariant();
                    if (_modifierColors.ContainsKey(key)) continue;

                    Color? col = TouIntegration.IsTouPresent ? TouIntegration.GetModifierColour(instance) : null;
                    string hex = ColorUtility.ToHtmlStringRGB(col ?? Color.magenta);

                    _modifierColors[key] = hex;

                    string? icon = TryGetModifierIcon(instance);
                    if (!string.IsNullOrEmpty(icon))
                    {
                        _modifierIcons[key] = icon;
                    }

                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[ModifierColorizer] Exception during Refresh: {ex}");
        }

        Log.LogInfo($"[ModifierColorizer] Loaded {count} modifiers");
        BuildRegex();
    }

    private static string GetModifierName(object instance)
    {
        var prop = instance.GetType().GetProperty("ModifierName", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null)
        {
            try
            {
                var val = prop.GetValue(instance) as string;
                if (!string.IsNullOrWhiteSpace(val)) return StripTags(val.Trim());
            }
            catch { }
        }
        return instance.GetType().Name;
    }

    private static string? TryGetModifierIcon(object instance)
    {
        try
        {
            var configProp = instance.GetType().GetProperty("Configuration", BindingFlags.Public | BindingFlags.Instance);
            if (configProp == null) return null;

            var configObj = configProp.GetValue(instance);
            if (configObj == null) return null;

            var popupProp = configObj.GetType().GetProperty("PopUpIconTmp", BindingFlags.Public | BindingFlags.Instance);
            if (popupProp == null) return null;

            var spriteAsset = popupProp.GetValue(configObj) as UnityEngine.Object;
            if (spriteAsset != null && !string.IsNullOrEmpty(spriteAsset.name))
            {
                return $"<sprite name=\"{spriteAsset.name}\">";
            }
        }
        catch { }

        return null;
    }

    private static void BuildRegex()
    {
        if (_modifierColors == null || _modifierColors.Count == 0)
        {
            _modifierRegex = null;
            Log.LogWarning("[ModifierColorizer] No modifiers found, regex not built");
            return;
        }

        var names = _modifierColors.Keys
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ToList();

        if (names.Count == 0)
        {
            _modifierRegex = null;
            return;
        }

        var pattern = "(?i)(?<![A-Za-z0-9])(?:" + string.Join("|", names.Select(Regex.Escape)) + ")(?![A-Za-z0-9])";
        _modifierRegex = new Regex(pattern, RegexOptions.Compiled);
        Log.LogInfo($"[ModifierColorizer] Regex built with {names.Count} modifier names");
    }

    private static string ApplyPattern(string raw, MatchEvaluator replacer)
    {
        if (string.IsNullOrEmpty(raw) || _modifierRegex == null)
            return raw;

        var result = new StringBuilder(raw.Length);
        int lastIndex = 0;

        foreach (Match tagMatch in TagRegex.Matches(raw))
        {
            if (tagMatch.Index > lastIndex)
            {
                string plain = raw.Substring(lastIndex, tagMatch.Index - lastIndex);
                result.Append(_modifierRegex.Replace(plain, replacer));
            }

            result.Append(tagMatch.Value);
            lastIndex = tagMatch.Index + tagMatch.Length;
        }

        if (lastIndex < raw.Length)
        {
            result.Append(_modifierRegex.Replace(raw.Substring(lastIndex), replacer));
        }

        return result.ToString();
    }

    public static string Apply(string raw)
    {
        if (!IsReady)
        {
            Refresh();
        }

        if (_modifierColors == null || _modifierRegex == null || raw.Length == 0)
            return raw;

        if (raw.Contains("<color=", StringComparison.OrdinalIgnoreCase))
            return raw;

        return ApplyPattern(raw, m =>
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