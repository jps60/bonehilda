using System;
using UnityEngine;

namespace Bonehilda;

// Pose the right arm after the native walk/idle animation. Every viewer derives
// the same sweep from its network start time and the point on the furniture.
internal sealed class FeatherDuster : MonoBehaviour
{
    internal const string StartKey = "BH_dustStart";
    internal const string SurfaceKey = "BH_dustSurface";
    internal const float Seconds = 3.6f;
    private ZNetView? view;
    private Transform? shoulder, elbow, hand, belt, tool;
    private Quaternion shoulderRest, elbowRest, handRest;
    private bool posed;

    private void Start()
    {
        view = GetComponent<ZNetView>();
        var animator = GetComponentInChildren<Animator>();
        shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        belt = Visuals.Child(hips, "BH_DusterLoop");
        belt.position = hips.position - transform.right * .22f + Vector3.up * .15f;
        belt.rotation = Quaternion.FromToRotation(Vector3.up, -Vector3.up - transform.right * .19f - transform.forward * .15f);
        tool = new GameObject("BH_FeatherDuster").transform;
        tool.gameObject.AddComponent<MeshFilter>().sharedMesh = ModelArt.Load("feather-duster").Mesh;
        tool.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Visuals.Palette(Visuals.Template(
            Jotunn.Managers.PrefabManager.Instance.GetPrefab("piece_chest_wood")), 0);
        tool.SetParent(belt, true);
        tool.localPosition = Vector3.zero;
        tool.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        if (!posed || shoulder == null || elbow == null || hand == null) return;
        shoulder.localRotation = shoulderRest;
        elbow.localRotation = elbowRest;
        hand.localRotation = handRest;
        posed = false;
    }

    private void LateUpdate()
    {
        if (tool == null || belt == null || shoulder == null || elbow == null || hand == null
            || view == null || !view.IsValid() || !ZNet.instance) return;
        var data = view.GetZDO();
        var start = data.GetLong(StartKey);
        var elapsed = (float)((ZNet.instance.GetTime().Ticks - start) / (double)TimeSpan.TicksPerSecond);
        if (start == 0 || elapsed < 0 || elapsed >= Seconds)
        {
            if (tool.parent != belt)
            {
                tool.SetParent(belt, true);
                tool.localPosition = Vector3.zero;
                tool.localRotation = Quaternion.identity;
            }
            return;
        }
        if (tool.parent != hand) tool.SetParent(hand, true);
        shoulderRest = shoulder.localRotation;
        elbowRest = elbow.localRotation;
        handRest = hand.localRotation;
        posed = true;
        var blend = Mathf.SmoothStep(0, 1, Mathf.Min(elapsed, Seconds - elapsed) / .32f);
        var stroke = elapsed * 2f * Mathf.PI * 1.15f;
        var tip = data.GetVec3(SurfaceKey, transform.position) + transform.right * (.20f * Mathf.Sin(stroke))
            + Vector3.up * (.035f + .045f * Mathf.Abs(Mathf.Cos(stroke)));
        var axis = (tip - (shoulder.position + transform.forward * .25f)).normalized;
        var grip = Vector3.Lerp(hand.position, tip - axis * .46f, blend);
        Reach(shoulder, elbow, hand, grip);
        hand.rotation = Quaternion.AngleAxis(12f * Mathf.Sin(stroke) * blend, transform.forward) * hand.rotation;
        tool.position = hand.position;
        tool.rotation = Quaternion.FromToRotation(Vector3.up, (tip - hand.position).normalized);
    }

    private void Reach(Transform shoulder, Transform elbow, Transform hand, Vector3 grip)
    {
        var arm = elbow.position - shoulder.position;
        var forearm = hand.position - elbow.position;
        var direction = (grip - shoulder.position).normalized;
        var distance = Mathf.Clamp(Vector3.Distance(grip, shoulder.position),
            Mathf.Abs(arm.magnitude - forearm.magnitude) + .001f, arm.magnitude + forearm.magnitude - .001f);
        var along = (arm.sqrMagnitude - forearm.sqrMagnitude + distance * distance) / (2f * distance);
        var bend = Vector3.ProjectOnPlane(transform.right - Vector3.up * .7f, direction).normalized;
        var upper = direction * along + bend * Mathf.Sqrt(Mathf.Max(0, arm.sqrMagnitude - along * along));
        shoulder.rotation = Quaternion.FromToRotation(arm, upper) * shoulder.rotation;
        var end = shoulder.position + direction * distance;
        elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, end - elbow.position) * elbow.rotation;
    }
}
