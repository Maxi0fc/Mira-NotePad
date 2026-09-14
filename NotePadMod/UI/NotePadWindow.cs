using System.Reflection;
using System.Collections.Generic;
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
    private sealed class RoleInfoEntry
    {
        public string Snippet = "";
        public GameObject? DeleteButton;
    }

    private enum NoteTab
    {
        General,
        RoleInfo,
    }

    private static readonly BepInEx.Logging.ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("NotePad");

    private static NotePadWindow? _instance;
    private static float _lastToggle = -1f;
    private string _content  = "";
    private int    _cursorPos = 0;
    private string _generalContent = "";
    private int    _generalCursorPos;
    private int    _generalFirstVisibleLine;
    private string _roleInfoContent = "";
    private readonly List<RoleInfoEntry> _roleInfoEntries = new();
    private int    _roleInfoCursorPos;
    private int    _roleInfoFirstVisibleLine;
    private NoteTab _activeTab = NoteTab.General;
    private bool   _focused  = false;
    private float  _cursorBlink  = 0f;
    private bool   _cursorVisible = true;
    private TouchScreenKeyboard? _touchKeyboard;
    private TextMeshPro? _displayTmp;
    private int _firstVisibleLine;
    private float _scrollRemainder;
    private float _backspaceHeld = 0f;
    private float _deleteHeld    = 0f;
    private GameObject?    _panelInstance;
    private GameObject?    _tabButton;
    private GameObject?    _linesObject;
    private GameObject?    _roleInfoLineOverlay;
    private SpriteRenderer? _backgroundRenderer;
    private Transform?     _scaleRoot;
    private Color _generalBackgroundColor = Color.white;
    private Sprite? _generalTabSprite;
    private Sprite? _roleInfoTabSprite;
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

    private void OpenTouchKeyboard()
    {
        if (!Application.isMobilePlatform || !TouchScreenKeyboard.isSupported) return;

        _touchKeyboard = TouchScreenKeyboard.Open(
            _content,
            TouchScreenKeyboardType.Default,
            false,
            true,
            false,
            false,
            "Notepad");
    }

    private void CloseTouchKeyboard()
    {
        if (_touchKeyboard == null) return;
        _touchKeyboard.active = false;
        _touchKeyboard = null;
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
        ApplyScale();
        _instance.gameObject.SetActive(true);
        _instance.transform.SetAsLastSibling();
        _instance._focused = true;
        _instance.OpenTouchKeyboard();

        StopLocalPlayer();
        Input.ResetInputAxes();
    }

    /// <summary>
    /// Applies the current Scale Factor local setting to the notepad window.
    /// Scales the panel, text, and side buttons together uniformly so every
    /// element keeps its position relative to the notepad window.
    /// </summary>
    public static void ApplyScale()
    {
        if (_instance == null || _instance._scaleRoot == null) return;

        float scale = Mathf.Clamp(NotePadPlugin.Settings.ScaleFactor.Value, 0.1f, 1f);
        _instance._scaleRoot.localScale = Vector3.one * scale;
    }

    public static void AppendText(string text)
    {
        EnsureInstance();
        if (_instance == null) return;

        _instance.AppendToTab(NoteTab.General, text);
    }

    public static void AppendRoleInfoText(string text)
    {
        EnsureInstance();
        if (_instance == null) return;

        _instance.AppendToTab(NoteTab.RoleInfo, text);
    }

    public static void AppendRoleInfoText(string title, string message)
    {
        EnsureInstance();
        if (_instance == null) return;

        _instance.AppendRoleInfoEntry(title, message);
    }

    public static void CloseWindow()
    {
        if (_instance != null)
        {
            _instance._focused = false;
            _instance.CloseTouchKeyboard();
        }
        if (_instance != null) _instance.gameObject.SetActive(false);

        StopLocalPlayer();
        Input.ResetInputAxes();
    }

    public static void ClearText()
    {
        if (_instance == null) return;
        _instance._generalContent = "";
        _instance._generalCursorPos = 0;
        _instance._generalFirstVisibleLine = 0;
        _instance._roleInfoContent = "";
        _instance.ClearRoleInfoEntries();
        _instance._roleInfoCursorPos = 0;
        _instance._roleInfoFirstVisibleLine = 0;
        _instance.LoadActiveTab();
        _instance.UpdateDisplay();
    }

    private void AppendToTab(NoteTab tab, string text)
    {
        SaveActiveTab();

        if (tab == NoteTab.General)
        {
            string separator = _generalContent.Length > 0 ? "\n" : "";
            _generalContent += separator + text;
            _generalCursorPos = _generalContent.Length;
        }
        else
        {
            string separator = _roleInfoContent.Length > 0 ? "\n" : "";
            _roleInfoContent += separator + text;
            _roleInfoCursorPos = _roleInfoContent.Length;
        }

        LoadActiveTab();
        UpdateDisplay();
    }

    private void AppendRoleInfoEntry(string title, string message)
    {
        SaveActiveTab();
        string snippet = $"{title}\n{message}\n\n";
        _roleInfoEntries.Add(new RoleInfoEntry { Snippet = snippet });
        _roleInfoContent += (_roleInfoContent.Length > 0 ? "\n" : "") + snippet;
        _roleInfoCursorPos = _roleInfoContent.Length;
        LoadActiveTab();
        UpdateDisplay();
    }

    private void ClearRoleInfoEntries()
    {
        foreach (var entry in _roleInfoEntries)
        {
            if (entry.DeleteButton != null)
                Object.Destroy(entry.DeleteButton);
        }
        _roleInfoEntries.Clear();
    }

    private void SaveActiveTab()
    {
        if (_activeTab == NoteTab.General)
        {
            _generalContent = _content;
            _generalCursorPos = _cursorPos;
            _generalFirstVisibleLine = _firstVisibleLine;
        }
        else
        {
            _roleInfoContent = _content;
            _roleInfoCursorPos = _cursorPos;
            _roleInfoFirstVisibleLine = _firstVisibleLine;
        }
    }

    private void LoadActiveTab()
    {
        if (_activeTab == NoteTab.General)
        {
            _content = _generalContent;
            _cursorPos = _generalCursorPos;
            _firstVisibleLine = _generalFirstVisibleLine;
        }
        else
        {
            _content = _roleInfoContent;
            _cursorPos = _roleInfoCursorPos;
            _firstVisibleLine = _roleInfoFirstVisibleLine;
        }
    }

    private void SwitchTab(NoteTab tab)
    {
        if (_activeTab == tab) return;
        SaveActiveTab();
        _activeTab = tab;
        LoadActiveTab();
        _cursorVisible = true;
        _cursorBlink = 0f;
        UpdateTabVisuals();
        UpdateDisplay();
    }

    private void ClearRoleInfoText()
    {
        SaveActiveTab();
        _roleInfoContent = "";
        ClearRoleInfoEntries();
        _roleInfoCursorPos = 0;
        _roleInfoFirstVisibleLine = 0;
        LoadActiveTab();
        UpdateDisplay();
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
        float scroll       = Input.mouseScrollDelta.y;
        string typed       = Input.inputString;

        if (escape) { CloseWindow(); return; }

        if (mouseDown)
        {
            if (IsMouseOverManagedButton())
                return;

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

        if (scroll != 0f)
        {
            _scrollRemainder += scroll;
            int scrollLines = Mathf.FloorToInt(Mathf.Abs(_scrollRemainder));
            if (scrollLines > 0)
            {
                _firstVisibleLine = Mathf.Max(
                    0,
                    _firstVisibleLine - (int)Mathf.Sign(_scrollRemainder) * scrollLines);
                _scrollRemainder -= Mathf.Sign(_scrollRemainder) * scrollLines;
                UpdateDisplay(false);
            }
        }

        if (!_focused) return;
        Input.ResetInputAxes();

        if (_touchKeyboard != null)
        {
            string keyboardText = _touchKeyboard.text ?? string.Empty;
            if (keyboardText != _content)
            {
                _content = keyboardText;
                _cursorPos = _content.Length;
                _cursorVisible = true;
                _cursorBlink = 0f;
                UpdateDisplay();
            }
        }

        if (_displayTmp != null)
            _displayTmp.color = GetTextColor();
        _cursorBlink += Time.deltaTime;
        if (_cursorBlink > 0.5f)
        {
            _cursorBlink   = 0f;
            _cursorVisible = !_cursorVisible;
            UpdateDisplay(false);
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
            _content = newContent;
            _cursorPos++;
            _cursorVisible = true; _cursorBlink = 0f; changed = true;
        }
        foreach (char c in typed)
        {
            if (c == '\b' || c == '\r' || c == '\n') continue;
            string newContent = _content.Insert(_cursorPos, c.ToString());
            _content = newContent;
            _cursorPos++;
            _cursorVisible = true; _cursorBlink = 0f; changed = true;
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
        int firstVisibleChar = GetLineStart(_content, _firstVisibleLine);
        _cursorPos = Mathf.Clamp(firstVisibleChar + bestChar, 0, _content.Length);
        _cursorVisible = true; _cursorBlink = 0f;
        UpdateDisplay();
    }

    private void UpdateDisplay(bool followCursor = true)
    {
        if (_displayTmp == null) return;

        int lineCount = GetLogicalLineCount(_content);
        if (followCursor)
            EnsureCursorVisible();
        _firstVisibleLine = Mathf.Clamp(_firstVisibleLine, 0, Mathf.Max(0, lineCount - MaxLines));

        int firstChar = GetLineStart(_content, _firstVisibleLine);
        int lastLine = Mathf.Min(lineCount, _firstVisibleLine + MaxLines);
        int lastChar = lastLine >= lineCount ? _content.Length : GetLineStart(_content, lastLine);
        string visibleContent = _content.Substring(firstChar, lastChar - firstChar);

        int cursorPos = Mathf.Clamp(_cursorPos, firstChar, lastChar);
        string display = visibleContent;
        if (_focused && _cursorVisible)
            display = display.Insert(cursorPos - firstChar, "|");
        display = RoleColorizer.Apply(display);
        display = ModifierColorizer.Apply(display);
        string? colorTag = GetPlainTextColorTag();
        if (colorTag != null)
            display = $"<color={colorTag}>{display}</color>";

        _displayTmp.text = display;
        UpdateRoleInfoDeleteButtons();
    }

    private void UpdateRoleInfoDeleteButtons()
    {
        HideRoleInfoDeleteButtons();
        if (_activeTab != NoteTab.RoleInfo || _panelInstance == null || _displayTmp == null)
        {
            return;
        }

        var template = _panelInstance.transform.Find("CloseButton");
        if (template == null) return;

        var backgroundBounds = _backgroundRenderer != null
            ? _backgroundRenderer.bounds
            : new Bounds(transform.position, new Vector3(4f, 4f, 1f));
        float lineHeight = _displayTmp.textInfo.lineInfo.Length > 0
            ? _displayTmp.textInfo.lineInfo[0].lineHeight * _displayTmp.transform.lossyScale.y
            : 0.2f;
        int searchStart = 0;
        foreach (var entry in _roleInfoEntries)
        {
            int entryStart = _content.IndexOf(entry.Snippet, searchStart, System.StringComparison.Ordinal);
            if (entryStart < 0)
                continue;
            searchStart = entryStart + entry.Snippet.Length;

            int entryLine = GetLogicalLineBefore(_content, entryStart);
            int visibleLine = entryLine - _firstVisibleLine;
            if (visibleLine < 0 || visibleLine >= MaxLines)
                continue;

            if (entry.DeleteButton == null)
            {
                entry.DeleteButton = Object.Instantiate(template.gameObject, _panelInstance.transform);
                entry.DeleteButton.name = "RoleInfoDeleteButton";
                var passiveButton = entry.DeleteButton.GetComponent<PassiveButton>();
                if (passiveButton != null)
                {
                    passiveButton.OnClick = new Button.ButtonClickedEvent();
                    var capturedEntry = entry;
                    passiveButton.OnClick.AddListener((UnityAction)(() => RemoveRoleInfoEntry(capturedEntry)));
                }
                foreach (var renderer in entry.DeleteButton.GetComponentsInChildren<SpriteRenderer>(true))
    renderer.sprite = NotepadAssets.DeleteInfoSprite.LoadAsset();
                SetSortingOrderRecursively(entry.DeleteButton.transform, PanelSortingOrder + 3);
            }

            entry.DeleteButton.SetActive(true);
            float buttonY = backgroundBounds.max.y - TextPadding - TextTopOffset - (visibleLine * lineHeight);
            Vector3 worldPosition = new Vector3(
                backgroundBounds.max.x - 0.28f,
                buttonY,
                _displayTmp.transform.position.z - 0.25f);
            entry.DeleteButton.transform.position = worldPosition;
            entry.DeleteButton.transform.localScale = Vector3.one * 0.3f;
        }
    }

    private void RemoveRoleInfoEntry(RoleInfoEntry entry)
    {
        SaveActiveTab();
        int start = _roleInfoContent.IndexOf(entry.Snippet, System.StringComparison.Ordinal);
        if (start >= 0)
            _roleInfoContent = _roleInfoContent.Remove(start, entry.Snippet.Length);
        if (entry.DeleteButton != null)
        {
            Object.Destroy(entry.DeleteButton);
            entry.DeleteButton = null;
        }
        _roleInfoEntries.Remove(entry);
        if (_roleInfoCursorPos > _roleInfoContent.Length)
            _roleInfoCursorPos = _roleInfoContent.Length;
        LoadActiveTab();
        UpdateDisplay();
    }

    private void HideRoleInfoDeleteButtons()
    {
        foreach (var entry in _roleInfoEntries)
        {
            if (entry.DeleteButton != null)
                entry.DeleteButton.SetActive(false);
        }
    }

    private static int GetLogicalLineBefore(string text, int characterIndex)
    {
        int line = 0;
        int end = Mathf.Clamp(characterIndex, 0, text.Length);
        for (int i = 0; i < end; i++)
        {
            if (text[i] == '\n') line++;
        }
        return line;
    }

    private bool IsMouseOverManagedButton()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = transform.position.z;

        var buttons = new List<GameObject?> { _tabButton };
        foreach (var entry in _roleInfoEntries)
            buttons.Add(entry.DeleteButton);
        foreach (var button in buttons)
        {
            if (button == null || !button.activeSelf) continue;
            var renderer = button.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.bounds.Contains(mouseWorld))
                return true;
        }

        return false;
    }

    private void UpdateTabVisuals()
    {
        bool roleInfoActive = _activeTab == NoteTab.RoleInfo;
        SetTabButtonSprite(roleInfoActive);
        SetButtonLabel(_tabButton, roleInfoActive ? "ROLE INFO" : "GENERAL");
        if (_linesObject != null)
            _linesObject.SetActive(!roleInfoActive);
        if (_backgroundRenderer != null)
        {
            _backgroundRenderer.color = roleInfoActive
                ? new Color(0.871f, 0.987f, 1.185f)
                : _generalBackgroundColor;
        }
        if (_roleInfoLineOverlay != null)
            _roleInfoLineOverlay.SetActive(roleInfoActive);
    }

    private void SetTabButtonSprite(bool roleInfoActive)
    {
        if (_tabButton == null) return;
        _generalTabSprite ??= NotepadAssets.GeneralTabSprite.LoadAsset();
        _roleInfoTabSprite ??= NotepadAssets.RolesTabSprite.LoadAsset();

        var sprite = roleInfoActive ? _roleInfoTabSprite : _generalTabSprite;
        if (sprite == null) return;
        foreach (var renderer in _tabButton.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sprite = sprite;
    }

    private static void SetButtonSprite(GameObject? button, bool active)
    {
        if (button == null) return;
        var sprite = active
            ? NotepadAssets.NotepadButtonActiveSprite.LoadAsset()
            : NotepadAssets.NotepadButtonSprite.LoadAsset();
        if (sprite == null) return;

        foreach (var renderer in button.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.sprite = sprite;
    }

    private static void SetButtonLabel(GameObject? button, string label)
    {
        if (button == null) return;
        var labelObject = button.transform.Find("TabButtonLabel");
        var labelTmp = labelObject?.GetComponent<TextMeshPro>();
        if (labelTmp != null)
            labelTmp.text = label;
    }

    private GameObject? CreateSideButton(
        Transform template,
        string name,
        Vector3 localPosition,
        string label,
        UnityAction action)
    {
        var button = Object.Instantiate(template.gameObject, _panelInstance!.transform);
        button.name = name;
        button.transform.localPosition = localPosition;
        button.transform.localScale = Vector3.one * 0.55f;

        var passiveButton = button.GetComponent<PassiveButton>();
        if (passiveButton == null) return button;
        passiveButton.OnClick = new Button.ButtonClickedEvent();
        passiveButton.OnClick.AddListener(action);
        if (name == "TabButton")
            SetTabButtonSprite(false);
        else
            SetButtonSprite(button, false);
        SetSortingOrderRecursively(button.transform, PanelSortingOrder + 3);

        if (_displayTmp != null)
        {
            var labelObject = Object.Instantiate(_displayTmp.gameObject, button.transform);
            labelObject.name = "TabButtonLabel";
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.2f);
            labelObject.transform.localScale = Vector3.one * 0.16f;
            var labelTmp = labelObject.GetComponent<TextMeshPro>();
            if (labelTmp != null)
            {
                labelTmp.text = label;
                labelTmp.color = Color.black;
                labelTmp.alignment = TextAlignmentOptions.Center;
                labelTmp.enableWordWrapping = false;
                labelTmp.overflowMode = TextOverflowModes.Overflow;
                labelTmp.sortingOrder = PanelSortingOrder + 4;
                ApplyOutline(labelTmp);
            }
        }

        return button;
    }

    private void CreateTabControls()
    {
        var template = _panelInstance?.transform.Find("CloseButton");
        if (template == null || _backgroundRenderer == null) return;

        var bounds = _backgroundRenderer.bounds;
        var localLeft = transform.InverseTransformPoint(
            new Vector3(bounds.min.x, bounds.center.y, bounds.center.z)).x;
        float buttonX = localLeft - 0.38f;
        float topY = transform.InverseTransformPoint(
            new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)).y - 0.95f;

        _tabButton = CreateSideButton(
            template,
            "TabButton",
            new Vector3(buttonX, topY, -0.2f),
            "ROLE",
            (UnityAction)(() => SwitchTab(
                _activeTab == NoteTab.General ? NoteTab.RoleInfo : NoteTab.General)));
        SetTabButtonSprite(_activeTab == NoteTab.RoleInfo);

        CreateRoleInfoLineOverlay(bounds);
        UpdateTabVisuals();
    }

    private void CreateRoleInfoLineOverlay(Bounds backgroundBounds)
    {
        var overlay = new GameObject("RoleInfoLineOverlay");
        overlay.transform.SetParent(_scaleRoot, false);
        overlay.transform.localPosition = transform.InverseTransformPoint(backgroundBounds.center);
        overlay.transform.localPosition = new Vector3(
            overlay.transform.localPosition.x,
            overlay.transform.localPosition.y,
            -0.05f);

        var renderer = overlay.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        renderer.color = Color.white;
        renderer.sortingOrder = PanelSortingOrder + 1;
        overlay.transform.localScale = new Vector3(
            Mathf.Max(0.1f, backgroundBounds.size.x - 0.45f),
            Mathf.Max(0.1f, backgroundBounds.size.y - 0.45f),
            1f);
        _roleInfoLineOverlay = overlay;
    }

    private static int GetLineStart(string text, int line)
    {
        if (line <= 0) return 0;

        int currentLine = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            currentLine++;
            if (currentLine == line) return i + 1;
        }

        return text.Length;
    }

    private static int GetLogicalLineCount(string text)
    {
        int count = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') count++;
        }
        return count;
    }

    private int GetCursorLine()
    {
        int line = 0;
        int cursor = Mathf.Clamp(_cursorPos, 0, _content.Length);
        for (int i = 0; i < cursor; i++)
        {
            if (_content[i] == '\n') line++;
        }
        return line;
    }

    private void EnsureCursorVisible()
    {
        int cursorLine = GetCursorLine();
        if (cursorLine < _firstVisibleLine)
            _firstVisibleLine = cursorLine;
        else if (cursorLine >= _firstVisibleLine + MaxLines)
            _firstVisibleLine = cursorLine - MaxLines + 1;
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

        var scaleRootGo = new GameObject("ScaleRoot");
        scaleRootGo.transform.SetParent(transform, false);
        scaleRootGo.layer = 5;
        _scaleRoot = scaleRootGo.transform;

        var prefab = NotepadAssets.Notepad.LoadAsset();
        if (prefab == null)
        {
            Log.LogError("Notepad prefab failed to load from the bundle!");
            return;
        }

        _panelInstance = Object.Instantiate(prefab, _scaleRoot);
        _panelInstance.name = "Panel";
        _panelInstance.transform.localPosition = Vector3.zero;
        _panelInstance.transform.localScale = Vector3.one;
        SetLayerRecursively(_panelInstance, 5);

        var textboxT = _panelInstance.transform.Find("Textbox");
        var linesT = textboxT != null ? textboxT.Find("Lines") : null;
        if (linesT != null)
            _linesObject = linesT.gameObject;

        LogHierarchy(_panelInstance.transform, "");

        var backgroundT = _panelInstance.transform.Find("Background");
        _backgroundRenderer = backgroundT != null ? backgroundT.GetComponent<SpriteRenderer>() : null;
        if (_backgroundRenderer != null)
            _generalBackgroundColor = _backgroundRenderer.color;
        SetSortingOrderRecursively(_panelInstance.transform, PanelSortingOrder);
        ApplyOutlinesRecursively(_panelInstance.transform);
        var template = HudManager.Instance?.Chat?.freeChatField?.textArea;
        if (template == null)
        {
            Log.LogError("Chat template missing!");
        }
        else
        {
            var dispGo = Object.Instantiate(template.outputText.gameObject, _scaleRoot);
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

        CreateTabControls();

        UpdateDisplay();
        ApplyScale();
    }

    private void OnDestroy() => _instance = null;
}