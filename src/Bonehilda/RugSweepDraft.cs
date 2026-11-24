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
    private GameObject? tool;
    private Vector3 axis, tip;
    private float length;
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
    private void Update() { left?.Restore(); right?.Restore(); }
    private void OnDisable() { left?.Restore(); right?.Restore(); if (tool) tool!.SetActive(false); }
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
        var phase = elapsed / 5f * Mathf.PI * 6;
        var variant = data.GetInt("BH_sweepDraftVariant");
        var x = Mathf.Sin(phase) * (variant == 0 ? .26f : variant == 1 ? .48f : .46f);
        var z = variant == 0 ? .38f * Mathf.Cos(phase) : variant == 1 ? .16f * Mathf.Cos(phase * 2) : .34f * Mathf.Sin(phase + .6f);
        var head = bounds.center + transform.right * (x * Mathf.Min(1, bounds.size.x / 1.4f))
            + transform.forward * (z * Mathf.Min(1, bounds.size.z)); head.y = bounds.max.y + .025f;
        var shaft = (Vector3.up - transform.forward * .25f).normalized;
        // Conservative whole-handle obstruction check. Real Kvastur bristle/grip
        // alignment is deliberately exposed for review rather than certified.
        if (Physics.CapsuleCastAll(head + Vector3.up * .12f, head + shaft * length, .035f, transform.forward, .01f,
            RepairGeometry.Solids, QueryTriggerInteraction.Ignore).Any(h => h.collider.GetComponentInParent<Piece>() != rug)) { Stop(); return; }
        var flip = data.GetBool("BH_sweepDraftFlip") ? -1 : 1;
        tool!.SetActive(true); tool.transform.rotation = Quaternion.FromToRotation(axis * flip, shaft);
        var actualTip = flip == 1 ? tip : tip + axis * length;
        // Counter parent scale: preserve the native Kvastur model's physical dimensions.
        var parentScale = transform.lossyScale;
        tool.transform.localScale = new Vector3(1 / parentScale.x, 1 / parentScale.y, 1 / parentScale.z);
        tool.transform.position = head - tool.transform.rotation * actualTip;
        var blend = Mathf.SmoothStep(0, 1, Mathf.Min(elapsed / .4f, (5 - elapsed) / .4f));
        var bodyScale = Mathf.Max(.5f, (left.Upper.position.y - transform.position.y) / 1.4f);
        left.ReachTarget(transform, head + shaft * Mathf.Min(length * .78f, 1.25f * bodyScale), blend);
        right.ReachTarget(transform, head + shaft * Mathf.Min(length * .53f, .95f * bodyScale), blend);
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
