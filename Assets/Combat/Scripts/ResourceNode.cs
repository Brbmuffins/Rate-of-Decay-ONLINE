using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ResourceNode : MonoBehaviour
{
    public ItemData yieldItem;
    public int hitsToDeplete = 3;
    public float respawnTime = 60f;
    public float interactRange = 3f;

    private int hitsRemaining;
    private Collider col;
    private Renderer[] renderers;
    private Inventory inventory;

    void Awake()
    {
        hitsRemaining = hitsToDeplete;
        col = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (yieldItem == null) return;

        if (inventory == null)
        {
            inventory = FindFirstObjectByType<Inventory>();
            if (inventory == null) return;
        }

        float dist = Vector3.Distance(transform.position, inventory.transform.position);
        if (dist <= interactRange && Keyboard.current.fKey.wasPressedThisFrame)
        {
            Harvest();
        }
    }

    void Harvest()
    {
        if (!inventory.AddItem(yieldItem)) return;

        hitsRemaining--;
        if (hitsRemaining <= 0)
        {
            StartCoroutine(Deplete());
        }
    }

    IEnumerator Deplete()
    {
        SetVisible(false);
        yield return new WaitForSeconds(respawnTime);
        hitsRemaining = hitsToDeplete;
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        col.enabled = visible;
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
    }
}
