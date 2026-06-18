using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// ── Setup in Unity ───────────────────────────────────────────────────────────
// 1. Create a new scene called "CharacterSelect".
// 2. Add a Canvas (Screen Space - Overlay).
// 3. Add a RawImage to the Canvas — this shows the 3D preview. Assign previewDisplay.
// 4. Add a left arrow Button → calls PreviousCharacter()
//    Add a right arrow Button → calls NextCharacter()
// 5. Add a TextMeshProUGUI for the class name. Assign classNameLabel.
// 6. Add a TextMeshProUGUI for the description. Assign descriptionLabel.
// 7. Add a colored Image panel for the class accent. Assign classColorPanel.
// 8. Add a "Play" Button → calls OnPlayClicked().
// 9. Create a RenderTexture asset (Project → Create → Render Texture, 512x512).
//    Assign it to previewRenderTexture AND to the RawImage texture field.
//10. Create a Preview Camera in the scene, set its Target Texture to the same RenderTexture.
//    Position it to face the previewSpawnPoint. Assign previewCamera.
//11. Create an empty GameObject as the previewSpawnPoint. Assign previewSpawnPoint.
//12. Add this script to any GameObject. Assign characters array with your CharacterData assets.

public class CharacterSelectUI : MonoBehaviour
{
    [Header("Character roster — assign CharacterData assets")]
    public CharacterData[] characters;

    [Header("UI references")]
    public RawImage previewDisplay;
    public TextMeshProUGUI classNameLabel;
    public TextMeshProUGUI descriptionLabel;
    public Image classColorPanel;

    [Header("3D Preview")]
    public Camera previewCamera;
    public RenderTexture previewRenderTexture;
    public Transform previewSpawnPoint;
    public float rotationSpeed = 30f;

    [Header("Scene to load")]
    public string gameScene = "SampleScene";

    private int currentIndex = 0;
    private GameObject currentPreviewInstance;

    void Start()
    {
        if (previewCamera != null)
            previewCamera.targetTexture = previewRenderTexture;

        if (previewDisplay != null)
            previewDisplay.texture = previewRenderTexture;

        ShowCharacter(0);
    }

    void Update()
    {
        if (currentPreviewInstance != null)
            currentPreviewInstance.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    public void NextCharacter()
    {
        currentIndex = (currentIndex + 1) % characters.Length;
        ShowCharacter(currentIndex);
    }

    public void PreviousCharacter()
    {
        currentIndex = (currentIndex - 1 + characters.Length) % characters.Length;
        ShowCharacter(currentIndex);
    }

    void ShowCharacter(int index)
    {
        if (characters == null || characters.Length == 0) return;

        CharacterData data = characters[index];

        // Refresh UI labels
        if (classNameLabel != null)   classNameLabel.text   = data.className;
        if (descriptionLabel != null) descriptionLabel.text = data.description;
        if (classColorPanel != null)  classColorPanel.color = data.classColor;

        // Swap 3D preview model
        if (currentPreviewInstance != null)
            Destroy(currentPreviewInstance);

        GameObject prefab = data.previewPrefab != null ? data.previewPrefab : data.prefab;
        if (prefab != null && previewSpawnPoint != null)
        {
            currentPreviewInstance = Instantiate(prefab, previewSpawnPoint.position, previewSpawnPoint.rotation);

            // Strip gameplay components so the preview is pure visuals
            foreach (var mb in currentPreviewInstance.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb is Animator) continue;
                mb.enabled = false;
            }

            // Put preview on a dedicated layer so only the preview camera sees it
            SetLayerRecursive(currentPreviewInstance, LayerMask.NameToLayer("CharacterPreview"));
        }
    }

    public void OnPlayClicked()
    {
        // Store chosen class for the game scene to read
        PlayerPrefs.SetInt("SelectedCharacter", currentIndex);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameScene);
    }

    void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return; // layer doesn't exist yet — safe to skip
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
