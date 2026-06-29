using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DropEntry — one item in a drop table.
/// itemId must match items.id in rod_online (e.g. "material_copper_shard").
/// weight is relative probability — does NOT need to sum to 1.
/// </summary>
[System.Serializable]
public class DropEntry
{
    public string itemId;
    [Tooltip("Relative probability weight. Higher = more common.")]
    public float weight = 1f;
    public int minQty = 1;
    public int maxQty = 1;
}

/// <summary>
/// DropTable — ScriptableObject assigned to each enemy prefab.
/// Created via BCE/Setup menu items, or manually:
///   Right-click in Project → Create → BCE → DropTable
///
/// Copy to: Assets/Game/Data/DropTables/
///
/// Baseline tables (from COMBAT.md):
///   Grunt:  60% nothing, 30% copper_shard(1–2), 10% copper_bar(1),   gold 1–5
///   Elite:  20% nothing, 40% copper_bar(1–2),  30% copper_shard(2–4),
///           10% random gear, gold 10–25
/// </summary>
[CreateAssetMenu(fileName = "DropTable", menuName = "BCE/DropTable")]
public class DropTable : ScriptableObject
{
    [Header("Gold")]
    public int minGold = 1;
    public int maxGold = 5;

    [Header("Items")]
    [Tooltip("Probability weight for dropping nothing. Tune alongside drops.")]
    [Range(0f, 10f)] public float nothingWeight = 6f;
    public List<DropEntry> drops = new List<DropEntry>();

    /// <summary>
    /// Roll the table. Returns all items that dropped and the gold amount.
    /// Rolls one item OR nothing — not multiple items per kill.
    /// Call server-side only.
    /// </summary>
    public (List<(string itemId, int qty)> items, int gold) RollDrops()
    {
        int gold = Random.Range(minGold, maxGold + 1);
        var items = new List<(string, int)>();

        // Build total weight pool
        float total = nothingWeight;
        foreach (var d in drops) total += Mathf.Max(0f, d.weight);
        if (total <= 0f) return (items, gold);

        float roll = Random.value * total;
        if (roll < nothingWeight) return (items, gold);

        // Walk the drop list
        float cumulative = nothingWeight;
        foreach (var d in drops)
        {
            cumulative += d.weight;
            if (roll < cumulative)
            {
                int qty = Random.Range(d.minQty, d.maxQty + 1);
                items.Add((d.itemId, qty));
                break;
            }
        }

        return (items, gold);
    }

    /// <summary>Normalized chance of getting any item (for tooltips/UI).</summary>
    public float ItemDropChance()
    {
        float total = nothingWeight;
        foreach (var d in drops) total += d.weight;
        return total > 0f ? (total - nothingWeight) / total : 0f;
    }
}
