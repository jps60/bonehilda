using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

// Grounded maids and tames currently yielding to one never need a purely
// vertical facing direction. Preserve the current facing instead of passing
// that direction into Character.SetLookDir's unchecked horizontal projection.
[HarmonyPatch(typeof(Character), nameof(Character.SetLookDir))]
internal static class MaidFacingSafety
{
    private static int reports;

    private static bool Prefix(Character __instance, Vector3 dir)
    {
        if (TameCrowdPolicy.HasPlanarDirection(dir.x, dir.z)) return true;
        var ai = __instance.GetComponent<MonsterAI>();
        if (!__instance.GetComponent<Maid>() && (!ai || !TameYield.Active.ContainsKey(ai))) return true;
        if (Plugin.ActivityLogging.Value && reports++ < 3)
            Plugin.Instance.LogInfo("FACING prevented invalid grounded turn for " + __instance.name
                + "; direction=" + dir + "\n" + new StackTrace(1, true));
        return false;
    }
}

// Capture evidence for any remaining source without suppressing engine logs
// or changing the user's global stack-trace settings. Limit output per session.
internal sealed class FacingDiagnostics : MonoBehaviour
{
    private int reports;
    private void OnEnable() => Application.logMessageReceived += Received;
    private void OnDisable() => Application.logMessageReceived -= Received;

    private void Received(string condition, string stackTrace, LogType type)
    {
        if (!Plugin.ActivityLogging.Value || condition != "Look rotation viewing vector is zero" || reports >= 3) return;
        reports++;
        Plugin.Instance.LogWarning("FACING engine diagnostic " + reports + "/3:\n"
            + (string.IsNullOrEmpty(stackTrace) ? new StackTrace(1, true).ToString() : stackTrace));
    }
}
