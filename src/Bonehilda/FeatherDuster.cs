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
    private Transform? spine, head, freeArm, freeElbow, freeHand, shoulder, elbow, hand, belt, tool, plume;
    private DusterGrip? grip;
    private FeatherDust? dust;
    private Quaternion spineRest, headRest, freeArmRest, freeElbowRest, shoulderRest, elbowRest, handRest;
    private bool posed;

    private void Start()
    {
        view = GetComponent<ZNetView>();
        var animator = GetComponentInChildren<Animator>();
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        freeArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        freeElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        freeHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
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
        plume = DusterPlume.Attach(tool, Visuals.Palette(Visuals.Template(
            Jotunn.Managers.PrefabManager.Instance.GetPrefab("piece_chest_wood")), 0));
        tool.SetParent(belt, true);
        tool.localPosition = Vector3.zero;
        tool.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        if (!posed || spine == null || shoulder == null || elbow == null || hand == null) return;
        spine.localRotation = spineRest;
        if (head != null) head.localRotation = headRest;
        if (freeArm != null && freeElbow != null && freeHand != null)
        {
            freeArm.localRotation = freeArmRest;
            freeElbow.localRotation = freeElbowRest;
        }
        shoulder.localRotation = shoulderRest;
        elbow.localRotation = elbowRest;
        hand.localRotation = handRest;
        grip?.Restore();
        posed = false;
    }

    private void LateUpdate()
    {
        if (plume == null || grip == null || spine == null || tool == null || belt == null || shoulder == null || elbow == null || hand == null
            || view == null || !view.IsValid() || !ZNet.instance) return;
        var data = view.GetZDO();
        var start = data.GetLong(StartKey);
        var elapsed = (float)((ZNet.instance.GetTime().Ticks - start) / (double)TimeSpan.TicksPerSecond);
        if (start == 0 || elapsed < 0 || elapsed >= Seconds)
        {
            if (tool.parent != belt)
            {
                plume.localRotation = Quaternion.identity;
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
        var blend = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed / .50f, (Seconds - elapsed) / .42f));
        var cycles = elapsed * .79f + .045f * Mathf.Sin(elapsed * 2.1f);
        var phase = cycles * 2f * Mathf.PI;
        var across = Mathf.Sin(phase);
        var recovery = Mathf.Max(0f, -Mathf.Cos(phase));
        var surface = data.GetVec3(SurfaceKey, transform.position);
        var height = Mathf.InverseLerp(.45f, 1.10f, surface.y - transform.position.y);
        var strokePoint = surface + transform.right * (Mathf.Lerp(.18f, .30f, height) * across)
            + transform.forward * (.15f + .075f * Mathf.Cos(phase));
        var tip = strokePoint + Vector3.up * (.003f + .085f * recovery);
        var lean = Mathf.Lerp(36f, 18f, height) - 5f * recovery;
        var torso = Quaternion.AngleAxis(across * 11f * blend, transform.up)
            * Quaternion.AngleAxis(lean * blend, transform.right);
        spine.rotation = torso * spine.rotation;
        if (head != null)
        {
            headRest = head.localRotation;
            var gaze = Vector3.Lerp(surface, strokePoint, .35f) - head.position;
            var nod = Quaternion.FromToRotation(torso * transform.forward, gaze);
            head.rotation = Quaternion.RotateTowards(Quaternion.identity, nod, 25f * blend) * head.rotation;
        }
        // Let the free hand rest beside the apron, away from the working hand.
        if (freeArm != null && freeElbow != null && freeHand != null)
        {
            freeArmRest = freeArm.localRotation;
            freeElbowRest = freeElbow.localRotation;
            var width = Vector3.Distance(freeArm.position, shoulder.position);
            var apron = spine.position - transform.right * (.85f * width) + transform.forward * .24f;
            Aim(freeArm, freeElbow.position, -transform.up - transform.right * .55f - transform.forward * .30f, blend);
            Aim(freeElbow, freeHand.position, apron - freeElbow.position, blend);
        }
        grip.Pose(elbow, hand, transform.up + transform.right * .25f, Mathf.Cos(phase - .20f), blend);
        var brush = tool.TransformPoint(Vector3.up * DusterPlume.Tip);
        SweepArm(shoulder, elbow, brush, tip, blend);
        // Account for feather deflection so the loaded stroke keeps skimming the surface.
        var plannedTip = tool.TransformPoint(Vector3.up * DusterPlume.Tip);
        DusterPlume.Bend(plume, tool, transform.right, Mathf.Cos(phase - .22f), 1f - recovery, blend);
        SweepArm(shoulder, elbow, DusterPlume.End(plume), plannedTip, blend);
        dust?.Sweep(start, cycles, DusterPlume.End(plume), surface, transform.right);
    }

    private static void Aim(Transform joint, Vector3 end, Vector3 direction, float blend)
    {
        var rotation = Quaternion.FromToRotation(end - joint.position, direction);
        joint.rotation = Quaternion.Slerp(Quaternion.identity, rotation, blend) * joint.rotation;
    }

    private void SweepArm(Transform shoulder, Transform elbow, Vector3 brush, Vector3 target, float blend)
    {
        // Solve to the feathers with a fixed wrist, exactly as for a longer forearm.
        var arm = elbow.position - shoulder.position;
        var forearm = brush - elbow.position;
        var direction = (target - shoulder.position).normalized;
        var distance = Mathf.Clamp(Vector3.Distance(target, shoulder.position),
            Mathf.Abs(arm.magnitude - forearm.magnitude) + .001f, arm.magnitude + forearm.magnitude - .001f);
        var along = (arm.sqrMagnitude - forearm.sqrMagnitude + distance * distance) / (2f * distance);
        var bend = Vector3.ProjectOnPlane(transform.right * .46f - transform.up - transform.forward * .40f, direction).normalized;
        var upper = direction * along + bend * Mathf.Sqrt(Mathf.Max(0, arm.sqrMagnitude - along * along));
        var swing = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(arm, upper), blend);
        shoulder.rotation = swing * shoulder.rotation;
        var end = shoulder.position + direction * distance;
        var elbowSwing = Quaternion.FromToRotation(swing * forearm, end - elbow.position);
        elbow.rotation = Quaternion.Slerp(Quaternion.identity, elbowSwing, blend) * elbow.rotation;
    }
}
