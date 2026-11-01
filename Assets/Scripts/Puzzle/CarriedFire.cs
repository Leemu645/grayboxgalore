using UnityEngine;

/// <summary>
/// A carried plank or splinter that has been lit on the furnace.
/// The item stays in the inventory. While it is selected, a small flame and a
/// warm point light follow the camera for ten seconds.
/// </summary>
public class CarriedFire : MonoBehaviour
{
    public static CarriedFire Instance { get; private set; }

    const float BurnSeconds = 10f;

    ItemId burning = ItemId.None;
    float burnOutTime;
    bool announcedOut;
    Transform viewCamera;
    GameObject flameRoot;
    Light flameLight;
    Material flameMaterial;
    AudioSource crackle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one CarriedFire is in the scene.", this);
            return;
        }

        Instance = this;
        Camera child = GetComponentInChildren<Camera>();
        if (child != null)
            viewCamera = child.transform;
        BuildFlame();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Ignite(ItemId item)
    {
        burning = item;
        burnOutTime = Time.time + BurnSeconds;
        announcedOut = false;
        if (PlayerInteractor.Instance != null)
            PlayerInteractor.Instance.ShowMessage(ItemNames.Label(item) + " catches fire.");

        if (GameAudio.Instance != null)
        {
            GameAudio.Instance.PlayCatchFire();
            if (crackle == null)
                crackle = GameAudio.Instance.BeginFireCrackle(transform);
        }
    }

    void Update()
    {
        if (burning == ItemId.None)
        {
            SetFlame(false);
            return;
        }

        if (Time.time >= burnOutTime)
        {
            burning = ItemId.None;
            SetFlame(false);
            if (GameAudio.Instance != null)
                GameAudio.Instance.EndLoop(crackle);
            crackle = null;
            if (!announcedOut && PlayerInteractor.Instance != null)
            {
                announcedOut = true;
                PlayerInteractor.Instance.ShowMessage("The flame dies out.");
            }

            return;
        }

        bool show = Inventory.Instance != null && Inventory.Instance.Selected == burning && !PlayerControl.Suspended;
        SetFlame(show);
        if (!show || flameLight == null)
            return;

        float flicker = 0.75f + Mathf.PerlinNoise(Time.time * 9f, 0.4f) * 0.5f;
        flameLight.intensity = 2.2f * flicker;
        if (flameRoot != null)
            flameRoot.transform.localScale = Vector3.one * (0.85f + 0.3f * flicker);
    }

    void SetFlame(bool on)
    {
        if (flameRoot != null && flameRoot.activeSelf != on)
            flameRoot.SetActive(on);
    }

    void BuildFlame()
    {
        if (viewCamera == null)
            return;

        flameRoot = new GameObject("CarriedFlame");
        flameRoot.transform.SetParent(viewCamera, false);
        flameRoot.transform.localPosition = new Vector3(0.22f, -0.18f, 0.42f);

        GameObject fire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fire.name = "Flame";
        fire.transform.SetParent(flameRoot.transform, false);
        fire.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
        Collider fireCollider = fire.GetComponent<Collider>();
        if (fireCollider != null)
            Destroy(fireCollider);

        flameMaterial = MakeFlameMaterial();
        Renderer renderer = fire.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (flameMaterial != null)
            renderer.sharedMaterial = flameMaterial;

        GameObject lightObject = new GameObject("FlameLight");
        lightObject.transform.SetParent(flameRoot.transform, false);
        flameLight = lightObject.AddComponent<Light>();
        flameLight.type = LightType.Point;
        flameLight.color = new Color(1f, 0.55f, 0.18f);
        flameLight.useColorTemperature = false;
        flameLight.range = 7f;
        flameLight.intensity = 2.2f;
        flameLight.shadows = LightShadows.None;

        flameRoot.SetActive(false);
    }

    static Material MakeFlameMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        Color color = new Color(1f, 0.45f, 0.08f);
        material.color = color;
        material.SetColor("_BaseColor", color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 2.5f);
        return material;
    }
}
