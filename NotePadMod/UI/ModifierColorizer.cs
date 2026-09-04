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
    private static Dictionary<string, string>? _modifierColors;
    private static Dictionary<string, string>? _modifierIcons;
    private static Regex? _modifierRegex;

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
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
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