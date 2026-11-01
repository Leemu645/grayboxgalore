using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Looks out from the camera and uses whatever Interactable is under the crosshair.
///
/// A tap of E (or left click, or the gamepad north button) uses a normal object.
/// An object with HoldInteractable instead fills a bar while E is held. The
/// template's Interact action is marked Hold, but WasPressedThisFrame still
/// fires on the first frame, and IsPressed stays true for the whole press, so
/// this script times the one-second pry itself.
///
/// Interaction only runs while the cursor is locked. Rays use the default
/// raycast mask, which passes through the Ignore Raycast layer, so the player
/// can use objects through the jail bars while the body still cannot walk
/// through them.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    public static PlayerInteractor Instance { get; private set; }

    [Header("References")]
    [Tooltip("Input Actions asset (Assets/InputSystem_Actions). The Player map's Interact action is read from it.")]
    [SerializeField] InputActionAsset inputActions;

    [Tooltip("Camera the ray is fired from. Leave empty to use a child camera.")]
    [SerializeField] Transform viewCamera;

    [Tooltip("Crosshair object. Hidden while the cursor is free.")]
    [SerializeField] GameObject crosshair;

    [Tooltip("Prompt under the crosshair, such as \"E: Interact\".")]
    [SerializeField] Text promptLabel;

    [Tooltip("Message shown lower on the screen after you interact.")]
    [SerializeField] Text messageLabel;

    [Header("Feel")]
    [Tooltip("How far in front of the camera an object can be and still be used, in meters.")]
    [Min(0.5f)]
    [SerializeField] float interactDistance = 3.75f;

    [Tooltip("How long a message stays on screen, in seconds. Long enough to read a full sentence.")]
    [Min(0.5f)]
    [SerializeField] float messageSeconds = 4f;

    InputActionAsset runtimeActions;
    InputAction interactAction;
    bool inputReady;

    readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    float messageTimeRemaining;

    GameObject holdBar;
    Image holdFill;
    float holdTime;
    bool holdBarReady;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("More than one PlayerInteractor is in the scene.", this);
        else
            Instance = this;

        if (viewCamera == null)
        {
            Camera child = GetComponentInChildren<Camera>();
            if (child != null)
                viewCamera = child.transform;
        }

        InputActionAsset source = inputActions != null ? inputActions : InputSystem.actions;
        if (source != null)
            runtimeActions = Instantiate(source);
    }

    void OnEnable()
    {
        inputReady = false;
        if (runtimeActions == null)
        {
            Debug.LogError(
                "PlayerInteractor on " + name + " has no Input Actions asset. Drag Assets/InputSystem_Actions into Input Actions.",
                this);
            return;
        }

        InputActionMap playerMap = runtimeActions.FindActionMap("Player", throwIfNotFound: false);
        interactAction = playerMap != null
            ? playerMap.FindAction("Interact", throwIfNotFound: false)
            : null;

        if (interactAction == null)
        {
            Debug.LogError("The Input Actions asset has no Player/Interact action.", this);
            return;
        }

        interactAction.Enable();
        inputReady = true;
    }

    void OnDisable()
    {
        if (interactAction != null)
            interactAction.Disable();
        inputReady = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (runtimeActions != null)
            Destroy(runtimeActions);
    }

    void Update()
    {
        TickMessage();

        if (PlayerControl.Suspended)
        {
            ShowPrompt(null);
            SetHoldVisible(false);
            holdTime = 0f;
            return;
        }

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (crosshair != null && crosshair.activeSelf != cursorLocked)
            crosshair.SetActive(cursorLocked);

        Interactable lookedAt = cursorLocked ? FindLookTarget() : null;
        ShowPrompt(lookedAt);

        HoldInteractable hold = lookedAt != null ? lookedAt.GetComponent<HoldInteractable>() : null;
        if (hold != null && hold.AcceptingHolds && hold.isActiveAndEnabled)
            UpdateHold(hold);
        else
        {
            holdTime = 0f;
            SetHoldVisible(false);
            if (lookedAt != null && WasInteractPressed())
                Use(lookedAt);
        }
    }

    void UpdateHold(HoldInteractable hold)
    {
        // The Interact action is also marked Hold in the input asset. Read the
        // key as well, so a one-second pry still works if that interaction
        // swallows IsPressed.
        bool holding = inputReady && interactAction.IsPressed();
        if (!holding && Keyboard.current != null && Keyboard.current.eKey.isPressed)
            holding = true;
        if (!holding && Gamepad.current != null && Gamepad.current.buttonNorth.isPressed)
            holding = true;
        if (!holding)
        {
            holdTime = 0f;
            SetHoldVisible(false);
            return;
        }

        EnsureHoldBar();
        holdTime += Time.deltaTime;
        float duration = Mathf.Max(0.2f, hold.HoldSeconds);
        SetHoldVisible(true);
        if (holdFill != null)
            holdFill.fillAmount = Mathf.Clamp01(holdTime / duration);

        if (holdTime < duration)
            return;

        holdTime = 0f;
        if (holdFill != null)
            holdFill.fillAmount = 0f;
        hold.RegisterHold();
    }

    void Use(Interactable lookedAt)
    {
        if (!string.IsNullOrEmpty(lookedAt.Message))
            ShowMessage(lookedAt.Message);
        lookedAt.Interact();
    }

    bool WasInteractPressed()
    {
        if (inputReady && interactAction.WasPressedThisFrame())
            return true;

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    Interactable FindLookTarget()
    {
        if (viewCamera == null)
            return null;

        Ray ray = new Ray(viewCamera.position, viewCamera.forward);
        int count = Physics.RaycastNonAlloc(
            ray,
            hitBuffer,
            interactDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        int closest = -1;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Transform hitTransform = hitBuffer[i].collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (hitBuffer[i].distance < closestDistance)
            {
                closestDistance = hitBuffer[i].distance;
                closest = i;
            }
        }

        if (closest < 0)
            return null;

        return hitBuffer[closest].collider.GetComponentInParent<Interactable>();
    }

    void ShowPrompt(Interactable target)
    {
        if (promptLabel == null)
            return;

        bool show = target != null && target.isActiveAndEnabled;
        if (promptLabel.gameObject.activeSelf != show)
            promptLabel.gameObject.SetActive(show);

        if (show)
        {
            HoldInteractable hold = target.GetComponent<HoldInteractable>();
            string verb = hold != null && hold.AcceptingHolds ? "Hold: " : "E: ";
            promptLabel.text = verb + target.Prompt;
        }
    }

    public void ShowMessage(string message)
    {
        if (messageLabel == null || string.IsNullOrEmpty(message))
            return;

        messageLabel.text = message;
        messageLabel.gameObject.SetActive(true);
        messageTimeRemaining = messageSeconds;
    }

    void TickMessage()
    {
        if (messageTimeRemaining <= 0f)
            return;

        messageTimeRemaining -= Time.deltaTime;
        if (messageTimeRemaining <= 0f && messageLabel != null)
            messageLabel.gameObject.SetActive(false);
    }

    void EnsureHoldBar()
    {
        if (holdBarReady || promptLabel == null || promptLabel.canvas == null)
            return;

        Sprite sprite = WhiteSprite();
        holdBar = new GameObject("HoldBar");
        holdBar.transform.SetParent(promptLabel.canvas.transform, false);
        Image background = holdBar.AddComponent<Image>();
        background.sprite = sprite;
        background.color = new Color(0f, 0f, 0f, 0.7f);
        background.raycastTarget = false;
        RectTransform barRect = background.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = new Vector2(240f, 16f);
        barRect.anchoredPosition = new Vector2(0f, -68f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(holdBar.transform, false);
        holdFill = fillObject.AddComponent<Image>();
        holdFill.sprite = sprite;
        holdFill.color = new Color(0.86f, 0.68f, 0.36f, 1f);
        holdFill.type = Image.Type.Filled;
        holdFill.fillMethod = Image.FillMethod.Horizontal;
        holdFill.fillAmount = 0f;
        holdFill.raycastTarget = false;
        RectTransform fillRect = holdFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        holdBar.SetActive(false);
        holdBarReady = true;
    }

    void SetHoldVisible(bool visible)
    {
        if (holdBar != null && holdBar.activeSelf != visible)
            holdBar.SetActive(visible);
    }

    static Sprite WhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }
}
