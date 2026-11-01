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

    bool unlocked;
    float angle;
    float targetAngle;

    public bool IsOpen => unlocked;

    public float OpenAngle => openAngle;

    void Update()
    {
        if (!unlocked)
            return;

        angle = Mathf.MoveTowards(angle, targetAngle, swingSpeed * Time.deltaTime);
        if (hingeLeft != null)
            hingeLeft.localRotation = Quaternion.Euler(0f, -angle, 0f);
        if (hingeRight != null)
            hingeRight.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    /// <summary>
    /// How far the doors sit open. Hiding pulls them almost shut, leaving a crack.
    /// </summary>
    public void SetDoorAngle(float degrees)
    {
        if (!unlocked)
            return;

        targetAngle = Mathf.Clamp(degrees, 0f, openAngle);
    }

    public void TryOpen()
    {
        if (PlayerInteractor.Instance == null)
            return;

        if (unlocked)
        {
            if (PlayerHiding.Instance != null)
                PlayerHiding.Instance.RequestCloset();
            return;
        }

        if (Inventory.Instance == null || Inventory.Instance.Selected != ItemId.ClosetKey)
        {
            PlayerInteractor.Instance.ShowMessage("It's locked.");
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayLockRattle(transform.position);
            return;
        }

        unlocked = true;
        targetAngle = openAngle;
        if (closedBlocker != null)
            closedBlocker.enabled = false;

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayClosetCreak(transform.position);
        PlayerInteractor.Instance.ShowMessage("The closet opens.");
    }
}
