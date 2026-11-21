using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal sealed class DusterGrip
{
    internal Transform Socket { get; }
    private readonly Transform palm;
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
        Socket = Visuals.Child(palm, "BH_DusterGrip");
        // The ferrule leads past the index finger. The tail crosses the heel of the palm.
        Socket.localPosition = new Vector3(.034f, -.024f, 0f);
        Socket.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(.74f, .14f, .66f));
        thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
        AddFinger(animator, HumanBodyBones.RightIndexProximal, -17f, -66f, -112f);
        AddFinger(animator, HumanBodyBones.RightMiddleProximal, -32f, -84f, -132f);
        AddFinger(animator, HumanBodyBones.RightRingProximal, -39f, -92f, -142f);
        AddFinger(animator, HumanBodyBones.RightLittleProximal, -42f, -95f, -139f);
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

    internal void Pose(Transform elbow, Transform hand, Vector3 up, float followThrough, float blend)
    {
        // Keep the wrist straight before solving the elbow and tool as one link.
        var forearm = (hand.position - elbow.position).normalized;
        var straighten = Quaternion.FromToRotation(palm.right, forearm);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, straighten, blend) * hand.rotation;
        // Turn the palm over by rolling the forearm. Keep axial twist out of the wrist.
        var dorsal = Vector3.ProjectOnPlane(up, forearm);
        if (dorsal.sqrMagnitude > .001f)
        {
            var roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(palm.up, forearm), dorsal, forearm);
            elbow.rotation = Quaternion.AngleAxis(Mathf.Clamp(roll, -65f, 65f) * blend, forearm) * elbow.rotation;
        }
        var follow = palm.rotation * Quaternion.Euler(0f, followThrough * 9f, 5f) * Quaternion.Inverse(palm.rotation);
        hand.rotation = Quaternion.Slerp(Quaternion.identity, follow, blend) * hand.rotation;
        // Flex in the knuckle plane only. Preserve the avatar's finger spacing and twist.
        foreach (var joint in fingers)
        {
            joint.Rest = joint.Bone.localRotation;
            var segment = Vector3.ProjectOnPlane(joint.Tip.position - joint.Bone.position, palm.forward);
            var angle = Vector3.SignedAngle(palm.right, segment, palm.forward);
            var bend = Mathf.Clamp(Mathf.DeltaAngle(angle, joint.Flex), -60f, 60f);
            joint.Bone.rotation = Quaternion.AngleAxis(bend * blend, palm.forward) * joint.Bone.rotation;
        }
        if (thumb)
        {
            thumbRest = thumb.localRotation;
            thumb.rotation = Quaternion.AngleAxis(13f * blend, palm.up) * thumb.rotation;
        }
    }

    internal void Restore()
    {
        foreach (var joint in fingers) joint.Bone.localRotation = joint.Rest;
        if (thumb) thumb.localRotation = thumbRest;
    }
}
