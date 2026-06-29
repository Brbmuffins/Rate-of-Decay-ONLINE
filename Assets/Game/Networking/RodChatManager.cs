using System;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ═══════════════════════════════════════════════════════════════════════════
//  RodChatManager
//  Attach to a GameObject with NetworkIdentity in the Hub scene.
//  The Hub scene builder (RoD/Setup/5) does this automatically.
//
//  Flow:
//    Client presses Enter or T → input opens
//    Client types + presses Enter → [Command] CmdSendChat(msg)
//    Server pulls username from conn.authenticationData (anti-spoof)
//    Server → [ClientRpc] RpcReceiveChat(username, msg, unixTimestamp)
//    All clients render the message
//
//  Keys:
//    Enter / T     — open / focus chat input
//    Enter         — send message
//    Escape        — close without sending
//
//  Chat fades out FADE_AFTER seconds after the last message when not active.
//  It reappears instantly on any new message or when the player opens it.
//
//  Inspector:
//    No public fields required — fully procedural UI.
// ═══════════════════════════════════════════════════════════════════════════

public class RodChatManager : NetworkBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    public static RodChatManager Instance { get; private set; }

    /// <summary>True while the chat input field is open. Used by CameraFollow to suppress camera orbit.</summary>
    public bool IsOpen => _open;

    // ── Tunables ──────────────────────────────────────────────────────────
    const int   MAX_MESSAGES = 60;
    const int   MAX_MSG_LEN  = 200;
    const float FADE_DELAY   = 8f;   // seconds of inactivity before fade starts
    const float FADE_TIME    = 2f;   // seconds to fully fade

    // ── UI ────────────────────────────────────────────────────────────────
    Canvas         _canvas;
    CanvasGroup    _cg;
    GameObject     _panel;
    TMP_Text       _log;
    GameObject     _inputArea;
    TMP_InputField _input;

    bool   _open;
    float  _lastMsgTime = -999f;
    string _typedText   = "";

    readonly List<string> _history = new();

    // ── Name colours — deterministic from username hash ───────────────────
    static readonly string[] NAME_COLORS =
        { "#f472b6", "#a78bfa", "#34d399", "#60a5fa", "#fbbf24", "#fb923c" };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (Instance != null && Instance != this) return;
        Instance = this;

        EnsureEventSystem();
        BuildUI();
        AddSystemMessage("Connected to Hub.");
    }

    /// <summary>
    /// Hub scene wipes all non-Network objects (including the EventSystem from LoginScene).
    /// Without an EventSystem, UI button clicks never fire and IsPointerOverGameObject() is
    /// always false — causing every left-click to lock the cursor and re-center it.
    /// </summary>
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var go = new GameObject("EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        // Don't destroy across scenes — Mirror may load scenes mid-session
        DontDestroyOnLoad(go);
    }

    void OnDestroy()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnTextInput;
        if (Instance == this) Instance = null;
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    void Update()
    {
        if (_canvas == null) return;

        // ── Input handling ─────────────────────────────────────────────
        var kb = Keyboard.current;
        if (kb == null) return;

        if (!_open)
        {
            bool openKey = kb.enterKey.wasPressedThisFrame
                        || kb.numpadEnterKey.wasPressedThisFrame
                        || kb.tKey.wasPressedThisFrame;

            if (openKey && !AnyOtherInputFocused())
                OpenInput();
        }
        else
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                CloseInput(send: true);
            else if (kb.escapeKey.wasPressedThisFrame)
                CloseInput(send: false);
            else if (kb.backspaceKey.wasPressedThisFrame && _typedText.Length > 0)
            {
                _typedText = _typedText[..^1];
                _input.text = _typedText;
            }
        }

        // ── Fade ───────────────────────────────────────────────────────
        if (!_open)
        {
            float elapsed = Time.unscaledTime - _lastMsgTime;
            float t = Mathf.InverseLerp(FADE_DELAY, FADE_DELAY + FADE_TIME, elapsed);
            _cg.alpha = Mathf.Lerp(1f, 0f, t);

            // Keep blocksRaycasts ON even when faded so clicking the chat area doesn't
            // count as "clicked on world" and trigger cursor lock → re-centering.
            // interactable=false prevents actual interaction with the invisible elements.
            _cg.blocksRaycasts = true;
            _cg.interactable   = false;
        }
        else
        {
            _cg.alpha          = 1f;
            _cg.blocksRaycasts = true;
            _cg.interactable   = true;
        }
    }

    // ── Server-side command ───────────────────────────────────────────────

    [Command(requiresAuthority = false)]
    void CmdSendChat(string message, NetworkConnectionToClient sender = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        message = message.Trim();
        if (message.Length > MAX_MSG_LEN)
            message = message[..MAX_MSG_LEN];

        // Pull username from server-authoritative auth data — cannot be spoofed
        string username = "Unknown";
        if (sender?.authenticationData is RodPlayerAuth auth)
            username = auth.username;

        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Debug.Log($"[CHAT] {username}: {message}");
        RpcReceiveChat(username, message, ts);
    }

    // ── Client-side receive ───────────────────────────────────────────────

    [ClientRpc]
    void RpcReceiveChat(string username, string message, long unixTs)
    {
        var time     = DateTimeOffset.FromUnixTimeSeconds(unixTs).ToLocalTime();
        string tStr  = time.ToString("HH:mm");
        string nCol  = NameColor(username);

        string line =
            $"<color=#64748b>[{tStr}]</color> " +
            $"<color={nCol}><b>{Esc(username)}</b></color>" +
            $"<color=#94a3b8>: </color>" +
            $"<color=#e2e8f0>{Esc(message)}</color>";

        PushLine(line);
    }

    // ── Public helpers ────────────────────────────────────────────────────

    /// <summary>Show a local-only system message (join, leave, etc.).</summary>
    public void AddSystemMessage(string msg) =>
        PushLine($"<color=#60a5fa>» </color><color=#94a3b8>{Esc(msg)}</color>");

    // ── Private ───────────────────────────────────────────────────────────

    void OpenInput()
    {
        _open      = true;
        _typedText = "";
        _inputArea.SetActive(true);
        _panel.SetActive(true);
        _cg.alpha          = 1f;
        _cg.interactable   = true;
        _cg.blocksRaycasts = true;
        _input.text = "";

        // Capture text via Input System directly — bypasses TMP's EventSystem dependency
        // so typing works regardless of which Input Module the EventSystem uses.
        Keyboard.current.onTextInput -= OnTextInput; // guard against double-subscribe
        Keyboard.current.onTextInput += OnTextInput;
    }

    void OnTextInput(char c)
    {
        if (!_open) return;
        if (c < 32 || c == 127) return;              // skip control characters
        if (_typedText.Length >= MAX_MSG_LEN) return;
        _typedText  += c;
        _input.text  = _typedText;
    }

    void CloseInput(bool send)
    {
        Keyboard.current.onTextInput -= OnTextInput;

        if (send)
        {
            string txt = _typedText.Trim();
            if (!string.IsNullOrEmpty(txt))
                CmdSendChat(txt);
        }
        _typedText = "";
        _open      = false;
        _inputArea.SetActive(false);
        _lastMsgTime = Time.unscaledTime;
    }

    void PushLine(string formatted)
    {
        _history.Add(formatted);
        if (_history.Count > MAX_MESSAGES) _history.RemoveAt(0);

        if (_log != null)
        {
            _log.text = string.Join("\n", _history);
            _lastMsgTime = Time.unscaledTime;

            if (!_panel.activeSelf) _panel.SetActive(true);
        }
    }

    bool AnyOtherInputFocused()
    {
        foreach (var f in FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None))
            if (f != _input && f.isFocused) return true;
        return false;
    }

    // Prevent TMP rich-text injection from user input.
    // Replace angle brackets so tags like <color=...> can't be smuggled in.
    static string Esc(string s) =>
        s.Replace("<", "[").Replace(">", "]");

    static string NameColor(string name)
    {
        int h = 0;
        foreach (char c in name) h = h * 31 + c;
        return NAME_COLORS[Math.Abs(h) % NAME_COLORS.Length];
    }

    // ── Procedural UI ─────────────────────────────────────────────────────
    // Layout (fraction of 1920×1080):
    //   Panel    — left edge, rows 38%→64% of screen height (sits above GmConsole)
    //   Log      — fills panel body
    //   InputBg  — bottom 13% of panel, hidden when not typing
    // ─────────────────────────────────────────────────────────────────────

    void BuildUI()
    {
        // ── Canvas ────────────────────────────────────────────────────────
        var cgo = new GameObject("ChatCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        _cg = cgo.GetComponent<CanvasGroup>();
        _cg.alpha          = 0f;
        _cg.blocksRaycasts = false;   // invisible → don't block mouse input
        _cg.interactable   = false;

        var root = _canvas.GetComponent<RectTransform>();

        // ── Panel ─────────────────────────────────────────────────────────
        _panel = MakeRect("ChatPanel", root,
            new Vector2(0f, 0.38f), new Vector2(0.42f, 0.64f));
        Img(_panel, new Color(0.02f, 0.02f, 0.06f, 0.88f));

        // ── Header ────────────────────────────────────────────────────────
        var header = MakeRect("Header", _panel.GetComponent<RectTransform>(),
            new Vector2(0f, 0.93f), new Vector2(1f, 1f));
        Img(header, new Color(0.05f, 0.03f, 0.14f, 1f));
        var titleLbl = MakeTmp("Title", header.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0f), new Vector2(1f, 1f));
        titleLbl.text      = "CHAT  <size=8><color=#475569>Enter/T to type · Esc to close</color></size>";
        titleLbl.fontSize  = 10f;
        titleLbl.color     = new Color(0.5f, 0.8f, 1f);
        titleLbl.fontStyle = FontStyles.Bold;
        titleLbl.alignment = TextAlignmentOptions.MidlineLeft;

        // ── Log text (plain fixed rect — no ScrollRect, no Mask/Viewport) ───
        var logGO = MakeRect("LogText", _panel.GetComponent<RectTransform>(),
            new Vector2(0f, 0.13f), new Vector2(1f, 0.93f),
            new Vector2(6f, 4f), new Vector2(-6f, -4f));
        _log = logGO.AddComponent<TextMeshProUGUI>();
        _log.fontSize         = 11f;
        _log.color            = Color.white;
        _log.alignment        = TextAlignmentOptions.TopLeft;
        _log.textWrappingMode = TextWrappingModes.Normal;
        _log.richText         = true;
        _log.overflowMode     = TextOverflowModes.Overflow;

        // ── Input area ────────────────────────────────────────────────────
        _inputArea = MakeRect("InputArea", _panel.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(1f, 0.13f));
        Img(_inputArea, new Color(0.04f, 0.03f, 0.12f, 1f));

        var promptLbl = MakeTmp("Prompt", _inputArea.GetComponent<RectTransform>(),
            new Vector2(0.01f, 0f), new Vector2(0.06f, 1f));
        promptLbl.text      = ">";
        promptLbl.fontSize  = 13f;
        promptLbl.color     = new Color(0.5f, 0.8f, 1f);
        promptLbl.alignment = TextAlignmentOptions.MidlineLeft;

        var inputGO = MakeRect("InputField", _inputArea.GetComponent<RectTransform>(),
            new Vector2(0.06f, 0f), new Vector2(1f, 1f));
        Img(inputGO, Color.clear);

        var phGO = MakeRect("Placeholder", inputGO.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one);
        var ph = phGO.AddComponent<TextMeshProUGUI>();
        ph.text      = "Say something...";
        ph.fontSize  = 13f;
        ph.color     = new Color(0.3f, 0.27f, 0.4f);
        ph.fontStyle = FontStyles.Italic;

        var txtGO = MakeRect("Text", inputGO.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 13f;
        txt.color    = Color.white;

        _input = inputGO.AddComponent<TMP_InputField>();
        _input.textComponent  = txt;
        _input.placeholder    = ph;
        _input.textViewport   = inputGO.GetComponent<RectTransform>();
        _input.caretColor     = new Color(0.5f, 0.8f, 1f);
        _input.characterLimit = MAX_MSG_LEN;
        // Enter is handled in Update() — onSubmit is unreliable with new Input System.

        _inputArea.SetActive(false);
        _panel.SetActive(false);
    }

    // ── UI helpers ────────────────────────────────────────────────────────

    static GameObject MakeRect(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return go;
    }

    static void Img(GameObject go, Color col)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = col;
    }

    static TextMeshProUGUI MakeTmp(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = MakeRect(name, parent, anchorMin, anchorMax);
        return go.AddComponent<TextMeshProUGUI>();
    }
}
