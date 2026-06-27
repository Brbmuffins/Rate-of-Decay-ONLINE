using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// ═══════════════════════════════════════════════════════════════════════════
//  LoginScreenVFX
//  Attach to a GameObject called "LoginScreenVFX" in your Login scene.
//  Handles all 3D atmosphere behind the login UI.
//
//  DRAG THESE PREFABS INTO THE INSPECTOR SLOTS:
//  ─────────────────────────────────────────────
//  dustMotes       → brbmuffins Technologies/…/Misc Effects/Prefabs/DustMotesEffect.prefab
//  fireFlies       → brbmuffins Technologies/…/Misc Effects/Prefabs/FireFlies.prefab
//  groundFog       → brbmuffins Technologies/…/Smoke & Steam Effects/Prefabs/GroundFog.prefab
//  electricSparks  → brbmuffins Technologies/…/Misc Effects/Prefabs/ElectricalSparks.prefab
//  lightPillar     → brbmuffins VFX/brbmuffins Free VFX/Prefab/FX_LightPillar.prefab
//  lootBeam        → brbmuffins VFX/brbmuffins Free VFX/Prefab/FX_LootDrop_Blue.prefab
//  magicCircle     → brbmuffins Studio/…/Magic circles/Magic circle.prefab
//  magicCircle2    → brbmuffins Studio/…/Magic circles/Magic circle 2.prefab
//  plexusAura      → brbmuffins Studio/…/Character auras/Plexus.prefab
//  lightningAura   → brbmuffins Studio/…/Character auras/Lightning aura.prefab
//  portalBlue      → brbmuffins Studio/…/Portals/Portal blue.prefab
//  glowingOrbs     → brbmuffins Dark Arts/…/Glowing orbs.prefab
//  cosmosTrail     → brbmuffins Trails/…/VFX/Particles/VFX_Trail_Cosmos.prefab
//  voidTrail       → brbmuffins Trails/…/VFX/Particles/VFX_Trail_Void.prefab
//
//  SKYBOX: Assign FS013_Night.mat or FS017_Night.mat to RenderSettings.skybox.
//  In Unity: Window → Rendering → Lighting → Environment → Skybox Material
//
//  SCENE SETUP:
//   1. Create empty scene
//   2. Camera at (0, 2, -10), rotation (8, 0, 0) — slight downward angle
//   3. Directional light at intensity 0.15, color #2233AA (deep blue night)
//   4. Add this script to a GameObject
//   5. Assign the LoginManager Canvas on top (Screen Space Overlay)
// ═══════════════════════════════════════════════════════════════════════════

[AddComponentMenu("BCE/UI/Login Screen VFX")]
public class LoginScreenVFX : MonoBehaviour
{
    [Header("Ambient — assign prefabs from brbmuffins packs")]
    public GameObject dustMotes;        // floating dust particles — covers whole scene
    public GameObject fireFlies;        // subtle floating lights
    public GameObject groundFog;        // fog at the base

    [Header("Energy — foreground life")]
    public GameObject electricSparks;   // occasional spark bursts
    public GameObject lightningAura;    // humming energy sphere in background

    [Header("Focal Points — background landmarks")]
    public GameObject lightPillar;      // vertical beam, off to one side
    public GameObject lootBeam;         // second beam — opposite side
    public GameObject magicCircle;      // slowly rotating in midground
    public GameObject magicCircle2;     // second ring, different scale/depth
    public GameObject plexusAura;       // network sphere — sci-fi centerpiece
    public GameObject portalBlue;       // portal at far background
    public GameObject glowingOrbs;      // ambient orbs floating near ground

    [Header("Trailing Energy — slow orbit")]
    public GameObject cosmosTrail;      // slow cosmos trail around centerpiece
    public GameObject voidTrail;        // dark void energy

    [Header("On-Event VFX (assigned at runtime or via Inspector)")]
    public GameObject loginSuccessVFX;  // FX_LootDrop_Blue or similar
    public GameObject loginFailVFX;     // ElectricalSparks burst

    [Header("Ambient Audio")]
    public AudioClip ambientLoop;       // AmbientNatureNightRainy.wav works great

    [Header("Timing")]
    public float sparksInterval = 8f;   // seconds between random spark bursts
    public float slowRotateSpeed = 3f;  // degrees/sec for magic circles

    // ── Runtime references ───────────────────────────────────────────────────
    private GameObject _circle1Instance;
    private GameObject _circle2Instance;
    private GameObject _plexusInstance;
    private AudioSource _audioSource;

    // ── Positions relative to camera ─────────────────────────────────────────
    // All Z values positive = in front of camera (scene origin = 0)
    readonly Vector3 POS_CENTER   = new Vector3( 0f,  0f,   8f);
    readonly Vector3 POS_LEFT     = new Vector3(-7f,  0f,  12f);
    readonly Vector3 POS_RIGHT    = new Vector3( 7f,  0f,  12f);
    readonly Vector3 POS_FARBACK  = new Vector3( 0f, -1f,  20f);
    readonly Vector3 POS_GROUND   = new Vector3( 0f, -3f,   6f);
    readonly Vector3 POS_ORBS     = new Vector3( 3f, -1f,   7f);

    void Start()
    {
        SpawnAtmosphere();
        StartCoroutine(RandomSparks());

        if (ambientLoop != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip       = ambientLoop;
            _audioSource.loop       = true;
            _audioSource.volume     = 0.25f;
            _audioSource.spatialBlend = 0f; // 2D
            _audioSource.Play();
            StartCoroutine(FadeInAudio());
        }
    }

    void Update()
    {
        // Slowly rotate both magic circles in opposite directions
        if (_circle1Instance != null)
            _circle1Instance.transform.Rotate(Vector3.up, slowRotateSpeed * Time.deltaTime);
        if (_circle2Instance != null)
            _circle2Instance.transform.Rotate(Vector3.up, -slowRotateSpeed * 0.6f * Time.deltaTime);
        // Gently bob the plexus sphere
        if (_plexusInstance != null)
            _plexusInstance.transform.position = POS_CENTER
                + Vector3.up * Mathf.Sin(Time.time * 0.4f) * 0.3f;
    }

    // ── Scene population ─────────────────────────────────────────────────────

    void SpawnAtmosphere()
    {
        // Wide-coverage ambient — cover the whole scene
        Spawn(dustMotes,     new Vector3(0f, 2f, 5f),   Vector3.one * 3f);
        Spawn(fireFlies,     new Vector3(0f, 0f, 6f),   Vector3.one * 2f);
        Spawn(groundFog,     POS_GROUND,                 new Vector3(4f, 1f, 4f));
        Spawn(glowingOrbs,   POS_ORBS,                   Vector3.one);

        // Energy focal points — give the scene depth and interest
        _plexusInstance   = Spawn(plexusAura,   POS_CENTER,   Vector3.one * 1.4f);
        _circle1Instance  = Spawn(magicCircle,  POS_CENTER + Vector3.down * 1.5f,  Vector3.one * 2.5f);
        _circle2Instance  = Spawn(magicCircle2, POS_CENTER + Vector3.down * 1.5f,  Vector3.one * 3.2f);

        // Landmarks at the edges — create a sense of location
        Spawn(lightPillar,   POS_LEFT  + Vector3.up * 2f, Vector3.one);
        Spawn(lootBeam,      POS_RIGHT + Vector3.up * 2f, Vector3.one * 0.7f);
        Spawn(lightningAura, POS_LEFT  + new Vector3(2f, -0.5f, 0f), Vector3.one * 0.8f);
        Spawn(portalBlue,    POS_FARBACK, Vector3.one * 1.8f);

        // Trailing energy — subtle motion in the background
        Spawn(cosmosTrail, POS_CENTER + new Vector3(-2f, 1f, -2f), Vector3.one * 0.5f);
        Spawn(voidTrail,   POS_CENTER + new Vector3( 2f, 0f, -2f), Vector3.one * 0.5f);
    }

    GameObject Spawn(GameObject prefab, Vector3 pos, Vector3 scale)
    {
        if (prefab == null) return null;
        GameObject go = Instantiate(prefab, pos, Quaternion.identity, transform);
        go.transform.localScale = scale;
        return go;
    }

    // ── Random events ────────────────────────────────────────────────────────

    IEnumerator RandomSparks()
    {
        while (true)
        {
            yield return new WaitForSeconds(sparksInterval + Random.Range(-2f, 2f));
            if (electricSparks != null)
            {
                Vector3 pos = new Vector3(
                    Random.Range(-5f, 5f),
                    Random.Range(-2f, 1f),
                    Random.Range(4f, 10f));
                GameObject sparks = Instantiate(electricSparks, pos, Quaternion.identity);
                Destroy(sparks, 3f);
            }
        }
    }

    IEnumerator FadeInAudio()
    {
        _audioSource.volume = 0f;
        float t = 0f;
        while (t < 3f)
        {
            t += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(0f, 0.25f, t / 3f);
            yield return null;
        }
    }

    // ── Public hooks — called by LoginManager ─────────────────────────────────

    /// <summary>Called when login succeeds. Plays success beam + audio swell.</summary>
    public void OnLoginSuccess()
    {
        if (loginSuccessVFX != null)
            Instantiate(loginSuccessVFX, POS_CENTER, Quaternion.identity);

        if (_audioSource != null)
            StartCoroutine(AudioSwell());
    }

    /// <summary>Called when login fails. Plays a spark burst.</summary>
    public void OnLoginFail()
    {
        if (loginFailVFX != null)
        {
            Vector3 pos = new Vector3(0f, -1f, 5f);
            GameObject go = Instantiate(loginFailVFX, pos, Quaternion.identity);
            Destroy(go, 2f);
        }
    }

    IEnumerator AudioSwell()
    {
        float start = _audioSource.volume;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            _audioSource.volume = Mathf.Lerp(start, 0.5f, t);
            yield return null;
        }
    }
}
