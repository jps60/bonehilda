using System;
using System.Linq;
using Jotunn.Entities;
using UnityEngine;

namespace Bonehilda;

// Manual review only. No rug scheduling/eligibility changes are made by this draft.
internal sealed class RugSweepDraft : MonoBehaviour
{
    private const string StartKey = "BH_sweepDraftStart", RugKey = "BH_sweepDraftRug";
    private Maid maid = null!;
    private ZNetView view = null!;
    private ShooGesture.Arm left = null!, right = null!;
    private Transform spine = null!, gaze = null!;
    private Quaternion spineRest, gazeRest;
    private bool bodyPosed;
    private GameObject? tool;
    private Vector3 axis, tip;
    private Vector3[] modelVertices = Array.Empty<Vector3>();
    private float length, visualScale;
    internal float LeftGripError { get; private set; }
    internal float RightGripError { get; private set; }
    private bool failedTool;
    private bool CanPreview => maid.AvailableForSocial && SessionSupport.Active && !maid.GetComponent<Character>().IsDead()
        && !maid.GetComponent<MaidSeating>().Attached && view.GetZDO().GetString("BH_idleEmote").Length == 0
        && (view.GetZDO().GetLong(FeatherDuster.StartKey) == 0
            || (ZNet.instance.GetTime().Ticks - view.GetZDO().GetLong(FeatherDuster.StartKey)) / (double)TimeSpan.TicksPerSecond >= FeatherDuster.Seconds);
    internal bool HoldIdle => view && view.IsValid() && view.GetZDO().GetLong(StartKey) != 0
        && ZNet.instance && ZNet.instance.GetTime().Ticks - view.GetZDO().GetLong(StartKey) < 5 * TimeSpan.TicksPerSecond;

    private void Start()
    {
        maid = GetComponent<Maid>(); view = GetComponent<ZNetView>();
        var animator = GetComponentInChildren<Animator>();
        left = new ShooGesture.Arm(animator, true); right = new ShooGesture.Arm(animator, false);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine); gaze = animator.GetBoneTransform(HumanBodyBones.Head);
    }
    internal bool Begin(Piece rug, int variant, bool flip)
    {
        if (!CanPreview || !view.IsOwner() || !BuildTool()) return false;
        var data = view.GetZDO();
        data.Set(ShooGesture.StartKey, 0L);
        data.Set("BH_sweepDraftVariant", variant); data.Set("BH_sweepDraftFlip", flip);
        data.Set(RugKey, rug.GetComponent<ZNetView>().GetZDO().m_uid);
        data.Set(StartKey, ZNet.instance.GetTime().Ticks);
        return true;
    }
    private void Update()
    {
        left?.Restore(); right?.Restore();
        if (bodyPosed) { spine.localRotation = spineRest; gaze.localRotation = gazeRest; bodyPosed = false; }
    }
    private void OnDisable() { Update(); if (tool) tool!.SetActive(false); }
    private void OnDestroy() { if (tool) Destroy(tool); }

    private bool BuildTool()
    {
        if (tool) return true;
        if (failedTool || !ZNetScene.instance) return false;
        var source = ZNetScene.instance.GetPrefab("BogWitchKvastur");
        if (!source) { failedTool = true; return false; }
        // Copy renderer data only. Never instantiate Kvastur's creature, AI,
        // attacks, ZNetView, audio, drops, riding, colliders or rigidbodies.
        tool = new GameObject("BH_Kvastur_review_visual");
        tool.transform.SetParent(transform, false); tool.transform.localPosition = Vector3.zero;
        foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
        {
            Mesh? mesh = null;
            // Use the native bind-pose mesh. Baking an uninitialized prefab's
            // animator can apply its imported bone scale a second time.
            if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            if (!mesh) continue;
            var part = new GameObject(renderer.name); part.transform.SetParent(tool.transform, false);
            part.transform.localPosition = Vector3.Scale(source.transform.InverseTransformPoint(renderer.transform.position), source.transform.lossyScale);
            part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * renderer.transform.rotation;
            // Retain the prefab root's imported unit conversion as well as the
            // renderer scale so prefabs authored in centimetres keep native units.
            part.transform.localScale = renderer.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = part.AddComponent<MeshRenderer>(); copy.sharedMaterials = renderer.sharedMaterials; copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var filters = tool.GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0) { Destroy(tool); tool = null; failedTool = true; return false; }
        var first = true; var bounds = new Bounds();
        foreach (var filter in filters)
        {
            var b = filter.sharedMesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                p = tool.transform.InverseTransformPoint(filter.transform.TransformPoint(p));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        }
        axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? Vector3.right : bounds.size.y >= bounds.size.z ? Vector3.up : Vector3.forward;
        length = Vector3.Dot(bounds.size, axis); tip = bounds.center - axis * length * .5f;
        // Bone lengths are pose-independent, unlike the current head height.
        // Include this maid's star-tier scale without fitting to a bent knee.
        visualScale = Mathf.Clamp(FittedLength(GetComponentInChildren<Animator>()) / length, .35f, 1.1f);
        modelVertices = filters.Where(f => f.sharedMesh.isReadable).SelectMany(f => f.sharedMesh.vertices
            .Select(p => tool.transform.InverseTransformPoint(f.transform.TransformPoint(p)))).ToArray();
        tool.SetActive(false);
        return length > .1f;
    }

    private void Stop()
    {
        if (tool) tool!.SetActive(false);
        if (view && view.IsValid() && view.IsOwner() && view.GetZDO().GetLong(StartKey) != 0) view.GetZDO().Set(StartKey, 0L);
    }
    private void LateUpdate()
    {
        if (!view || !view.IsValid() || left == null || !ZNet.instance) return;
        if (!HoldIdle || !CanPreview) { Stop(); return; }
        var data = view.GetZDO(); var obj = ZNetScene.instance.FindInstance(data.GetZDOID(RugKey));
        if (!obj || !BuildTool()) { Stop(); return; }
        var rug = obj.GetComponent<Piece>();
        var colliders = obj.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
        if (colliders.Length == 0) { Stop(); return; }
        var bounds = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
        if (Vector3.Distance(transform.position, bounds.ClosestPoint(transform.position)) > 1.5f) { Stop(); return; }
        var elapsed = (float)((ZNet.instance.GetTime().Ticks - data.GetLong(StartKey)) / (double)TimeSpan.TicksPerSecond);
        var variant = data.GetInt("BH_sweepDraftVariant");
        var phase = Mathf.Repeat(Mathf.Clamp01((elapsed - .45f) / 4.1f) * (variant == 0 ? 2f : 3f), 1f);
        // Draw slowly along the rug, lift slightly, then reset more quickly.
        // The recovery is distinct from a working pass, rather than an ellipse.
        var working = phase < .65f;
        var stroke = working ? Mathf.SmoothStep(0, 1, phase / .65f) : 1 - Mathf.SmoothStep(0, 1, (phase - .65f) / .35f);
        var x = variant == 0 ? -.12f + .08f * stroke : variant == 1 ? -.15f + .30f * stroke : -.20f + .40f * stroke;
        var z = variant == 0 ? .10f - .20f * stroke : variant == 1 ? .08f - .15f * stroke : .10f - .18f * stroke;
        var anchor = bounds.ClosestPoint(transform.position + transform.forward * .32f);
        var head = bounds.ClosestPoint(anchor + transform.right * x + transform.forward * z);
        head.y = bounds.max.y + .012f + (working ? 0f : .045f * Mathf.Sin((phase - .65f) / .35f * Mathf.PI));
        var shaft = (Vector3.up + transform.forward * (.025f - .05f * stroke) + transform.right * (x * .05f)).normalized;
        // Conservative whole-handle obstruction check. Real Kvastur bristle/grip
        // alignment is deliberately exposed for review rather than certified.
        if (Physics.CapsuleCastAll(head + Vector3.up * .12f, head + shaft * (length * visualScale), .035f, transform.forward, .01f,
            RepairGeometry.Solids, QueryTriggerInteraction.Ignore).Any(h => h.collider.GetComponentInParent<Piece>() != rug)) { Stop(); return; }
        var flip = data.GetBool("BH_sweepDraftFlip") ? -1 : 1;
        tool!.SetActive(true); tool.transform.rotation = Quaternion.FromToRotation(axis * flip, shaft);
        var actualTip = flip == 1 ? tip : tip + axis * length;
        // Counter parent scale, then apply the stature-fitted broom scale.
        var parentScale = transform.lossyScale;
        tool.transform.localScale = new Vector3(visualScale / parentScale.x, visualScale / parentScale.y, visualScale / parentScale.z);
        tool.transform.position = head - tool.transform.rotation * (actualTip * visualScale);
        // The hooked handle is offset from the overall bounding-box axis.
        // Fit hands to its measured cross sections, not the box centreline.
        if (modelVertices.Length > 0)
        {
            var bottom = modelVertices.Min(p => tool.transform.TransformPoint(p).y);
            tool.transform.position += Vector3.up * (head.y - bottom);
        }
        var blend = Mathf.SmoothStep(0, 1, Mathf.Min(elapsed / .4f, (5 - elapsed) / .4f));
        spineRest = spine.localRotation; gazeRest = gaze.localRotation; bodyPosed = true;
        spine.rotation = Quaternion.AngleAxis(12f * blend, transform.right) * spine.rotation;
        gaze.rotation = Quaternion.AngleAxis(12f * blend, transform.right) * gaze.rotation;
        var shoulderHeight = left.Upper.position.y - head.y;
        LeftGripError = left.GripShaft(transform, tool.transform.TransformPoint(ShaftPoint(Mathf.Min(length * .84f, (shoulderHeight + .02f) / visualScale), flip)), shaft, blend);
        RightGripError = right.GripShaft(transform, tool.transform.TransformPoint(ShaftPoint(Mathf.Min(length * .75f, (shoulderHeight - .13f) / visualScale), flip)), shaft, blend);
    }

    private Vector3 ShaftPoint(float distance, int flip)
    {
        var start = flip == 1 ? tip : tip + axis * length;
        var samples = modelVertices.Where(p => Mathf.Abs(Vector3.Dot(p - start, axis * flip) - distance) < .045f).ToArray();
        if (samples.Length == 0) return start + axis * (flip * distance);
        var center = Vector3.zero; foreach (var p in samples) center += p;
        return center / samples.Length;
    }

    internal static float FittedLength(Animator animator)
    {
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var bone = animator.GetBoneTransform(HumanBodyBones.Head);
        var size = .12f * animator.transform.lossyScale.y;
        while (bone && bone != hips && bone.parent) { size += Vector3.Distance(bone.position, bone.parent.position); bone = bone.parent; }
        var thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var shin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        return .8f * (size + Vector3.Distance(hips.position, thigh.position) + Vector3.Distance(thigh.position, shin.position) + Vector3.Distance(shin.position, foot.position));
    }
}

internal sealed class RugSweepDraftCommand : ConsoleCommand
{
    public override string Name => "bonehilda_sweep";
    public override string Help => "Review a Kvastur rug sweep: long|gather|fan [flip]. Stand a free maid within 1.5 m of a rug. Preview only; no automatic sweeping.";
    public override void Run(string[] args)
    {
        if (!Player.m_localPlayer) return;
        var name = args.Length == 0 ? "long" : args[0].ToLowerInvariant();
        var variant = name == "long" ? 0 : name == "gather" ? 1 : name == "fan" ? 2 : -1;
        if (variant < 0) { Console.instance.Print(Help); return; }
        var maid = Maid.Loaded.Where(m => m && m.View.IsValid() && m.View.IsOwner() && m.AvailableForSocial
            && (m.transform.position - Player.m_localPlayer.transform.position).sqrMagnitude < 64f)
            .OrderBy(m => (m.transform.position - Player.m_localPlayer.transform.position).sqrMagnitude).FirstOrDefault();
        if (!maid) { Console.instance.Print("No nearby free owned maid."); return; }
        var rug = UnityEngine.Object.FindObjectsOfType<Piece>().Where(p => p && p.name.IndexOf("rug", StringComparison.OrdinalIgnoreCase) >= 0
            && p.IsPlacedByPlayer() && p.GetComponent<ZNetView>() && p.GetComponent<ZNetView>().IsValid()
            && Maid.Allowed(p.transform.position, p.GetCreator()) && (p.transform.position - maid.transform.position).sqrMagnitude < 9f)
            .OrderBy(p => (p.transform.position - maid.transform.position).sqrMagnitude).FirstOrDefault();
        if (!rug || !maid.GetComponent<RugSweepDraft>().Begin(rug, variant, args.Any(a => a.Equals("flip", StringComparison.OrdinalIgnoreCase))))
        { Console.instance.Print("Preview unavailable: move a free maid close to a player-built rug, with Kvastur visuals loaded."); return; }
        Console.instance.Print("Kvastur sweep draft: " + name + ". Review bristle end, grip, clipping, and reach; try flip if the model faces the wrong way.");
    }
}
