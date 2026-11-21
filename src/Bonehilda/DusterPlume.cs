using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal static class DusterPlume
{
    private const float Ferrule = .22f;
    internal const float Tip = .51f;
    private static Vector3[] contactVertices = System.Array.Empty<Vector3>();

    internal static Transform Attach(Transform handle, Material material)
    {
        var mesh = ModelArt.Load("feather-duster").Mesh;
        if (mesh.bindposes.Length == 0)
        {
            var vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var bend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.245f, Tip, vertices[i].y));
                weights[i] = bend > .5f
                    ? new BoneWeight { boneIndex0 = 1, weight0 = bend, boneIndex1 = 0, weight1 = 1f - bend }
                    : new BoneWeight { boneIndex0 = 0, weight0 = 1f - bend, boneIndex1 = 1, weight1 = bend };
            }
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(Vector3.down * Ferrule) };
            mesh.boneWeights = weights;
            contactVertices = FeatherOutline(vertices);
        }
        var plume = Visuals.Child(handle, "BH_Plume");
        plume.localPosition = Vector3.up * Ferrule;
        var skin = handle.gameObject.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = mesh;
        skin.sharedMaterial = material;
        skin.rootBone = handle;
        skin.bones = new[] { handle, plume };
        skin.quality = SkinQuality.Bone2;
        var bounds = mesh.bounds;
        bounds.Expand(.24f);
        skin.localBounds = bounds;
        return plume;
    }

    internal static void Bend(Transform plume, Transform handle, Vector3 sweep, float velocity, float pressure, float blend)
    {
        var drag = -Vector3.ProjectOnPlane(sweep, handle.up) * ((.25f + .34f * pressure) * velocity);
        plume.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.up + handle.InverseTransformDirection(drag) * blend);
        var compression = Mathf.Clamp01(pressure) * blend;
        plume.localScale = new Vector3(1f + .06f * compression, 1f - .10f * compression, 1f + .06f * compression);
    }

    // Keep the outer vanes from the authored mesh. The low feather changes with
    // both shaft inclination and drag, even while the centreline clears the board.
    private static Vector3[] FeatherOutline(Vector3[] vertices)
    {
        var outline = new HashSet<Vector3>();
        for (var bearing = 0; bearing < 360; bearing += 30)
        {
            var radial = Quaternion.AngleAxis(bearing, Vector3.up) * Vector3.right;
            for (var pitch = -30; pitch <= 90; pitch += 30)
            {
                var angle = pitch * Mathf.Deg2Rad;
                var direction = radial * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                var best = float.NegativeInfinity;
                var outer = Vector3.zero;
                foreach (var v in vertices)
                {
                    if (v.y <= Ferrule) continue;
                    var distance = Vector3.Dot(v, direction);
                    if (distance <= best) continue;
                    best = distance;
                    outer = v;
                }
                outline.Add(outer);
            }
        }
        var points = new Vector3[outline.Count];
        outline.CopyTo(points);
        return points;
    }

    internal static Vector3 Contact(Transform handle, Transform plume)
    {
        var low = End(plume);
        foreach (var vertex in contactVertices)
        {
            var bend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.245f, Tip, vertex.y));
            var feather = Vector3.Lerp(handle.TransformPoint(vertex),
                plume.TransformPoint(vertex - Vector3.up * Ferrule), bend);
            if (feather.y < low.y) low = feather;
        }
        return low;
    }

    internal static Vector3 End(Transform plume) => plume.TransformPoint(Vector3.up * (Tip - Ferrule));
}
