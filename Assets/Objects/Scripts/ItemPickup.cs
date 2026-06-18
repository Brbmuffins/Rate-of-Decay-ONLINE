using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ItemPickup : MonoBehaviour
{
    public ItemData item;
    public float respawnTime = 30f;

    public GameObject visualRoot;

    private bool playerInRange = false;
    private bool available = true;
    private Inventory inventory;

    void Awake()
    {
        if (visualRoot == null && transform.parent != null)
        {
            visualRoot = transform.parent.gameObject;
        }
    }

    void Update()
    {
        if (!available)
            return;

        if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryPickup();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        inventory = other.GetComponentInParent<Inventory>();

        if (inventory == null)
            return;

        playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        inventory = null;
    }

    void TryPickup()
    {
        if (inventory == null)
            return;

        if (item == null)
        {
            Debug.Log("ItemPickup has no ItemData assigned.");
            return;
        }

        if (inventory.AddItem(item))
        {
            StartCoroutine(Respawn());
        }
    }

    IEnumerator Respawn()
    {
        available = false;
        playerInRange = false;
        inventory = null;

        SetVisuals(false);

        yield return new WaitForSeconds(respawnTime);

        SetVisuals(true);
        available = true;
    }

    void SetVisuals(bool visible)
    {
        if (visualRoot == null)
            return;

        foreach (Transform child in visualRoot.transform)
        {
            if (child == transform)
                continue;

            child.gameObject.SetActive(visible);
        }
    }
}