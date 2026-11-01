using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Closet and bed hiding, and the "am I safe from the visitor" check.
///
/// The locked jail cell counts on its own: you are safe when your feet are
/// inside the cell trigger and the bars are shut with the lock back on.
/// The closet and the bed count only after you have actually climbed in.
/// Leaving a spot makes you unsafe immediately, even while you are still
/// crawling out.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHiding : MonoBehaviour
{
    public static PlayerHiding Instance { get; private set; }

    enum Spot
    {
        None,
        Closet,
        Bed
    }

    [SerializeField] FirstPersonController body;
    [SerializeField] ClosetLock closet;
    [SerializeField] Transform closetHide;
    [SerializeField] Transform closetStand;
    [SerializeField] Transform bedHide;
    [SerializeField] Transform bedStand;
    [SerializeField] JailLock jailLock;
    [SerializeField] Collider cellVolume;

    [SerializeField] float closetYawHalf = 14f;
    [SerializeField] float closetPitchHalf = 10f;
    [SerializeField] float bedYawHalf = 32f;
    [SerializeField] float bedPitchHalf = 18f;

    Spot spot;
    bool concealed;
    bool busy;
    Coroutine motion;
    Vector3 cameraHome;
    bool cameraHomeCached;

    float savedNearClip = 0.3f;
    bool nearClipOverridden;
    GameObject overlay;
    Image veil;
    Image barTop;
    Image barBottom;
    Image barLeft;
    Image barRight;
    Text hiddenLabel;

    public bool BlocksWorldUse => concealed || busy;

    public bool CanLeave => concealed && !busy;

    public string LeavePrompt => spot == Spot.Bed ? "Crawl out" : "Step out";

    /// <summary>
    /// True while committed to the closet or the bed, including the crawl in.
    /// False the moment a crawl out starts.
    /// </summary>
    public bool IsConcealed => concealed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one PlayerHiding is in the scene.", this);
            return;
        }

        Instance = this;
        if (body == null)
            body = GetComponent<FirstPersonController>();
        CacheCameraHome();
        BuildOverlay();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Locked cell, or actually hidden. Standing in the open closet does not count.
    /// </summary>
    public bool IsSafe()
    {
        if (concealed)
            return true;

        if (jailLock != null && jailLock.IsSecured && cellVolume != null)
        {
            Vector3 feet = transform.position + Vector3.up * 0.35f;
            if (cellVolume.bounds.Contains(feet))
                return true;
        }

        return false;
    }

    public void RequestCloset()
    {
        if (busy || PlayerControl.Suspended)
            return;

        if (concealed)
        {
            if (spot == Spot.Closet)
                BeginExit();
            return;
        }

        if (closet == null || !closet.IsOpen || closetHide == null || closetStand == null)
            return;

        BeginEnter(Spot.Closet, closetHide, 1.42f, 8f);
    }

    public void RequestBed()
    {
        if (busy || PlayerControl.Suspended)
            return;

        if (concealed)
        {
            if (spot == Spot.Bed)
                BeginExit();
            return;
        }

        if (bedHide == null || bedStand == null)
            return;

        BeginEnter(Spot.Bed, bedHide, 0.2f, 0f);
    }

    public void RequestLeave()
    {
        if (!CanLeave || PlayerControl.Suspended)
            return;

        BeginExit();
    }

    /// <summary>
    /// The jumpscare takes the camera. Stop the crawl and drop the vignette.
    /// </summary>
    public void AbortForGameOver()
    {
        if (motion != null)
            StopCoroutine(motion);
        motion = null;
        busy = false;
        concealed = false;
        spot = Spot.None;
        SetOverlay(0f, false);
        if (body != null)
        {
            body.ClearLookWindow();
            body.SetFacingLocked(false);
        }

        SetCloseClip(false);
    }

    void BeginEnter(Spot next, Transform hide, float cameraY, float closetGap)
    {
        spot = next;
        concealed = true;
        busy = true;
        if (next == Spot.Closet && closet != null)
        {
            closet.SetDoorAngle(closetGap);
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayClosetCreak(closet.transform.position);
        }

        if (PlayerInteractor.Instance != null)
        {
            PlayerInteractor.Instance.ShowMessage(next == Spot.Bed
                ? "I crawl under the bed."
                : "I step inside and pull the doors to.");
        }

        if (motion != null)
            StopCoroutine(motion);
        motion = StartCoroutine(MoveBody(hide, cameraY, true, hide));
    }

    void BeginExit()
    {
        concealed = false;
        busy = true;
        Transform stand = spot == Spot.Bed ? bedStand : closetStand;
        if (spot == Spot.Closet && closet != null)
        {
            closet.SetDoorAngle(closet.OpenAngle);
            if (GameAudio.Instance != null)
                GameAudio.Instance.PlayClosetCreak(closet.transform.position);
        }

        if (motion != null)
            StopCoroutine(motion);
        motion = StartCoroutine(MoveBody(stand, cameraHome.y, false, stand));
    }

    IEnumerator MoveBody(Transform goal, float cameraY, bool entering, Transform faceFrom)
    {
        CacheCameraHome();
        SetCloseClip(entering);
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        if (body != null)
        {
            body.SetMovementLocked(true);
            body.SetFacingLocked(true);
        }

        Vector3 from = transform.position;
        Transform cam = body != null ? body.View : null;
        float fromY = cam != null ? cam.localPosition.y : cameraHome.y;
        float fromZ = cam != null ? cam.localPosition.z : 0f;
        float targetZ = entering && spot == Spot.Bed ? 0.16f : 0f;
        float yaw = faceFrom != null ? faceFrom.eulerAngles.y : transform.eulerAngles.y;
        float pitch = spot == Spot.Bed ? 4f : 0f;
        float duration = 0.55f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (PlayerControl.Suspended)
            {
                SetCloseClip(false);
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (goal != null)
                transform.position = Vector3.Lerp(from, goal.position, t);
            if (cam != null)
            {
                cam.localPosition = new Vector3(
                    0f,
                    Mathf.Lerp(fromY, cameraY, t),
                    Mathf.Lerp(fromZ, entering ? targetZ : cameraHome.z, t));
            }

            if (body != null)
                body.Face(yaw, pitch);

            float veil = entering ? t : 1f - t;
            SetOverlay(veil, true);
            yield return null;
        }

        if (goal != null)
            transform.position = goal.position;
        if (cam != null)
        {
            cam.localPosition = new Vector3(0f, cameraY, entering ? targetZ : cameraHome.z);
        }

        busy = false;
        motion = null;

        if (!entering)
        {
            spot = Spot.None;
            SetOverlay(0f, false);
            if (body != null)
            {
                body.ClearLookWindow();
                body.SetFacingLocked(false);
                body.SetMovementLocked(false);
            }

            if (controller != null)
                controller.enabled = true;
            SetCloseClip(false);
            yield break;
        }

        if (body != null)
        {
            body.Face(yaw, pitch);
            body.SetFacingLocked(false);
            float yawHalf = spot == Spot.Bed ? bedYawHalf : closetYawHalf;
            float pitchHalf = spot == Spot.Bed ? bedPitchHalf : closetPitchHalf;
            body.SetLookWindow(yaw, pitch, yawHalf, pitchHalf);
        }

        SetOverlay(1f, true);
    }

    void SetCloseClip(bool close)
    {
        if (body == null || body.View == null)
            return;

        Camera cam = body.View.GetComponent<Camera>();
        if (cam == null)
            return;

        if (close)
        {
            if (!nearClipOverridden)
            {
                savedNearClip = cam.nearClipPlane;
                nearClipOverridden = true;
            }

            cam.nearClipPlane = 0.05f;
            return;
        }

        if (!nearClipOverridden)
            return;

        cam.nearClipPlane = savedNearClip;
        nearClipOverridden = false;
    }

    void CacheCameraHome()
    {
        if (cameraHomeCached || body == null || body.View == null)
            return;

        cameraHome = body.View.localPosition;
        cameraHomeCached = true;
    }

    void BuildOverlay()
    {
        overlay = new GameObject("HidingOverlay");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite white = White();
        veil = Bar(overlay.transform, "Veil", white, new Color(0f, 0f, 0f, 0.55f));
        veil.rectTransform.anchorMin = Vector2.zero;
        veil.rectTransform.anchorMax = Vector2.one;
        veil.rectTransform.offsetMin = Vector2.zero;
        veil.rectTransform.offsetMax = Vector2.zero;

        barTop = Edge(overlay.transform, "BarTop", white, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        barBottom = Edge(overlay.transform, "BarBottom", white, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        barLeft = Edge(overlay.transform, "BarLeft", white, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        barRight = Edge(overlay.transform, "BarRight", white, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));

        GameObject labelObject = new GameObject("Hidden");
        labelObject.transform.SetParent(overlay.transform, false);
        hiddenLabel = labelObject.AddComponent<Text>();
        hiddenLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hiddenLabel.text = "Hidden";
        hiddenLabel.fontSize = 20;
        hiddenLabel.alignment = TextAnchor.MiddleCenter;
        hiddenLabel.color = new Color(0.85f, 0.82f, 0.74f, 0.45f);
        hiddenLabel.raycastTarget = false;
        RectTransform labelRect = hiddenLabel.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(240f, 36f);
        labelRect.anchoredPosition = new Vector2(0f, 78f);

        overlay.SetActive(false);
    }

    static Image Bar(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Image Edge(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        Image image = Bar(parent, name, sprite, new Color(0f, 0f, 0f, 0.92f));
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        return image;
    }

    void SetOverlay(float amount, bool visible)
    {
        if (overlay == null)
            return;

        if (overlay.activeSelf != visible)
            overlay.SetActive(visible);
        if (!visible)
            return;

        bool closet = spot == Spot.Closet;
        float veilAlpha = closet ? 0.72f : 0.38f;
        if (veil != null)
            veil.color = new Color(0f, 0f, 0f, veilAlpha * amount);

        float thick = (closet ? 150f : 70f) * amount;
        float side = (closet ? 220f : 90f) * amount;
        Size(barTop, new Vector2(0f, thick));
        Size(barBottom, new Vector2(0f, thick));
        Size(barLeft, new Vector2(side, 0f));
        Size(barRight, new Vector2(side, 0f));
        if (hiddenLabel != null)
        {
            Color color = hiddenLabel.color;
            color.a = 0.5f * amount;
            hiddenLabel.color = color;
        }
    }

    static void Size(Image image, Vector2 size)
    {
        if (image == null)
            return;
        image.rectTransform.sizeDelta = size;
    }

    static Sprite White()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }
}
