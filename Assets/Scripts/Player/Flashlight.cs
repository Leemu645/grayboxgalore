using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A flashlight the player can carry. It does nothing until something calls Give,
/// usually the pickup on the basement floor. After that, F (or the gamepad right
/// bumper) toggles the beam.
///
/// F is read straight from the keyboard because the template input asset has no
/// flashlight action. That is the same style as Escape on the player controller.
/// The beam and the small model in the corner of the view are created under the
/// camera when Play starts, using the numbers below.
/// </summary>
public class Flashlight : MonoBehaviour
{
    [Header("Beam")]
    [Tooltip("How far the beam reaches, in meters.")]
    [Min(1f)]
    [SerializeField] float range = 20f;

    [Tooltip("Width of the beam at the end, in degrees. 55 is a medium cone.")]
    [Range(10f, 120f)]
    [SerializeField] float spotAngle = 55f;

    [Tooltip("Bright center of the cone, in degrees. Keep this lower than Spot Angle. The gap between the two is the soft edge.")]
    [Range(0f, 120f)]
    [SerializeField] float innerSpotAngle = 32f;

    [Tooltip("Warm-white color of the beam.")]
    [SerializeField] Color beamColor = new Color(1f, 0.94f, 0.82f);

    [Tooltip("Brightness. Uses the same scale as the basement bulbs (the hanging bulb is about 2). A spot concentrates that light, so 10 reads as a strong flashlight.")]
    [Min(0f)]
    [SerializeField] float intensity = 10f;

    [Tooltip("Cast shadows from the beam. Turn this off if the beam looks blotchy up close.")]
    [SerializeField] bool castShadows = true;

    [Header("Flicker")]
    [Tooltip("Wobble the brightness a little. Off by default, so the beam stays steady.")]
    [SerializeField] bool flicker;

    [Tooltip("How deep the wobble goes. 0.25 means the beam dips to 75 percent at its dimmest.")]
    [Range(0f, 0.8f)]
    [SerializeField] float flickerStrength = 0.25f;

    [Header("Optional")]
    [Tooltip("Fires when the beam is toggled. Hook a click sound up here later. No audio clip is required.")]
    [SerializeField] UnityEvent onToggle;

    [Header("Look")]
    [Tooltip("Metal for the flashlight in your hand. If empty, a simple dark material is made at Play.")]
    [SerializeField] Material bodyMaterial;

    [Tooltip("Dim glow for the lens in your hand. If empty, a faint warm material is made at Play.")]
    [SerializeField] Material lensMaterial;

    Transform viewCamera;
    Light spot;
    GameObject heldModel;
    Material runtimeBodyMaterial;
    Material runtimeLensMaterial;
    bool owned;
    bool turnedOn;

    public bool HasFlashlight => owned;

    public bool IsOn => owned && turnedOn;

    bool IsSelected()
    {
        return owned && Inventory.Instance != null && Inventory.Instance.Selected == ItemId.Flashlight;
    }

    /// <summary>
    /// The player now has the flashlight. The beam stays off until they press F.
    /// </summary>
    public void Give()
    {
        owned = true;
        turnedOn = false;
        ApplyBeamSettings();
        RefreshVisuals();
    }

    void Awake()
    {
        EnsureRig();
        ApplyBeamSettings();
        RefreshVisuals();
    }

    void Update()
    {
        if (PlayerControl.Suspended || !owned || spot == null)
            return;

        // F toggles any time the flashlight is owned. The model hides when another
        // item is selected, but a lit beam stays clipped to the camera.
        if (WasTogglePressed())
        {
            turnedOn = !turnedOn;
            onToggle?.Invoke();
        }

        RefreshVisuals();

        if (!spot.enabled)
            return;

        spot.intensity = flicker ? intensity * FlickerScale() : intensity;
    }

    void OnValidate()
    {
        range = Mathf.Max(1f, range);
        spotAngle = Mathf.Clamp(spotAngle, 10f, 120f);
        innerSpotAngle = Mathf.Clamp(innerSpotAngle, 0f, spotAngle);
        intensity = Mathf.Max(0f, intensity);

        if (Application.isPlaying && spot != null)
            ApplyBeamSettings();
    }

    bool WasTogglePressed()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            return true;

        // Right bumper. The face buttons are already jump, interact, crouch, and attack.
        return Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame;
    }

    float FlickerScale()
    {
        float noise = Mathf.PerlinNoise(Time.time * 9f, 1.7f);
        return Mathf.Lerp(1f - flickerStrength, 1f, noise);
    }

    void EnsureRig()
    {
        if (viewCamera == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
                viewCamera = childCamera.transform;
        }

        if (viewCamera == null)
        {
            Debug.LogError("Flashlight on " + name + " could not find a child camera.", this);
            return;
        }

        if (spot == null)
        {
            // Sit the bulb just in front of the head. The camera is inside the
            // body capsule, and a light left in there cannot shine out through the mesh.
            GameObject beamObject = new GameObject("FlashlightBeam");
            beamObject.transform.SetParent(viewCamera, false);
            beamObject.transform.localPosition = new Vector3(0.16f, -0.12f, 0.62f);
            beamObject.transform.localRotation = Quaternion.identity;

            spot = beamObject.AddComponent<Light>();
            spot.enabled = false;
            beamObject.AddComponent<UniversalAdditionalLightData>();
        }

        if (heldModel == null)
        {
            heldModel = new GameObject("HeldFlashlight");
            heldModel.transform.SetParent(viewCamera, false);
            heldModel.transform.localPosition = new Vector3(0.22f, -0.18f, 0.38f);
            heldModel.transform.localRotation = Quaternion.identity;
            heldModel.SetActive(false);

            // A cylinder's length is its Y axis. Tilting it 90 degrees on X aims that length forward.
            CreatePiece(heldModel.transform, "Body", new Vector3(0f, 0f, -0.01f), new Vector3(0.04f, 0.07f, 0.04f));
            CreatePiece(heldModel.transform, "Lens", new Vector3(0f, 0f, 0.07f), new Vector3(0.05f, 0.012f, 0.05f));
        }

        ApplyLookMaterials();
    }

    static void CreatePiece(Transform parent, string pieceName, Vector3 localPosition, Vector3 localScale)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        piece.transform.localScale = localScale;

        Collider pieceCollider = piece.GetComponent<Collider>();
        if (pieceCollider != null)
            Destroy(pieceCollider);

        piece.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void ApplyLookMaterials()
    {
        if (heldModel == null)
            return;

        Transform body = heldModel.transform.Find("Body");
        Transform lens = heldModel.transform.Find("Lens");
        Material bodyMat = BodyMaterial();
        Material lensMat = LensMaterial();
        if (body != null && bodyMat != null)
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        if (lens != null && lensMat != null)
            lens.GetComponent<Renderer>().sharedMaterial = lensMat;
    }

    Material BodyMaterial()
    {
        if (bodyMaterial != null)
            return bodyMaterial;

        if (runtimeBodyMaterial == null)
            runtimeBodyMaterial = FallbackMaterial(new Color(0.12f, 0.12f, 0.13f), 0f);
        return runtimeBodyMaterial;
    }

    Material LensMaterial()
    {
        if (lensMaterial != null)
            return lensMaterial;

        if (runtimeLensMaterial == null)
            runtimeLensMaterial = FallbackMaterial(new Color(1f, 0.75f, 0.4f), 0.6f);
        return runtimeLensMaterial;
    }

    static Material FallbackMaterial(Color color, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        if (emission > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }

        return material;
    }

    void ApplyBeamSettings()
    {
        if (spot == null)
            return;

        spot.type = LightType.Spot;
        spot.color = beamColor;
        spot.useColorTemperature = false;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle);
        spot.intensity = intensity;
        spot.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        spot.shadowStrength = 0.8f;
    }

    void RefreshVisuals()
    {
        if (heldModel != null)
            heldModel.SetActive(IsSelected());

        if (spot != null)
        {
            spot.enabled = turnedOn;
            if (spot.enabled)
                spot.intensity = intensity;
        }
    }
}
