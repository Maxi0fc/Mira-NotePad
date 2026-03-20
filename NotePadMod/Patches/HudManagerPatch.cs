using HarmonyLib;
using NotePadMod.UI;
using TownOfUs.Patches;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Reflection;
namespace NotePadMod.Patches;
[HarmonyPatch]
public static class HudManagerPatch
{
    public static GameObject? NotePadButtonObj;
    public static AspectPosition? NotePadAspectPos;
    private static Sprite? _inactiveSprite;
    private static Sprite? _activeSprite;
    private static Sprite? LoadEmbeddedSprite(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(tex, bytes);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 115f);
    }
    public static void CreateNotePadButton(HudManager instance)
    {
        if (!NotePadButtonObj)
        {
            NotePadButtonObj = Object.Instantiate(
                instance.MapButton.gameObject,
                instance.MapButton.transform.parent
            );
            NotePadButtonObj.name = "NotePadButton";
            var btn = NotePadButtonObj.GetComponent<PassiveButton>();
            btn.OnClick = new Button.ButtonClickedEvent();
            btn.OnClick.AddListener((UnityAction)NotePadWindow.Toggle);
            NotePadButtonObj.transform.Find("Background").localPosition = Vector3.zero;
            if (_inactiveSprite == null)
                _inactiveSprite = LoadEmbeddedSprite("NotePadMod.Resources.notepad_inactive.png");
            if (_activeSprite == null)
                _activeSprite = LoadEmbeddedSprite("NotePadMod.Resources.notepad_active.png");
            if (_inactiveSprite != null)
                NotePadButtonObj.transform.Find("Inactive").GetComponent<SpriteRenderer>().sprite = _inactiveSprite;
            if (_activeSprite != null)
                NotePadButtonObj.transform.Find("Active").GetComponent<SpriteRenderer>().sprite = _activeSprite;
            NotePadAspectPos = NotePadButtonObj.GetComponentInChildren<AspectPosition>();
        }
        if (NotePadButtonObj && NotePadAspectPos != null && HudManagerPatches.WikiAspectPos != null)
        {
            var dist = HudManagerPatches.WikiAspectPos.DistanceFromEdge;
            dist.x += 0.84f;
            NotePadAspectPos.DistanceFromEdge = dist;
            NotePadAspectPos.Alignment = HudManagerPatches.WikiAspectPos.Alignment;
            NotePadAspectPos.AdjustPosition();
        }
        if (NotePadButtonObj)
        {
            bool wikiVisible = HudManagerPatches.WikiButton != null && HudManagerPatches.WikiButton.activeSelf;
            NotePadButtonObj.SetActive(wikiVisible);
        }
    }
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudManagerUpdatePatch(HudManager __instance)
    {
        if (PlayerControl.LocalPlayer?.Data == null) return;
        CreateNotePadButton(__instance);
    }
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    [HarmonyPostfix]
    public static void ChatUpdatePatch()
    {
        if (NotePadWindow.IsOpen)
            NotePadWindow.ForceToFront();
    }
    // Blockera tangentbordsrörelse
    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    [HarmonyPrefix]
    public static bool KeyboardJoystickUpdatePatch()
    {
        return !NotePadWindow.IsOpen;
    }
    // Blockera zoom via FollowerCamera
    [HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
    [HarmonyPrefix]
    public static bool FollowerCameraUpdatePatch()
    {
        return !NotePadWindow.IsOpen;
    }
    // Blockera lobbyn
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    [HarmonyPrefix]
    public static bool LobbyBehaviourUpdatePatch()
    {
        return !NotePadWindow.IsOpen;
    }
}
