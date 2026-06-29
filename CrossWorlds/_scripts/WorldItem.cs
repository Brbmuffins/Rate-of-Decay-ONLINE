using UnityEngine;
using Mirror;

/// <summary>
/// WorldItem — A dropped item in the world that players can walk into to pick up.
/// Floats, rotates, and glows by rarity. Pickup is server-authoritative:
///   Client enters trigger → CmdPickup → server destroys object → RpcPickedUp
///   → local player's InventoryManager.OnItemPickedUp → POST /api/inventory/save
///
/// itemId prefixes that determine glow:
///   "gold:"       → gold award (parsed by InventoryManager)
///   "material_"   → common (gray glow)
///   "copper_bar"  → uncommon (green glow)
///   "*_iron"      → rare (blue glow)
///   "epic_*"      → epic (purple glow)
///
/// Copy to: Assets/Game/Combat/WorldItem.cs
/// Prefab:  BCE/Setup/4d ▶ Create WorldItem Prefab
/// </summary>
public class WorldItem : NetworkBehaviour
{
    // ─── Synced Data ──────────────────────────────────────────────────────────
    [SyncVar(hook = nameof(OnItemIdChanged))]
    public string itemId = "";

    [SyncVar]
    public int quantity = 1;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Float / Spin")]
    public float floatSpeed     = 1.3f;
    public float floatAmplitude = 0.18f;
    public float rotateSpeed    = 55f;

    [Header("Glow")]
    public Light glowLight;   // assign in prefab; built automatically by EnemyBuilder

    // ─── Internal ─────────────────────────────────────────────────────────────
    private Vector3 _origin;
    private bool    _pickedUp = false;

    // Rarity glow palette
    static readonly Color ColorCommon   = new Color(0.75f, 0.75f, 0.75f);
    static readonly Color ColorUncommon = new Color(0.2f,  0.9f,  0.2f);
    static readonly Color ColorRare     = new Color(0.2f,  0.5f,  1f);
    static readonly Color ColorEpic     = new Color(0.7f,  0.1f,  1f);
    static readonly Color ColorGold     = new Color(1f,    0.8f,  0.1f);

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        _origin = transform.position;
        ApplyRarityGlow(itemId);
    }

    void Update()
    {
        // Float
        float y = _origin.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
        // Spin
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    // ─── Pickup Trigger ───────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_pickedUp) return;
        if (!other.CompareTag("Player")) return;

        // Only the local player triggers pickup
        var netId = other.GetComponent<NetworkIdentity>();
        if (netId == null || !netId.isLocalPlayer) return;

        _pickedUp = true;
        CmdPickup(netId.netId);
    }

    [Command(requiresAuthority = false)]
    void CmdPickup(uint playerNetId)
    {
        // Guard against double-pickup (e.g. two players collide simultaneously)
        if (_pickedUp && !isServer) return;
        _pickedUp = true;

        RpcOnPickedUp(playerNetId, itemId, quantity);
        NetworkServer.Destroy(gameObject);
    }

    // ─── Client RPC — notifies the picking-up player ──────────────────────────
    [ClientRpc]
    void RpcOnPickedUp(uint pickerNetId, string pickedItemId, int qty)
    {
        // Only the player who picked it up processes this
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        var localNetId = localPlayer.GetComponent<NetworkIdentity>();
        if (localNetId == null || localNetId.netId != pickerNetId) return;

        var inv = InventoryManager.Instance;
        if (inv != null)
            inv.OnItemPickedUp(pickedItemId, qty);
        else
            Debug.LogWarning("[LOOT] InventoryManager not found — item not registered");
    }

    // ─── SyncVar Hook — apply glow as soon as itemId is synced ───────────────
    void OnItemIdChanged(string oldVal, string newVal)
    {
        ApplyRarityGlow(newVal);
    }

    // ─── Glow ─────────────────────────────────────────────────────────────────
    void ApplyRarityGlow(string id)
    {
        if (glowLight == null) return;
        glowLight.color = GetRarityColor(id);
    }

    public static Color GetRarityColor(string id)
    {
        if (string.IsNullOrEmpty(id)) return ColorCommon;
        if (id.StartsWith("gold:"))              return ColorGold;
        if (id.Contains("epic"))                 return ColorEpic;
        if (id.Contains("iron") || id.Contains("rare")) return ColorRare;
        if (id.Contains("bar") || id.Contains("uncommon")) return ColorUncommon;
        return ColorCommon;
    }
}
