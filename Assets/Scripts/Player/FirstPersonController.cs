using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person movement for a grey-box level.
///
/// Put this on the player object, together with a CharacterController.
/// The camera should be a child of the player, around eye height. Horizontal
/// look turns the whole body, so walking always follows the direction you
/// face. Vertical look only tilts the camera, and it is clamped so you cannot
/// flip upside down.
///
/// This uses a CharacterController, a kinematic collider: we move it ourselves
/// and Unity stops it at walls. Gravity and jumping are applied here, in this
/// script, each frame.
///
/// Input comes from the project's Input System asset (Assets/InputSystem_Actions).
/// That asset already maps WASD, the mouse, and a gamepad. The old Input Manager
/// is turned off for this project, so Input.GetAxis would throw.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class FirstPersonController : MonoBehaviour
{
    const float JumpBufferSeconds = 0.12f;

    [Header("References")]
    [Tooltip("The Input Actions asset shipped with this project: Assets/InputSystem_Actions. The Player map's Move, Look, Jump, and Sprint actions are read from it.")]
    [SerializeField] InputActionAsset inputActions;

    [Tooltip("Camera to tilt up and down. Leave empty to use a Camera on a child of this object.")]
    [SerializeField] Transform viewCamera;

    [Header("Movement")]
    [Tooltip("Walking speed, in meters per second.")]
    [Min(0f)]
    [SerializeField] float moveSpeed = 5f;

    [Tooltip("Speed while Sprint is held (Left Shift, or clicking the left stick on a gamepad), in meters per second.")]
    [Min(0f)]
    [SerializeField] float sprintSpeed = 8f;

    [Tooltip("How high a jump reaches, in meters.")]
    [Min(0f)]
    [SerializeField] float jumpHeight = 1.2f;

    [Tooltip("Downward pull, in meters per second squared. 9.81 matches Earth. 20 feels snappier, which is nicer for a game.")]
    [Min(0f)]
    [SerializeField] float gravity = 20f;

    [Header("Look")]
    [Tooltip("How many degrees the view turns for each pixel the mouse moves. Raise this if looking around feels slow.")]
    [Min(0f)]
    [SerializeField] float mouseSensitivity = 0.1f;

    [Tooltip("How fast a gamepad stick (or joystick) turns the view, in degrees per second at full tilt. The mouse does not use this.")]
    [Min(0f)]
    [SerializeField] float stickLookSpeed = 200f;

    [Tooltip("Lowest the camera can look, in degrees. Negative looks down.")]
    [SerializeField] float minPitch = -85f;

    [Tooltip("Highest the camera can look, in degrees. Positive looks up.")]
    [SerializeField] float maxPitch = 85f;

    [Header("Cursor")]
    [Tooltip("Lock and hide the cursor when play starts. Press Escape (or the gamepad Start button) to release it, then click in the Game view to lock it again.")]
    [SerializeField] bool lockCursorOnPlay = true;

    CharacterController controller;
    InputActionAsset runtimeActions;
    InputActionMap playerMap;
    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;
    InputAction sprintAction;

    float verticalVelocity;
    float yaw;
    float pitch;
    float jumpBuffer;
    bool inputReady;
    bool cursorLocked;
    bool suppressPointerLook;

    /// <summary>
    /// Runs in the editor when the component is first added. Lines the capsule
    /// up so its bottom sits on this object's pivot. Place the player on the
    /// floor and the feet will rest on it.
    /// </summary>
    void Reset()
    {
        CharacterController body = GetComponent<CharacterController>();
        if (body == null)
            return;

        body.height = 2f;
        body.radius = 0.5f;
        body.center = new Vector3(0f, 1f, 0f);
        body.stepOffset = 0.3f;
        body.slopeLimit = 45f;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (viewCamera == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
                viewCamera = childCamera.transform;
        }

        if (viewCamera == null)
        {
            Debug.LogError(
                "FirstPersonController on " + name + " has no camera. Add a Camera as a child and assign it to View Camera.",
                this);
        }

        // Copy the asset so enabling actions does not change the project file,
        // and so two players would not share one action state.
        InputActionAsset source = inputActions != null ? inputActions : InputSystem.actions;
        if (source != null)
            runtimeActions = Instantiate(source);

        yaw = transform.eulerAngles.y;
        if (viewCamera != null)
        {
            pitch = viewCamera.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;
        }
    }

    void OnEnable()
    {
        EnableInput();
        if (lockCursorOnPlay)
            SetCursorLocked(true);
    }

    void OnDisable()
    {
        if (playerMap != null)
            playerMap.Disable();

        inputReady = false;

        if (cursorLocked)
            SetCursorLocked(false);
    }

    void OnDestroy()
    {
        if (runtimeActions != null)
            Destroy(runtimeActions);
    }

    void Update()
    {
        // CharacterController is meant to be moved from Update, not FixedUpdate.
        UpdateCursor();
        Look();
        Move();
    }

    void EnableInput()
    {
        inputReady = false;
        playerMap = null;

        if (runtimeActions == null)
        {
            Debug.LogError(
                "FirstPersonController on " + name + " has no Input Actions asset. Drag Assets/InputSystem_Actions into the Input Actions field.",
                this);
            return;
        }

        playerMap = runtimeActions.FindActionMap("Player", throwIfNotFound: false);
        if (playerMap == null)
        {
            Debug.LogError("The Input Actions asset has no 'Player' action map.", this);
            return;
        }

        moveAction = playerMap.FindAction("Move", throwIfNotFound: false);
        lookAction = playerMap.FindAction("Look", throwIfNotFound: false);
        jumpAction = playerMap.FindAction("Jump", throwIfNotFound: false);
        sprintAction = playerMap.FindAction("Sprint", throwIfNotFound: false);

        if (moveAction == null || lookAction == null || jumpAction == null || sprintAction == null)
        {
            Debug.LogError("The Player action map is missing Move, Look, Jump, or Sprint.", this);
            return;
        }

        playerMap.Enable();
        inputReady = true;
    }

    void UpdateCursor()
    {
        bool releaseCursor = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (!releaseCursor && Gamepad.current != null)
            releaseCursor = Gamepad.current.startButton.wasPressedThisFrame;

        if (cursorLocked && releaseCursor)
        {
            SetCursorLocked(false);
            return;
        }

        // Click the Game view (left mouse button) to capture the cursor again.
        if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);
    }

    void SetCursorLocked(bool locked)
    {
        // The frame the cursor locks, the mouse often reports one huge delta.
        // Drop that sample so the view does not spin as play starts.
        if (locked && !cursorLocked)
            suppressPointerLook = true;

        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Look()
    {
        if (viewCamera == null)
        {
            suppressPointerLook = false;
            return;
        }

        if (inputReady)
        {
            Vector2 look = lookAction.ReadValue<Vector2>();

            // The mouse binding reports a per-frame pixel delta. A stick reports
            // a deflection from -1 to 1 that stays put while you hold it, so a
            // stick has to be scaled by time or the turn rate depends on frame rate.
            bool fromPointer = lookAction.activeControl != null && lookAction.activeControl.device is Pointer;

            if (fromPointer && cursorLocked && !suppressPointerLook)
            {
                yaw += look.x * mouseSensitivity;
                pitch -= look.y * mouseSensitivity;
            }
            else if (!fromPointer)
            {
                yaw += look.x * stickLookSpeed * Time.deltaTime;
                pitch -= look.y * stickLookSpeed * Time.deltaTime;
            }

            float low = Mathf.Min(minPitch, maxPitch);
            float high = Mathf.Max(minPitch, maxPitch);
            pitch = Mathf.Clamp(pitch, low, high);
        }

        suppressPointerLook = false;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        viewCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        // isGrounded is only true after a Move that touched the ground, so we
        // still call Move when the player is standing still.
        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        float gravityAccel = -Mathf.Abs(gravity);

        if (inputReady && jumpAction.WasPressedThisFrame())
            jumpBuffer = JumpBufferSeconds;

        // A press a fraction of a second before landing still counts. Without
        // this, jump often does nothing when you press it as you touch down.
        if (grounded && jumpBuffer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravityAccel);
            jumpBuffer = 0f;
        }
        else if (jumpBuffer > 0f)
        {
            jumpBuffer -= Time.deltaTime;
        }

        verticalVelocity += gravityAccel * Time.deltaTime;

        Vector3 wish = Vector3.zero;
        if (inputReady)
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            // Body yaw only, so looking up does not make WASD carry you into the air.
            wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 1f)
                wish.Normalize();
        }

        bool sprinting = inputReady && sprintAction.IsPressed();
        float speed = sprinting ? sprintSpeed : moveSpeed;

        Vector3 velocity = wish * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
