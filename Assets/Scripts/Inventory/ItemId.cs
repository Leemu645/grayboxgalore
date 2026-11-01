/// <summary>
/// Things the player can carry. New puzzle items should be added here so the
/// hotbar, pickups, and locked objects all speak the same language.
/// </summary>
public enum ItemId
{
    None,
    Flashlight,
    FloorboardPlank,
    Splinter,
    ClosetKey,
    JailKey,
    Axe
}

public static class ItemNames
{
    public static string Label(ItemId id)
    {
        switch (id)
        {
            case ItemId.Flashlight: return "Flashlight";
            case ItemId.FloorboardPlank: return "Plank";
            case ItemId.Splinter: return "Splinter";
            case ItemId.ClosetKey: return "Key";
            case ItemId.JailKey: return "Jail Key";
            case ItemId.Axe: return "Axe";
            default: return "";
        }
    }
}
