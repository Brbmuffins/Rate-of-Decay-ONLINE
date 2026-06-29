using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest
using System.Text;

/// <summary>
/// InventoryManager — Singleton. Tracks the local player's inventory in memory
/// and syncs to the auth server via POST /api/inventory/save.
///
/// Copy to: Assets/Game/Systems/InventoryManager.cs
///
/// Usage:
///   InventoryManager.Instance.OnItemPickedUp(itemId, qty);
///   InventoryManager.Instance.GetSlots();
///
/// Wiring:
///   - Add to a persistent GameObject in Hub/Arena scenes (or DontDestroyOnLoad).
///   - Reads AuthManager.Token and AuthManager.CharacterId at runtime.
///   - On scene load: call LoadInventory() to hydrate from GET /api/inventory/:id
///
/// AuthManager expected statics (wire to your existing auth class):
///   AuthManager.Token       — JWT string
///   AuthManager.CharacterId — int character id
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static InventoryManager Instance { get; private set; }

    // ─── Config ───────────────────────────────────────────────────────────────
    [Header("Server")]
    public string authServerUrl = "http://15.204.243.36:3000";

    [Header("Inventory Size")]
    public int maxSlots = 32;   // 8×4 grid

    // ─── State ────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class InventorySlot
    {
        public int    slot_index;
        public string item_id;
        public int    quantity;
        public int    equipped;   // 0 = bag, 1 = equipped
    }

    private List<InventorySlot> _slots = new List<InventorySlot>();
    private bool _dirty = false;   // pending save

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(LoadInventory());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Called by WorldItem.RpcOnPickedUp after a successful pickup.</summary>
    public void OnItemPickedUp(string itemId, int qty)
    {
        // Gold is a special case — not stored in inventory
        if (itemId.StartsWith("gold:"))
        {
            if (int.TryParse(itemId.Substring(5), out int gold))
                OnGoldPickedUp(gold);
            return;
        }

        // Try to stack into an existing slot
        var existing = _slots.Find(s => s.item_id == itemId && s.equipped == 0);
        if (existing != null)
        {
            existing.quantity += qty;
        }
        else
        {
            int nextSlot = FindNextFreeSlot();
            if (nextSlot < 0)
            {
                Debug.LogWarning($"[LOOT] Inventory full — {itemId} could not be added");
                return;
            }
            _slots.Add(new InventorySlot
            {
                slot_index = nextSlot,
                item_id    = itemId,
                quantity   = qty,
                equipped   = 0
            });
        }

        _dirty = true;
        Debug.Log($"[LOOT] Picked up {qty}x {itemId}");
        StartCoroutine(SaveInventory());
    }

    /// <summary>Returns a copy of the current slot list (read-only for UI).</summary>
    public List<InventorySlot> GetSlots() => new List<InventorySlot>(_slots);

    // ─── Gold ─────────────────────────────────────────────────────────────────
    void OnGoldPickedUp(int amount)
    {
        Debug.Log($"[LOOT] Picked up {amount} gold — will sync via save-progress");
        // Gold is tracked on the character row, not inventory.
        // Queue it into the next save-progress call (hook into your ProgressManager).
        // For now, log and let the player's session-end sync handle it.
    }

    // ─── Load from server ─────────────────────────────────────────────────────
    public IEnumerator LoadInventory()
    {
        int charId = AuthManager.CharacterId;
        string token = AuthManager.Token;

        if (charId <= 0 || string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[LOOT] LoadInventory: AuthManager not ready — skipping");
            yield break;
        }

        string url = $"{authServerUrl}/api/inventory/{charId}";
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", $"Bearer {token}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LOOT] LoadInventory failed: {req.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<InventoryResponse>(req.downloadHandler.text);
        if (response != null && response.success)
        {
            _slots = response.data ?? new List<InventorySlot>();
            Debug.Log($"[LOOT] Loaded {_slots.Count} inventory slots for char#{charId}");
        }
        else
        {
            Debug.LogError($"[LOOT] LoadInventory server error: {response?.error}");
        }
    }

    // ─── Save to server ───────────────────────────────────────────────────────
    public IEnumerator SaveInventory()
    {
        int charId = AuthManager.CharacterId;
        string token = AuthManager.Token;

        if (charId <= 0 || string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[LOOT] SaveInventory: AuthManager not ready — skipping");
            yield break;
        }

        var payload = new InventorySavePayload
        {
            characterId = charId,
            slots       = _slots
        };
        string json = JsonUtility.ToJson(payload);

        string url = $"{authServerUrl}/api/inventory/save";
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {token}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[LOOT] SaveInventory failed: {req.error}");
        else
        {
            _dirty = false;
            Debug.Log($"[LOOT] Inventory saved — {_slots.Count} slots");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    int FindNextFreeSlot()
    {
        var used = new HashSet<int>();
        foreach (var s in _slots) used.Add(s.slot_index);
        for (int i = 0; i < maxSlots; i++)
            if (!used.Contains(i)) return i;
        return -1;
    }

    // ─── JSON Shapes ──────────────────────────────────────────────────────────
    [System.Serializable]
    class InventoryResponse
    {
        public bool               success;
        public List<InventorySlot> data;
        public string             error;
    }

    [System.Serializable]
    class InventorySavePayload
    {
        public int                characterId;
        public List<InventorySlot> slots;
    }
}

// ─── AuthManager stub ─────────────────────────────────────────────────────────
// Wire these to your actual AuthManager fields.
// If you already have an AuthManager class, delete this and update the references above.
#if !AUTHMANAGER_EXISTS
public static class AuthManager
{
    public static string Token       { get; set; } = "";
    public static int    CharacterId { get; set; } = 0;
}
#endif
