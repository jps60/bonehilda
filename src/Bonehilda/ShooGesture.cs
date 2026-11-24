using System;
using UnityEngine;

namespace Bonehilda;

// Additive arm-only draft. Never changes locomotion parameters, root motion,
// hips, legs, speed or the native player animation state machine.
internal sealed class ShooGesture : MonoBehaviour
{
    internal const string StartKey = "BH_shooPoseStart", VariantKey = "BH_shooPoseVariant";
    private Maid maid = null!;
    private ZNetView view = null!;
    private WorkAnimations work = null!;
    private Arm left = null!, right = null!;

    private void Start()
    {
        maid = GetComponent<Maid>(); view = GetComponent<ZNetView>(); work = GetComponent<WorkAnimations>();
        var animator = GetComponentInChildren<Animator>();
        left = new Arm(animator, true); right = new Arm(animator, false);
    }

    internal static bool Begin(Maid maid, int variant = -1, bool force = false)
    {
        if (!Plugin.ShooGestures.Value || !maid.View || !maid.View.IsValid() || !maid.View.IsOwner() || !maid.CanShooGesture) return false;
        var data = maid.View.GetZDO(); var now = ZNet.instance.GetTime().Ticks;
        if (!force && now - data.GetLong(StartKey) < 4 * TimeSpan.TicksPerSecond) return false;
        if (variant < 0)
        {
            var previous = data.GetInt(VariantKey, -1);
            variant = (previous + 1 + UnityEngine.Random.Range(0, previous < 0 ? 3 : 2)) % 3;
        }
        // Keep the hammer hand available while running between repairs.
        if (maid.GetComponent<WorkAnimations>().HammerDrawn) variant = 0;
        data.Set(VariantKey, Mathf.Clamp(variant, 0, 2)); data.Set(StartKey, now);
        if (Plugin.ShooGestureSounds.Value) WorkSounds.ShooVoice(maid);
        return true;
    }

    private void Update() => Restore();
    private void OnDisable() => Restore();
    private void Restore() { left?.Restore(); right?.Restore(); }

    private void LateUpdate()
    {
        if (!view || !view.IsValid() || !ZNet.instance || left == null) return;
        var data = view.GetZDO();
        if (!Plugin.ShooGestures.Value || !maid.CanShooGesture)
        {
            if (view.IsOwner() && data.GetLong(StartKey) != 0) data.Set(StartKey, 0L);
            return;
        }
        var elapsed = (float)((ZNet.instance.GetTime().Ticks - data.GetLong(StartKey)) / (double)TimeSpan.TicksPerSecond);
        if (data.GetLong(StartKey) == 0 || elapsed < 0 || elapsed >= ShooMotion.Seconds) return;
        var pose = ShooMotion.Sample(data.GetInt(VariantKey), elapsed);
        var width = Vector3.Distance(left.Upper.position, right.Upper.position);
        var reach = (Vector3.Distance(left.Upper.position, left.Lower.position) + Vector3.Distance(left.Lower.position, left.Hand.position));
        if (pose.Left) left.Apply(transform, pose, width, reach);
        if (pose.Right && !work.HammerDrawn) right.Apply(transform, pose, width, reach);
    }

    internal sealed class Arm
    {
        internal readonly Transform Upper, Lower, Hand;
        private readonly Transform index, little;
        private readonly Transform[] fingerBones;
        private readonly Quaternion[] fingerRest;
        private readonly float side;
        private Quaternion upperRest, lowerRest, handRest;
        private bool posed;
        internal Arm(Animator animator, bool left)
        {
            side = left ? -1 : 1;
            Upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var bones = new System.Collections.Generic.List<Transform>();
            foreach (var name in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), (left ? "Left" : "Right") + name + "Proximal"));
                for (var i = 0; i < 3; i++)
                {
                    if (bone == null) break;
                    bones.Add(bone);
                    bone = bone.childCount > 0 ? bone.GetChild(0) : null;
                }
            }
            fingerBones = bones.ToArray(); fingerRest = new Quaternion[fingerBones.Length];
        }
        private void Capture()
        {
            upperRest = Upper.localRotation; lowerRest = Lower.localRotation; handRest = Hand.localRotation; posed = true;
            for (var i = 0; i < fingerBones.Length; i++) fingerRest[i] = fingerBones[i].localRotation;
        }
        internal void Apply(Transform root, ShooPose pose, float width, float length)
        {
            if (!Upper || !Lower || !Hand) return;
            Capture();
            var center = Upper.position - root.right * (side * width * .5f);
            var target = center + root.right * (side * width * pose.Spread) + Vector3.up * (pose.Lift * length)
                + root.forward * (pose.Reach * length);
            DustingStance.Reach(Upper, Lower, Hand.position, target,
                root.right * (side * .5f) - Vector3.up * .8f - root.forward * .2f, pose.Blend);
            if (!index || !little) return;
            var fingers = (index.position + little.position) * .5f - Hand.position;
            var dorsal = Vector3.Cross(index.position - little.position, fingers) * side;
            var desiredFingers = root.forward * .6f + Vector3.up * (-.55f + .28f * pose.Wrist);
            var desiredBack = -root.forward + Vector3.up * .2f;
            if (fingers.sqrMagnitude < .000001f || dorsal.sqrMagnitude < .000001f) return;
            var rotation = Quaternion.LookRotation(desiredFingers, desiredBack)
                * Quaternion.Inverse(Quaternion.LookRotation(fingers, dorsal)) * Hand.rotation;
            Hand.rotation = Quaternion.Slerp(Hand.rotation, rotation, pose.Blend);
        }
        internal void ReachTarget(Transform root, Vector3 target, float blend)
        {
            if (!Upper || !Lower || !Hand) return;
            Capture();
            DustingStance.Reach(Upper, Lower, Hand.position, target, root.right * side - root.forward * .25f, blend);
        }
        internal float GripShaft(Transform root, Vector3 center, Vector3 shaft, float blend)
        {
            if (!Upper || !Lower || !Hand || !index || !little) return float.PositiveInfinity;
            Capture();
            var fingers = (index.position + little.position) * .5f - Hand.position;
            var dorsal = Vector3.Cross(index.position - little.position, fingers) * side;
            var back = Vector3.ProjectOnPlane(root.right * side, shaft).normalized;
            var alongFingers = Vector3.Cross(shaft, back).normalized;
            if (fingers.sqrMagnitude > .000001f && dorsal.sqrMagnitude > .000001f)
                Hand.rotation = Quaternion.LookRotation(alongFingers, back)
                    * Quaternion.Inverse(Quaternion.LookRotation(fingers, dorsal)) * Hand.rotation;
            // Fit the palm to the cylinder, rather than putting the wrist on it.
            var palm = (index.position + little.position) * .5f;
            var contact = center + back * .023f;
            var pole = root.right * (side * .5f) - Vector3.up * .65f - root.forward * .25f;
            DustingStance.Reach(Upper, Lower, palm, contact, pole, 1f);
            // Re-establish hand orientation after the elbow solve.
            fingers = (index.position + little.position) * .5f - Hand.position;
            dorsal = Vector3.Cross(index.position - little.position, fingers) * side;
            if (fingers.sqrMagnitude > .000001f && dorsal.sqrMagnitude > .000001f)
                Hand.rotation = Quaternion.LookRotation(alongFingers, back)
                    * Quaternion.Inverse(Quaternion.LookRotation(fingers, dorsal)) * Hand.rotation;
            palm = (index.position + little.position) * .5f;
            DustingStance.Reach(Upper, Lower, palm, contact, pole, 1f);
            // Curl each segment toward the handle; constrain changes so knuckles
            // retain their native spacing and cannot reverse through the palm.
            foreach (var bone in fingerBones)
            {
                if (bone.childCount == 0) continue;
                var end = bone.GetChild(0).position;
                var nearest = center + shaft * Vector3.Dot(end - center, shaft);
                var radial = Vector3.ProjectOnPlane(end - center, shaft).normalized;
                var goal = nearest + radial * .025f;
                var turn = Quaternion.FromToRotation(end - bone.position, goal - bone.position);
                bone.rotation = Quaternion.RotateTowards(Quaternion.identity, turn, 48f) * bone.rotation;
            }
            var error = Vector3.Distance((index.position + little.position) * .5f, contact);
            Upper.localRotation = Quaternion.Slerp(upperRest, Upper.localRotation, blend);
            Lower.localRotation = Quaternion.Slerp(lowerRest, Lower.localRotation, blend);
            Hand.localRotation = Quaternion.Slerp(handRest, Hand.localRotation, blend);
            for (var i = 0; i < fingerBones.Length; i++) fingerBones[i].localRotation = Quaternion.Slerp(fingerRest[i], fingerBones[i].localRotation, blend);
            return error;
        }
        internal void Restore()
        {
            if (!posed) return;
            if (Upper) Upper.localRotation = upperRest;
            if (Lower) Lower.localRotation = lowerRest;
            if (Hand) Hand.localRotation = handRest;
            for (var i = 0; i < fingerBones.Length; i++) fingerBones[i].localRotation = fingerRest[i];
            posed = false;
        }
    }
}
