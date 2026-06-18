using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    public string characterSelectScene = "CharacterSelect";

    public void OnPlayClicked()
    {
        SceneManager.LoadScene(characterSelectScene);
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }
}
