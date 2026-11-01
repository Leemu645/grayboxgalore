using UnityEngine;

/// <summary>
/// The jail key starts on the table outside the bars.
/// The floorboard plank knocks it to the floor, then a second prod pushes it
/// out of reach. A splinter opens the hook mini-game for that far key.
/// </summary>
public class JailKeyPuzzle : MonoBehaviour
{
    enum Stage
    {
        OnTable,
        NearBars,
        OutOfReach
    }

    [SerializeField] Vector3 nearBars = new Vector3(1.35f, 0.04f, 4.9f);
    [SerializeField] Vector3 pushedAway = new Vector3(1.9f, 0.04f, 3.55f);
    [SerializeField] HookMinigame minigame;

    Stage stage = Stage.OnTable;

    void Awake()
    {
        if (minigame == null)
            minigame = FindAnyObjectByType<HookMinigame>();
    }

    public void Use()
    {
        if (PlayerInteractor.Instance == null || Inventory.Instance == null)
            return;

        ItemId held = Inventory.Instance.Selected;

        if (stage == Stage.OnTable)
        {
            if (held == ItemId.FloorboardPlank)
            {
                stage = Stage.NearBars;
                transform.position = nearBars;
                PlayerInteractor.Instance.ShowMessage("The key clatters off the table, just outside the bars.");
            }
            else
            {
                PlayerInteractor.Instance.ShowMessage("I can't reach it.");
            }

            return;
        }

        if (stage == Stage.NearBars)
        {
            if (held == ItemId.FloorboardPlank)
            {
                stage = Stage.OutOfReach;
                transform.position = pushedAway;
                PlayerInteractor.Instance.ShowMessage("FUCK! I was so close, what the fuck do I do now?");
            }
            else
            {
                PlayerInteractor.Instance.ShowMessage("I can't reach it.");
            }

            return;
        }

        if (held == ItemId.Splinter)
        {
            if (minigame == null)
                minigame = FindAnyObjectByType<HookMinigame>();
            if (minigame != null)
                minigame.Begin(this);
        }
        else if (held == ItemId.FloorboardPlank)
        {
            PlayerInteractor.Instance.ShowMessage("It's too far. The plank doesn't reach anymore.");
        }
        else
        {
            PlayerInteractor.Instance.ShowMessage("I can't reach it.");
        }
    }

    public void TakeFromMinigame()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.Add(ItemId.JailKey);

        if (PlayerInteractor.Instance != null)
            PlayerInteractor.Instance.ShowMessage("The jail key is in my hand.");

        gameObject.SetActive(false);
    }
}
