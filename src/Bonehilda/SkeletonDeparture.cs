using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal static class SkeletonDeparture
{
    internal static void Play(Maid maid)
    {
        try { Spawn(maid); }
        catch (System.Exception e) { Plugin.Instance.LogWarning("Skeleton departure effects: " + e.GetBaseException().Message); }
    }

    private static void Spawn(Maid maid)
    {
        var view = maid.View;
        var actor = maid.GetComponent<Character>();
        // Actual combat deaths already run Character.OnDeath and its effects.
        if (!view || !view.IsValid() || !view.IsOwner() || actor.IsDead() || view.GetZDO().GetBool("BH_departed")) return;
        view.GetZDO().Set("BH_departed", true);
        var hue = 0f; var saturation = 0f; var value = 0f;
        var levels = maid.GetComponentInChildren<LevelEffects>();
        if (levels) levels.GetColorChanges(out hue, out saturation, out value);
        foreach (var effect in WorkSounds.Spawn(actor.m_deathEffects, maid.transform.position, maid.transform.rotation))
        {
            var ragdoll = effect.GetComponent<Ragdoll>();
            if (!ragdoll) continue;
            ragdoll.m_dropItems = false;
            var setup = AccessTools.Method(typeof(Ragdoll), "Setup");
            var args = setup.GetParameters().Length == 7
                ? new object[] { Vector3.zero, hue, saturation, value, null!, actor.GetLevel(), false }
                : new object[] { Vector3.zero, hue, saturation, value, null!, actor.GetLevel() };
            setup.Invoke(ragdoll, args);
        }
    }
}

// Explicit network destruction, including other mods/console despawns, gets
// the same farewell. Ordinary scene streaming does not destroy the saved ZDO.
[HarmonyPatch(typeof(ZNetScene), "Destroy", new[] { typeof(GameObject) })]
internal static class MaidDespawnEffects
{
    private static void Prefix(GameObject __0)
    {
        if (__0 && __0.TryGetComponent<Maid>(out var maid)) maid.PrepareDeparture();
    }
}
