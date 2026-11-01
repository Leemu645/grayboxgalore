using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player is carrying, and which of those things is in their hand.
/// Puzzle objects look at Selected. Picking something up selects it, so the
/// next interaction uses the new item.
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    readonly List<ItemId> items = new List<ItemId>();

    public event Action Changed;

    public IReadOnlyList<ItemId> Items => items;

    public int SelectedIndex { get; private set; } = -1;

    public ItemId Selected =>
        SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex] : ItemId.None;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one Inventory is in the scene. Keeping the first.", this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Has(ItemId id)
    {
        return id != ItemId.None && items.Contains(id);
    }

    public bool Add(ItemId id)
    {
        if (id == ItemId.None || items.Contains(id))
            return false;

        items.Add(id);
        SelectedIndex = items.Count - 1;
        Changed?.Invoke();
        return true;
    }

    public void Select(int index)
    {
        if (items.Count == 0)
        {
            SelectedIndex = -1;
            return;
        }

        index = Mathf.Clamp(index, 0, items.Count - 1);
        if (index == SelectedIndex)
            return;

        SelectedIndex = index;
        Changed?.Invoke();
    }

    public void Cycle(int direction)
    {
        if (items.Count == 0)
            return;

        int next = SelectedIndex + (direction > 0 ? 1 : -1);
        if (next < 0)
            next = items.Count - 1;
        if (next >= items.Count)
            next = 0;
        Select(next);
    }
}
