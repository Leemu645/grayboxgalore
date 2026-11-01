using UnityEngine;

/// <summary>
/// Five completed holds pull this board up. The plank goes into the inventory
/// and the closet key, which was hidden under the board, becomes visible.
/// </summary>
public class PryFloorboard : MonoBehaviour
{
    [SerializeField] HoldInteractable hold;
    [SerializeField] GameObject boardVisual;
    [SerializeField] GameObject hiddenKey;
    [SerializeField] Collider boardCollider;

    void Awake()
    {
        if (hold == null)
            hold = GetComponent<HoldInteractable>();
        if (boardCollider == null)
            boardCollider = GetComponent<Collider>();
        if (hiddenKey != null)
            hiddenKey.SetActive(false);
    }

    void OnEnable()
    {
        if (hold != null)
            hold.HoldCompleted += OnHoldCompleted;
    }

    void OnDisable()
    {
        if (hold != null)
            hold.HoldCompleted -= OnHoldCompleted;
    }

    void OnHoldCompleted(int count)
    {
        if (PlayerInteractor.Instance == null)
            return;

        if (count == 1)
            PlayerInteractor.Instance.ShowMessage("It looks like you can pull it out, but you're not sure.");
        else if (count == 2)
            PlayerInteractor.Instance.ShowMessage("It shifts a little.");
        else if (count == 3)
            PlayerInteractor.Instance.ShowMessage("Is this hopeless? I'm never getting out of here.");
        else if (count == 4)
            PlayerInteractor.Instance.ShowMessage("The board groans, but it's still stuck.");
        else if (count >= 5)
            Finish();
    }

    void Finish()
    {
        if (hold != null)
            hold.AcceptingHolds = false;

        if (Inventory.Instance != null)
            Inventory.Instance.Add(ItemId.FloorboardPlank);

        if (PlayerInteractor.Instance != null)
            PlayerInteractor.Instance.ShowMessage("I can't believe that worked. But why would there be a key here?");

        if (boardVisual != null)
            boardVisual.SetActive(false);
        if (boardCollider != null)
            boardCollider.enabled = false;
        if (hiddenKey != null)
            hiddenKey.SetActive(true);

        Interactable prompt = GetComponent<Interactable>();
        if (prompt != null)
            prompt.enabled = false;
    }
}
