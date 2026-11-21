using UnityEngine;

namespace Bonehilda;

internal static class CoffinVisuals
{
    internal static float Size(int stars) => 1f + .10f * stars;

    private static readonly Vector2[] Outline =
    {
        new Vector2(-.405f, .12f), new Vector2(.405f, .12f), new Vector2(.675f, 1.87f),
        new Vector2(.355f, 2.54f), new Vector2(-.355f, 2.54f), new Vector2(-.675f, 1.87f)
    };
    internal static void Build(GameObject prefab, int stars)
    {
        var palette = Visuals.Palette(Visuals.Template(prefab), stars);
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var collider in prefab.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var root = Visuals.Child(prefab.transform, "BonehildaCoffin");
        // Scale the complete cabinet and its colliders with the native skeleton tier.
        root.localScale = Vector3.one * Size(stars);
        Model(root, "cabinet", palette);
        Model(root, "bone-trim-" + stars, palette);
        Hull(root.gameObject, "back-hull");
        for (var i = 0; i < Outline.Length; i++)
        {
            var a = Outline[i];
            var b = Outline[(i + 1) % Outline.Length];
            var mid = (a + b) * .5f;
            var wall = Visuals.Child(root, i == 0 ? "BH_CoffinFloor" : "Cabinet wall");
            wall.localPosition = new Vector3(mid.x, mid.y, -.005f);
            wall.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg - 90f);
            Collider(wall.gameObject, Vector3.zero, new Vector3(.10f, Vector2.Distance(a, b), .60f));
        }

        var hinge = Visuals.Child(root, "BH_DoorHinge");
        hinge.localPosition = new Vector3(-.627f, 0f, .405f);
        var door = Model(hinge, "door", palette);
        // The Blender parts share the cabinet origin. Offset once at the hinge.
        door.localPosition = -hinge.localPosition;
        Hull(door.gameObject, "door-hull");
        Model(door, "knot-" + stars, palette);
        for (var i = 0; i < stars; i++)
            Model(door, "star", palette).localPosition = Vector3.right * ((i - (stars - 1) * .5f) * .208f);

        var ember = new Material(palette) { name = "BH candle flame" };
        ember.EnableKeyword("_EMISSION");
        if (ember.HasProperty("_EmissionMap")) ember.SetTexture("_EmissionMap", palette.mainTexture);
        if (ember.HasProperty("_EmissionColor")) ember.SetColor("_EmissionColor", new Color(1.8f, 1.30f, .60f));
        Model(door, "candle-" + stars, palette);
        var wick = Visuals.Child(door, "BH_Wick");
        var flame = Model(wick, "flame-" + stars, ember);
        var bounds = flame.GetComponent<MeshFilter>().sharedMesh.bounds;
        wick.localPosition = bounds.center - Vector3.up * bounds.extents.y;
        flame.localPosition = -wick.localPosition;
        var candle = Visuals.Child(wick, "Candlelight");
        candle.localPosition = Vector3.up * bounds.extents.y + Vector3.forward * .029f;
        var light = candle.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .63f, .29f);
        light.intensity = .85f;
        light.range = 1.45f;
        light.shadows = LightShadows.None;
        wick.gameObject.AddComponent<CandleFlame>();

        var chest = prefab.GetComponent<Container>();
        // Coffin animates a real hinge; the vanilla chest toggles two static lid objects.
        chest.m_closed = null!;
        chest.m_open = null!;
        var wear = prefab.GetComponent<WearNTear>();
        wear.m_new = null!; wear.m_worn = null!; wear.m_broken = null!;
        wear.m_fragmentRoots = new[] { root.gameObject };
        EntryRamp(root);
        prefab.GetComponent<Piece>().m_icon = CoffinIcons.Create(stars);
    }

    private static Transform Model(Transform parent, string name, Material material)
    {
        var model = Visuals.Child(parent, name);
        model.gameObject.AddComponent<MeshFilter>().sharedMesh = ModelArt.Load(name).Mesh;
        model.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return model;
    }

    private static void Hull(GameObject obj, string name)
    {
        var collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = ModelArt.Load(name).Mesh;
        collider.convex = true;
        obj.layer = LayerMask.NameToLayer("piece");
    }

    private static void Collider(GameObject obj, Vector3 center, Vector3 size)
    {
        var collider = obj.AddComponent<BoxCollider>(); collider.center = center; collider.size = size;
        obj.layer = LayerMask.NameToLayer("piece");
    }

    private static void EntryRamp(Transform parent)
    {
        // A shallow physical threshold, without a visible plank in front of the cabinet.
        var obj = new GameObject("BH_EntryRamp");
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("piece");
        var mesh = new Mesh { name = "BH coffin threshold wedge" };
        mesh.vertices = new[] {
            new Vector3(-.35f, -.10f, .08f), new Vector3(.35f, -.10f, .08f),
            new Vector3(.35f, -.10f, 1.4f), new Vector3(-.35f, -.10f, 1.4f),
            new Vector3(-.35f, .20f, .08f), new Vector3(.35f, .20f, .08f),
            new Vector3(.35f, .01f, 1.4f), new Vector3(-.35f, .01f, 1.4f)
        };
        mesh.triangles = new[] { 0,1,2, 0,2,3, 4,6,5, 4,7,6, 0,5,1, 0,4,5, 3,6,7, 3,2,6, 0,7,4, 0,3,7, 1,6,2, 1,5,6 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;
    }
}
