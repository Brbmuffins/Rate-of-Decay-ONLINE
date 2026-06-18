using UnityEngine;

public class EnemyUI : MonoBehaviour
{
    public GameObject nameplateAndHealthbar;

    void Start()
    {
        nameplateAndHealthbar.SetActive(false);
    }

    public void ShowUI()
    {
        nameplateAndHealthbar.SetActive(true);
    }

    public void HideUI()
    {
        nameplateAndHealthbar.SetActive(false);
    }
}