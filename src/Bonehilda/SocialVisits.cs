using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Bonehilda;

// Gestures belong only to the visiting maid. Acquaintances keep their own AI,
// ownership, taming state, inventory, and (in Odin's case) disappearance rules.
internal sealed class SocialVisits
{
    private readonly Dictionary<ZDOID, long> cooldowns = new Dictionary<ZDOID, long>();
    private Component? target;
    private SocialKind kind;
    private Vector3 origin, standingPoint;
    private float deadline;
    private bool reacting;
    private string emote = "";
    private float settledAt;
    internal bool Performed { get; private set; }
    internal ZNetView? Target => target ? target!.GetComponent<ZNetView>() : null;
    internal string Describe => target ? kind + ": " + target!.name + (reacting ? " greeting" : " approaching") : "none";

    internal bool TryBegin(Maid maid, Coffin home)
    {
        target = null; Performed = false; reacting = false; settledAt = 0;
        if (DangerNear(maid, maid.transform.position)) return false;
        var now = ZNet.instance.GetTime().Ticks;
        foreach (var id in new List<ZDOID>(cooldowns.Keys)) if (cooldowns[id] <= now) cooldowns.Remove(id);
        var best = float.PositiveInfinity;
        foreach (var character in Character.GetAllCharacters())
        {
            if (!character || character.gameObject == maid.gameObject) continue;
            var category = Classify(maid, character);
            if (category != SocialKind.None) Consider(maid, home, character, category, now, ref best);
        }
        foreach (var odin in OdinWatch.Loaded)
            if (odin) Consider(maid, home, odin, SocialKind.Odin, now, ref best);
        deadline = Time.time + Mathf.Min(20f, Plugin.JobTimeout.Value);
        return target;
    }

    private void Consider(Maid maid, Coffin home, Component candidate, SocialKind category, long now, ref float best)
    {
        var view = candidate.GetComponent<ZNetView>();
        var range = Mathf.Min(Plugin.SocialRadius.Value, Plugin.Radius.Value);
        if (!view || !view.IsValid() || (candidate.transform.position - home.transform.position).sqrMagnitude > range * range
            || !maid.CanIdleVisit(view) || !TaskClaims.Available(maid, view, work: false)
            || !Maid.Allowed(candidate.transform.position, home.Piece.GetCreator()) || DangerNear(maid, candidate.transform.position)) return;
        var id = view.GetZDO().m_uid;
        if (cooldowns.TryGetValue(id, out var until) && SocialPolicy.CoolingDown(now, until)) return;
        var data = maid.View.GetZDO();
        if (data.GetZDOID("BH_lastSocial") == id && SocialPolicy.CoolingDown(now, data.GetLong("BH_lastSocialUntil"))) return;
        var character = candidate as Character;
        var distance = category == SocialKind.Odin ? 4f : Mathf.Max(1.6f, character!.GetRadius() + 1f);
        var toward = maid.transform.position - candidate.transform.position; toward.y = 0;
        if (toward.sqrMagnitude < .01f) toward = candidate.transform.forward;
        // A few near-side standing spots, never the creature's own nav destination.
        foreach (var angle in new[] { 0f, 45f, -45f, 90f, -90f })
        {
            var point = candidate.transform.position + Quaternion.Euler(0, angle, 0) * toward.normalized * distance;
            if (!IdleActivities.Floor(point, candidate.transform.position.y, out point)
                || Mathf.Abs(point.y - candidate.transform.position.y) > .75f
                || (point - home.transform.position).sqrMagnitude > range * range || !Maid.Allowed(point, home.Piece.GetCreator())
                || !SpaceFree(maid, point) || !Visible(point + maid.GetComponent<Character>().GetEyePoint() - maid.transform.position, Aim(candidate))) continue;
            var score = (point - maid.transform.position).sqrMagnitude;
            if (score >= best || !maid.CanIdleApproach(home, point)) continue;
            best = score; target = candidate; kind = category; standingPoint = point; origin = candidate.transform.position;
        }
    }

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, float delta)
    {
        if (!target || !Target || !Target!.IsValid() || Time.time >= deadline || !SafeTarget(maid, home)) return false;
        var offset = standingPoint - maid.transform.position; offset.y = 0;
        if (offset.magnitude > .4f || Mathf.Abs(standingPoint.y - maid.transform.position.y) > .5f)
        {
            if (reacting) return false;
            settledAt = 0;
            maid.Navigate(standingPoint, delta, .3f, offset.magnitude < 1f && RepairGeometry.ClearLastStep(maid, standingPoint));
            return true;
        }
        var aim = Aim(target!);
        if (!Visible(maid.GetComponent<Character>().GetEyePoint(), aim)) return false;
        maid.StopNavigation(); ai.StopMoving(); ai.LookAt(aim);
        var facing = aim - maid.transform.position; facing.y = 0;
        if (!reacting && Vector3.Dot(maid.transform.forward, facing.normalized) > .9f)
        {
            if (settledAt == 0) settledAt = Time.time + .6f;
            if (Time.time < settledAt) return true;
            var choices = SocialPolicy.Emotes[kind];
            emote = IdleRoutine.Gesture(maid, "social_" + kind, choices);
            maid.GetComponent<WorkAnimations>().Emote(emote, 5f);
            reacting = true; Performed = true; deadline = Time.time + 5f;
            var until = ZNet.instance.GetTime().Ticks + (long)(Plugin.SocialTargetCooldown.Value * TimeSpan.TicksPerSecond);
            var id = Target.GetZDO().m_uid;
            cooldowns[id] = until;
            maid.View.GetZDO().Set("BH_lastSocial", id);
            maid.View.GetZDO().Set("BH_lastSocialUntil", until);
        }
        return true;
    }

    private bool SafeTarget(Maid maid, Coffin home)
    {
        var point = target!.transform.position;
        var offset = point - origin;
        var range = Mathf.Min(Plugin.SocialRadius.Value, Plugin.Radius.Value);
        return SocialPolicy.InPlace(offset.x * offset.x + offset.z * offset.z, offset.y)
            && (point - home.transform.position).sqrMagnitude <= range * range
            && Maid.Allowed(point, home.Piece.GetCreator()) && !DangerNear(maid, point) && !DangerNear(maid, maid.transform.position)
            && (kind == SocialKind.Odin || target is Character character && Classify(maid, character) == kind);
    }

    private static SocialKind Classify(Maid visitor, Character character)
    {
        var maid = character.GetComponent<Maid>();
        var ai = character.GetBaseAI();
        // Maid AI combat flags are suppressed; native creature flags remain authoritative.
        var busy = maid ? !maid!.AvailableForSocial : ai && (ai.IsAlerted() || ai.HaveTarget() || ai.IsSleeping());
        return SocialPolicy.Classify(character.gameObject.name, maid, character.IsTamed(), character.IsPlayer(),
            BaseAI.IsEnemy(visitor.GetComponent<Character>(), character), character.IsDead(), busy);
    }

    private static Vector3 Aim(Component candidate) => candidate is Character character ? character.GetCenterPoint() : candidate.transform.position + Vector3.up * 1.5f;
    private static bool Visible(Vector3 eye, Vector3 aim) => !Physics.Linecast(eye, aim, RepairGeometry.SightMask, QueryTriggerInteraction.Ignore);

    private static bool SpaceFree(Maid maid, Vector3 point)
    {
        foreach (var other in Character.GetAllCharacters())
            if (other && other.gameObject != maid.gameObject && !other.IsDead()
                && Mathf.Abs(other.transform.position.y - point.y) < other.GetHeight()
                && Utils.DistanceXZ(point, other.transform.position) < other.GetRadius() + .55f) return false;
        return true;
    }

    private static bool DangerNear(Maid maid, Vector3 point)
    {
        foreach (var other in Character.GetAllCharacters())
            if (other && !other.IsDead() && (other.transform.position - point).sqrMagnitude < 100f
                && BaseAI.IsEnemy(maid.GetComponent<Character>(), other)) return true;
        return false;
    }

    internal void Cancel(Maid maid)
    {
        if (reacting)
        {
            maid.GetComponent<WorkAnimations>().StopEmote();
            if (emote == "kneel") maid.PauseToStand();
        }
        target = null; reacting = false;
    }
}

internal sealed class OdinWatch : MonoBehaviour
{
    internal static readonly HashSet<Odin> Loaded = new HashSet<Odin>();
    private void OnEnable() => Loaded.Add(GetComponent<Odin>());
    private void OnDisable() => Loaded.Remove(GetComponent<Odin>());
}

[HarmonyPatch(typeof(Odin), "Awake")]
internal static class ObserveOdin
{
    private static void Postfix(Odin __instance) => __instance.gameObject.AddComponent<OdinWatch>();
}

// Suppress the inherited, uncoordinated ambient timer. Animation-linked voices
// create one native networked sound effect with an independent cooldown instead.
[HarmonyPatch(typeof(BaseAI), "DoIdleSound")]
internal static class MaidIdleVoice
{
    private static bool Prefix(BaseAI __instance) => !__instance.GetComponent<Maid>();
}
