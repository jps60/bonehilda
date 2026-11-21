using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal sealed class DusterGrip
{
    internal Transform Socket { get; }
    private readonly List<FingerJoint> joints = new List<FingerJoint>(15);

    private sealed class FingerJoint
    {
        internal readonly Transform Bone, Tip;
        internal readonly float Turn;
        internal Quaternion Rest;

        internal FingerJoint(Transform bone, Transform tip, float turn)
        {
            Bone = bone;
            Tip = tip;
            Turn = turn;
        }
    }

    internal DusterGrip(Animator animator, Transform equipmentSocket)
    {
        Socket = Visuals.Child(equipmentSocket, "BH_DusterGrip");
        // The skeleton's knuckles form a diagonal across the equipment socket.
        // Seat the wood on the palm side, with the plume leaving by the thumb.
        Socket.localPosition = new Vector3(.022f, -.015f, 0);
        Socket.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(.38f, .32f, 1f));
        foreach (var root in new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightThumbProximal })
        {
            var bone = animator.GetBoneTransform(root);
            for (var i = 0; i < 3 && bone && bone.childCount > 0; i++)
            {
                var next = bone.GetChild(0);
                joints.Add(new FingerJoint(bone, next, root == HumanBodyBones.RightThumbProximal ? 1f : -1f));
                bone = next;
            }
        }
    }

    internal void Close(float blend)
    {
        // Aim each phalanx tangent to the handle so the fingers wrap around the wood.
        foreach (var joint in joints)
        {
            joint.Rest = joint.Bone.localRotation;
            var radial = Vector3.ProjectOnPlane(joint.Bone.position - Socket.position, Socket.up);
            var reach = radial.magnitude;
            if (reach < .001f) continue;
            var outward = radial / reach;
            var tangent = Vector3.Cross(Socket.up, outward) * joint.Turn;
            var ratio = Mathf.Min(1f, .021f / reach);
            var direction = tangent * ratio - outward * Mathf.Sqrt(1f - ratio * ratio);
            var curl = Quaternion.FromToRotation(joint.Tip.position - joint.Bone.position, direction);
            joint.Bone.rotation = Quaternion.Slerp(Quaternion.identity, curl, blend) * joint.Bone.rotation;
        }
    }

    internal void Restore()
    {
        foreach (var joint in joints) joint.Bone.localRotation = joint.Rest;
    }
}
