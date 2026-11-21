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
    private Transform? spine, shoulder, elbow, hand, belt, tool;
    private DusterGrip? grip;
    private FeatherDust? dust;
    private Quaternion spineRest, shoulderRest, elbowRest, handRest;
    private bool posed;

    private void Start()
    {
        view = GetComponent<ZNetView>();
        var animator = GetComponentInChildren<Animator>();
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        grip = new DusterGrip(animator, GetComponent<VisEquipment>().m_rightHand);
        dust = FeatherDust.Create(transform);
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
        if (!posed || spine == null || shoulder == null || elbow == null || hand == null) return;
        spine.localRotation = spineRest;
        shoulder.localRotation = shoulderRest;
        elbow.localRotation = elbowRest;
        hand.localRotation = handRest;
        grip?.Restore();
        posed = false;
    }

    private void LateUpdate()
    {
        if (grip == null || spine == null || tool == null || belt == null || shoulder == null || elbow == null || hand == null
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
        var socket = grip.Socket;
        if (tool.parent != socket)
        {
            tool.SetParent(socket, true);
            tool.localPosition = Vector3.zero;
            tool.localRotation = Quaternion.identity;
        }
        spineRest = spine.localRotation;
        shoulderRest = shoulder.localRotation;
        elbowRest = elbow.localRotation;
        handRest = hand.localRotation;
        posed = true;
        var blend = Mathf.SmoothStep(0, 1, Mathf.Min(elapsed, Seconds - elapsed) / .32f);
        var cycles = elapsed * 1.15f;
        var stroke = cycles * 2f * Mathf.PI;
        var surface = data.GetVec3(SurfaceKey, transform.position);
        var tip = surface + transform.right * (.20f * Mathf.Sin(stroke))
            + Vector3.up * (.035f + .045f * Mathf.Abs(Mathf.Cos(stroke)));
        var surfaceHeight = surface.y - transform.position.y;
        var lean = Mathf.Lerp(26f, 9f, Mathf.InverseLerp(.45f, 1.2f, surfaceHeight));
        spine.rotation = Quaternion.AngleAxis(lean * blend, transform.right) * spine.rotation;
        var axis = (tip - shoulder.position).normalized;
        var palmRotation = Quaternion.FromToRotation(socket.up, axis);
        var wrist = tip - axis * .49f - palmRotation * (socket.position - hand.position);
        Reach(shoulder, elbow, hand, Vector3.Lerp(hand.position, wrist, blend));
        // The palm is offset from the wrist. Aim the full wrist-to-feathers lever
        // so rotating the hand also puts the plume on the furniture.
        var brush = tool.TransformPoint(Vector3.up * .51f);
        var aim = Quaternion.FromToRotation(brush - hand.position, tip - hand.position);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, aim, blend) * hand.rotation;
        grip.Close(blend);
        dust?.Sweep(start, cycles, tool.TransformPoint(Vector3.up * .51f), surface, transform.right);
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
