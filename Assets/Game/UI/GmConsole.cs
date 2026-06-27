using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

// ═══════════════════════════════════════════════════════════════════════════
//  GmConsole
//  In-game GM command console. Opens with backtick (`) or F1.
//  Attach to a persistent GameObject in GameWorld.
//
//  COMMANDS
//  ────────
//  speed <n>         — set movement speed multiplier (e.g. speed 3)
//  fly               — toggle fly mode (frees Y-axis, disables gravity)
//  god               — toggle invulnerability
//  heal              — full heal self
//  kill              — kill all enemies in scene
//  spawn <count>     — spawn <count> basic enemies near you (uses EnemyAI capsules)
//  wave [n]          — start WaveManager, or jump to wave n if running
//  tp <x> <y> <z>   — teleport self to world position
//  pos               — print current world position (useful for level building)
//  players           — list all networked players + positions
//  goto <name>       — teleport to a named player
//  noclip            — toggle collider pass-through (disables own colliders)
//  clear             — clear console history
//  help              — list all commands
//
//  GM ACCESS CONTROL
//  ─────────────────
//  Only usernames in GM_USERS are allowed. Add yours before testing.
//  In a build, consider server-side verification before trusting these.
// ═══════════════════════════════════════════════════════════════════════════

public class GmConsole : MonoBehaviour
{
    // ── Auto-bootstrap ────────────────────────────────────────────────────
    // Creates itself once per play session — no scene object required.
    static GmConsole _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return; // headless server — no console
        if (_instance != null) return;

        var go = new GameObject("GmConsole");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<GmConsole>();
    }

    // ── GM allowlist ──────────────────────────────────────────────────────
    static readonly HashSet<string> GM_USERS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DevPlayer",
        "brbmuffins",
        "ForYurHealth",
        "YaDingusMD",
        "SleepyBoySteve",
    };

    // ── UI ────────────────────────────────────────────────────────────────
    Canvas          _canvas;
    GameObject      _panel;
    TMP_InputField  _input;
    TMP_Text        _log;
    ScrollRect      _scroll;
    bool            _open;

    readonly List<string> _history    = new List<string>();
    readonly List<string> _cmdHistory = new List<string>();
    int _cmdHistoryIdx = -1;

    // ── State ─────────────────────────────────────────────────────────────
    GameObject   _localPlayer;
    PlayerMovement _movement;
    Rigidbody    _rb;
    Health       _health;

    bool  _flyActive;
    bool  _godActive;
    bool  _noclipActive;
    float _baseSpeed;
    float _baseMass;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        BuildUI();
        _panel.SetActive(false);
    }

    void Update()
    {
        // No-op on headless dedicated server — no display, no input
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;

        // Don't steal keystrokes while player is typing in chat or any other input field
        var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        bool typingInUI = sel != null && sel.GetComponent<TMPro.TMP_InputField>() != null;
        if (typingInUI && !_open) return;

        // Toggle open/close
        var kb = Keyboard.current;
        bool toggle = kb != null && (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame);
        if (toggle)
        {
            _open = !_open;
            _panel.SetActive(_open);
            if (_open)
            {
                _input.ActivateInputField();
                _input.Select();
                TryFindPlayer();
            }
        }

        if (!_open) return;

        // Submit on Enter
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            Submit();

        // Command history navigation
        if (kb != null && kb.upArrowKey.wasPressedThisFrame && _cmdHistory.Count > 0)
        {
            _cmdHistoryIdx = Mathf.Min(_cmdHistoryIdx + 1, _cmdHistory.Count - 1);
            _input.text = _cmdHistory[_cmdHistory.Count - 1 - _cmdHistoryIdx];
            _input.MoveTextEnd(false);
        }
        if (kb != null && kb.downArrowKey.wasPressedThisFrame && _cmdHistory.Count > 0)
        {
            _cmdHistoryIdx = Mathf.Max(_cmdHistoryIdx - 1, -1);
            _input.text = _cmdHistoryIdx < 0 ? "" : _cmdHistory[_cmdHistory.Count - 1 - _cmdHistoryIdx];
            _input.MoveTextEnd(false);
        }

        // Fly physics — move on Y with same input keys
        if (_flyActive && _rb != null)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, GetFlyVertical() * 8f, _rb.linearVelocity.z);
        }
    }

    // ── GM check ──────────────────────────────────────────────────────────

    bool IsGM()
    {
        string user = PlayerPrefs.GetString("username", "");
        return GM_USERS.Contains(user);
    }

    void TryFindPlayer()
    {
        if (_localPlayer != null) return;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var id = go.GetComponent<NetworkIdentity>();
            if (id != null && id.isLocalPlayer)
            {
                _localPlayer = go;
                _movement    = go.GetComponent<PlayerMovement>();
                _rb          = go.GetComponent<Rigidbody>();
                _health      = go.GetComponent<Health>();
                if (_movement != null) _baseSpeed = _movement.moveSpeed;
                if (_rb       != null) _baseMass  = _rb.mass;
                return;
            }
        }
        // Fallback for HOST dev mode where NetworkIdentity may not be set
        var fallback = GameObject.FindGameObjectWithTag("Player");
        if (fallback != null)
        {
            _localPlayer = fallback;
            _movement    = fallback.GetComponent<PlayerMovement>();
            _rb          = fallback.GetComponent<Rigidbody>();
            _health      = fallback.GetComponent<Health>();
            if (_movement != null) _baseSpeed = _movement.moveSpeed;
            if (_rb       != null) _baseMass  = _rb.mass;
        }
    }

    // ── Command parsing ───────────────────────────────────────────────────

    void Submit()
    {
        string raw = _input.text.Trim();
        _input.text = "";
        _input.ActivateInputField();
        _cmdHistoryIdx = -1;

        if (string.IsNullOrEmpty(raw)) return;
        _cmdHistory.Add(raw);

        Log($"<color=#888>> {raw}</color>");

        if (!IsGM())
        {
            Log("<color=#f87171>Access denied — username not in GM list.</color>");
            return;
        }

        string[] parts = raw.Split(' ');
        string cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "speed":   CmdSpeed(parts);   break;
            case "fly":     CmdFly();          break;
            case "god":     CmdGod();          break;
            case "heal":    CmdHeal();         break;
            case "kill":    CmdKill();         break;
            case "spawn":   CmdSpawn(parts);   break;
            case "wave":    CmdWave(parts);    break;
            case "tp":      CmdTp(parts);      break;
            case "noclip":  CmdNoclip();       break;
            case "pos":     CmdPos();          break;
            case "players": CmdPlayers();      break;
            case "goto":    CmdGoto(parts);    break;
            case "clear":   _history.Clear(); _log.text = ""; break;
            case "help":    CmdHelp();         break;
            default:        Log($"<color=#f87171>Unknown command: {cmd}. Type 'help'.</color>"); break;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    void CmdSpeed(string[] parts)
    {
        if (_movement == null) { Log("<color=#f87171>No player found.</color>"); return; }
        if (parts.Length < 2 || !float.TryParse(parts[1], out float mult))
        {
            Log("Usage: speed <multiplier>  (e.g. speed 3)");
            return;
        }
        _movement.moveSpeed  = _baseSpeed * mult;
        _movement.sprintSpeed = _baseSpeed * 1.8f * mult;
        Log($"<color=#4ade80>Speed set to ×{mult} ({_movement.moveSpeed:F1} u/s)</color>");
    }

    void CmdFly()
    {
        if (_rb == null) { Log("<color=#f87171>No Rigidbody found on player.</color>"); return; }
        _flyActive = !_flyActive;
        _rb.useGravity = !_flyActive;
        if (_flyActive)  _rb.linearVelocity = Vector3.zero;
        Log(_flyActive
            ? "<color=#4ade80>Fly ON — W/S for forward, hold Space/Ctrl for vertical</color>"
            : "<color=#94a3b8>Fly OFF</color>");
    }

    void CmdGod()
    {
        if (_health == null) { Log("<color=#f87171>No Health component found.</color>"); return; }
        _godActive = !_godActive;
        _health.isInvulnerable = _godActive;
        Log(_godActive
            ? "<color=#4ade80>God mode ON — invulnerable</color>"
            : "<color=#94a3b8>God mode OFF</color>");
    }

    void CmdHeal()
    {
        if (_health == null) { Log("<color=#f87171>No Health component found.</color>"); return; }
        _health.Heal(_health.maxHealth);
        Log($"<color=#4ade80>Healed to full ({_health.maxHealth})</color>");
    }

    void CmdKill()
    {
        int count = 0;
        foreach (var go in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            var h = go.GetComponent<Health>();
            if (h != null) h.TakeDamage(999999f, _localPlayer);
            else Destroy(go);
            count++;
        }
        Log($"<color=#4ade80>Killed {count} enemies.</color>");
    }

    void CmdSpawn(string[] parts)
    {
        if (_localPlayer == null) { Log("<color=#f87171>No player found.</color>"); return; }
        int count = 1;
        if (parts.Length >= 2) int.TryParse(parts[1], out count);
        count = Mathf.Clamp(count, 1, 50);

        for (int i = 0; i < count; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * 4f;
            Vector3 pos = _localPlayer.transform.position + new Vector3(r.x, 0f, r.y) + Vector3.up;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "GM_Enemy";
            go.tag  = "Enemy";
            go.transform.position = pos;
            go.GetComponent<Renderer>().material.color = new Color(0.7f, 0.1f, 0.1f);

            go.AddComponent<StatusEffectManager>();
            var h = go.AddComponent<Health>();
            h.maxHealth = 60f;
            var ai = go.AddComponent<EnemyAI>();
            ai.moveSpeed    = 2.5f;
            ai.attackDamage = 8f;
        }
        Log($"<color=#4ade80>Spawned {count} enemy capsule(s) nearby.</color>");
    }

    void CmdWave(string[] parts)
    {
        var wm = FindAnyObjectByType<WaveManager>();
        if (wm == null) { Log("<color=#f87171>No WaveManager found in scene.</color>"); return; }

        if (parts.Length >= 2 && int.TryParse(parts[1], out int waveNum))
        {
            // Jump to a specific wave index
            wm.JumpToWave(waveNum);
            Log($"<color=#4ade80>Jumped to wave {waveNum}.</color>");
        }
        else if (!wm.IsRunning)
        {
            wm.StartArena();
            Log("<color=#4ade80>WaveManager started.</color>");
        }
        else
        {
            Log($"<color=#94a3b8>WaveManager running — wave {wm.CurrentWave}/{wm.TotalWaves}. Use 'wave <n>' to jump.</color>");
        }
    }

    void CmdPos()
    {
        if (_localPlayer == null) { Log("<color=#f87171>No player found.</color>"); return; }
        Vector3 p = _localPlayer.transform.position;
        Log($"<color=#4ade80>Position: ({p.x:F2}, {p.y:F2}, {p.z:F2})</color>");
    }

    void CmdPlayers()
    {
        var identities = UnityEngine.Object.FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);
        if (identities.Length == 0)
        {
            Log("<color=#94a3b8>No players found in scene.</color>");
            return;
        }
        Log($"<color=#60a5fa>── {identities.Length} Player(s) ────────────────</color>");
        foreach (var id in identities)
        {
            Vector3 p = id.transform.position;
            string local = id.isLocalPlayer ? " <color=#fbbf24>(you)</color>" : "";
            Log($"  <color=#e2e8f0>{id.playerName}</color> [{id.ClassName}]{local} @ ({p.x:F1},{p.y:F1},{p.z:F1})");
        }
    }

    void CmdGoto(string[] parts)
    {
        if (_localPlayer == null) { Log("<color=#f87171>No local player found.</color>"); return; }
        if (parts.Length < 2) { Log("Usage: goto <playername>"); return; }

        string target = string.Join(" ", parts, 1, parts.Length - 1);
        var identities = UnityEngine.Object.FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);
        foreach (var id in identities)
        {
            if (string.Equals(id.playerName, target, StringComparison.OrdinalIgnoreCase))
            {
                Vector3 dest = id.transform.position + id.transform.forward * 2f + Vector3.up * 0.1f;
                _localPlayer.transform.position = dest;
                if (_rb != null) _rb.linearVelocity = Vector3.zero;
                Log($"<color=#4ade80>Teleported to {id.playerName}.</color>");
                return;
            }
        }
        Log($"<color=#f87171>Player '{target}' not found.</color>");
    }

    void CmdTp(string[] parts)
    {
        if (_localPlayer == null) { Log("<color=#f87171>No player found.</color>"); return; }
        if (parts.Length < 4 ||
            !float.TryParse(parts[1], out float x) ||
            !float.TryParse(parts[2], out float y) ||
            !float.TryParse(parts[3], out float z))
        {
            Log("Usage: tp <x> <y> <z>");
            return;
        }
        _localPlayer.transform.position = new Vector3(x, y, z);
        if (_rb != null) _rb.linearVelocity = Vector3.zero;
        Log($"<color=#4ade80>Teleported to ({x}, {y}, {z})</color>");
    }

    void CmdNoclip()
    {
        if (_localPlayer == null) { Log("<color=#f87171>No player found.</color>"); return; }
        _noclipActive = !_noclipActive;
        foreach (var col in _localPlayer.GetComponents<Collider>())
            col.enabled = !_noclipActive;
        Log(_noclipActive
            ? "<color=#4ade80>Noclip ON — colliders disabled</color>"
            : "<color=#94a3b8>Noclip OFF — colliders restored</color>");
    }

    void CmdHelp()
    {
        Log("<color=#60a5fa>── GM Commands ──────────────────────────</color>");
        Log("  <color=#e2e8f0>speed <n></color>       — movement speed multiplier");
        Log("  <color=#e2e8f0>fly</color>             — toggle fly mode");
        Log("  <color=#e2e8f0>god</color>             — toggle invulnerability");
        Log("  <color=#e2e8f0>heal</color>            — full heal self");
        Log("  <color=#e2e8f0>kill</color>            — kill all enemies");
        Log("  <color=#e2e8f0>spawn [count]</color>   — spawn enemies near you");
        Log("  <color=#e2e8f0>wave [n]</color>        — start WaveManager / jump to wave n");
        Log("  <color=#e2e8f0>tp <x> <y> <z></color> — teleport to position");
        Log("  <color=#e2e8f0>pos</color>             — print current world position");
        Log("  <color=#e2e8f0>players</color>         — list all players in scene");
        Log("  <color=#e2e8f0>goto <name></color>     — teleport to a player");
        Log("  <color=#e2e8f0>noclip</color>          — toggle collision");
        Log("  <color=#e2e8f0>clear</color>           — clear console");
        Log("  <color=#e2e8f0>help</color>            — show this list");
        Log("<color=#60a5fa>─────────────────────────────────────────</color>");
    }

    // ── Log ───────────────────────────────────────────────────────────────

    void Log(string msg)
    {
        _history.Add(msg);
        if (_history.Count > 80) _history.RemoveAt(0);
        _log.text = string.Join("\n", _history);

        // Scroll to bottom
        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = 0f;
    }

    // ── Fly vertical input ────────────────────────────────────────────────

    float GetFlyVertical()
    {
        float v = 0f;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.isPressed)        v += 1f;
        if (kb != null && kb.leftCtrlKey.isPressed)     v -= 1f;
        return v;
    }

    // ── UI Construction ───────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var cgo = new GameObject("GmConsoleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(cgo);
        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        var root = _canvas.GetComponent<RectTransform>();

        // Panel — bottom 35% of screen
        _panel = new GameObject("GmPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(root, false);
        _panel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.06f, 0.92f);
        var panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0.55f, 0.38f);
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

        // Header bar
        var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(panelRt, false);
        header.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.18f, 1f);
        var hRt = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 0.92f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.offsetMin = hRt.offsetMax = Vector2.zero;

        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(hRt, false);
        var title = titleGO.GetComponent<TextMeshProUGUI>();
        title.text      = "GM CONSOLE  <size=9><color=#475569>` or F1 to toggle</color></size>";
        title.fontSize  = 11f;
        title.color     = new Color(0.6f, 0.4f, 1f);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var tRt = titleGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.01f, 0f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        // Scroll view for log
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGO.transform.SetParent(panelRt, false);
        scrollGO.GetComponent<Image>().color = Color.clear;
        var scrollRt = scrollGO.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0.12f);
        scrollRt.anchorMax = new Vector2(1f, 0.92f);
        scrollRt.offsetMin = new Vector2(4f, 0f);
        scrollRt.offsetMax = new Vector2(-4f, 0f);
        _scroll = scrollGO.GetComponent<ScrollRect>();
        _scroll.horizontal     = false;
        _scroll.scrollSensitivity = 20f;

        // Viewport
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRt, false);
        viewport.GetComponent<Image>().color = Color.clear;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
        _scroll.viewport = vpRt;

        // Log text content
        var logGO = new GameObject("LogText", typeof(RectTransform), typeof(TextMeshProUGUI));
        logGO.transform.SetParent(vpRt, false);
        _log = logGO.GetComponent<TextMeshProUGUI>();
        _log.fontSize   = 11f;
        _log.color      = new Color(0.85f, 0.85f, 0.9f, 1f);
        _log.alignment  = TextAlignmentOptions.BottomLeft;
        _log.textWrappingMode = TextWrappingModes.Normal;
        _log.richText   = true;
        var logRt = logGO.GetComponent<RectTransform>();
        logRt.anchorMin = new Vector2(0f, 0f);
        logRt.anchorMax = new Vector2(1f, 1f);
        logRt.offsetMin = new Vector2(4f, 0f);
        logRt.offsetMax = new Vector2(-4f, 0f);
        logRt.pivot     = new Vector2(0f, 0f);
        _scroll.content = logRt;

        // Input field
        var inputBg = new GameObject("InputBg", typeof(RectTransform), typeof(Image));
        inputBg.transform.SetParent(panelRt, false);
        inputBg.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.12f, 1f);
        var ibRt = inputBg.GetComponent<RectTransform>();
        ibRt.anchorMin = new Vector2(0f, 0f);
        ibRt.anchorMax = new Vector2(1f, 0.12f);
        ibRt.offsetMin = ibRt.offsetMax = Vector2.zero;

        var promptGO = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        promptGO.transform.SetParent(ibRt, false);
        var prompt = promptGO.GetComponent<TextMeshProUGUI>();
        prompt.text     = ">";
        prompt.fontSize = 13f;
        prompt.color    = new Color(0.6f, 0.4f, 1f, 1f);
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
        var pRt = promptGO.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.01f, 0f);
        pRt.anchorMax = new Vector2(0.05f, 1f);
        pRt.offsetMin = pRt.offsetMax = Vector2.zero;

        var inputGO = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGO.transform.SetParent(ibRt, false);
        inputGO.GetComponent<Image>().color = Color.clear;
        var inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.05f, 0f);
        inputRt.anchorMax = new Vector2(1f, 1f);
        inputRt.offsetMin = inputRt.offsetMax = Vector2.zero;

        var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGO.transform.SetParent(inputGO.transform, false);
        var ph = phGO.GetComponent<TextMeshProUGUI>();
        ph.text     = "type command...";
        ph.fontSize = 13f;
        ph.color    = new Color(0.3f, 0.28f, 0.4f, 1f);
        ph.fontStyle = FontStyles.Italic;
        var phRt = phGO.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
        phRt.offsetMin = phRt.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(inputGO.transform, false);
        var txt = txtGO.GetComponent<TextMeshProUGUI>();
        txt.fontSize = 13f;
        txt.color    = Color.white;
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

        _input = inputGO.GetComponent<TMP_InputField>();
        _input.textComponent = txt;
        _input.placeholder   = ph;
        _input.textViewport  = inputRt;
        _input.caretColor    = new Color(0.6f, 0.4f, 1f);
        _input.onSubmit.AddListener(_ => Submit());

        Log("<color=#6366f1>GM Console ready. Type 'help' for commands.</color>");
    }
}
