using UnityEngine;

/// <summary>
/// Closet doors stay shut until the closet key is selected. Opening them
/// swings the hinges and drops the blocker so the axe inside can be taken.
/// </summary>
public class ClosetLock : MonoBehaviour
{
    [SerializeField] Transform hingeLeft;
    [SerializeField] Transform hingeRight;
    [SerializeField] Collider closedBlocker;
    [SerializeField] float openAngle = 110f;
    [SerializeField] float swingSpeed = 140f;

    bool open;
    float angle;

    public bool IsOpen => open;

    void Update()
    {
        if (!open)
            return;

        angle = Mathf.MoveTowards(angle, openAngle, swingSpeed * Time.deltaTime);
        if (hingeLeft != null)
            hingeLeft.localRotation = Quaternion.Euler(0f, -angle, 0f);
        if (hingeRight != null)
            hingeRight.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    public void TryOpen()
    {
        if (open || PlayerInteractor.Instance == null)
            return;

        if (Inventory.Instance == null || Inventory.Instance.Selected != ItemId.ClosetKey)
        {
            PlayerInteractor.Instance.ShowMessage("It's locked.");
            return;
        }

        open = true;
        if (closedBlocker != null)
            closedBlocker.enabled = false;

        PlayerInteractor.Instance.ShowMessage("The closet opens.");
    }
}
