using UnityEngine;

/// <summary>
/// Press E to put this object's item into the inventory, then hide it.
/// Hook Collect to the Interactable's On Interact event.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] ItemId item = ItemId.None;

    [Tooltip("Shown if this item is already being carried.")]
    [SerializeField] string alreadyCarryingMessage = "I already have one of these.";

    public void Collect()
    {
        if (Inventory.Instance == null)
        {
            Debug.LogError("There is no Inventory in the scene.", this);
            return;
        }

        if (item == ItemId.None)
            return;

        if (!Inventory.Instance.Add(item))
        {
            if (PlayerInteractor.Instance != null && !string.IsNullOrEmpty(alreadyCarryingMessage))
                PlayerInteractor.Instance.ShowMessage(alreadyCarryingMessage);
        }
        else if (GameAudio.Instance != null)
        {
            GameAudio.Instance.PlayPickup();
        }

        gameObject.SetActive(false);
    }
}
