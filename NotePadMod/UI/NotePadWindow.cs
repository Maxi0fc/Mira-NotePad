using System.Reflection;
using Reactor.Utilities.Attributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using BepInEx.Configuration;

namespace NotePadMod.UI;

[RegisterInIl2Cpp]
public class NotePadWindow(nint ptr) : MonoBehaviour(ptr)
{
    private static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("NotePad");
    private static NotePadWindow? _instance;
    private static float _lastToggle = -1f;
    private string _content = "";
    private int _cursorPos = 0;
    private bool _focused = false;
    private float _cursorBlink = 0f;
    private bool _cursorVisible = true;
    private TextMeshPro? _displayTmp;
    private float _backspaceHeld = 0f;
    private float _deleteHeld = 0f;
    private const float HoldDelay = 0.4f;
    private const float HoldRepeat = 0.05f;
    private const int MaxLines = 13;

    // ── Layout ───────────────────────────────────────────────────────────────
    // These match the ORIGINAL working values exactly (BgScale 0.5, text at -1.8/1.0).
    // The ruled lines are tuned to sit inside that known-good layout.
    private const float WindowX    =  0.4f;
    private const float WindowY    =  1.5f;
    private const float WindowZ    = -50f;
    private const float BgScale    =  0.5f;   // original — do not change without re-tuning lines

    // Text area local position (original working values)
    private const float TextLocalX = -1.8f;
    private const float TextLocalY =  1.0f;
    private const float TextScale  =  0.7f;
    private const float TextWidth  =  3.4f;

    // Ruled lines — tuned to sit inside the panel at BgScale 0.5.
    // TMP fontSize 2.2 * TextScale 0.7 gives ~0.215 world-unit line height.
    // Text top is at TextLocalY (1.0), first baseline is ~0.15 below that.
    private const float RuledFirstY  =  0.83f;
    private const float RuledSpacing =  0.215f;
    private const float RuledWidth   =  2.55f;   // fits inside panel at scale 0.5
    private const float RuledLocalX  = -0.58f;   // horizontally centred under text

    public static ConfigEntry<bool>? ColorBlack;
    public static ConfigEntry<bool>? ColorWhite;
    public static ConfigEntry<bool>? ColorRed;
    public static ConfigEntry<bool>? ColorYellow;
    public static ConfigEntry<bool>? ColorGreen;
    public static ConfigEntry<bool>? ColorCyan;
    public static ConfigEntry<bool>? ColorGrey;
    public static ConfigEntry<bool>? WindowGrey;
    public static ConfigEntry<bool>? WindowBlack;

    public static void InitConfig(ConfigFile config)
    {
        ColorBlack  = config.Bind("TextColor", "Black",  true,  "Use black text");
        ColorWhite  = config.Bind("TextColor", "White",  false, "Use white text");
        ColorRed    = config.Bind("TextColor", "Red",    false, "Use red text");
        ColorYellow = config.Bind("TextColor", "Yellow", false, "Use yellow text");
        ColorGreen  = config.Bind("TextColor", "Green",  false, "Use green text");
        ColorCyan   = config.Bind("TextColor", "Cyan",   false, "Use cyan text");
        ColorGrey   = config.Bind("TextColor", "Grey",   false, "Use grey text");
        WindowGrey  = config.Bind("WindowSkin", "Grey",  true,  "Use grey notepad window");
        WindowBlack = config.Bind("WindowSkin", "Black", false, "Use black notepad window");
    }

    private static Color GetTextColor()
    {
        // IMPORTANT: TMP applies .color as a vertex color multiplier over any
        // <color> tags. Returning Color.black here would make all role-name
        // color tags render as black. We use Color.black only for plain text
        // and inject it via a <color> tag wrapper in UpdateDisplay instead,
        // keeping the TMP component color at white so rich-text tags are unaffected.
        if (ColorRed?.Value    == true) return Color.red;
        if (ColorYellow?.Value == true) return Color.yellow;
        if (ColorGreen?.Value  == true) return Color.green;
        if (ColorCyan?.Value   == true) return Color.cyan;
        if (ColorGrey?.Value   == true) return Color.grey;
        // White and Black both use white component color; black text is applied
        // via the <color> wrapper so role highlights still show through.
        return Color.white;
    }

    private static string GetWindowSpriteName() =>
        WindowBlack?.Value == true
            ? "NotePadMod.Resources.notepad_window_black.png"
            : "NotePadMod.Resources.notepad_window.png";

    public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

    public static void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (Time.time - _lastToggle < 0.3f) return;
        _lastToggle = Time.time;
        Open();
    }

    public static void Open()
    {
        if (_instance == null)
        {
            var go = new GameObject("NotePadWindow");
            go.transform.SetParent(HudManager.Instance.Chat.transform.parent, false);
            go.transform.localPosition = new Vector3(WindowX, WindowY, WindowZ);
            _instance = go.AddComponent<NotePadWindow>();
        }
        _instance.gameObject.SetActive(true);
        _instance.transform.SetAsLastSibling();
        _instance._focused = true;

        // Flush held inputs so movement/zoom doesn't carry over when notepad opens
        Input.ResetInputAxes();
    }

    public static void Close()
    {
        if (_instance != null) _instance._focused = false;
        _instance?.gameObject.SetActive(false);

        // Flush again on close so game doesn't lurch when input is re-enabled
        Input.ResetInputAxes();
    }

    public static void ClearText()
    {
        if (_instance != null)
        {
            _instance._content = "";
            _instance._cursorPos = 0;
            _instance.UpdateDisplay();
        }
    }

    public static void ForceToFront() => _instance?.transform.SetAsLastSibling();

    private int GetLineCount(string text)
    {
        if (_displayTmp == null) return 1;
        // Save and restore so we don't wipe the colored display string
        string saved = _displayTmp.text;
        _displayTmp.text = text;
        _displayTmp.ForceMeshUpdate();
        int count = _displayTmp.textInfo.lineCount;
        _displayTmp.text = saved;
        _displayTmp.ForceMeshUpdate();
        return count;
    }

    private void Update()
    {
        if (!IsOpen) return;

        bool mouseDown     = Input.GetMouseButtonDown(0);
        bool leftArrow     = Input.GetKeyDown(KeyCode.LeftArrow);
        bool rightArrow    = Input.GetKeyDown(KeyCode.RightArrow);
        bool upArrow       = Input.GetKeyDown(KeyCode.UpArrow);
        bool downArrow     = Input.GetKeyDown(KeyCode.DownArrow);
        bool home          = Input.GetKeyDown(KeyCode.Home);
        bool end           = Input.GetKeyDown(KeyCode.End);
        bool backspace     = Input.GetKeyDown(KeyCode.Backspace);
        bool backspaceHeld = Input.GetKey(KeyCode.Backspace);
        bool delete        = Input.GetKeyDown(KeyCode.Delete);
        bool deleteHeld    = Input.GetKey(KeyCode.Delete);
        bool enter         = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool escape        = Input.GetKeyDown(KeyCode.Escape);
        string typed       = Input.inputString;

        if (escape) { Close(); return; }

        if (mouseDown)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3 localClick = transform.InverseTransformPoint(mouseWorld);
            if (Mathf.Abs(localClick.x) < 2.1f && Mathf.Abs(localClick.y) < 1.9f)
            {
                _focused = true;
                PlaceCursorAtMouse();
            }
            else
            {
                _focused = false;
                return;
            }
        }

        if (!_focused) return;
        Input.ResetInputAxes();

        _cursorBlink += Time.deltaTime;
        if (_cursorBlink > 0.5f)
        {
            _cursorBlink = 0f;
            _cursorVisible = !_cursorVisible;
            UpdateDisplay();
        }

        bool changed = false;

        if (leftArrow && _cursorPos > 0)
        { _cursorPos--; _cursorVisible = true; _cursorBlink = 0f; changed = true; }
        if (rightArrow && _cursorPos < _content.Length)
        { _cursorPos++; _cursorVisible = true; _cursorBlink = 0f; changed = true; }
        if (upArrow)
        { MoveCursorVertical(-1); _cursorVisible = true; _cursorBlink = 0f; changed = true; }
        if (downArrow)
        { MoveCursorVertical(1); _cursorVisible = true; _cursorBlink = 0f; changed = true; }

        if (home)
        {
            int start = _cursorPos;
            while (start > 0 && _content[start - 1] != '\n') start--;
            _cursorPos = start; changed = true;
        }
        if (end)
        {
            int endPos = _cursorPos;
            while (endPos < _content.Length && _content[endPos] != '\n') endPos++;
            _cursorPos = endPos; changed = true;
        }

        if (backspaceHeld)
        {
            _backspaceHeld += Time.deltaTime;
            bool doIt = backspace || (_backspaceHeld > HoldDelay && ((_backspaceHeld - HoldDelay) % HoldRepeat) < Time.deltaTime);
            if (doIt && _cursorPos > 0)
            {
                _content = _content.Remove(_cursorPos - 1, 1);
                _cursorPos--;
                _cursorVisible = true; _cursorBlink = 0f; changed = true;
            }
        }
        else { _backspaceHeld = 0f; }

        if (deleteHeld)
        {
            _deleteHeld += Time.deltaTime;
            bool doIt = delete || (_deleteHeld > HoldDelay && ((_deleteHeld - HoldDelay) % HoldRepeat) < Time.deltaTime);
            if (doIt && _cursorPos < _content.Length)
            {
                _content = _content.Remove(_cursorPos, 1);
                _cursorVisible = true; _cursorBlink = 0f; changed = true;
            }
        }
        else { _deleteHeld = 0f; }

        if (enter)
        {
            string newContent = _content.Insert(_cursorPos, "\n");
            if (GetLineCount(newContent) <= MaxLines)
            {
                _content = newContent;
                _cursorPos++;
                _cursorVisible = true; _cursorBlink = 0f; changed = true;
            }
        }

        foreach (char c in typed)
        {
            if (c == '\b' || c == '\r' || c == '\n') continue;
            string newContent = _content.Insert(_cursorPos, c.ToString());
            if (GetLineCount(newContent) <= MaxLines)
            {
                _content = newContent;
                _cursorPos++;
                _cursorVisible = true; _cursorBlink = 0f; changed = true;
            }
        }

        if (changed) UpdateDisplay();
    }

    private void MoveCursorVertical(int dir)
    {
        if (_displayTmp == null || _content.Length == 0) return;
        _displayTmp.text = _content;
        _displayTmp.ForceMeshUpdate();
        var info = _displayTmp.textInfo;
        int lineCount = info.lineCount;
        if (lineCount <= 1) return;
        int curLine = 0;
        for (int i = 0; i < lineCount; i++)
        {
            int first = info.lineInfo[i].firstCharacterIndex;
            int last  = info.lineInfo[i].lastCharacterIndex;
            if (_cursorPos >= first && _cursorPos <= last)
            { curLine = i; break; }
        }
        int targetLine = Mathf.Clamp(curLine + dir, 0, lineCount - 1);
        int col = _cursorPos - info.lineInfo[curLine].firstCharacterIndex;
        int newPos = info.lineInfo[targetLine].firstCharacterIndex + Mathf.Min(col, info.lineInfo[targetLine].characterCount - 1);
        _cursorPos = Mathf.Clamp(newPos, 0, _content.Length);
    }

    private void PlaceCursorAtMouse()
    {
        if (_displayTmp == null) return;
        _displayTmp.text = _content;
        _displayTmp.ForceMeshUpdate();
        var info = _displayTmp.textInfo;
        if (info.characterCount == 0) { _cursorPos = 0; return; }
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = _displayTmp.transform.position.z;
        Vector3 localMouse = _displayTmp.transform.InverseTransformPoint(mouseWorld);
        float minDist = float.MaxValue;
        int bestChar = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            var charInfo = info.characterInfo[i];
            if (!charInfo.isVisible) continue;
            Vector3 charCenter = (charInfo.bottomLeft + charInfo.topRight) * 0.5f;
            float dist = Vector2.Distance(localMouse, charCenter);
            if (dist < minDist)
            {
                minDist = dist;
                bestChar = localMouse.x > charCenter.x ? i + 1 : i;
            }
        }
        _cursorPos = Mathf.Clamp(bestChar, 0, _content.Length);
        _cursorVisible = true; _cursorBlink = 0f;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_displayTmp == null) return;

        // Apply role coloring first
        string display = RoleColorizer.Apply(_content);

        // Insert cursor before wrapping
        if (_focused && _cursorVisible)
        {
            int taggedCursorPos = MapCursorToTaggedString(_content, display, _cursorPos);
            display = display.Insert(taggedCursorPos, "|");
        }

        // Wrap everything in the user's chosen base color so plain text matches
        // their config choice. Role <color> tags will override this locally.
        // This avoids setting _displayTmp.color to black (which would kill all
        // rich-text color tags as a vertex multiplier).
        string baseHex = GetBaseColorHex();
        _displayTmp.text = $"<color=#{baseHex}>{display}</color>";
    }

    private static string GetBaseColorHex()
    {
        if (ColorRed?.Value    == true) return "FF0000";
        if (ColorYellow?.Value == true) return "FFFF00";
        if (ColorGreen?.Value  == true) return "00FF00";
        if (ColorCyan?.Value   == true) return "00FFFF";
        if (ColorGrey?.Value   == true) return "808080";
        if (ColorWhite?.Value  == true) return "FFFFFF";
        return "000000"; // default black
    }

    private static int MapCursorToTaggedString(string plain, string tagged, int cursorInPlain)
    {
        int p = 0;
        int t = 0;

        while (p < cursorInPlain && t < tagged.Length)
        {
            if (tagged[t] == '<')
            {
                int close = tagged.IndexOf('>', t);
                t = close >= 0 ? close + 1 : tagged.Length;
            }
            else
            {
                p++;
                t++;
            }
        }

        while (t < tagged.Length && tagged[t] == '<')
        {
            int close = tagged.IndexOf('>', t);
            t = close >= 0 ? close + 1 : tagged.Length;
        }

        return t;
    }

    /// <summary>
    /// Draws horizontal ruled lines as children of the window transform.
    /// Because they are children, all positions are in LOCAL space and will
    /// always stay inside the panel regardless of where the window is placed.
    /// </summary>
    private void CreateRuledLines()
    {
        // 1×1 pixel texture — localScale.x/y control the actual size in local units
        var lineTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        lineTex.SetPixel(0, 0, new Color(0.45f, 0.45f, 0.45f, 0.40f));
        lineTex.Apply();
        // pixels-per-unit = 1 so localScale directly equals world/local size
        var lineSprite = Sprite.Create(lineTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        for (int i = 0; i < MaxLines; i++)
        {
            var go = new GameObject($"RuledLine_{i}");
            go.transform.SetParent(transform, false);
            go.layer = 5;

            float localY = RuledFirstY - i * RuledSpacing;
            go.transform.localPosition = new Vector3(RuledLocalX, localY, -0.05f);
            go.transform.localScale    = new Vector3(RuledWidth, 0.012f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = lineSprite;
            sr.sortingOrder = 1001; // above BG (1000), below text (1002)
        }
    }

    private void Start()
    {
        gameObject.layer = 5;

        // ── Background ───────────────────────────────────────────────────────
        var bgSprite = LoadSprite(GetWindowSpriteName());
        if (bgSprite != null)
        {
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            bgGo.transform.localScale    = new Vector3(BgScale, BgScale, 1f);
            bgGo.layer = 5;
            var sr = bgGo.AddComponent<SpriteRenderer>();
            sr.sprite       = bgSprite;
            sr.sortingOrder = 1000;
        }

        // ── Text display ─────────────────────────────────────────────────────
        var template = HudManager.Instance?.Chat?.freeChatField?.textArea;
        if (template == null) { Log.LogError("Mall saknas!"); return; }

        var dispGo = Object.Instantiate(template.outputText.gameObject, transform);
        dispGo.name = "NoteText";
        dispGo.layer = 5;
        dispGo.transform.localPosition = new Vector3(TextLocalX, TextLocalY, -0.1f);
        dispGo.transform.localScale    = new Vector3(TextScale, TextScale, 1f);
        _displayTmp = dispGo.GetComponent<TextMeshPro>();
        if (_displayTmp != null)
        {
            _displayTmp.fontSize         = 2.2f;
            _displayTmp.color            = Color.white; // keep white so <color> tags are not multiplied away
            _displayTmp.enableWordWrapping = true;
            _displayTmp.overflowMode     = TextOverflowModes.Overflow;
            _displayTmp.enableAutoSizing = false;
            _displayTmp.alignment        = TextAlignmentOptions.TopLeft;
            _displayTmp.richText         = true;
            _displayTmp.text             = "";
            _displayTmp.sortingOrder     = 1002;
            var rt = _displayTmp.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot    = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(TextWidth, 20f);
            }
        }

        // ── Ruled lines ──────────────────────────────────────────────────────
        CreateRuledLines();

        // ── Clear button ─────────────────────────────────────────────────────
        var clearSprite      = LoadSprite("NotePadMod.Resources.notepad_clear.png");
        var clearHoverSprite = LoadSprite("NotePadMod.Resources.notepad_clear_hover.png");
        if (clearSprite != null)
        {
            var btnGo = new GameObject("ClearButton");
            btnGo.transform.SetParent(transform, false);
            btnGo.transform.localPosition = new Vector3(0.65f, -1.55f, -0.2f);
            btnGo.transform.localScale    = new Vector3(0.24f, 0.24f, 1f);
            btnGo.layer = 5;
            var sr = btnGo.AddComponent<SpriteRenderer>();
            sr.sprite       = clearSprite;
            sr.sortingOrder = 1002;
            var bcol = btnGo.AddComponent<BoxCollider2D>();
            bcol.size = new Vector2(1.5f, 0.5f);
            var bpb = btnGo.AddComponent<PassiveButton>();
            bpb.OnClick     = new Button.ButtonClickedEvent();
            bpb.OnMouseOver = new UnityEvent();
            bpb.OnMouseOut  = new UnityEvent();
            bpb.OnClick.AddListener((UnityAction)ClearText);
            if (clearHoverSprite != null)
            {
                bpb.OnMouseOver.AddListener((UnityAction)(() => sr.sprite = clearHoverSprite));
                bpb.OnMouseOut.AddListener((UnityAction)(() => sr.sprite = clearSprite));
            }
        }

        UpdateDisplay();
    }

    private void OnDestroy() => _instance = null;

    private static Sprite? LoadSprite(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(name);
        if (s == null) return null;
        var b = new byte[s.Length];
        s.Read(b, 0, b.Length);
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(t, b);
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
    }
}