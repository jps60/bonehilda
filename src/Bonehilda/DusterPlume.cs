using UnityEngine;

namespace Bonehilda;

internal static class DusterPlume
{
    private const float Ferrule = .22f;
    internal const float Tip = .51f;

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
        var drag = -sweep * (.12f + .29f * pressure) * velocity + Vector3.up * (.18f * pressure);
        plume.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.up + handle.InverseTransformDirection(drag) * blend);
    }

    internal static Vector3 End(Transform plume) => plume.TransformPoint(Vector3.up * (Tip - Ferrule));
}
