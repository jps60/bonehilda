using System.Collections.Generic;
using UnityEngine;

namespace Bonehilda;

internal sealed class DusterGrip
{
    internal Transform Socket { get; }
    private readonly Transform palm;
    private readonly Vector3 backOfHand;
    private readonly List<FingerJoint> fingers = new List<FingerJoint>(15);

    private sealed class FingerJoint
    {
        internal readonly Transform Bone, Tip;
        internal readonly Vector3 Direction;
        internal Quaternion Rest;

        internal FingerJoint(Transform bone, Transform tip, Vector3 direction)
        {
            Bone = bone;
            Tip = tip;
            Direction = direction;
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
        Socket.localPosition = new Vector3(.024f, .001f, .009f);
        Socket.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(.86f, .44f, .65f));
        // Directions in the equipment socket frame, fitted around the tapered ash shaft.
        // The thumb lies along the grip. The other distal phalanges curl back underneath.
        AddFinger(animator, HumanBodyBones.RightThumbProximal,
            new Vector3(.995f, .073f, -.069f), new Vector3(.998f, -.040f, .038f), new Vector3(.780f, -.254f, .572f));
        AddFinger(animator, HumanBodyBones.RightIndexProximal,
            new Vector3(.934f, -.277f, -.226f), new Vector3(.227f, -.971f, -.079f), new Vector3(-.948f, -.318f, .022f));
        AddFinger(animator, HumanBodyBones.RightMiddleProximal,
            new Vector3(.810f, -.530f, .252f), new Vector3(-.615f, -.777f, .131f), new Vector3(-.952f, -.087f, -.294f));
        AddFinger(animator, HumanBodyBones.RightRingProximal,
            new Vector3(.783f, -.374f, .496f), new Vector3(-.628f, -.746f, .221f), new Vector3(-.952f, -.182f, -.247f));
        AddFinger(animator, HumanBodyBones.RightLittleProximal,
            new Vector3(.982f, .129f, .140f), new Vector3(-.218f, -.948f, .230f), new Vector3(-.819f, -.540f, -.195f));
    }

    private void AddFinger(Animator animator, HumanBodyBones finger, params Vector3[] directions)
    {
        var bone = animator.GetBoneTransform(finger);
        foreach (var direction in directions)
        {
            if (!bone || bone.childCount == 0) break;
            var next = bone.GetChild(0);
            fingers.Add(new FingerJoint(bone, next, direction));
            bone = next;
        }
    }

    internal Vector3 Dorsal => palm.TransformDirection(backOfHand);

    internal void CloseFingers()
    {
        foreach (var joint in fingers)
        {
            joint.Rest = joint.Bone.localRotation;
            joint.Bone.rotation = Quaternion.FromToRotation(joint.Tip.position - joint.Bone.position,
                palm.TransformDirection(joint.Direction)) * joint.Bone.rotation;
        }
    }

    internal void Restore()
    {
        foreach (var joint in fingers) joint.Bone.localRotation = joint.Rest;
    }
}
