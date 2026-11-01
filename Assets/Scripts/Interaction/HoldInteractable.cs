using System;
using UnityEngine;

/// <summary>
/// An object that is used by holding E, not tapping it. PlayerInteractor fills
/// the bar and calls RegisterHold once each time the hold finishes.
/// Completed holds stay counted if the player lets go early.
/// </summary>
public class HoldInteractable : MonoBehaviour
{
    [Tooltip("How long E must be held for one attempt, in seconds.")]
    [Min(0.2f)]
    [SerializeField] float holdSeconds = 1f;

    public float HoldSeconds => holdSeconds;

    public int Completed { get; private set; }

    public bool AcceptingHolds { get; set; } = true;

    public event Action<int> HoldCompleted;

    public void RegisterHold()
    {
        if (!AcceptingHolds)
            return;

        Completed++;
        HoldCompleted?.Invoke(Completed);
    }
}
