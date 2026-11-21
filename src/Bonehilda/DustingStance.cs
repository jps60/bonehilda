using UnityEngine;

namespace Bonehilda;

// Shift weight toward the work without dragging the feet out of the native idle pose.
internal sealed class DustingStance
{
    private readonly Transform hips;
    private readonly Leg left, right;
    private Vector3 hipsRest;

    internal DustingStance(Animator animator)
    {
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        left = new Leg(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        right = new Leg(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
    }

    internal void Shift(Vector3 offset, Vector3 forward, Vector3 sideways)
    {
        hipsRest = hips.localPosition;
        left.Capture();
        right.Capture();
        hips.position += offset;
        left.Plant(forward - sideways * .12f);
        right.Plant(forward + sideways * .12f);
    }

    internal void Restore()
    {
        hips.localPosition = hipsRest;
        left.Restore();
        right.Restore();
    }

    private sealed class Leg
    {
        private readonly Transform thigh, shin, foot;
        private Quaternion thighRest, shinRest, footRest, plantedRotation;
        private Vector3 plantedPosition;

        internal Leg(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones ankle)
        {
            thigh = animator.GetBoneTransform(upper);
            shin = animator.GetBoneTransform(lower);
            foot = animator.GetBoneTransform(ankle);
        }

        internal void Capture()
        {
            thighRest = thigh.localRotation;
            shinRest = shin.localRotation;
            footRest = foot.localRotation;
            plantedPosition = foot.position;
            plantedRotation = foot.rotation;
        }

        internal void Plant(Vector3 direction)
        {
            // Retain the native knee plane as the weight transfer blends in.
            var axis = (foot.position - thigh.position).normalized;
            var nativeBend = Vector3.ProjectOnPlane(shin.position - thigh.position, axis);
            var change = Vector3.Distance(foot.position, plantedPosition);
            DustingStance.Reach(thigh, shin, foot.position, plantedPosition, nativeBend + change * direction, 1f);
            foot.rotation = plantedRotation;
        }

        internal void Restore()
        {
            thigh.localRotation = thighRest;
            shin.localRotation = shinRest;
            foot.localRotation = footRest;
        }
    }

    internal static void Reach(Transform upper, Transform lower, Vector3 end, Vector3 target, Vector3 pole, float blend)
    {
        var first = lower.position - upper.position;
        var second = end - lower.position;
        var direction = (target - upper.position).normalized;
        var distance = Mathf.Clamp(Vector3.Distance(target, upper.position),
            Mathf.Abs(first.magnitude - second.magnitude) + .001f, first.magnitude + second.magnitude - .001f);
        var along = (first.sqrMagnitude - second.sqrMagnitude + distance * distance) / (2f * distance);
        var bend = Vector3.ProjectOnPlane(pole, direction).normalized;
        var link = direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, first.sqrMagnitude - along * along));
        var swing = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(first, link), blend);
        upper.rotation = swing * upper.rotation;
        var remaining = upper.position + direction * distance - lower.position;
        lower.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(swing * second, remaining), blend) * lower.rotation;
    }
}
