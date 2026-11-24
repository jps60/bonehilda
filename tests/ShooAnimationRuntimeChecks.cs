using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;

internal static class ShooAnimationRuntimeChecks
{
    internal static IEnumerator Run()
    {
        while (!Player.m_localPlayer) yield return null;
        var origin = Player.m_localPlayer.transform.position + Vector3.up * 100;
        var coffin = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("BH_Coffin_0Star"), origin + Vector3.right * 3, Quaternion.identity);
        ((MonoBehaviour)coffin.GetComponent("Coffin")).enabled = false;
        var worker = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("BH_Bonehilda"), origin, Quaternion.identity);
        var maid = (MonoBehaviour)worker.GetComponent("Maid"); maid.enabled = false;
        AccessTools.Method(maid.GetType(), "Bind").Invoke(maid, new object[] { coffin.GetComponent("Coffin"), false });
        yield return null;
        var pose = worker.GetComponent("ShooGesture");
        var animator = worker.GetComponentInChildren<Animator>(); animator.enabled = false;
        var view = worker.GetComponent<ZNetView>();
        var legs = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot }.Select(animator.GetBoneTransform).ToArray();
        var rotations = legs.Select(t => t.localRotation).ToArray();
        var positions = legs.Select(t => t.localPosition).ToArray();
        var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var initialLeft = left.localRotation; var initialRight = right.localRotation;
        for (var variant = 0; variant < 3; variant++)
        {
            view.GetZDO().Set("BH_shooPoseStart", ZNet.instance.GetTime().Ticks - (long)(.8 * TimeSpan.TicksPerSecond));
            view.GetZDO().Set("BH_shooPoseVariant", variant);
            AccessTools.Method(pose.GetType(), "LateUpdate").Invoke(pose, null);
            if (variant != 1 && Quaternion.Angle(initialLeft, left.localRotation) < 1) throw new Exception("Left shoo arm failed to move");
            if (variant != 0 && Quaternion.Angle(initialRight, right.localRotation) < 1) throw new Exception("Right shoo arm failed to move");
            if (variant == 0 && Quaternion.Angle(initialRight, right.localRotation) > .01f || variant == 1 && Quaternion.Angle(initialLeft, left.localRotation) > .01f)
                throw new Exception("One-arm shoo altered the other arm");
            for (var i = 0; i < legs.Length; i++)
                if (Quaternion.Angle(rotations[i], legs[i].localRotation) > .01f || Vector3.Distance(positions[i], legs[i].localPosition) > .0001f)
                    throw new Exception("Shoo modified hips/legs");
            AccessTools.Method(pose.GetType(), "Update").Invoke(pose, null);
            if (Quaternion.Angle(initialLeft, left.localRotation) > .01f || Quaternion.Angle(initialRight, right.localRotation) > .01f)
                throw new Exception("Shoo arm pose accumulated");
            Debug.Log("SHOO ANIMATION PASS: variant=" + variant + " independent arms; hips/legs unchanged; pose restored");
        }
        view.GetZDO().Set("BH_returning", true);
        AccessTools.Method(pose.GetType(), "LateUpdate").Invoke(pose, null);
        if (Quaternion.Angle(initialLeft, left.localRotation) > .01f || Quaternion.Angle(initialRight, right.localRotation) > .01f)
            throw new Exception("Dismissal failed to interrupt gesture");
        var items = ObjectDB.instance.m_items.Where(p => p && p.GetComponent<ItemDrop>() && (p.name.IndexOf("kvast", StringComparison.OrdinalIgnoreCase) >= 0
            || p.name.IndexOf("broom", StringComparison.OrdinalIgnoreCase) >= 0
            || Localization.instance.Localize(p.GetComponent<ItemDrop>().m_itemData.m_shared.m_name).IndexOf("kvast", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        foreach (var item in items) Debug.Log("ANIMATION REVIEW KVASTUR: " + item.name + " = " + Localization.instance.Localize(item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name)
            + "; attach=" + (bool)item.transform.Find("attach"));
        foreach (var prefab in ZNetScene.instance.m_prefabs.Where(p => p && (p.name.IndexOf("kvast", StringComparison.OrdinalIgnoreCase) >= 0 || p.name.IndexOf("broom", StringComparison.OrdinalIgnoreCase) >= 0)))
            Debug.Log("ANIMATION REVIEW KVASTUR PREFAB: " + prefab.name + "; meshes=" + prefab.GetComponentsInChildren<MeshFilter>(true).Length);
        var sweep = worker.GetComponent("RugSweepDraft");
        if (!(bool)AccessTools.Method(sweep.GetType(), "BuildTool").Invoke(sweep, null)) throw new Exception("Native Kvastur visual could not be copied");
        var visual = (GameObject)AccessTools.Field(sweep.GetType(), "tool").GetValue(sweep);
        var visualLength = (float)AccessTools.Field(sweep.GetType(), "length").GetValue(sweep);
        if (visualLength < .3f || visualLength > 5f) throw new Exception("Kvastur unit conversion failed: " + visualLength);
        var scale = (float)AccessTools.Field(sweep.GetType(), "visualScale").GetValue(sweep);
        var expectedLength = (float)AccessTools.Method(sweep.GetType(), "FittedLength").Invoke(null, new object[] { animator });
        if (Mathf.Abs(visualLength * scale - expectedLength) > .01f) throw new Exception("Broom does not fit maid stature");
        if (visual.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || visual.GetComponentsInChildren<Collider>(true).Length != 0
            || visual.GetComponentsInChildren<Rigidbody>(true).Length != 0) throw new Exception("Kvastur review prop contains gameplay behavior");
        Debug.Log("SWEEP VISUAL PASS: native BogWitchKvastur renderer data only; nativeLength=" + visualLength + "; fittedLength=" + (visualLength * scale));
        view.GetZDO().Set("BH_returning", false);
        var rugPrefab = ZNetScene.instance.m_prefabs.First(p => p && p.GetComponent<Piece>() && p.name.StartsWith("rug_", StringComparison.Ordinal));
        var rug = UnityEngine.Object.Instantiate(rugPrefab, worker.transform.position + worker.transform.forward * .6f, Quaternion.identity);
        Physics.SyncTransforms();
        for (var variant = 0; variant < 3; variant++)
        {
            if (!(bool)AccessTools.Method(sweep.GetType(), "Begin").Invoke(sweep, new object[] { rug.GetComponent<Piece>(), variant, false }))
                throw new Exception("Rug preview could not start");
            view.GetZDO().Set("BH_sweepDraftStart", ZNet.instance.GetTime().Ticks - (long)(.8 * TimeSpan.TicksPerSecond));
            AccessTools.Method(sweep.GetType(), "LateUpdate").Invoke(sweep, null);
            if (!visual.activeSelf) throw new Exception("Rug draft cancelled before displaying the model");
            AccessTools.Method(sweep.GetType(), "Update").Invoke(sweep, null);
            if (Quaternion.Angle(initialLeft, left.localRotation) > .01f || Quaternion.Angle(initialRight, right.localRotation) > .01f)
                throw new Exception("Sweep pose did not restore");
            Debug.Log("SWEEP POSE PASS: variant=" + variant + " native prop shown; arms restored");
        }
        view.GetZDO().Set("BH_returning", true);
        AccessTools.Method(sweep.GetType(), "LateUpdate").Invoke(sweep, null);
        if (visual.activeSelf || view.GetZDO().GetLong("BH_sweepDraftStart") != 0) throw new Exception("Dismissal did not clear sweeping draft");
        ZNetScene.instance.Destroy(rug);
        foreach (var prefab in ZNetScene.instance.m_prefabs.Where(p => p && p.GetComponent<Piece>()).Where(p =>
            new[] { "ask", "skeleton", "armorstand", "pot", "lava", "adorn", "treasure", "crystal" }.Any(n => p.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)))
            Debug.Log("ANIMATION REVIEW TARGET: " + prefab.name + " = " + Localization.instance.Localize(prefab.GetComponent<Piece>().m_name));
        ZNetScene.instance.Destroy(worker); ZNetScene.instance.Destroy(coffin);
        Debug.Log("SHOO ANIMATION COMPLETE: three variants and dismissal preemption; visual gait/wrist and multiplayer review pending");
    }
}
