using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal sealed class DusterGrip
{
    internal Transform Socket { get; }
    private readonly Transform palm;
    private readonly Vector3 backOfHand;
    private readonly List<FingerJoint> fingers = new List<FingerJoint>(12);
    private readonly Transform thumb;
    private Quaternion thumbRest;

    private sealed class FingerJoint
    {
        internal readonly Transform Bone, Tip;
        internal readonly float Flex;
        internal Quaternion Rest;

        internal FingerJoint(Transform bone, Transform tip, float flex)
        {
            Bone = bone;
            Tip = tip;
            Flex = flex;
        }
    }

    internal DusterGrip(Animator animator, Transform equipmentSocket)
    {
        palm = equipmentSocket;
        var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
        var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        backOfHand = Vector3.up;
        if (index && little)
        {
            var knuckles = (index.position + little.position) * .5f;
            var dorsal = Vector3.Cross(index.position - little.position, knuckles - wrist);
            backOfHand = palm.InverseTransformDirection(dorsal.normalized);
        }
        Socket = Visuals.Child(palm, "BH_DusterGrip");
        // The ferrule leads past the index finger. The tail crosses the heel of the palm.
        Socket.localPosition = new Vector3(.018f, .003f, .017f);
        Socket.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(.64f, .92f, 1f));
        thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
        AddFinger(animator, HumanBodyBones.RightIndexProximal, -10f, -100f, -150f);
        AddFinger(animator, HumanBodyBones.RightMiddleProximal, -46f, -130f, -147f);
        AddFinger(animator, HumanBodyBones.RightRingProximal, -43f, -127f, -141f);
        AddFinger(animator, HumanBodyBones.RightLittleProximal, -40f, -71f, -127f);
    }

    private void AddFinger(Animator animator, HumanBodyBones finger, params float[] flex)
    {
        var bone = animator.GetBoneTransform(finger);
        foreach (var angle in flex)
        {
            if (!bone || bone.childCount == 0) break;
            var next = bone.GetChild(0);
            fingers.Add(new FingerJoint(bone, next, angle));
            bone = next;
        }
    }

    internal void Pose(Transform elbow, Transform hand, float followThrough, float blend)
    {
        // Keep the wrist straight before solving the elbow and tool as one link.
        var forearm = (hand.position - elbow.position).normalized;
        var straighten = Quaternion.FromToRotation(palm.right, forearm);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, straighten, blend) * hand.rotation;
        var follow = palm.rotation * Quaternion.Euler(0f, followThrough * 9f, 5f) * Quaternion.Inverse(palm.rotation);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, follow, blend) * hand.rotation;
        // Flex in the knuckle plane only. Preserve the avatar's finger spacing and twist.
        foreach (var joint in fingers)
        {
            joint.Rest = joint.Bone.localRotation;
            var segment = Vector3.ProjectOnPlane(joint.Tip.position - joint.Bone.position, palm.forward);
            var angle = Vector3.SignedAngle(palm.right, segment, palm.forward);
            var bend = Mathf.DeltaAngle(angle, joint.Flex);
            joint.Bone.rotation = Quaternion.AngleAxis(bend * blend, palm.forward) * joint.Bone.rotation;
        }
        if (thumb)
        {
            thumbRest = thumb.localRotation;
            thumb.rotation = Quaternion.AngleAxis(19f * blend, palm.up) * thumb.rotation;
        }
    }

    internal void Pronate(Transform elbow, Vector3 brushTip, float blend)
    {
        // Roll the posed forearm around the reach line so the feathers stay on target.
        var reach = (brushTip - elbow.position).normalized;
        var dorsal = Vector3.ProjectOnPlane(palm.TransformDirection(backOfHand), reach);
        var up = Vector3.ProjectOnPlane(Vector3.up, reach);
        if (up.sqrMagnitude < .001f || dorsal.sqrMagnitude < .001f) return;
        var roll = Vector3.SignedAngle(dorsal, up, reach);
        elbow.rotation = Quaternion.AngleAxis(roll * blend, reach) * elbow.rotation;
    }

    internal void Restore()
    {
        foreach (var joint in fingers) joint.Bone.localRotation = joint.Rest;
        if (thumb) thumb.localRotation = thumbRest;
    }
}
