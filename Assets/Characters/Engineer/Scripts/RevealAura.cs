using UnityEngine;

public class RevealAura : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        ObjectUI ui = other.GetComponentInParent<ObjectUI>();

        if (ui != null)
            ui.ShowUI();
    }

    private void OnTriggerExit(Collider other)
    {
        ObjectUI ui = other.GetComponentInParent<ObjectUI>();

        if (ui != null)
            ui.HideUI();
    }
}