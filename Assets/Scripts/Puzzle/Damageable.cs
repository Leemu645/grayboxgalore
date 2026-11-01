using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Something an axe swing can strike. Hits count down to a break.
/// Put this on the object, or on a parent of the collider that gets hit.
/// An enemy can use the same component later.
/// </summary>
public class Damageable : MonoBehaviour
{
    [Tooltip("How many axe hits this can take before it breaks.")]
    [Min(1)]
    [SerializeField] int hitsToBreak = 1;

    [Tooltip("Shown on a hit that does not break it yet. Leave empty for no line.")]
    [SerializeField] string hitMessage = "The wood splits.";

    [Tooltip("Shown when the last hit lands.")]
    [SerializeField] string breakMessage = "It breaks apart.";

    [Tooltip("Turn this object off when it breaks.")]
    [SerializeField] bool disableOnBreak = true;

    [Tooltip("Optional extra work when it breaks, such as enabling debris.")]
    [SerializeField] UnityEvent onBroken;

    int hits;
    bool broken;

    public bool IsBroken => broken;

    public void Hit()
    {
        if (broken)
            return;

        hits++;
        if (hits < hitsToBreak)
        {
            if (PlayerInteractor.Instance != null && !string.IsNullOrEmpty(hitMessage))
                PlayerInteractor.Instance.ShowMessage(hitMessage);
            return;
        }

        broken = true;
        if (PlayerInteractor.Instance != null && !string.IsNullOrEmpty(breakMessage))
            PlayerInteractor.Instance.ShowMessage(breakMessage);

        onBroken?.Invoke();
        if (disableOnBreak)
            gameObject.SetActive(false);
    }
}
