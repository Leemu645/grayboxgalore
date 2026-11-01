using UnityEngine;

/// <summary>
/// The door at the top of the ramp. It still tells the player it is sealed,
/// and a solid blocker stays in the frame so they can never walk through.
/// The visitor swings the visible door, walks up to it, and the blocker
/// ignores only that body.
/// </summary>
public class SealedStairDoor : MonoBehaviour
{
    [SerializeField] Transform hinge;
    [SerializeField] Collider solidBlocker;
    [SerializeField] float openAngle = -105f;
    [SerializeField] float swingSpeed = 95f;

    float angle;
    float target;

    public bool IsShut => Mathf.Abs(angle) < 1f && Mathf.Abs(target) < 1f;

    void Update()
    {
        if (hinge == null)
            return;

        angle = Mathf.MoveTowards(angle, target, swingSpeed * Time.deltaTime);
        hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    public void OpenForVisitor()
    {
        target = openAngle;
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayClosetCreak(transform.position);
    }

    public void CloseAfterVisitor()
    {
        target = 0f;
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayClosetCreak(transform.position);
    }

    /// <summary>
    /// The blocker stays solid for the player. The visitor is allowed through it.
    /// </summary>
    public void LetThrough(CharacterController body)
    {
        if (body == null)
            return;

        if (solidBlocker != null)
            Physics.IgnoreCollision(body, solidBlocker, true);

        Collider[] own = GetComponentsInChildren<Collider>();
        for (int i = 0; i < own.Length; i++)
        {
            if (own[i] != null)
                Physics.IgnoreCollision(body, own[i], true);
        }
    }
}
