using UnityEngine;

/// <summary>
/// The lock on the barred door. The jail key opens it and the lock object
/// hides so it does not block the way out. The closet key does not fit.
/// The door puts the lock back only after the bars have closed again.
/// </summary>
public class JailLock : MonoBehaviour
{
    [SerializeField] JailDoor door;

    public void TryOpen()
    {
        if (PlayerInteractor.Instance == null)
            return;

        if (door != null && (door.IsOpen || door.IsMoving))
            return;

        ItemId held = Inventory.Instance != null ? Inventory.Instance.Selected : ItemId.None;
        if (held == ItemId.ClosetKey)
        {
            PlayerInteractor.Instance.ShowMessage("It's no use, it doesn't fit.");
            return;
        }

        if (held != ItemId.JailKey)
        {
            PlayerInteractor.Instance.ShowMessage("You need a key");
            return;
        }

        if (door != null)
            door.Open();

        PlayerInteractor.Instance.ShowMessage("The lock turns. The bars slide aside.");
        gameObject.SetActive(false);
    }

    public void ShowAgain()
    {
        gameObject.SetActive(true);
    }
}
