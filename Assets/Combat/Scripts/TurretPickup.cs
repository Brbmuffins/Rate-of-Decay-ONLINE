using UnityEngine;
using UnityEngine.InputSystem;

public class TurretPickup : MonoBehaviour
{
    public ItemData item;
    public Inventory inventory;
    public float interactRange = 3f;

    void Update()
    {
        if (inventory == null || item == null) return;

        float dist = Vector3.Distance(transform.position, inventory.transform.position);
        if (dist <= interactRange && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (inventory.AddItem(item))
            {
                Destroy(gameObject);
            }
        }
    }
}
