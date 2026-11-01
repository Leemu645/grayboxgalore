using UnityEngine;

/// <summary>
/// The plank breaks this crate. Splinters are separate objects so they stay
/// behind when the crate is hidden.
/// </summary>
public class SmashableCrate : MonoBehaviour
{
    [SerializeField] GameObject splinters;

    public void Bash()
    {
        if (PlayerInteractor.Instance == null || Inventory.Instance == null)
            return;

        if (Inventory.Instance.Selected != ItemId.FloorboardPlank)
        {
            PlayerInteractor.Instance.ShowMessage("It's solid. I need something to hit it with.");
            return;
        }

        if (splinters != null)
            splinters.SetActive(true);

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayCrateSmash(transform.position);
        PlayerInteractor.Instance.ShowMessage("The box breaks into splinters.");
        gameObject.SetActive(false);
    }
}
