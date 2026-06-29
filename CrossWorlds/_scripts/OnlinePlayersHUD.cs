using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// Displays connected players as a clean vertical list in the top-left HUD.
/// Replaces the world-position-based name overlay that caused overlap.
///
/// Setup:
///   1. Attach this to your HUD Canvas GameObject.
///   2. Create a child Panel: OnlinePlayersPanel
///        - Add a VerticalLayoutGroup (spacing 4, child force expand width ON)
///        - Add a ContentSizeFitter (vertical: PreferredSize)
///   3. Create a Text prefab for each row (assign to rowPrefab below).
///        - Font size 13, color white, alignment MiddleLeft
///        - Add LayoutElement, preferredHeight = 20
///   4. Assign the panel and prefab in the Inspector.
/// </summary>
public class OnlinePlayersHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The panel containing the vertical list. Has VerticalLayoutGroup.")]
    public Transform rowContainer;

    [Tooltip("A UI Text prefab used for each player row.")]
    public GameObject rowPrefab;

    [Header("Settings")]
    [Tooltip("How often (seconds) to refresh the list.")]
    public float refreshInterval = 1.5f;

    // ── internals ──────────────────────────────────────────────────────────
    private float _timer;
    private readonly List<GameObject> _rows = new List<GameObject>();

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            Refresh();
        }
    }

    void Refresh()
    {
        // Collect current player names from Mirror connections
        var names = new List<string>();

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;

            // Try to get the player's display name.
            // Adjust the component/property to match your player prefab.
            var player = conn.identity.GetComponent<PlayerInfo>();
            if (player != null)
                names.Add(player.displayName);
            else
                names.Add($"Player {conn.connectionId}");
        }

        // If running as client (not host), fall back to NetworkClient identities
        if (!NetworkServer.active)
        {
            names.Clear();
            foreach (var identity in FindObjectsOfType<NetworkIdentity>())
            {
                var player = identity.GetComponent<PlayerInfo>();
                if (player != null)
                    names.Add(player.displayName);
            }
        }

        RebuildList(names);
    }

    void RebuildList(List<string> names)
    {
        // Reuse existing rows, create new ones, destroy extras
        for (int i = 0; i < names.Count; i++)
        {
            GameObject row;
            if (i < _rows.Count)
            {
                row = _rows[i];
            }
            else
            {
                row = Instantiate(rowPrefab, rowContainer);
                _rows.Add(row);
            }

            row.SetActive(true);
            var label = row.GetComponent<Text>();
            if (label != null)
                label.text = $"● {names[i]}";
        }

        // Hide unused rows
        for (int i = names.Count; i < _rows.Count; i++)
            _rows[i].SetActive(false);

        // Update the header count
        UpdateHeader(names.Count);
    }

    void UpdateHeader(int count)
    {
        // Optional: if you have a separate Text for "ONLINE  N players"
        if (_headerLabel != null)
            _headerLabel.text = $"ONLINE  {count} {(count == 1 ? "player" : "players")}";
    }

    [Header("Optional")]
    [Tooltip("The 'ONLINE N players' header Text (optional).")]
    public Text _headerLabel;
}
