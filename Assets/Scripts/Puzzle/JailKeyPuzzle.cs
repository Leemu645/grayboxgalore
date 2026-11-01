using System.Collections;
using UnityEngine;

/// <summary>
/// The jail key starts on the table outside the bars.
/// The floorboard plank knocks it to the floor, then a second prod pushes it
/// out of reach. Both moves play out over a short slide instead of a teleport.
/// A splinter opens the hook mini-game for that far key.
/// </summary>
public class JailKeyPuzzle : MonoBehaviour
{
    enum Stage
    {
        OnTable,
        NearBars,
        OutOfReach
    }

    [SerializeField] Vector3 nearBars = new Vector3(2.35f, 0.045f, 4.95f);
    [SerializeField] Vector3 pushedAway = new Vector3(2.55f, 0.045f, 3.4f);
    [SerializeField] HookMinigame minigame;

    Stage stage = Stage.OnTable;
    bool busy;
    Material plankMaterial;

    void Awake()
    {
        if (minigame == null)
            minigame = FindAnyObjectByType<HookMinigame>();
    }

    public void Use()
    {
        if (busy || PlayerInteractor.Instance == null || Inventory.Instance == null)
            return;

        ItemId held = Inventory.Instance.Selected;

        if (stage == Stage.OnTable)
        {
            if (held == ItemId.FloorboardPlank)
            {
                stage = Stage.NearBars;
                StartCoroutine(SlideTo(nearBars, "The key clatters off the table, just outside the bars.", true));
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
                StartCoroutine(SlideTo(pushedAway, "FUCK! I was so close, what the fuck do I do now?", false));
            }
            else if (held == ItemId.Splinter)
            {
                BeginHook();
            }
            else
            {
                PlayerInteractor.Instance.ShowMessage("I can't reach it.");
            }

            return;
        }

        if (held == ItemId.Splinter)
        {
            BeginHook();
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

    IEnumerator SlideTo(Vector3 destination, string message, bool offTheTable)
    {
        busy = true;
        if (PlayerInteractor.Instance != null)
            PlayerInteractor.Instance.ShowMessage(message);

        Vector3 from = transform.position;
        if (GameAudio.Instance != null)
        {
            if (offTheTable)
                GameAudio.Instance.PlayKeyJingle(from);
            else
                GameAudio.Instance.PlayKeySkid(from);
        }
        Quaternion fromRotation = transform.rotation;
        GameObject plank = CreatePokePlank();
        Vector3 pokeStart = new Vector3(from.x, 1.05f, 5.32f);

        const float duration = 0.75f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float slide = Mathf.SmoothStep(0f, 1f, t);
            float hop = Mathf.Sin(slide * Mathf.PI) * 0.12f;
            transform.position = Vector3.Lerp(from, destination, slide) + Vector3.up * hop;
            float spin = Mathf.Sin(slide * Mathf.PI) * 70f;
            transform.rotation = fromRotation * Quaternion.Euler(spin * 0.25f, spin, 0f);

            float poke = Mathf.Sin(t * Mathf.PI);
            Vector3 pokeEnd = Vector3.Lerp(pokeStart, from + Vector3.down * 0.4f, poke);
            PlacePlank(plank.transform, Vector3.Lerp(pokeStart, pokeEnd, 0.15f), pokeEnd);
            yield return null;
        }

        transform.position = destination;
        transform.rotation = Quaternion.Euler(0f, 40f, 8f);
        if (plank != null)
            Destroy(plank);

        busy = false;
    }

    GameObject CreatePokePlank()
    {
        GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = "PokePlank";
        Collider plankCollider = plank.GetComponent<Collider>();
        if (plankCollider != null)
            Destroy(plankCollider);

        Renderer renderer = plank.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (plankMaterial == null)
            plankMaterial = MakeWood();
        if (plankMaterial != null)
            renderer.sharedMaterial = plankMaterial;
        return plank;
    }

    static void PlacePlank(Transform plank, Vector3 start, Vector3 end)
    {
        Vector3 span = end - start;
        float length = Mathf.Max(0.05f, span.magnitude);
        plank.position = (start + end) * 0.5f;
        if (span.sqrMagnitude > 0.0001f)
            plank.rotation = Quaternion.LookRotation(span.normalized, Vector3.up);
        plank.localScale = new Vector3(0.08f, 0.025f, length);
    }

    static Material MakeWood()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        Color color = new Color(0.32f, 0.2f, 0.11f);
        material.color = color;
        material.SetColor("_BaseColor", color);
        return material;
    }

    void BeginHook()
    {
        if (minigame == null)
            minigame = FindAnyObjectByType<HookMinigame>();
        if (minigame != null)
            minigame.Begin(this);
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
