using UnityEngine;

/// <summary>
/// The furnace burns a carried plank or splinter for ten seconds. Other items,
/// and empty hands, still get the usual hot warning. The item is not destroyed.
/// </summary>
public class FurnaceFire : MonoBehaviour
{
    public void TryLight()
    {
        if (PlayerInteractor.Instance == null)
            return;

        ItemId held = Inventory.Instance != null ? Inventory.Instance.Selected : ItemId.None;
        if (held != ItemId.FloorboardPlank && held != ItemId.Splinter)
        {
            PlayerInteractor.Instance.ShowMessage("Ow, it's hot. What do I expect will happen?");
            return;
        }

        if (CarriedFire.Instance == null)
        {
            PlayerInteractor.Instance.ShowMessage("Ow, it's hot. What do I expect will happen?");
            return;
        }

        CarriedFire.Instance.Ignite(held);
    }
}
