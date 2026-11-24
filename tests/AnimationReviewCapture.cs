using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

// Private-world, rendered review. Captures the native rig, outfit, controller and
// runtime pose code, rather than drawing a schematic replacement character.
internal static class AnimationReviewCapture
{
    internal static IEnumerator Run()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new Exception("Animation review needs a private rendered probe");
        while (!Player.m_localPlayer) yield return null;
        var output = Path.Combine(Paths.ConfigPath, "animation-review"); Directory.CreateDirectory(output);
        var origin = Player.m_localPlayer.transform.position + Vector3.up * 110;
        var coffin = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("BH_Coffin_0Star"), origin + Vector3.right * 8, Quaternion.identity);
        ((MonoBehaviour)coffin.GetComponent("Coffin")).enabled = false;
        var worker = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("BH_Bonehilda"), origin, Quaternion.identity);
        var maid = (MonoBehaviour)worker.GetComponent("Maid"); maid.enabled = false;
        AccessTools.Method(maid.GetType(), "Bind").Invoke(maid, new object[] { coffin.GetComponent("Coffin"), false });
        yield return null;
        worker.GetComponent<MonsterAI>().enabled = false;
        worker.GetComponent<Humanoid>().enabled = false;
        worker.GetComponent<ZSyncAnimation>().enabled = false;
        worker.GetComponent<Rigidbody>().isKinematic = true;
        foreach (var lod in worker.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
        var animator = worker.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = false;
        animator.SetBool("wakeup", false);
        animator.SetBool("onGround", true); animator.SetBool("falling", false); animator.SetBool("inWater", false);
        for (var warmup = 0; warmup < 150; warmup++) animator.Update(1f / 30f);
        Debug.Log("ANIMATION REVIEW PARAMS " + string.Join(",", animator.parameters.Select(p => p.name)));
        var shoo = (MonoBehaviour)worker.GetComponent("ShooGesture"); shoo.enabled = false;
        var sweep = (MonoBehaviour)worker.GetComponent("RugSweepDraft"); sweep.enabled = false;
        var data = worker.GetComponent<ZNetView>().GetZDO();
        var rugPrefab = ZNetScene.instance.m_prefabs.First(p => p && p.GetComponent<Piece>() && p.name.StartsWith("rug_", StringComparison.Ordinal));
        var rug = UnityEngine.Object.Instantiate(rugPrefab, origin + Vector3.forward * .7f, Quaternion.identity);
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.transform.position = origin - Vector3.up * .11f; platform.transform.localScale = new Vector3(5, .2f, 5);
        platform.GetComponent<Renderer>().sharedMaterial = rug.GetComponentInChildren<Renderer>().sharedMaterial;
        // Layer isolation avoids rendering world scenery; no user scene is modified.
        const int layer = 30;
        foreach (var obj in new[] { worker, rug, platform }) foreach (var t in obj.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        var key = new GameObject("Private review light").AddComponent<Light>(); key.type = LightType.Directional;
        key.transform.rotation = Quaternion.Euler(35, -30, 0); key.intensity = 1.6f; key.cullingMask = 1 << layer;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
        var camera = new GameObject("Private review camera").AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f, .14f, .17f);
        camera.cullingMask = 1 << layer; camera.orthographic = true; camera.orthographicSize = 1.55f;
        var target = new RenderTexture(480, 540, 24); camera.targetTexture = target;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        var names = new[] { "shoo-left", "shoo-right", "shoo-both", "sweep-long", "sweep-gather", "sweep-fan" };
        var measurements = new System.Collections.Generic.List<string> { "motion,time,leftGripMetres,rightGripMetres,propVisible" };
        foreach (var name in names)
        {
            var isShoo = name.StartsWith("shoo"); var variant = Array.IndexOf(names, name) % 3;
            var frames = isShoo ? 40 : 68; var seconds = isShoo ? 3.25f : 5.6f;
            data.Set("BH_shooPoseStart", 0L); data.Set("BH_sweepDraftStart", 0L);
            animator.SetBool("wakeup", false); animator.SetFloat("forward_speed", isShoo ? 1.4f : 0f);
            animator.Update(.5f);
            if (!isShoo)
            {
                if (!(bool)AccessTools.Method(sweep.GetType(), "Begin").Invoke(sweep, new object[] { rug.GetComponent<Piece>(), variant, false }))
                    throw new Exception("Could not begin rendered rug review");
                foreach (var t in worker.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            }
            var component = isShoo ? shoo : sweep;
            var keyName = isShoo ? "BH_shooPoseStart" : "BH_sweepDraftStart";
            data.Set("BH_shooPoseVariant", variant);
            for (var frame = 0; frame < frames; frame++)
            {
                AccessTools.Method(component.GetType(), "Update").Invoke(component, null);
                animator.Update(seconds / (frames - 1));
                if (frame == 10) Debug.Log("ANIMATION REVIEW CLIPS " + name + " " + string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name)));
                var time = seconds * frame / (frames - 1) - .25f;
                data.Set(keyName, ZNet.instance.GetTime().Ticks - (long)(time * TimeSpan.TicksPerSecond));
                AccessTools.Method(component.GetType(), "LateUpdate").Invoke(component, null);
                if (!isShoo)
                {
                    var leftError = AccessTools.Property(sweep.GetType(), "LeftGripError");
                    var rightError = AccessTools.Property(sweep.GetType(), "RightGripError");
                    var prop = (GameObject)AccessTools.Field(sweep.GetType(), "tool").GetValue(sweep);
                    measurements.Add(string.Join(",", new[] { name, time.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        leftError?.GetValue(sweep, null)?.ToString(), rightError?.GetValue(sweep, null)?.ToString(), prop.activeSelf.ToString() }));
                }
                foreach (var t in worker.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                for (var angle = 0; angle < 2; angle++)
                {
                    var focus = origin + Vector3.up * 1.1f + Vector3.forward * .2f;
                    camera.transform.position = focus + (angle == 0 ? new Vector3(3, 1.1f, 5) : new Vector3(5, .5f, -1));
                    camera.transform.LookAt(focus); camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 480, 540), angle * 480, 0); image.Apply();
                }
                File.WriteAllBytes(Path.Combine(output, name + "-" + frame.ToString("D3") + ".png"), image.EncodeToPNG());
                if (frame % 8 == 0) yield return null;
            }
            AccessTools.Method(component.GetType(), "Update").Invoke(component, null);
            data.Set(keyName, 0L);
            Debug.Log("ANIMATION REVIEW CAPTURE " + name + " frames=" + frames);
        }
        File.WriteAllLines(Path.Combine(output, "measurements.csv"), measurements.ToArray());
        RenderTexture.active = null; camera.targetTexture = null; target.Release();
        UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(camera.gameObject);
        UnityEngine.Object.Destroy(key.gameObject); UnityEngine.Object.Destroy(platform);
        ZNetScene.instance.Destroy(worker); ZNetScene.instance.Destroy(coffin); ZNetScene.instance.Destroy(rug);
        Debug.Log("ANIMATION REVIEW CAPTURE COMPLETE " + output);
    }
}
