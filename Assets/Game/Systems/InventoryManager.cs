using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

/// <summary>
/// InventoryManager — Singleton. Tracks local player's inventory in memory
/// and syncs to auth server via POST /api/inventory/save.
///
/// Reads AuthManager.Token and AuthManager.CharacterId at runtime.
/// Wire those to your existing auth class, or use the stub at the bottom of this file.
///
/// On scene load this auto-calls LoadInventory().
/// WorldItem.RpcOnPickedUp calls OnItemPickedUp() which immediately POSTs a save.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Server")]
    public string authServerUrl = "http://15.204.243.36:3000";

    [Header("Inventory Size")]
    public int maxSlots = 32;

    [System.Serializable]
    public class InventorySlot
    {
        public int    slot_index;
        public string item_id;
        public int    quantity;
        public int    equipped;
    }

    private List<InventorySlot> _slots = new List<InventorySlot>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() => StartCoroutine(LoadInventory());

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void OnItemPickedUp(string itemId, int qty)
    {
        if (itemId.StartsWith("gold:"))
        {
            if (int.TryParse(itemId.Substring(5), out int gold))
                Debug.Log($"[LOOT] Picked up {gold} gold — sync via save-progress");
            return;
        }

        var existing = _slots.Find(s => s.item_id == itemId && s.equipped == 0);
        if (existing != null)
        {
            existing.quantity += qty;
        }
        else
        {
            int next = FindNextFreeSlot();
            if (next < 0) { Debug.LogWarning($"[LOOT] Inventory full — {itemId} dropped"); return; }
            _slots.Add(new InventorySlot { slot_index = next, item_id = itemId, quantity = qty, equipped = 0 });
        }

        Debug.Log($"[LOOT] Picked up {qty}x {itemId}");
        StartCoroutine(SaveInventory());
    }

    public List<InventorySlot> GetSlots() => new List<InventorySlot>(_slots);

    // ─── Load ─────────────────────────────────────────────────────────────────

    public IEnumerator LoadInventory()
    {
        int charId    = AuthManager.CharacterId;
        string token  = AuthManager.Token;
        if (charId <= 0 || string.IsNullOrEmpty(token)) { Debug.LogWarning("[LOOT] LoadInventory: auth not ready"); yield break; }

        using var req = UnityWebRequest.Get($"{authServerUrl}/api/inventory/{charId}");
        req.SetRequestHeader("Authorization", $"Bearer {token}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        { Debug.LogError($"[LOOT] LoadInventory failed: {req.error}"); yield break; }

        var response = JsonUtility.FromJson<InventoryResponse>(req.downloadHandler.text);
        if (response?.success == true)
        {
            _slots = response.data ?? new List<InventorySlot>();
            Debug.Log($"[LOOT] Loaded {_slots.Count} slots for char#{charId}");
        }
    }

    // ─── Save ─────────────────────────────────────────────────────────────────

    public IEnumerator SaveInventory()
    {
        int charId   = AuthManager.CharacterId;
        string token = AuthManager.Token;
        if (charId <= 0 || string.IsNullOrEmpty(token)) { Debug.LogWarning("[LOOT] SaveInventory: auth not ready"); yield break; }

        string json = JsonUtility.ToJson(new InventorySavePayload { characterId = charId, slots = _slots });
        using var req = new UnityWebRequest($"{authServerUrl}/api/inventory/save", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {token}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[LOOT] SaveInventory failed: {req.error}");
        else
            Debug.Log($"[LOOT] Inventory saved — {_slots.Count} slots");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    int FindNextFreeSlot()
    {
        var used = new HashSet<int>();
        foreach (var s in _slots) used.Add(s.slot_index);
        for (int i = 0; i < maxSlots; i++) if (!used.Contains(i)) return i;
        return -1;
    }

    [System.Serializable] class InventoryResponse  { public bool success; public List<InventorySlot> data; public string error; }
    [System.Serializable] class InventorySavePayload { public int characterId; public List<InventorySlot> slots; }
}

// ─── AuthManager stub ─────────────────────────────────────────────────────────
// If you already have an AuthManager with Token + CharacterId, delete this block
// and add AUTHMANAGER_EXISTS to Project Settings → Player → Scripting Define Symbols.
#if !AUTHMANAGER_EXISTS
public static class AuthManager
{
    public static string Token       { get; set; } = "";
    public static int    CharacterId { get; set; } = 0;
}
#endif
