using UnityEngine;
using Mirror;

/// <summary>
/// WorldItem — Dropped item in the world. Floats, rotates, glows by rarity.
///
/// Pickup flow:
///   Player enters trigger → CmdPickup (server) → RpcOnPickedUp (local client only)
///   → InventoryManager.OnItemPickedUp → POST /api/inventory/save
///
/// itemId "gold:N" awards gold instead of an inventory item.
/// Prefab built by BCE/Setup/4d.
/// </summary>
public class WorldItem : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnItemIdChanged))]
    public string itemId = "";

    [SyncVar]
    public int quantity = 1;

    [Header("Float / Spin")]
    public float floatSpeed     = 1.3f;
    public float floatAmplitude = 0.18f;
    public float rotateSpeed    = 55f;

    [Header("Glow")]
    public Light glowLight;

    private Vector3 _origin;
    private bool    _pickedUp = false;

    static readonly Color ColorCommon   = new Color(0.75f, 0.75f, 0.75f);
    static readonly Color ColorUncommon = new Color(0.2f,  0.9f,  0.2f);
    static readonly Color ColorRare     = new Color(0.2f,  0.5f,  1f);
    static readonly Color ColorEpic     = new Color(0.7f,  0.1f,  1f);
    static readonly Color ColorGold     = new Color(1f,    0.8f,  0.1f);

    void Start()
    {
        _origin = transform.position;
        ApplyRarityGlow(itemId);
    }

    void Update()
    {
        float y = _origin.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_pickedUp || !other.CompareTag("Player")) return;
        var netId = other.GetComponent<NetworkIdentity>();
        if (netId == null || !netId.isLocalPlayer) return;

        _pickedUp = true;
        CmdPickup(netId.netId);
    }

    [Command(requiresAuthority = false)]
    void CmdPickup(uint playerNetId)
    {
        if (_pickedUp && !isServer) return;
        _pickedUp = true;
        RpcOnPickedUp(playerNetId, itemId, quantity);
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcOnPickedUp(uint pickerNetId, string pickedItemId, int qty)
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        var localNetId = localPlayer.GetComponent<NetworkIdentity>();
        if (localNetId == null || localNetId.netId != pickerNetId) return;

        var inv = InventoryManager.Instance;
        if (inv != null)
            inv.OnItemPickedUp(pickedItemId, qty);
        else
            Debug.LogWarning($"[LOOT] InventoryManager not found — {pickedItemId} x{qty} lost on client");
    }

    void OnItemIdChanged(string _, string newVal) => ApplyRarityGlow(newVal);

    void ApplyRarityGlow(string id)
    {
        if (glowLight == null) return;
        glowLight.color = GetRarityColor(id);
    }

    public static Color GetRarityColor(string id)
    {
        if (string.IsNullOrEmpty(id))                    return ColorCommon;
        if (id.StartsWith("gold:"))                      return ColorGold;
        if (id.Contains("epic"))                         return ColorEpic;
        if (id.Contains("iron") || id.Contains("rare"))  return ColorRare;
        if (id.Contains("bar")  || id.Contains("uncommon")) return ColorUncommon;
        return ColorCommon;
    }
}
