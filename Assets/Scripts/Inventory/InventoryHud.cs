using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hotbar along the bottom of the screen. Number keys and the scroll wheel
/// change the selected item. The selected slot is the one interactions use.
/// </summary>
public class InventoryHud : MonoBehaviour
{
    Inventory inventory;
    RectTransform row;
    readonly System.Collections.Generic.List<Image> slots = new System.Collections.Generic.List<Image>();
    readonly System.Collections.Generic.List<Text> labels = new System.Collections.Generic.List<Text>();
    Font font;
    Sprite white;

    void Start()
    {
        inventory = GetComponent<Inventory>();
        if (inventory == null)
            inventory = Inventory.Instance;
        if (inventory == null)
        {
            Debug.LogError("InventoryHud needs an Inventory on the player.", this);
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        white = WhiteSprite();
        BuildCanvas();
        inventory.Changed += Rebuild;
        Rebuild();
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.Changed -= Rebuild;
    }

    void Update()
    {
        if (PlayerControl.Suspended || inventory == null || inventory.Items.Count == 0)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) inventory.Select(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) inventory.Select(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) inventory.Select(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) inventory.Select(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) inventory.Select(4);
            else if (keyboard.digit6Key.wasPressedThisFrame) inventory.Select(5);
        }

        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0.1f)
            inventory.Cycle(-1);
        else if (scroll < -0.1f)
            inventory.Cycle(1);
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("InventoryUI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rowObject = new GameObject("Slots");
        rowObject.transform.SetParent(canvasObject.transform, false);
        row = rowObject.AddComponent<RectTransform>();
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.anchoredPosition = new Vector2(0f, 24f);
        row.sizeDelta = new Vector2(800f, 78f);

        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    void Rebuild()
    {
        if (row == null)
            return;

        for (int i = row.childCount - 1; i >= 0; i--)
            DestroyImmediate(row.GetChild(i).gameObject);
        slots.Clear();
        labels.Clear();

        if (inventory.Items.Count == 0)
        {
            Text empty = NewText(row, "Nothing carried", 18);
            RectTransform emptyRect = empty.rectTransform;
            emptyRect.sizeDelta = new Vector2(220f, 40f);
            return;
        }

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            GameObject slotObject = new GameObject("Slot" + (i + 1));
            slotObject.transform.SetParent(row, false);
            Image image = slotObject.AddComponent<Image>();
            image.sprite = white;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(110f, 64f);

            Text label = NewText(rect, (i + 1) + "  " + ItemNames.Label(inventory.Items[i]), 16);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4f, 4f);
            label.rectTransform.offsetMax = new Vector2(-4f, -4f);

            slots.Add(image);
            labels.Add(label);
        }

        PaintSelection();
    }

    void PaintSelection()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            bool selected = i == inventory.SelectedIndex;
            slots[i].color = selected
                ? new Color(0.45f, 0.32f, 0.16f, 0.95f)
                : new Color(0.08f, 0.07f, 0.06f, 0.8f);
            labels[i].color = selected
                ? new Color(1f, 0.92f, 0.75f, 1f)
                : new Color(0.75f, 0.7f, 0.62f, 1f);
        }
    }

    Text NewText(Transform parent, string value, int size)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.95f, 0.9f, 0.78f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        text.text = value;
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
