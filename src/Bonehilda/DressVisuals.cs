using System;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace Bonehilda;

internal static class DressVisuals
{
    private const string Outfit = "BH_Outfit";

    internal static void SetTier(Transform skeleton, int stars)
    {
        var outfit = skeleton.Find(Outfit);
        if (outfit && outfit.TryGetComponent<SkinnedMeshRenderer>(out var skin))
        {
            var tier = Mathf.Clamp(stars, 0, 2);
            skin.sharedMaterial = Visuals.Palette(skin.sharedMaterial, tier);
            skin.SetBlendShapeWeight(0, tier * 50f);
        }
    }

    internal static void Build(Transform skeleton)
    {
        if (skeleton.Find(Outfit)) return;
        var source = skeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.sharedMesh && r.bones.Length == r.sharedMesh.bindposes.Length
                && r.bones.Any(bone => bone && bone.name == "Spine2"));
        if (!source) throw new InvalidOperationException("Bonehilda needs the friendly Skeleton skin to bind her outfit.");
        var art = ModelArt.Load("outfit");
        var bones = new Transform[art.Joints.Length];
        var bind = new Matrix4x4[bones.Length];
        var nativeBones = source.bones;
        var nativeBind = source.sharedMesh.bindposes;
        // Authored vertices are in character space. Native inverse binds expect
        // imported mesh space (the Valheim mesh also has a scale and a half-turn).
        var meshFromCharacter = source.transform.worldToLocalMatrix * skeleton.localToWorldMatrix;
        for (var i = 0; i < bones.Length; i++)
        {
            var ribbon = art.Joints[i] == "ApronRibbon";
            var jointName = ribbon ? "Spine" : art.Joints[i];
            var native = Array.FindIndex(nativeBones, bone => bone && bone.name == jointName);
            if (native < 0) throw new InvalidOperationException($"Cannot bind Bonehilda's outfit. Skeleton joint '{jointName}' is missing.");
            bones[i] = nativeBones[native];
            bind[i] = nativeBind[native] * meshFromCharacter;
            if (!ribbon) continue;

            var anchor = bind[i].MultiplyPoint3x4(new Vector3(0, 1.194f, -.134f));
            bones[i] = Visuals.Child(bones[i], "ApronRibbon");
            bones[i].localPosition = anchor;
            bind[i] = Matrix4x4.Translate(-anchor) * bind[i];
            bones[i].gameObject.AddComponent<ApronRibbon>();
        }

        var skin = Visuals.Child(skeleton, Outfit).gameObject.AddComponent<SkinnedMeshRenderer>();
        art.Mesh.bindposes = bind;
        skin.sharedMesh = art.Mesh;
        skin.bones = bones;
        skin.rootBone = skeleton;
        skin.quality = SkinQuality.Bone4;
        // Bounds follow the character root. Allow for the larger native rig at two stars.
        var levelEffects = skeleton.GetComponentInChildren<LevelEffects>(true);
        var maxScale = levelEffects ? levelEffects.m_levelSetups.Take(2).Select(level => level.m_scale).DefaultIfEmpty(1f).Max() : 1f;
        skin.localBounds = new Bounds(new Vector3(0, 1.04f, 0) * maxScale, new Vector3(2.25f, 2.45f, 1.7f) * maxScale);
        var template = Visuals.Template(PrefabManager.Instance.GetPrefab("piece_chest_wood"));
        skin.sharedMaterial = Visuals.Palette(template, 0);
        var lod = skeleton.GetComponentInChildren<LODGroup>(true);
        if (!lod) return;
        var levels = lod.GetLODs();
        for (var i = 0; i < levels.Length; i++) levels[i].renderers = levels[i].renderers.Concat(new[] { skin }).ToArray();
        lod.SetLODs(levels);
        lod.RecalculateBounds();
    }
}
