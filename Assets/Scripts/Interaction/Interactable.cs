using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Something the player can look at and use.
/// Put this on the object that has the collider (or on a parent of that collider).
/// PlayerInteractor reads the message and shows it on screen. On Interact is an
/// optional hook for later: a sound, a door, a key check. Leave it empty for now.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Tooltip("Short name shown in the prompt, after \"E:\". Example: Interact.")]
    [SerializeField] string prompt = "Interact";

    [Tooltip("Sentence shown on screen when the player interacts.")]
    [TextArea(2, 4)]
    [SerializeField] string message = "Nothing happens.";

    [Tooltip("Extra work to do when this is used. The message still shows either way.")]
    [SerializeField] UnityEvent onInteract;

    [Tooltip("Turn off for objects that should not advertise themselves. The loose floorboard uses this.")]
    [SerializeField] bool showPrompt = true;

    public string Prompt => string.IsNullOrEmpty(prompt) ? "Interact" : prompt;

    public bool ShowsPrompt => showPrompt && !string.IsNullOrEmpty(prompt);

    public string Message => message;

    /// <summary>
    /// The event shown as On Interact in the Inspector. Other scripts, and the
    /// basement builder, hook extra work onto this (the flashlight pickup uses it).
    /// </summary>
    public UnityEvent OnInteract => onInteract;

    public void Interact()
    {
        onInteract?.Invoke();
    }
}
