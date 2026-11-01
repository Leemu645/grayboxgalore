using UnityEngine;

/// <summary>
/// Slides the barred door aside. While it is shut, only the Ignore Raycast
/// blocker is solid, so the player can still use objects through the bars.
/// A small handle collider turns on once the door is open, off to the side of
/// the gap, and pressing E on it closes the door. The lock comes back only
/// after the door has finished closing.
/// </summary>
public class JailDoor : MonoBehaviour
{
    [SerializeField] float slideDistance = 1.55f;
    [SerializeField] float slideSpeed = 1.6f;
    [SerializeField] JailLock lockPiece;
    [SerializeField] Collider handleCollider;

    Vector3 closedPosition;
    Vector3 openPosition;
    bool moving;
    bool wantsOpen;

    public bool IsOpen { get; private set; }

    public bool IsMoving => moving;

    void Awake()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + new Vector3(slideDistance, 0f, 0f);
        if (handleCollider != null)
            handleCollider.enabled = false;
    }

    void Update()
    {
        if (!moving)
            return;

        Vector3 goal = wantsOpen ? openPosition : closedPosition;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, goal, slideSpeed * Time.deltaTime);
        if (transform.localPosition != goal)
            return;

        moving = false;
        IsOpen = wantsOpen;
        if (IsOpen)
            return;

        if (handleCollider != null)
            handleCollider.enabled = false;
        if (lockPiece != null)
            lockPiece.ShowAgain();
    }

    public void Open()
    {
        wantsOpen = true;
        IsOpen = true;
        moving = true;
        if (handleCollider != null)
            handleCollider.enabled = true;
    }

    public void RequestClose()
    {
        if (PlayerInteractor.Instance == null)
            return;

        if (!IsOpen || moving)
        {
            if (!IsOpen)
                PlayerInteractor.Instance.ShowMessage("The bars are shut.");
            return;
        }

        wantsOpen = false;
        moving = true;
        PlayerInteractor.Instance.ShowMessage("I pull the bars shut.");
    }
}
