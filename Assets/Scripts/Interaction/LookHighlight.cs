using UnityEngine;

/// <summary>
/// Brightens this object's renderers while the player is looking at it.
/// Used on the loose floorboard so it can be found without a text prompt.
/// </summary>
public class LookHighlight : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer[] renderers;
    Material[] sharedMaterials;
    bool highlighted;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        sharedMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            sharedMaterials[i] = renderers[i].sharedMaterial;
    }

    public void SetHighlighted(bool on)
    {
        if (on == highlighted || renderers == null)
            return;

        highlighted = on;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!on)
            {
                renderer.sharedMaterial = sharedMaterials[i];
                continue;
            }

            Material copy = renderer.material;
            Color warm = new Color(0.72f, 0.48f, 0.24f);
            if (copy.HasProperty(BaseColorId))
                copy.SetColor(BaseColorId, warm);
            if (copy.HasProperty(ColorId))
                copy.SetColor(ColorId, warm);
            copy.EnableKeyword("_EMISSION");
            if (copy.HasProperty(EmissionId))
                copy.SetColor(EmissionId, new Color(0.4f, 0.2f, 0.05f));
        }
    }
}
