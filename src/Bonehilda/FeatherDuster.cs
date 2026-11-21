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
    // One reach and pullback. The wrist starts turning before the elbow finishes
    // the reach. A separate angle track lets the handle take a different path home.
    private static readonly AnimationCurve Travel = new AnimationCurve(
        new Keyframe(0f, -1f, 0f, 0f), new Keyframe(.24f, .02f, 6f, 6f),
        new Keyframe(.48f, 1f, 0f, 0f), new Keyframe(.76f, -.04f, -5.4f, -5.4f),
        new Keyframe(1f, -1f, 0f, 0f));
    private static readonly AnimationCurve HandleYaw = new AnimationCurve(
        new Keyframe(0f, -55f, 45f, 45f), new Keyframe(.20f, -48f, 32f, 32f),
        new Keyframe(.39f, -41.5f, 0f, 0f), new Keyframe(.54f, -48.5f, -65f, -65f),
        new Keyframe(.83f, -62f, 0f, 0f), new Keyframe(1f, -55f, 45f, 45f));
    private static readonly AnimationCurve HandlePitch = new AnimationCurve(
        new Keyframe(0f, 18f, -32f, -32f), new Keyframe(.38f, 7f, 0f, 0f),
        new Keyframe(.62f, 16f, 64f, 64f), new Keyframe(.85f, 27f, 0f, 0f),
        new Keyframe(1f, 18f, -32f, -32f));
    private ZNetView? view;
    private Transform? spine, head, freeArm, freeElbow, freeHand, shoulder, elbow, hand, belt, tool, plume;
    private DusterGrip? grip;
    private DustingStance? stance;
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
        stance = new DustingStance(animator);
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
        stance?.Restore();
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
        if (stance == null || plume == null || grip == null || spine == null || tool == null || belt == null || shoulder == null || elbow == null || hand == null
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
        var cycles = elapsed / 1.55f;
        var phase = Mathf.Repeat(cycles, 1f);
        var across = Travel.Evaluate(phase);
        var velocity = (Travel.Evaluate(Mathf.Repeat(phase + .01f, 1f))
            - Travel.Evaluate(Mathf.Repeat(phase - .01f, 1f))) / .12f;
        var reach = (across + 1f) * .5f;
        var surface = data.GetVec3(SurfaceKey, transform.position);
        var height = Mathf.InverseLerp(.45f, 1.10f, surface.y - transform.position.y);
        var strokePoint = surface + transform.right * (Mathf.Lerp(.13f, .23f, height) * across + .08f * (1f - reach))
            + transform.forward * (.11f + .075f * reach);
        var tip = strokePoint + Vector3.up * .003f;
        PoseBody(spine, shoulder, elbow, hand, stance, surface, strokePoint, height, across, reach, blend);
        grip.CloseFingers(blend);
        var pitch = HandlePitch.Evaluate(phase) + Mathf.Lerp(11f, 5f, height);
        var yaw = HandleYaw.Evaluate(phase) - 4.5f * height;
        var brushDirection = transform.rotation * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        var palmRoll = Mathf.Lerp(-30f, -7f, height) + 6.5f * across;
        var turn = Quaternion.AngleAxis(palmRoll, brushDirection) * Quaternion.LookRotation(brushDirection, Vector3.up)
            * Quaternion.Inverse(Quaternion.LookRotation(tool.up, grip.Dorsal));
        var desiredHand = turn * hand.rotation;
        var nativeHand = hand.localRotation;
        hand.rotation = desiredHand;
        var sweep = (transform.right + transform.forward * .40f).normalized;
        DusterPlume.Bend(plume, tool, sweep, velocity, Mathf.Abs(velocity), blend);
        var wristTarget = tip - (DusterPlume.End(plume) - hand.position);
        var dorsal = grip.Dorsal;
        hand.localRotation = nativeHand;
        var elbowPole = transform.right * .30f - transform.up * .68f - transform.forward;
        DustingStance.Reach(shoulder, elbow, hand.position, wristTarget, elbowPole, blend);
        // Let the forearm carry the axial roll, then finish the steering at the wrist.
        var forearm = (hand.position - elbow.position).normalized;
        var roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(grip.Dorsal, forearm),
            Vector3.ProjectOnPlane(dorsal, forearm), forearm);
        elbow.rotation = Quaternion.AngleAxis(roll * blend, forearm) * elbow.rotation;
        hand.rotation = Quaternion.Slerp(hand.rotation, desiredHand, blend);
        dust?.Sweep(start, cycles + .25f, DusterPlume.End(plume), surface, sweep);
    }

    private void PoseBody(Transform spine, Transform shoulder, Transform elbow, Transform hand, DustingStance stance,
        Vector3 surface, Vector3 strokePoint, float height, float across, float reach, float blend)
    {
        var lean = Mathf.Lerp(41f, 18f, height) + 7f * reach;
        var torso = Quaternion.AngleAxis(across * 11f * blend, transform.up)
            * Quaternion.AngleAxis(lean * blend, transform.right);
        spine.rotation = torso * spine.rotation;
        var armSpan = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
        var drop = Mathf.Clamp((shoulder.position.y - surface.y - .91f * armSpan) * .57f, .025f, .32f * armSpan);
        var advance = Mathf.Lerp(.18f, .12f, height) * (.55f + .45f * reach);
        stance.Shift((transform.forward * advance - Vector3.up * drop) * blend, transform.forward, transform.right);
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
    }

    private static void Aim(Transform joint, Vector3 end, Vector3 direction, float blend)
    {
        var rotation = Quaternion.FromToRotation(end - joint.position, direction);
        joint.rotation = Quaternion.Slerp(Quaternion.identity, rotation, blend) * joint.rotation;
    }
}
