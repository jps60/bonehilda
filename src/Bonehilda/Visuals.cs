using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bonehilda;

internal static class Visuals
{
    private static readonly Dictionary<int, Material> palettes = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material> clothPalettes = new Dictionary<int, Material>();

    internal static Material Template(GameObject source)
    {
        var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m && m.shader).ToArray();
        // The null graphics device cannot reflect bundled shader properties, but
        // still retains the referenced materials for CPU geometry validation.
        return materials.FirstOrDefault(m => m.HasProperty("_Color")) ?? materials.First();
    }

    internal static Material Palette(Material template, int stars)
    {
        if (palettes.TryGetValue(stars, out var material)) return material;
        material = MakePalette(template, stars);
        palettes.Add(stars, material);
        return material;
    }

    private static Material MakePalette(Material template, int stars)
    {
        // Valheim loads its shaders from asset bundles. Shader.Find cannot reliably see them
        // during prefab registration, and Standard is absent from current game builds.
        // Clone a referenced vanilla material so Unity retains the bundled shader.
        var material = new Material(template) { name = "BH tier " + stars };
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_MainTex"))
        {
            material.mainTexture = ModelArt.Texture("palette" + stars, true);
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
        }
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
        if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", null);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", null);
        if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", Color.black);
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_EMISSION");
        for (var i = 0; i < material.shader.GetPropertyCount(); i++)
        {
            var property = material.shader.GetPropertyName(i);
            var lower = property.ToLowerInvariant();
            if (!lower.Contains("gloss") && !lower.Contains("smooth") && !lower.Contains("metallic")) continue;
            var type = material.shader.GetPropertyType(i);
            if (type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range)
                material.SetFloat(property, 0f);
            else if (type == UnityEngine.Rendering.ShaderPropertyType.Texture) material.SetTexture(property, null);
            else if (type == UnityEngine.Rendering.ShaderPropertyType.Vector) material.SetVector(property, Vector4.zero);
        }
        return material;
    }

    internal static void BuildCoffin(GameObject prefab, int stars) => CoffinVisuals.Build(prefab, stars);

    internal static Material ClothPalette(Material template, int stars)
    {
        if (clothPalettes.TryGetValue(stars, out var cloth)) return cloth;
        // Use a native clothing material, independently of the cabinet palette.
        // Never feed a cached wood/snow shader back into the garment's tiers.
        cloth = MakePalette(template, stars);
        cloth.name = "BH matte cloth tier " + stars;
        if (cloth.HasProperty("_Color")) cloth.SetColor("_Color", new Color(.8f, .8f, .8f, 1f));
        foreach (var property in new[] { "_ValueNoise", "_ValueNoiseVertex", "_AddRain", "_AddSnow", "_SnowCover", "_TriplanarMap", "_SpecularHighlights", "_GlossyReflections", "_Hue", "_Saturation", "_Value", "_UseStyles", "_NoiseGlowEnabled" })
            if (cloth.HasProperty(property)) cloth.SetFloat(property, 0f);
        if (cloth.HasProperty("_NoiseTex")) cloth.SetTexture("_NoiseTex", null);
        if (cloth.HasProperty("_MoveableObject")) cloth.SetFloat("_MoveableObject", 1f);
        // Shader toggle properties also have compiled keywords. Changing the
        // float alone leaves an inherited rain/noise variant enabled.
        foreach (var keyword in new[] { "_ADDRAIN_ON", "_ADDSNOW_ON", "_VALUENOISEVERTEX_ON", "_TRIPLANARMAP_ON", "_METALLICGLOSSMAP", "_SPECGLOSSMAP", "_USESTYLES_ON", "NOISEGLOW" })
            cloth.DisableKeyword(keyword);
        clothPalettes.Add(stars, cloth);
        return cloth;
    }

    internal static void Dress(Transform skeleton) => DressVisuals.Build(skeleton);

    internal static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
}
