using UnityEngine;

/// <summary>
/// The lock on the barred door. The jail key opens it. Anything else keeps
/// the original "You need a key" line.
/// </summary>
public class JailLock : MonoBehaviour
{
    [SerializeField] JailDoor door;

    public void TryOpen()
    {
        if (PlayerInteractor.Instance == null)
            return;

        if (door != null && door.IsOpen)
            return;

        if (Inventory.Instance == null || Inventory.Instance.Selected != ItemId.JailKey)
        {
            PlayerInteractor.Instance.ShowMessage("You need a key");
            return;
        }

        if (door != null)
            door.Open();

        PlayerInteractor.Instance.ShowMessage("The lock turns. The bars slide aside.");
    }
}
