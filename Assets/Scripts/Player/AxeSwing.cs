using UnityEngine;

/// <summary>
/// The axe in the player's hand. It is visible only while Axe is the selected
/// hotbar item. Left click swings it. At the middle of the swing a short
/// raycast hits anything with Damageable. Other surfaces get a thunk and a nod.
/// </summary>
public class AxeSwing : MonoBehaviour
{
    public static AxeSwing Instance { get; private set; }

    [SerializeField] Material handleMaterial;
    [SerializeField] Material headMaterial;

    [Tooltip("Seconds before another swing can start.")]
    [Min(0.2f)]
    [SerializeField] float cooldown = 0.55f;

    [Tooltip("How far the swing reaches, in meters.")]
    [Min(0.5f)]
    [SerializeField] float reach = 1.8f;

    Transform viewCamera;
    Transform held;
    float nextSwingTime;
    float swingTime;
    bool swinging;
    bool didHit;
    Material runtimeHandle;
    Material runtimeHead;

    const float SwingDuration = 0.32f;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("More than one AxeSwing is in the scene.", this);
        else
            Instance = this;

        Camera child = GetComponentInChildren<Camera>();
        if (child != null)
            viewCamera = child.transform;

        BuildHeldModel();
        RefreshHeld();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (PlayerControl.Suspended || HidingBlocks())
        {
            RefreshHeld();
            return;
        }

        if (!swinging)
        {
            RefreshHeld();
            return;
        }

        swingTime += Time.deltaTime;
        float u = Mathf.Clamp01(swingTime / SwingDuration);
        // Wind up, then chop down through the crosshair, then recover.
        float angle;
        if (u < 0.35f)
            angle = Mathf.Lerp(25f, -55f, u / 0.35f);
        else if (u < 0.6f)
            angle = Mathf.Lerp(-55f, 50f, (u - 0.35f) / 0.25f);
        else
            angle = Mathf.Lerp(50f, 25f, (u - 0.6f) / 0.4f);

        if (held != null)
            held.localRotation = Quaternion.Euler(angle, -8f, 6f);

        if (!didHit && u >= 0.48f)
        {
            didHit = true;
            Strike();
        }

        if (u >= 1f)
        {
            swinging = false;
            didHit = false;
            if (held != null)
                held.localRotation = Quaternion.Euler(25f, -8f, 6f);
        }
    }

    public bool IsSelected()
    {
        return Inventory.Instance != null && Inventory.Instance.Has(ItemId.Axe)
            && Inventory.Instance.Selected == ItemId.Axe;
    }

    /// <summary>
    /// Returns true when the axe is selected, so a left click should not also
    /// count as a normal interaction. A swing on cooldown still consumes the click.
    /// </summary>
    public bool TryConsumeAttack()
    {
        if (!IsSelected() || PlayerControl.Suspended || HidingBlocks())
            return false;
        if (Cursor.lockState != CursorLockMode.Locked)
            return false;

        if (Time.time < nextSwingTime || swinging)
            return true;

        nextSwingTime = Time.time + cooldown;
        swinging = true;
        swingTime = 0f;
        didHit = false;
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayAxeWhoosh();
        return true;
    }

    void Strike()
    {
        if (viewCamera == null)
            return;

        Ray ray = new Ray(viewCamera.position, viewCamera.forward);
        RaycastHit[] hits = Physics.SphereCastAll(ray, 0.22f, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            Damageable damageable = hit.collider.GetComponentInParent<Damageable>();
            if (damageable != null && !damageable.IsBroken)
            {
                damageable.Hit();
                if (GameAudio.Instance != null)
                    GameAudio.Instance.PlayAxeHit(hit.point, true);
                Kick(-3.5f);
                return;
            }

            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayAxeHit(hit.point, IsWood(hit.collider));
            Kick(-2f);
            if (PlayerInteractor.Instance != null)
                PlayerInteractor.Instance.ShowMessage("The axe thunks against it.");
            return;
        }
    }

    static bool IsWood(Collider collider)
    {
        if (collider == null)
            return false;

        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer == null)
            renderer = collider.GetComponentInParent<Renderer>();
        Material material = renderer != null ? renderer.sharedMaterial : null;
        return material != null && material.name.IndexOf("Wood", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HidingBlocks()
    {
        return PlayerHiding.Instance != null && PlayerHiding.Instance.BlocksWorldUse;
    }

    void Kick(float pitch)
    {
        FirstPersonController body = GetComponent<FirstPersonController>();
        if (body != null)
            body.AddLookKick(pitch);
    }

    void RefreshHeld()
    {
        if (held != null)
            held.gameObject.SetActive(IsSelected() && !PlayerControl.Suspended && !HidingBlocks());
    }

    void BuildHeldModel()
    {
        if (viewCamera == null)
            return;

        GameObject root = new GameObject("HeldAxe");
        root.transform.SetParent(viewCamera, false);
        root.transform.localPosition = new Vector3(0.28f, -0.24f, 0.48f);
        root.transform.localRotation = Quaternion.Euler(25f, -8f, 6f);
        held = root.transform;

        // Blade sits on local +Z, away from the camera, so the edge leads the chop.
        CreatePiece(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(0.035f, 0.16f, 0.035f), Quaternion.Euler(8f, 0f, 0f), false);
        CreatePiece(root.transform, "Head", PrimitiveType.Cube, new Vector3(0f, 0.14f, 0.02f), new Vector3(0.05f, 0.07f, 0.16f), Quaternion.identity, true);
        CreatePiece(root.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.14f, 0.11f), new Vector3(0.02f, 0.11f, 0.06f), Quaternion.identity, true);
        root.SetActive(false);
    }

    void CreatePiece(Transform parent, string pieceName, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, bool metal)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = pieceName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = scale;

        Collider pieceCollider = piece.GetComponent<Collider>();
        if (pieceCollider != null)
            Destroy(pieceCollider);

        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material material = metal ? HeadMaterial() : HandleMaterial();
        if (material != null)
            renderer.sharedMaterial = material;
    }

    Material HandleMaterial()
    {
        if (handleMaterial != null)
            return handleMaterial;
        if (runtimeHandle == null)
            runtimeHandle = Fallback(new Color(0.32f, 0.2f, 0.1f));
        return runtimeHandle;
    }

    Material HeadMaterial()
    {
        if (headMaterial != null)
            return headMaterial;
        if (runtimeHead == null)
            runtimeHead = Fallback(new Color(0.18f, 0.18f, 0.2f));
        return runtimeHead;
    }

    static Material Fallback(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        return material;
    }
}
