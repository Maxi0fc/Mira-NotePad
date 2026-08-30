using System.Reflection;
using NotePadMod.Assets;
using Reactor.Utilities.Attributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace NotePadMod.UI;

[RegisterInIl2Cpp]
public class NotePadWindow(nint ptr) : MonoBehaviour(ptr)
{
    private static readonly BepInEx.Logging.ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("NotePad");

    private static NotePadWindow? _instance;
    private static float _lastToggle = -1f;

    private string _content  = "";
    private int    _cursorPos = 0;
    private bool   _focused  = false;
    private float  _cursorBlink  = 0f;
    private bool   _cursorVisible = true;
    private TextMeshPro? _displayTmp;
    private float _backspaceHeld = 0f;
    private float _deleteHeld    = 0f;

    // The bundle-authored "Notepad" prefab instance (Background, CloseButton,
    // Title, Textbox, Lines) and its background renderer, used both for the
    // window-skin swap and for computing click-inside/outside bounds.
    private GameObject?    _panelInstance;
    private SpriteRenderer? _backgroundRenderer;

    private const float HoldDelay  = 0.4f;
    private const float HoldRepeat = 0.05f;
    private const int   MaxLines   = 13;

    // Window layout
    private const float WindowZ    = -50f;
    private const float TextWidth  =  3.5f;

    /// <summary>
    /// Returns the color to set on the TMP component itself.
    /// Black is handled via a rich-text wrapper instead (so that
    /// <color> tags inside the text are not multiplied).
    /// </summary>
    private static Color GetTextColor()
    {
        var settings = NotePadPlugin.Settings;
        return settings.TextColor.Value switch
        {
            NotepadTextColor.Red    => Color.red,
            NotepadTextColor.Yellow => Color.yellow,
            NotepadTextColor.Green  => Color.green,
            NotepadTextColor.Cyan   => Color.cyan,
            NotepadTextColor.Grey   => Color.grey,
            // White and Black: keep TMP component white so <color> tags work
            _ => Color.white,
        };
    }

    /// <summary>
    /// Returns a hex color string to wrap plain text in, or null when the TMP
    /// component color already represents the intended color.
    /// </summary>
    private static string? GetPlainTextColorTag()
    {
        var settings = NotePadPlugin.Settings;
        return settings.TextColor.Value == NotepadTextColor.Black ? "#000000" : null;
    }

    private static string GetWindowResourceName()
    {
        var settings = NotePadPlugin.Settings;
        return settings.WindowSkin.Value == NotepadWindowSkin.Black
            ? "NotePadMod.Resources.notepad_window_black.png"
            : "NotePadMod.Resources.notepad_window.png";
    }

    // ── Public state ──────────────────────────────────────────────────────────

    public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

    /// <summary>
    /// Computes the window's local position so it appears just below-left of the
    /// notepad button, regardless of which HUD row it's currently sitting in.
    /// </summary>
    private static Vector3 GetWindowPositionRelativeToButton()
    {
        var btn = Patches.HudManagerPatch.NotePadButtonObj;
        var parent = HudManager.Instance.Chat.transform.parent;

        if (btn != null && parent != null)
        {
            // Convert the button's world position into the window parent's local space.
            Vector3 btnLocal = parent.InverseTransformPoint(btn.transform.position);
            // Offset so the window appears below and to the left of the button.
            return new Vector3(btnLocal.x - 1.5f, btnLocal.y - 1.0f, WindowZ);
        }

        // Fallback if button isn't ready yet.
        return new Vector3(0.4f, 1.5f, WindowZ);
    }

    /// <summary>
    /// Immediately zeros the local player's rigidbody velocity so they don't
    /// keep sliding when the notepad is opened or closed.
    /// </summary>
    private static void StopLocalPlayer()
    {
        var player = PlayerControl.LocalPlayer;
        if (player?.MyPhysics?.body != null)
            player.MyPhysics.body.velocity = Vector2.zero;
    }

    public static void Toggle()
    {
        if (IsOpen) { Close(); return; }
        if (Time.time - _lastToggle < 0.3f) return;
        _lastToggle = Time.time;
        Open();
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        if (HudManager.Instance == null) return;

        var go = new GameObject("NotePadWindow");
        go.SetActive(false);
        go.transform.SetParent(HudManager.Instance.Chat.transform.parent, false);
        _instance = go.AddComponent<NotePadWindow>();
    }

    public static void Open()
    {
        EnsureInstance();
        if (_instance == null) return;

        _instance.transform.localPosition = GetWindowPositionRelativeToButton();
        _instance.gameObject.SetActive(true);
        _instance.transform.SetAsLastSibling();
        _instance._focused = true;

        // Stop the player in place and flush held inputs so movement/zoom
        // doesn't carry over into the notepad session.
        StopLocalPlayer();
        Input.ResetInputAxes();
    }

    public static void AppendText(string text)
    {
        EnsureInstance();
        if (_instance == null) return;

        string separator = _instance._content.Length > 0 ? "\n" : "";
        string newContent = _instance._content + separator + text;

        if (_instance.GetLineCount(newContent) <= MaxLines)
            _instance._content = newContent;

        _instance._cursorPos = _instance._content.Length;
        _instance.UpdateDisplay();
    }

    public static void Close()
    {
        if (_instance != null) _instance._focused = false;
        _instance?.gameObject.SetActive(false);

        // Stop the player again so they don't lurch forward when control
        // is returned (FixedUpdate will resume next frame).
        StopLocalPlayer();

        // Flush again so the game doesn't lurch when input is re-enabled.
        Input.ResetInputAxes();
    }

    public static void ClearText()
    {
        if (_instance == null) return;
        _instance._content   = "";
        _instance._cursorPos = 0;
        _instance.UpdateDisplay();
    }

    public static void ForceToFront() => _instance?.transform.SetAsLastSibling();

    // ── Internal helpers ──────────────────────────────────────────────────────

    private int GetLineCount(string text)
    {
        if (_displayTmp == null) return 1;
        string saved = _displayTmp.text;
        _displayTmp.text = text;
        _displayTmp.ForceMeshUpdate();
        int count = _displayTmp.textInfo.lineCount;
        _displayTmp.text = saved;
        return count;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

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

            bool insideWindow;
            if (_backgroundRenderer != null)
            {
                // Use the bundled Background sprite's actual world-space bounds
                // so this keeps working regardless of the prefab's authored size.
                insideWindow = _backgroundRenderer.bounds.Contains(
                    new Vector3(mouseWorld.x, mouseWorld.y, _backgroundRenderer.bounds.center.z));
            }
            else
            {
                Vector3 localClick = transform.InverseTransformPoint(mouseWorld);
                insideWindow = Mathf.Abs(localClick.x) < 2.1f && Mathf.Abs(localClick.y) < 1.9f;
            }

            if (insideWindow)
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

        // Keep TMP color in sync with the setting (may be changed while open).
        if (_displayTmp != null)
            _displayTmp.color = GetTextColor();

        _cursorBlink += Time.deltaTime;
        if (_cursorBlink > 0.5f)
        {
            _cursorBlink   = 0f;
            _cursorVisible = !_cursorVisible;
            UpdateDisplay();
        }

        bool changed = false;

        if (leftArrow  && _cursorPos > 0)             { _cursorPos--; _cursorVisible = true; _cursorBlink = 0f; changed = true; }
        if (rightArrow && _cursorPos < _content.Length){ _cursorPos++; _cursorVisible = true; _cursorBlink = 0f; changed = true; }
        if (upArrow)   { MoveCursorVertical(-1); _cursorVisible = true; _cursorBlink = 0f; changed = true; }
        if (downArrow) { MoveCursorVertical( 1); _cursorVisible = true; _cursorBlink = 0f; changed = true; }

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

        // Backspace with hold-to-repeat
        if (backspaceHeld)
        {
            _backspaceHeld += Time.deltaTime;
            bool doIt = backspace || (_backspaceHeld > HoldDelay &&
                        ((_backspaceHeld - HoldDelay) % HoldRepeat) < Time.deltaTime);
            if (doIt && _cursorPos > 0)
            {
                _content   = _content.Remove(_cursorPos - 1, 1);
                _cursorPos--;
                _cursorVisible = true; _cursorBlink = 0f; changed = true;
            }
        }
        else { _backspaceHeld = 0f; }

        // Delete with hold-to-repeat
        if (deleteHeld)
        {
            _deleteHeld += Time.deltaTime;
            bool doIt = delete || (_deleteHeld > HoldDelay &&
                        ((_deleteHeld - HoldDelay) % HoldRepeat) < Time.deltaTime);
            if (doIt && _cursorPos < _content.Length)
            {
                _content   = _content.Remove(_cursorPos, 1);
                _cursorVisible = true; _cursorBlink = 0f; changed = true;
            }
        }
        else { _deleteHeld = 0f; }

        // Enter — new line
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

        // Printable characters
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
        var info      = _displayTmp.textInfo;
        int lineCount = info.lineCount;
        if (lineCount <= 1) return;

        int curLine = 0;
        for (int i = 0; i < lineCount; i++)
        {
            int first = info.lineInfo[i].firstCharacterIndex;
            int last  = info.lineInfo[i].lastCharacterIndex;
            if (_cursorPos >= first && _cursorPos <= last) { curLine = i; break; }
        }

        int targetLine = Mathf.Clamp(curLine + dir, 0, lineCount - 1);
        int col    = _cursorPos - info.lineInfo[curLine].firstCharacterIndex;
        int newPos = info.lineInfo[targetLine].firstCharacterIndex
                   + Mathf.Min(col, info.lineInfo[targetLine].characterCount - 1);
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
                minDist  = dist;
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

        // Insert cursor caret into the plain string before colorising.
        string display = _content;
        if (_focused && _cursorVisible)
            display = display.Insert(Mathf.Clamp(_cursorPos, 0, display.Length), "|");

        // Apply role-name color tags.
        display = RoleColorizer.Apply(display);
        display = ModifierColorizer.Apply(display);
        string? colorTag = GetPlainTextColorTag();
        if (colorTag != null)
            display = $"<color={colorTag}>{display}</color>";

        _displayTmp.text = display;
    }

    /// <summary>Recursively sets a GameObject and all its children to the given layer.</summary>
    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>
    /// Swaps the bundled Background sprite for one of the two embedded window
    /// skins (grey/black) according to the current setting. The bundle only
    /// ships the single "freePlay_base" look, so the skin option is applied
    /// as a texture swap on top of it, same as it was applied before porting.
    /// </summary>
    private void ApplyWindowSkin()
    {
        if (_backgroundRenderer == null) return;
        var skinSprite = LoadSprite(GetWindowResourceName());
        if (skinSprite != null)
            _backgroundRenderer.sprite = skinSprite;
    }

    private void Start()
    {
        gameObject.layer = 5;

        // ── Bundled panel (Background, CloseButton, Title, Textbox, Lines) ──────
        var prefab = NotepadAssets.Notepad.LoadAsset();
        if (prefab == null)
        {
            Log.LogError("Notepad prefab failed to load from the bundle!");
            return;
        }

        _panelInstance = Object.Instantiate(prefab, transform);
        _panelInstance.name = "Panel";
        _panelInstance.transform.localPosition = Vector3.zero;
        // The bundle was authored in a different project's HUD scale; if it
        // renders too large/small in-game, tune this scale factor.
        _panelInstance.transform.localScale = Vector3.one;
        SetLayerRecursively(_panelInstance, 5);

        var backgroundT = _panelInstance.transform.Find("Background");
        _backgroundRenderer = backgroundT != null ? backgroundT.GetComponent<SpriteRenderer>() : null;
        ApplyWindowSkin();

        // ── Text field (bundle's Textbox/Text_TMP) ──────────────────────────────
        var textboxT = _panelInstance.transform.Find("Textbox");
        var textTmpT = textboxT != null ? textboxT.Find("Text_TMP") : null;
        _displayTmp = textTmpT != null ? textTmpT.GetComponent<TextMeshPro>() : null;

        if (_displayTmp == null)
        {
            Log.LogError("Text_TMP missing from the Notepad prefab!");
        }
        else
        {
            if (_displayTmp.spriteAsset == null)
                _displayTmp.spriteAsset = TMP_Settings.defaultSpriteAsset;

            _displayTmp.color              = GetTextColor();
            _displayTmp.enableWordWrapping  = true;
            _displayTmp.overflowMode        = TextOverflowModes.Overflow;
            _displayTmp.enableAutoSizing    = false;
            _displayTmp.alignment           = TextAlignmentOptions.TopLeft;
            _displayTmp.richText            = true;
            _displayTmp.text                = "";

            var rt = _displayTmp.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = new Vector2(TextWidth, rt.sizeDelta.y);
        }

        // ── Click-to-focus (bundle's Textbox already carries a PassiveButton) ───
        var textboxButton = textboxT != null ? textboxT.GetComponent<PassiveButton>() : null;
        if (textboxButton != null)
        {
            textboxButton.OnClick = new Button.ButtonClickedEvent();
            textboxButton.OnClick.AddListener((UnityAction)(() =>
            {
                _focused = true;
                PlaceCursorAtMouse();
            }));
        }

        // ── Close button (bundle's CloseButton already carries a PassiveButton) ─
        var closeButton = _panelInstance.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
        if (closeButton != null)
        {
            closeButton.OnClick = new Button.ButtonClickedEvent();
            closeButton.OnClick.AddListener((UnityAction)Close);
        }

        // ── Clear button ─────────────────────────────────────────────────────────
        // Not part of the bundle (LaunchpadReloaded's notepad has no clear
        // button) — kept as its own runtime-built object, same as before porting.
        var clearSprite      = LoadSprite("NotePadMod.Resources.notepad_clear.png");
        var clearHoverSprite = LoadSprite("NotePadMod.Resources.notepad_clear_hover.png");
        if (clearSprite != null)
        {
            var btnGo = new GameObject("ClearButton");
            btnGo.transform.SetParent(transform, false);
            btnGo.transform.localPosition = new Vector3(0.6f, -1.5f, -0.2f);
            btnGo.transform.localScale    = new Vector3(0.24f, 0.24f, 1f);
            btnGo.layer = 5;

            var sr = btnGo.AddComponent<SpriteRenderer>();
            sr.sprite       = clearSprite;
            sr.sortingOrder = 1002;

            var bcol = btnGo.AddComponent<BoxCollider2D>();
            bcol.size = new Vector2(1.5f, 0.5f);

            var bpb = btnGo.AddComponent<PassiveButton>();
            bpb.OnClick    = new Button.ButtonClickedEvent();
            bpb.OnMouseOver = new UnityEvent();
            bpb.OnMouseOut  = new UnityEvent();
            bpb.OnClick.AddListener((UnityAction)ClearText);

            if (clearHoverSprite != null)
            {
                bpb.OnMouseOver.AddListener((UnityAction)(() => sr.sprite = clearHoverSprite));
                bpb.OnMouseOut.AddListener((UnityAction)(() => sr.sprite  = clearSprite));
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