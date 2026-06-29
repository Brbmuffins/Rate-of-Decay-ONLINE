using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DropEntry — one item in a DropTable.
/// itemId must match items.id in rod_online DB (e.g. "material_copper_shard").
/// </summary>
[System.Serializable]
public class DropEntry
{
    public string itemId;
    [Tooltip("Relative probability weight. Higher = more common than other entries.")]
    public float weight = 1f;
    public int   minQty = 1;
    public int   maxQty = 1;
}

/// <summary>
/// DropTable — ScriptableObject assigned to each enemy prefab's EnemyController.
///
/// Create via: Right-click in Project → Create → BCE → DropTable
/// Or auto-created by BCE/Setup/4a–4c editor menu items.
///
/// Baseline drop rates (from COMBAT.md):
///   Grunt:  60% nothing, 30% copper_shard(1–2), 10% copper_bar(1),   gold 1–5
///   Ranged: 65% nothing, 35% copper_shard(1–2),                       gold 1–3
///   Elite:  20% nothing, 40% copper_bar(1–2),  30% copper_shard(2–4),
///           10% random gear, gold 10–25
/// </summary>
[CreateAssetMenu(fileName = "DropTable", menuName = "BCE/DropTable")]
public class DropTable : ScriptableObject
{
    [Header("Gold (always awarded on kill)")]
    public int minGold = 1;
    public int maxGold = 5;

    [Header("Item Drops")]
    [Tooltip("Weight given to 'drop nothing'. Set high to make items rare.")]
    [Range(0f, 20f)] public float nothingWeight = 6f;
    public List<DropEntry> drops = new List<DropEntry>();

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Roll the drop table. Returns the item(s) that dropped and gold amount.
    /// Server-side only — do not call on clients.
    /// </summary>
    public (List<(string itemId, int qty)> items, int gold) RollDrops()
    {
        int gold  = Random.Range(minGold, maxGold + 1);
        var items = new List<(string, int)>();

        float total = nothingWeight;
        foreach (var d in drops)
            total += Mathf.Max(0f, d.weight);

        if (total <= 0f || drops == null || drops.Count == 0)
            return (items, gold);

        float roll = Random.value * total;

        // Nothing roll
        if (roll < nothingWeight)
            return (items, gold);

        // Item roll — walk the cumulative distribution
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

    /// Probability that at least one item drops (0–1).
    public float ItemDropChance()
    {
        float total = nothingWeight;
        foreach (var d in drops) total += d.weight;
        return total > 0f ? (total - nothingWeight) / total : 0f;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Warn on zero-weight entries that would never drop
        if (drops == null) return;
        foreach (var d in drops)
            if (d.weight <= 0f)
                UnityEngine.Debug.LogWarning($"[DropTable] {name}: entry '{d.itemId}' has weight ≤ 0 and will never drop.");
    }
#endif
}
