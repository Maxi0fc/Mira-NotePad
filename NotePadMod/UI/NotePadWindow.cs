using System.Reflection;
using NotePadMod.Assets;
using Reactor.Utilities.Attributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace NotePadMod.UI;

[RegisterInIl2Cpp]
public class NotePadWindow(nint ptr) : Minigame(ptr)
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
    private GameObject?    _panelInstance;
    private SpriteRenderer? _backgroundRenderer;
    private const float HoldDelay  = 0.4f;
    private const float HoldRepeat = 0.05f;
    private const int   MaxLines   = 13;
    private const float WindowZ       = -50f;
    private const float TextPadding   = 0.25f;
    private const float TextWidthFrac = 0.85f;
    private const float TextTopOffset = 0.26f;
    private const float LinePitch     = 1.87f;
    private const float OutlineWidth  = 0.2f;
    private static readonly Color OutlineColor = Color.black;

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
            _ => Color.white,
        };
    }

    private static string? GetPlainTextColorTag()
    {
        var settings = NotePadPlugin.Settings;
        return settings.TextColor.Value == NotepadTextColor.Black ? "#000000" : null;
    }
    public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;
    private static Vector3 GetWindowPosition() => new Vector3(0f, 0f, WindowZ);

    private static void StopLocalPlayer()
    {
        var player = PlayerControl.LocalPlayer;
        if (player?.MyPhysics?.body != null)
            player.MyPhysics.body.velocity = Vector2.zero;
    }

    public static void Toggle()
    {
        if (IsOpen) { CloseWindow(); return; }
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
        go.transform.SetParent(HudManager.Instance.transform, false);
        _instance = go.AddComponent<NotePadWindow>();
    }

    public static void Open()
    {
        EnsureInstance();
        if (_instance == null) return;

        _instance.transform.localPosition = GetWindowPosition();
        _instance.gameObject.SetActive(true);
        _instance.transform.SetAsLastSibling();
        _instance._focused = true;

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

    public static void CloseWindow()
    {
        if (_instance != null) _instance._focused = false;
        if (_instance != null) _instance.gameObject.SetActive(false);

        StopLocalPlayer();
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

        if (escape) { CloseWindow(); return; }

        if (mouseDown)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            bool insideWindow;
            if (_backgroundRenderer != null)
            {
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
        string display = _content;
        if (_focused && _cursorVisible)
            display = display.Insert(Mathf.Clamp(_cursorPos, 0, display.Length), "|");
        display = RoleColorizer.Apply(display);
        display = ModifierColorizer.Apply(display);
        string? colorTag = GetPlainTextColorTag();
        if (colorTag != null)
            display = $"<color={colorTag}>{display}</color>";

        _displayTmp.text = display;
    }
    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }
    private static void LogHierarchy(Transform t, string indent)
    {
        var comps = t.GetComponents<Component>();
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < comps.Length; i++)
            names.Append(comps[i].GetType().Name).Append(", ");
        Log.LogInfo($"{indent}{t.name} [{names}]");
        for (int i = 0; i < t.childCount; i++)
            LogHierarchy(t.GetChild(i), indent + "  ");
    }
    private const int PanelSortingOrder = 1000;

    private static void SetSortingOrderRecursively(Transform t, int order)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = order;
        var mr = t.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = order;
        for (int i = 0; i < t.childCount; i++)
            SetSortingOrderRecursively(t.GetChild(i), order);
    }

    private static void ApplyOutline(TMP_Text tmp)
    {
        var mat = tmp.fontMaterial;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        tmp.fontMaterial = mat;
        tmp.UpdateMeshPadding();
    }

    private static void ApplyOutlinesRecursively(Transform t)
    {
        var tmp3d = t.GetComponent<TextMeshPro>();
        if (tmp3d != null) ApplyOutline(tmp3d);
        var tmpUgui = t.GetComponent<TextMeshProUGUI>();
        if (tmpUgui != null) ApplyOutline(tmpUgui);
        for (int i = 0; i < t.childCount; i++)
            ApplyOutlinesRecursively(t.GetChild(i));
    }

    private void Start()
    {
        gameObject.layer = 5;
        var prefab = NotepadAssets.Notepad.LoadAsset();
        if (prefab == null)
        {
            Log.LogError("Notepad prefab failed to load from the bundle!");
            return;
        }

        _panelInstance = Object.Instantiate(prefab, transform);
        _panelInstance.name = "Panel";
        _panelInstance.transform.localPosition = Vector3.zero;
        _panelInstance.transform.localScale = Vector3.one;
        SetLayerRecursively(_panelInstance, 5);

        LogHierarchy(_panelInstance.transform, "");

        var backgroundT = _panelInstance.transform.Find("Background");
        _backgroundRenderer = backgroundT != null ? backgroundT.GetComponent<SpriteRenderer>() : null;
        SetSortingOrderRecursively(_panelInstance.transform, PanelSortingOrder);
        ApplyOutlinesRecursively(_panelInstance.transform);
        var template = HudManager.Instance?.Chat?.freeChatField?.textArea;
        if (template == null)
        {
            Log.LogError("Chat template missing!");
        }
        else
        {
            var dispGo = Object.Instantiate(template.outputText.gameObject, transform);
            dispGo.name  = "NoteText";
            dispGo.layer = 5;
            dispGo.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            _displayTmp = dispGo.GetComponent<TextMeshPro>();
            if (_displayTmp != null)
            {
                _displayTmp.fontSize            = 3.5f;
                _displayTmp.color               = GetTextColor();
                _displayTmp.enableWordWrapping   = true;
                _displayTmp.overflowMode         = TextOverflowModes.Overflow;
                _displayTmp.enableAutoSizing     = false;
                _displayTmp.alignment            = TextAlignmentOptions.TopLeft;
                _displayTmp.richText             = true;
                _displayTmp.text                 = "";
                _displayTmp.sortingOrder         = 1002;
                ApplyOutline(_displayTmp);

                var dispScale = dispGo.transform.localScale.y;
                _displayTmp.text = "A\nA";
                _displayTmp.ForceMeshUpdate();
                var naturalLineHeight = _displayTmp.textInfo.lineInfo[0].lineHeight;
                _displayTmp.lineSpacing = 1f;
                _displayTmp.m_lineHeight = naturalLineHeight;
                _displayTmp.m_lineSpacing = _displayTmp.lineSpacing;
                _displayTmp.m_lineOffset = _displayTmp.lineSpacing / 2f;
                _displayTmp.text = "";

                float textWidth = 3.5f;
                Vector3 textLocalPos = new Vector3(-1.8f, 1.0f - TextTopOffset, -0.1f);

                if (_backgroundRenderer != null)
                {
                    var b = _backgroundRenderer.bounds;
                    Vector3 topLeftWorld = new Vector3(b.min.x + TextPadding, b.max.y - TextPadding - TextTopOffset, b.center.z);
                    var localTopLeft = transform.InverseTransformPoint(topLeftWorld);
                    textLocalPos = new Vector3(localTopLeft.x, localTopLeft.y, -0.1f);
                    textWidth = b.size.x * TextWidthFrac;
                }

                dispGo.transform.localPosition = textLocalPos;

                var rt = _displayTmp.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.pivot     = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(textWidth / dispGo.transform.localScale.x, 20f);
                }
            }
        }
        var closeButton = _panelInstance.transform.Find("CloseButton")?.GetComponent<PassiveButton>();
        if (closeButton != null)
        {
            closeButton.OnClick = new Button.ButtonClickedEvent();
            closeButton.OnClick.AddListener((UnityAction)CloseWindow);
        }

        UpdateDisplay();
    }

    private void OnDestroy() => _instance = null;
}