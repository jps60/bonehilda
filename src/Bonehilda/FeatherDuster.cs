using System;
using UnityEngine;

namespace Bonehilda;

// Pose the right arm after the native walk/idle animation. Every viewer derives
// the same sweep from its network start time and the point on the furniture.
internal sealed class FeatherDuster : MonoBehaviour
{
    internal const string StartKey = "BH_dustStart";
    internal const string SurfaceKey = "BH_dustSurface";
    internal const string SideKey = "BH_dustSide";
    internal const string FarKey = "BH_dustFar";
    private const float Settle = .42f;
    private const float Cycle = 1.45f;
    internal const float Seconds = Settle + 2f * Cycle + .40f;
    // A deliberate reach, a short turn, then a quicker gathering stroke.
    private static readonly AnimationCurve Travel = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f), new Keyframe(.26f, .49f, 2.6f, 2.6f),
        new Keyframe(.51f, 1f, 0f, 0f), new Keyframe(.57f, .99f, -.45f, -.45f),
        new Keyframe(.78f, .42f, -3.3f, -3.3f), new Keyframe(.97f, 0f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f));
    private ZNetView? view;
    private Transform? spine, head, freeArm, freeElbow, freeHand, shoulder, elbow, hand, belt, tool, plume;
    private Transform? freeThigh, freeIndex, freeLittle;
    private DusterGrip? grip;
    private DustingStance? stance;
    private FeatherDust? dust;
    private Quaternion spineRest, headRest, freeArmRest, freeElbowRest, freeHandRest, shoulderRest, elbowRest, handRest;
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
        freeThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        freeIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
        freeLittle = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
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
            freeHand.localRotation = freeHandRest;
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
                plume.localScale = Vector3.one;
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
        var cycles = Mathf.Clamp((elapsed - Settle) / Cycle, 0f, 2f);
        var phase = Mathf.Repeat(cycles, 1f);
        var travel = Travel.Evaluate(phase);
        var surface = data.GetVec3(SurfaceKey, transform.position);
        var side = data.GetVec3(SideKey, surface + transform.right * .40f);
        var far = data.GetVec3(FarKey, surface + transform.forward * .25f);
        var stroke = (cycles < 1f ? side : far) - surface;
        var strokePoint = surface + travel * stroke;
        var height = Mathf.InverseLerp(.45f, 1.10f, surface.y - transform.position.y);
        var across = Mathf.Clamp(Vector3.Dot(strokePoint - (surface + side) * .5f, transform.right) * 3f, -1f, 1f);
        var reach = cycles < 1f ? .55f - .20f * across : .55f - .20f * across + travel * .30f;
        var restingBrush = DusterPlume.Contact(tool, plume);
        PoseBody(spine, shoulder, elbow, hand, stance, surface, strokePoint, height, across, reach, blend);
        grip.CloseFingers();

        // Solve to the working feathers. The forearm, seated handle and wrist form
        // the second link, so a change in reach opens the elbow without folding the wrist.
        elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, transform.forward) * elbow.rotation;
        var delayedPhase = Mathf.Repeat(phase - .055f, 1f);
        var drag = (Travel.Evaluate(Mathf.Repeat(delayedPhase + .01f, 1f))
            - Travel.Evaluate(Mathf.Repeat(delayedPhase - .01f, 1f))) / .065f;
        var direction = transform.rotation * Quaternion.Euler(9f + 3f * drag, -29f + 3f * across, 0f) * Vector3.forward;
        hand.rotation = Quaternion.LookRotation(direction, Vector3.up)
            * Quaternion.Inverse(Quaternion.LookRotation(tool.up, grip.Dorsal)) * hand.rotation;
        var restTip = tool.TransformPoint(Vector3.up * DusterPlume.Tip);
        var contact = strokePoint + Vector3.up * .012f;
        var pole = transform.right * .92f - transform.up * .36f - transform.forward * .46f;
        DustingStance.Reach(shoulder, elbow, restTip, contact, pole, 1f);
        // Roll the solved chain around the contact line to keep the back of the hand facing up.
        var axis = (tool.TransformPoint(Vector3.up * DusterPlume.Tip) - elbow.position).normalized;
        var roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(grip.Dorsal, axis),
            Vector3.ProjectOnPlane(Vector3.up, axis), axis);
        elbow.rotation = Quaternion.AngleAxis(roll, axis) * elbow.rotation;

        DusterPlume.Bend(plume, tool, stroke.normalized, drag, Mathf.Abs(drag), blend);
        // Follow the middle of the fan horizontally. The lowest vane supplies
        // the height only, so switching vanes cannot yank the wrist sideways.
        var center = Vector3.Lerp(tool.TransformPoint(Vector3.up * .45f),
            plume.TransformPoint(Vector3.up * .23f), .84f);
        var bottom = DusterPlume.Contact(tool, plume);
        var underside = Vector3.ProjectOnPlane(Vector3.down, plume.up).normalized * (.075f * tool.lossyScale.x);
        var goal = strokePoint - Vector3.ProjectOnPlane(underside, Vector3.up)
            + Vector3.up * (center.y - bottom.y - .002f);
        DustingStance.Reach(shoulder, elbow, center, goal, pole, 1f);

        shoulder.localRotation = Quaternion.Slerp(shoulderRest, shoulder.localRotation, blend);
        elbow.localRotation = Quaternion.Slerp(elbowRest, elbow.localRotation, blend);
        hand.localRotation = Quaternion.Slerp(handRest, hand.localRotation, blend);
        if (blend < 1f)
        {
            // Raise on the maid's side of the rim, cross above it, then settle.
            // The exit traverses the same three-part path in reverse.
            var clearance = Mathf.Max(restingBrush.y, strokePoint.y) + .14f;
            var waypoint = Vector3.Lerp(restingBrush, strokePoint, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, .80f, blend)));
            waypoint.y = blend < .5f
                ? Mathf.Lerp(restingBrush.y, clearance, Mathf.SmoothStep(0f, 1f, blend / .5f))
                : Mathf.Lerp(clearance, strokePoint.y, Mathf.SmoothStep(0f, 1f, (blend - .5f) / .5f));
            var low = DusterPlume.Contact(tool, plume);
            var bend = Vector3.ProjectOnPlane(elbow.position - shoulder.position, low - shoulder.position);
            DustingStance.Reach(shoulder, elbow, low, waypoint, bend, 1f);
        }
        dust?.Sweep(start, cycles + .25f, DusterPlume.Contact(tool, plume), strokePoint, stroke.normalized);
    }

    private void PoseBody(Transform spine, Transform shoulder, Transform elbow, Transform hand, DustingStance stance,
        Vector3 surface, Vector3 strokePoint, float height, float across, float reach, float blend)
    {
        var lean = Mathf.Lerp(31f, 13f, height) + 6f * reach;
        var torso = Quaternion.AngleAxis(-across * 4f * blend, transform.up)
            * Quaternion.AngleAxis(lean * blend, transform.right);
        spine.rotation = torso * spine.rotation;
        var armSpan = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
        var drop = Mathf.Clamp((shoulder.position.y - surface.y - .91f * armSpan) * .57f, .025f, .32f * armSpan);
        var advance = Mathf.Lerp(.18f, .12f, height) * (.38f + .48f * reach);
        stance.Shift((transform.forward * advance - Vector3.up * drop) * blend, transform.forward, transform.right);
        if (head != null)
        {
            headRest = head.localRotation;
            var gaze = Vector3.Lerp(surface, strokePoint, .35f) - head.position;
            var nod = Quaternion.FromToRotation(torso * transform.forward, gaze);
            head.rotation = Quaternion.RotateTowards(Quaternion.identity, nod, 25f * blend) * head.rotation;
        }
        if (freeArm != null && freeElbow != null && freeHand != null)
        {
            freeArmRest = freeArm.localRotation;
            freeElbowRest = freeElbow.localRotation;
            freeHandRest = freeHand.localRotation;
            if (freeThigh != null && freeIndex != null && freeLittle != null)
            {
                // Rest the spare hand against the apron at the outside of the thigh.
                var width = Vector3.Distance(freeArm.position, shoulder.position);
                var rest = freeThigh.position + width * (-transform.right * .48f
                    + transform.forward * .38f - transform.up * .15f);
                DustingStance.Reach(freeArm, freeElbow, freeHand.position, rest,
                    -transform.right - transform.forward * .45f, blend);
                var knuckles = (freeIndex.position + freeLittle.position) * .5f - freeHand.position;
                var dorsal = Vector3.Cross(knuckles, freeIndex.position - freeLittle.position);
                var restRotation = Quaternion.LookRotation(-transform.up + transform.forward * .20f, -transform.right)
                    * Quaternion.Inverse(Quaternion.LookRotation(knuckles, dorsal)) * freeHand.rotation;
                freeHand.rotation = Quaternion.Slerp(freeHand.rotation, restRotation, blend);
            }
        }
    }

}
