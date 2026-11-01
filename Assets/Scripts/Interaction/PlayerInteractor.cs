using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Looks out from the camera and uses whatever Interactable is under the crosshair.
///
/// Press E, the gamepad north button, or the left mouse button. Those come from
/// the Interact action in Assets/InputSystem_Actions, plus a plain left click.
/// The Interact action is set up as a Hold in that asset. WasPressedThisFrame
/// still fires on the first frame of the press, so a tap is enough. You do not
/// have to hold the key.
///
/// Interaction only runs while the cursor is locked, so the click that captures
/// the mouse does not also use whatever is in the center of the screen.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
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
    [SerializeField] float interactDistance = 3f;

    [Tooltip("How long the message stays on screen, in seconds.")]
    [Min(0.5f)]
    [SerializeField] float messageSeconds = 2.5f;

    InputActionAsset runtimeActions;
    InputAction interactAction;
    bool inputReady;

    readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    float messageTimeRemaining;

    void Awake()
    {
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

        // Enable just this action. FirstPersonController already handles Move and Look.
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
        if (runtimeActions != null)
            Destroy(runtimeActions);
    }

    void Update()
    {
        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (crosshair != null && crosshair.activeSelf != cursorLocked)
            crosshair.SetActive(cursorLocked);

        Interactable lookedAt = cursorLocked ? FindLookTarget() : null;
        ShowPrompt(lookedAt);

        if (lookedAt != null && WasInteractPressed())
        {
            ShowMessage(lookedAt.Message);
            lookedAt.Interact();
        }

        if (messageTimeRemaining > 0f)
        {
            messageTimeRemaining -= Time.deltaTime;
            if (messageTimeRemaining <= 0f && messageLabel != null)
                messageLabel.gameObject.SetActive(false);
        }
    }

    bool WasInteractPressed()
    {
        if (inputReady && interactAction.WasPressedThisFrame())
            return true;

        // Left click is not on the Interact action (that binding is E and the
        // gamepad north button). Read the mouse directly.
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

        // The camera sits inside the player's capsule. Skip our own colliders,
        // then use the nearest thing in front. A wall in the way blocks objects behind it.
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

        bool show = target != null;
        if (promptLabel.gameObject.activeSelf != show)
            promptLabel.gameObject.SetActive(show);

        if (show)
            promptLabel.text = "E: " + target.Prompt;
    }

    void ShowMessage(string message)
    {
        if (messageLabel == null)
            return;

        messageLabel.text = message;
        messageLabel.gameObject.SetActive(true);
        messageTimeRemaining = messageSeconds;
    }
}
