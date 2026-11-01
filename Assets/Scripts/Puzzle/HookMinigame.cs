using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A short 2D snag-the-key game. The mouse aims the splinter, but the tip
/// lags and wobbles. Hook the ring, then carry it to the right edge.
/// A hard jerk pulls the splinter out of the ring. Escape cancels.
/// </summary>
public class HookMinigame : MonoBehaviour
{
    const float HoleRadius = 34f;
    const float BreakDistance = 52f;
    const float BreakSpeed = 1100f;
    const float Noise = 32f;

    GameObject root;
    RectTransform panel;
    RectTransform tip;
    RectTransform key;
    Text status;
    Sprite white;
    JailKeyPuzzle puzzle;
    bool playing;
    bool hooked;
    Vector2 tipPosition;
    Vector2 tipVelocity;
    Vector2 keyPosition;

    public bool IsPlaying => playing;

    public void Begin(JailKeyPuzzle source)
    {
        if (playing || source == null)
            return;

        puzzle = source;
        EnsureUi();
        root.SetActive(true);
        Canvas.ForceUpdateCanvases();

        playing = true;
        hooked = false;
        tipVelocity = Vector2.zero;
        if (panel.rect.width < 10f)
        {
            tipPosition = new Vector2(640f, -40f);
            keyPosition = new Vector2(-420f, 20f);
        }
        else
        {
            tipPosition = new Vector2(panel.rect.xMax - 180f, -40f);
            keyPosition = new Vector2(panel.rect.xMin + 300f, 20f);
        }

        ApplyPositions();
        status.text = "Hook the ring, then drag it to the right edge. Move slowly. Escape cancels.";
        PlayerControl.Suspend();
    }

    void EnsureUi()
    {
        if (root != null)
            return;

        white = WhiteSprite();

        root = new GameObject("HookMinigame");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root.transform, false);
        Image shade = panelObject.AddComponent<Image>();
        shade.sprite = white;
        shade.color = new Color(0.02f, 0.015f, 0.012f, 0.92f);
        shade.raycastTarget = false;
        panel = shade.rectTransform;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;

        status = MakeText(panel, "Status", 22, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1100f, 60f));

        MakeText(panel, "RightEdgeLabel", 18, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(100f, 40f)).text = "HERE";
        Image goal = MakeImage(panel, "Goal", new Color(0.35f, 0.28f, 0.14f, 0.45f));
        RectTransform goalRect = goal.rectTransform;
        goalRect.anchorMin = new Vector2(1f, 0f);
        goalRect.anchorMax = new Vector2(1f, 1f);
        goalRect.pivot = new Vector2(1f, 0.5f);
        goalRect.sizeDelta = new Vector2(110f, 0f);
        goalRect.anchoredPosition = Vector2.zero;

        key = MakeImage(panel, "KeyRing", new Color(0.72f, 0.55f, 0.22f, 1f)).rectTransform;
        key.sizeDelta = new Vector2(78f, 78f);
        Image hole = MakeImage(key, "Hole", new Color(0.02f, 0.015f, 0.012f, 1f));
        hole.rectTransform.anchorMin = hole.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hole.rectTransform.sizeDelta = new Vector2(36f, 36f);

        tip = MakeImage(panel, "Splinter", new Color(0.62f, 0.42f, 0.22f, 1f)).rectTransform;
        tip.sizeDelta = new Vector2(18f, 70f);

        root.SetActive(false);
    }

    void Update()
    {
        if (!playing || Mouse.current == null)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            End(false);
            return;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector2 desired;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screen, null, out desired))
            desired = tipPosition;

        float wobbleX = (Mathf.PerlinNoise(Time.time * 1.6f, 0.4f) - 0.5f) * 2f * Noise;
        float wobbleY = (Mathf.PerlinNoise(2.2f, Time.time * 1.9f) - 0.5f) * 2f * Noise;
        if (Random.value < 0.015f)
        {
            wobbleX += Random.Range(-18f, 18f);
            wobbleY += Random.Range(-18f, 18f);
        }

        Vector2 goal = desired + new Vector2(wobbleX, wobbleY);
        // SmoothDamp lags behind the cursor, so a quick jerk overshoots the ring.
        tipPosition = Vector2.SmoothDamp(tipPosition, goal, ref tipVelocity, 0.14f, Mathf.Infinity, Time.deltaTime);
        float speed = tipVelocity.magnitude;

        if (!hooked)
        {
            if (Vector2.Distance(tipPosition, keyPosition) <= HoleRadius)
            {
                hooked = true;
                status.text = "Hooked. Ease it over to the right edge.";
            }
        }
        else
        {
            keyPosition = Vector2.Lerp(keyPosition, tipPosition, 1f - Mathf.Exp(-8f * Time.deltaTime));
            bool yanked = Vector2.Distance(tipPosition, keyPosition) > BreakDistance || speed > BreakSpeed;
            if (yanked)
            {
                hooked = false;
                status.text = "It slipped off. Hook the ring again.";
            }
            else if (tipPosition.x >= panel.rect.xMax - 100f)
            {
                End(true);
                return;
            }
        }

        ApplyPositions();
    }

    void ApplyPositions()
    {
        if (tip != null)
            tip.anchoredPosition = tipPosition;
        if (key != null)
            key.anchoredPosition = keyPosition;
    }

    void End(bool success)
    {
        playing = false;
        hooked = false;
        if (root != null)
            root.SetActive(false);

        PlayerControl.Resume();

        if (success && puzzle != null)
            puzzle.TakeFromMinigame();

        puzzle = null;
    }

    Image MakeImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = white;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text MakeText(Transform parent, string name, int size, Vector2 anchor, Vector2 offset, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.95f, 0.9f, 0.78f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = offset;
        return text;
    }

    static Sprite WhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }
}
