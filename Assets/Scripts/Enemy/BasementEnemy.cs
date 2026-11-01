using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The visitor. Waits offstage, warns with footsteps and a rattling handle,
/// walks down from the sealed door, browses, and either leaves or sprints
/// into a jumpscare. Movement is a CharacterController on waypoints: the
/// bars block it, and an open jail door is an explicit gap in the path.
/// </summary>
[DefaultExecutionOrder(20)]
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class BasementEnemy : MonoBehaviour
{
    enum Phase
    {
        Waiting,
        Warning,
        Entering,
        Searching,
        Chasing,
        Leaving,
        Scare
    }

    [Header("Timing")]
    [Tooltip("Soonest the next visit can start, in seconds after the level starts or after the previous one leaves.")]
    [SerializeField] float minArrivalSeconds = 40f;

    [Tooltip("Latest the next visit can start, in seconds.")]
    [SerializeField] float maxArrivalSeconds = 70f;

    [Tooltip("Shortest browse once the visitor is on the basement floor, in seconds.")]
    [SerializeField] float minSearchSeconds = 8f;

    [Tooltip("Longest browse, in seconds. Chasing does not use up this timer.")]
    [SerializeField] float maxSearchSeconds = 12f;

    [Tooltip("Footsteps and the rattling handle before the door opens, in seconds.")]
    [SerializeField] float warningSeconds = 2.6f;

    [Header("Debug")]
    [Tooltip("Tick during Play to start the warning now. Cleared automatically. In the editor, F9 does the same while the visitor is waiting.")]
    [SerializeField] bool summonNow;

    [Header("Route")]
    [Tooltip("From the doorway down onto the floor. Reversed on the way out.")]
    [SerializeField] Vector3[] stairDown;

    [Tooltip("Spots on the basement floor. Three are picked each visit.")]
    [SerializeField] Vector3[] browsePoints;

    [SerializeField] float barPlaneZ = 5.15f;
    [SerializeField] float walkSpeed = 1.55f;
    [SerializeField] float sprintSpeed = 9.2f;
    [SerializeField] float catchDistance = 1.35f;

    [Header("Links")]
    [SerializeField] Transform player;
    [SerializeField] JailDoor jailDoor;
    [SerializeField] SealedStairDoor stairDoor;
    [SerializeField] PlayerHiding hiding;
    [SerializeField] HookMinigame hookGame;
    [SerializeField] Material bodyMaterial;
    [SerializeField] Material eyeMaterial;

    Phase phase = Phase.Waiting;
    CharacterController body;
    Transform figure;
    Transform hips;
    Transform armL;
    Transform armR;
    Transform legL;
    Transform legR;
    Transform head;
    float verticalVelocity;
    float nextEventTime;
    float warningEnd;
    float warnStepIn;
    int rattlesLeft;
    float searchEnd = float.PositiveInfinity;
    bool searchStarted;
    bool chaseAnnounced;
    int routeIndex;
    Vector3[] route;
    Vector3[] chosenBrowse;
    int browseIndex;
    bool looking;
    float lookUntil;
    float lookBaseYaw;
    float stuckTime;
    Vector3 lastPosition;
    float stepTimer;
    float gait;
    bool sprinting;
    bool figureShown;
    bool gameOver;
    bool reloading;
    bool menuReady;
    float scareTime;
    Vector3 scareCamStart;
    Quaternion scareCamStartRot;
    GameObject gameOverRoot;
    Image fade;
    GameObject menu;
    bool ready;

    void Start()
    {
        body = GetComponent<CharacterController>();
        body.height = 2.35f;
        body.radius = 0.28f;
        body.center = new Vector3(0f, 1.15f, 0f);
        body.stepOffset = 0.4f;
        body.slopeLimit = 55f;
        body.enabled = false;

        if (player == null)
        {
            FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
                player = controller.transform;
        }

        if (hiding == null)
            hiding = FindAnyObjectByType<PlayerHiding>();
        if (jailDoor == null)
            jailDoor = FindAnyObjectByType<JailDoor>();
        if (stairDoor == null)
            stairDoor = FindAnyObjectByType<SealedStairDoor>();
        if (hookGame == null)
            hookGame = FindAnyObjectByType<HookMinigame>();

        EnsureFigure();
        SetFigureVisible(false);
        if (stairDoor != null)
            stairDoor.LetThrough(body);

        if (stairDown != null && stairDown.Length > 0)
            transform.position = stairDown[0];

        nextEventTime = Time.time + RandomRange(minArrivalSeconds, maxArrivalSeconds);
        phase = Phase.Waiting;
        ready = true;
    }

    void Update()
    {
        if (!ready || gameOver)
            return;

        if (summonNow)
        {
            summonNow = false;
            SummonNow();
        }

#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            SummonNow();
#endif

        switch (phase)
        {
            case Phase.Waiting:
                if (Time.time >= nextEventTime)
                    BeginWarning();
                break;
            case Phase.Warning:
                TickWarning();
                break;
            case Phase.Entering:
                TickRoute(walkSpeed);
                if (WantCatch())
                    BeginChase();
                else if (RouteDone())
                    BeginSearch();
                break;
            case Phase.Searching:
                TickSearch();
                break;
            case Phase.Chasing:
                TickChase();
                break;
            case Phase.Leaving:
                TickLeave();
                break;
        }

        Animate();
        Footfalls();
    }

    void LateUpdate()
    {
        if (!gameOver)
            return;

        scareTime += Time.deltaTime;
        Camera cam = Camera.main;
        if (cam != null && head != null)
        {
            float snap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scareTime / 0.22f));
            Vector3 eyes = head.position;
            Vector3 faceCam = eyes + transform.forward * 0.5f;
            float shake = 0.045f * Mathf.Clamp01(1.1f - scareTime);
            faceCam += new Vector3(
                Mathf.Sin(scareTime * 47f) * shake,
                Mathf.Sin(scareTime * 63f) * shake * 0.6f,
                0f);
            cam.transform.position = Vector3.Lerp(scareCamStart, faceCam, snap);
            Quaternion look = Quaternion.LookRotation(eyes - cam.transform.position, Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(scareCamStartRot, look, snap);
        }

        if (fade != null)
        {
            float fadeIn = Mathf.Clamp01((scareTime - 0.72f) / 0.38f);
            Color color = fade.color;
            color.a = fadeIn;
            fade.color = color;
        }

        if (!menuReady && scareTime >= 1.05f)
            ShowMenu();

        if (!menuReady)
            return;

        bool retry = Keyboard.current != null &&
            (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame);
        if (!retry && Gamepad.current != null)
            retry = Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame;
        if (retry)
        {
            Retry();
            return;
        }
    }

    /// <summary>
    /// Starts the warning immediately. Does nothing if a visit is already underway.
    /// </summary>
    public void SummonNow()
    {
        if (!ready || gameOver || phase != Phase.Waiting)
            return;

        BeginWarning();
    }

    void BeginWarning()
    {
        phase = Phase.Warning;
        warningEnd = Time.time + Mathf.Max(0.4f, warningSeconds);
        warnStepIn = 0.05f;
        rattlesLeft = 2;
        sprinting = false;
    }

    void TickWarning()
    {
        Vector3 at = stairDoor != null ? stairDoor.transform.position : transform.position;
        warnStepIn -= Time.deltaTime;
        if (warnStepIn <= 0f)
        {
            warnStepIn = 0.52f;
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayFootstep(at, false, null, 0.58f, 30f);
        }

        float elapsed = warningSeconds - (warningEnd - Time.time);
        if (rattlesLeft == 2 && elapsed >= 0.35f)
        {
            rattlesLeft = 1;
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayLockRattle(at);
        }
        else if (rattlesLeft == 1 && elapsed >= 1.35f)
        {
            rattlesLeft = 0;
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayLockRattle(at);
        }

        if (Time.time < warningEnd)
            return;

        BeginEntry();
    }

    void BeginEntry()
    {
        phase = Phase.Entering;
        sprinting = false;
        searchStarted = false;
        searchEnd = float.PositiveInfinity;
        chaseAnnounced = false;
        if (stairDown == null || stairDown.Length == 0)
        {
            BeginSearch();
            return;
        }

        transform.position = stairDown[0] + Vector3.up * 0.05f;
        if (body != null)
            body.enabled = true;
        SetFigureVisible(true);
        route = stairDown;
        routeIndex = 1;
        if (stairDoor != null)
            stairDoor.OpenForVisitor();
        stuckTime = 0f;
        lastPosition = transform.position;
    }

    void BeginSearch()
    {
        phase = Phase.Searching;
        sprinting = false;
        looking = false;
        browseIndex = 0;
        if (!searchStarted)
        {
            searchStarted = true;
            searchEnd = Time.time + RandomRange(minSearchSeconds, maxSearchSeconds);
            chosenBrowse = PickBrowse();
        }

        stuckTime = 0f;
    }

    void TickSearch()
    {
        if (WantCatch())
        {
            BeginChase();
            return;
        }

        if (Time.time >= searchEnd)
        {
            BeginLeave();
            return;
        }

        if (chosenBrowse == null || chosenBrowse.Length == 0)
        {
            StayGrounded();
            return;
        }

        if (looking)
        {
            if (Time.time >= lookUntil)
            {
                looking = false;
                browseIndex = (browseIndex + 1) % chosenBrowse.Length;
            }
            else
            {
                float yaw = lookBaseYaw + Mathf.Sin(Time.time * 0.85f) * 50f;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                StayGrounded();
            }

            return;
        }

        if (MoveToward(chosenBrowse[browseIndex], walkSpeed))
        {
            looking = true;
            lookUntil = Time.time + 1.45f;
            lookBaseYaw = transform.eulerAngles.y;
        }
    }

    void BeginChase()
    {
        phase = Phase.Chasing;
        sprinting = true;
        looking = false;
        if (!chaseAnnounced)
        {
            chaseAnnounced = true;
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayStinger(0);
        }
    }

    void TickChase()
    {
        if (!WantCatch())
        {
            chaseAnnounced = false;
            sprinting = false;
            if (!searchStarted || Time.time < searchEnd)
                BeginSearch();
            else
                BeginLeave();
            return;
        }

        Vector3 dest = player.position;
        if (SeparatedByBars(transform.position, player.position))
        {
            if (transform.position.z < barPlaneZ - 0.35f)
                dest = new Vector3(0f, player.position.y, barPlaneZ - 0.85f);
            else if (transform.position.z < barPlaneZ + 0.55f)
                dest = new Vector3(0f, player.position.y, barPlaneZ + 0.8f);
        }

        MoveToward(dest, sprintSpeed);
        float dx = player.position.x - transform.position.x;
        float dz = player.position.z - transform.position.z;
        if (dx * dx + dz * dz <= catchDistance * catchDistance)
            BeginScare();
    }

    void BeginLeave()
    {
        phase = Phase.Leaving;
        sprinting = false;
        looking = false;
        BuildLeaveRoute();
        routeIndex = 0;
        stuckTime = 0f;
        lastPosition = transform.position;
    }

    void TickLeave()
    {
        if (WantCatch())
        {
            BeginChase();
            return;
        }

        if (route == null || route.Length == 0 || RouteDone())
        {
            Depart();
            return;
        }

        TickRoute(walkSpeed);
        if (RouteDone())
            Depart();
    }

    void Depart()
    {
        phase = Phase.Waiting;
        sprinting = false;
        SetFigureVisible(false);
        if (body != null)
            body.enabled = false;
        if (stairDoor != null)
            stairDoor.CloseAfterVisitor();
        nextEventTime = Time.time + RandomRange(minArrivalSeconds, maxArrivalSeconds);
    }

    void BeginScare()
    {
        if (gameOver)
            return;

        gameOver = true;
        phase = Phase.Scare;
        sprinting = false;
        scareTime = 0f;
        menuReady = false;
        if (hookGame != null)
            hookGame.CancelWithoutResume();
        if (hiding != null)
            hiding.AbortForGameOver();

        if (player != null)
        {
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerControl.Suspend(false);

        Camera cam = Camera.main;
        if (cam != null)
        {
            scareCamStart = cam.transform.position;
            scareCamStartRot = cam.transform.rotation;
        }

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayScream();

        BuildGameOver();
    }

    bool WantCatch()
    {
        if (player == null)
            return false;
        if (hiding != null && hiding.IsSafe())
            return false;
        return CanReach(player.position);
    }

    bool CanReach(Vector3 target)
    {
        if (!SeparatedByBars(transform.position, target))
            return true;
        return jailDoor != null && jailDoor.IsOpen;
    }

    bool SeparatedByBars(Vector3 a, Vector3 b)
    {
        return (a.z - barPlaneZ) * (b.z - barPlaneZ) < 0f;
    }

    void TickRoute(float speed)
    {
        if (route == null || routeIndex >= route.Length)
            return;

        if (MoveToward(route[routeIndex], speed))
            routeIndex++;
    }

    bool RouteDone()
    {
        return route == null || routeIndex >= route.Length;
    }

    bool MoveToward(Vector3 worldTarget, float speed)
    {
        if (body == null || !body.enabled)
            return true;

        // Stay on the flight while still on it, so a chase does not walk off the side.
        if (transform.position.z < -2.35f)
            worldTarget.x = 0f;

        Vector3 flat = worldTarget - transform.position;
        flat.y = 0f;
        float distance = flat.magnitude;
        Vector3 horizontal = distance > 0.08f ? flat / distance * speed : Vector3.zero;

        if (body.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += -20f * Time.deltaTime;

        Vector3 velocity = horizontal;
        velocity.y = verticalVelocity;
        body.Move(velocity * Time.deltaTime);

        if (horizontal.sqrMagnitude > 0.04f)
        {
            Quaternion face = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, 10f * Time.deltaTime);
        }

        float moved = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(lastPosition.x, 0f, lastPosition.z));
        if (distance > 0.4f && moved < 0.015f)
            stuckTime += Time.deltaTime;
        else
            stuckTime = 0f;
        lastPosition = transform.position;

        if (stuckTime > 0.7f)
        {
            Vector3 shove = transform.position.z < -2.2f
                ? new Vector3(0f, 0f, 0.45f)
                : Vector3.Cross(Vector3.up, flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward) * 0.45f;
            body.Move(shove);
            stuckTime = 0.2f;
        }

        sprinting = speed > walkSpeed + 0.5f;
        return distance <= 0.4f;
    }

    void StayGrounded()
    {
        if (body == null || !body.enabled)
            return;

        if (body.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += -20f * Time.deltaTime;
        body.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
    }

    void Footfalls()
    {
        if (!figureShown || body == null || !body.enabled)
            return;

        Vector3 flat = new Vector3(body.velocity.x, 0f, body.velocity.z);
        if (flat.sqrMagnitude < 0.15f)
        {
            stepTimer = 0.05f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f)
            return;

        stepTimer = sprinting ? 0.28f : 0.64f;
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayFootstep(transform.position, sprinting, null, sprinting ? 0.9f : 0.66f, 22f);
    }

    void Animate()
    {
        if (hips == null)
            return;

        float moving = figureShown && body != null && body.enabled
            ? Mathf.Clamp01(new Vector3(body.velocity.x, 0f, body.velocity.z).magnitude / 0.45f)
            : 0f;
        gait += Time.deltaTime * (sprinting ? 8.2f : 3.2f);
        float swing = Mathf.Sin(gait) * 28f * moving;
        float bob = Mathf.Sin(gait * 2f) * (sprinting ? 0.055f : 0.03f) * moving;
        float sway = Mathf.Sin(gait) * 3.5f * moving;
        hips.localPosition = new Vector3(0f, bob, 0f);
        hips.localRotation = Quaternion.Euler(6f, 0f, sway);
        if (legL != null)
            legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (legR != null)
            legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (armL != null)
            armL.localRotation = Quaternion.Euler(-swing * 0.85f, 0f, 6f);
        if (armR != null)
            armR.localRotation = Quaternion.Euler(swing * 0.85f, 0f, -6f);
    }

    void BuildLeaveRoute()
    {
        int stairCount = stairDown != null ? stairDown.Length : 0;
        int extra = transform.position.z > barPlaneZ - 0.25f ? 2 : 0;
        int start = 0;
        if (stairCount > 0)
        {
            start = stairCount - 1;
            while (start > 0 && stairDown[start].z > transform.position.z)
                start--;
        }

        int stairSteps = stairCount == 0 ? 0 : start + 1;
        route = new Vector3[extra + stairSteps];
        int n = 0;
        if (extra > 0)
        {
            route[n++] = new Vector3(0f, 0f, barPlaneZ + 0.75f);
            route[n++] = new Vector3(0f, 0f, barPlaneZ - 0.9f);
        }

        for (int i = start; i >= 0 && n < route.Length; i--)
            route[n++] = stairDown[i];
        routeIndex = 0;
    }

    Vector3[] PickBrowse()
    {
        if (browsePoints == null || browsePoints.Length == 0)
            return new Vector3[] { new Vector3(0f, 0f, -1f) };

        int count = Mathf.Min(3, browsePoints.Length);
        Vector3[] picked = new Vector3[count];
        bool[] used = new bool[browsePoints.Length];
        for (int i = 0; i < count; i++)
        {
            int choice = Random.Range(0, browsePoints.Length);
            for (int attempt = 0; attempt < browsePoints.Length; attempt++)
            {
                int index = (choice + attempt) % browsePoints.Length;
                if (used[index])
                    continue;
                used[index] = true;
                picked[i] = browsePoints[index];
                break;
            }
        }

        return picked;
    }

    void EnsureFigure()
    {
        if (figure != null)
            return;

        GameObject root = new GameObject("Figure");
        root.transform.SetParent(transform, false);
        figure = root.transform;
        hips = root.transform;

        Material skin = BodyMaterial();
        Material eyes = EyeMaterial();
        Part(hips, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.55f, 0.02f), new Vector3(0.32f, 0.72f, 0.16f), skin);
        Part(hips, "Chest", PrimitiveType.Cube, new Vector3(0f, 1.95f, 0.03f), new Vector3(0.38f, 0.28f, 0.18f), skin);
        GameObject headObject = Part(hips, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.28f, 0.04f), new Vector3(0.2f, 0.26f, 0.2f), skin);
        head = headObject.transform;
        Part(head, "EyeL", PrimitiveType.Sphere, new Vector3(-0.045f, 0.03f, 0.08f), Vector3.one * 0.045f, eyes);
        Part(head, "EyeR", PrimitiveType.Sphere, new Vector3(0.045f, 0.03f, 0.08f), Vector3.one * 0.045f, eyes);

        armL = Limb(hips, "ArmL", new Vector3(-0.24f, 1.85f, 0f), 0.62f, skin);
        armR = Limb(hips, "ArmR", new Vector3(0.24f, 1.85f, 0f), 0.62f, skin);
        legL = Limb(hips, "LegL", new Vector3(-0.1f, 1.05f, 0f), 0.55f, skin);
        legR = Limb(hips, "LegR", new Vector3(0.1f, 1.05f, 0f), 0.55f, skin);
    }

    Transform Limb(Transform parent, string limbName, Vector3 shoulder, float length, Material material)
    {
        GameObject pivot = new GameObject(limbName);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = shoulder;
        Part(pivot.transform, "Bone", PrimitiveType.Cube, new Vector3(0f, -length * 0.5f, 0f), new Vector3(0.06f, length, 0.06f), material);
        return pivot.transform;
    }

    GameObject Part(Transform parent, string partName, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            DestroyImmediate(col);
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = partName.StartsWith("Eye")
            ? UnityEngine.Rendering.ShadowCastingMode.Off
            : UnityEngine.Rendering.ShadowCastingMode.On;
        if (material != null)
            renderer.sharedMaterial = material;
        return go;
    }

    Material BodyMaterial()
    {
        if (bodyMaterial != null)
            return bodyMaterial;
        return RuntimeLit(new Color(0.02f, 0.018f, 0.022f), 0f);
    }

    Material EyeMaterial()
    {
        if (eyeMaterial != null)
            return eyeMaterial;
        return RuntimeLit(new Color(0.7f, 0.12f, 0.08f), 1.6f);
    }

    static Material RuntimeLit(Color color, float emission)
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

    void SetFigureVisible(bool visible)
    {
        figureShown = visible;
        if (figure == null)
            return;
        Renderer[] renderers = figure.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    void BuildGameOver()
    {
        gameOverRoot = new GameObject("GameOver");
        Canvas canvas = gameOverRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = gameOverRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (EventSystem.current == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.transform.SetParent(gameOverRoot.transform, false);
            events.AddComponent<EventSystem>();
            InputSystemUIInputModule module = events.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        Sprite white = WhiteSprite();
        GameObject fadeObject = new GameObject("Fade");
        fadeObject.transform.SetParent(gameOverRoot.transform, false);
        fade = fadeObject.AddComponent<Image>();
        fade.sprite = white;
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
        RectTransform fadeRect = fade.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        menu = new GameObject("Menu");
        menu.transform.SetParent(gameOverRoot.transform, false);
        RectTransform menuRect = menu.AddComponent<RectTransform>();
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;

        Text title = Label(menu.transform, "Title", 72, new Vector2(0.5f, 0.62f), new Vector2(900f, 100f));
        title.text = "GAME OVER";
        Text hint = Label(menu.transform, "Hint", 22, new Vector2(0.5f, 0.28f), new Vector2(700f, 40f));
        hint.text = "R or E";
        hint.color = new Color(0.8f, 0.78f, 0.72f, 0.8f);

        GameObject buttonObject = new GameObject("TryAgain");
        buttonObject.transform.SetParent(menu.transform, false);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = white;
        buttonImage.color = new Color(0.18f, 0.16f, 0.14f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(Retry);
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.42f);
        buttonRect.sizeDelta = new Vector2(280f, 64f);
        Text buttonLabel = Label(buttonObject.transform, "Label", 28, new Vector2(0.5f, 0.5f), new Vector2(260f, 50f));
        buttonLabel.text = "Try Again";
        buttonLabel.rectTransform.anchorMin = Vector2.zero;
        buttonLabel.rectTransform.anchorMax = Vector2.one;
        buttonLabel.rectTransform.offsetMin = Vector2.zero;
        buttonLabel.rectTransform.offsetMax = Vector2.zero;
        buttonLabel.rectTransform.anchoredPosition = Vector2.zero;

        menu.SetActive(false);
    }

    void ShowMenu()
    {
        menuReady = true;
        if (menu != null)
            menu.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Retry()
    {
        if (reloading)
            return;

        reloading = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        PlayerControl.ResetForSceneLoad();
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

    static Text Label(Transform parent, string name, int size, Vector2 anchor, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.93f, 0.9f, 0.84f, 1f);
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = sizeDelta;
        return text;
    }

    static Sprite WhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    static float RandomRange(float a, float b)
    {
        float min = Mathf.Min(a, b);
        float max = Mathf.Max(a, b);
        if (max - min < 0.01f)
            return min;
        return Random.Range(min, max);
    }
}
