using UnityEngine;

/// <summary>
/// World flashlight lying on the floor. Interactable shows the prompt and the
/// "you picked it up" line. This component's Collect method is hooked to
/// Interactable's On Interact event, so pressing E also gives the player the light
/// and hides this object.
/// </summary>
public class FlashlightPickup : MonoBehaviour
{
    /// <summary>
    /// Called from the Interactable event when the player uses this object.
    /// </summary>
    public void Collect()
    {
        Flashlight flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight == null)
        {
            Debug.LogError("Nothing in the scene has a Flashlight component, so this pickup has nowhere to go.", this);
            return;
        }

        flashlight.Give();
        if (Inventory.Instance != null)
            Inventory.Instance.Add(ItemId.Flashlight);
        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayPickup();
        gameObject.SetActive(false);
    }
}
