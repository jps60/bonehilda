using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal static class CrowdGeometry
{
    internal static bool Clear(Character actor, Vector3 from, Vector3 to, Character? ignore = null)
        => ClearReason(actor, from, to, ignore, out _);

    internal static bool ClearReason(Character actor, Vector3 from, Vector3 to, Character? ignore, out string reason)
    {
        reason = "clear";
        var capsule = actor.GetComponent<CapsuleCollider>();
        if (!capsule || (to - from).magnitude > 6f || Mathf.Abs(to.y - from.y) > .3f) { reason = "unsupported capsule, distance or height"; return false; }
        var previousY = from.y;
        var count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .35f));
        for (var i = 1; i <= count; i++)
        {
            var sample = Vector3.Lerp(from, to, i / (float)count);
            if (!Floor(sample, previousY, out var floor) || Mathf.Abs(floor.y - sample.y) > .2f)
            { reason = "floor support/height at " + Vector3.Distance(from, sample).ToString("F1") + "m"; return false; }
            previousY = floor.y;
        }
        // Wolves use a horizontal capsule; preserve its actual orientation.
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var scale = capsule.transform.lossyScale;
        var sizes = new[] { Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z) };
        var radius = capsule.radius * Mathf.Max(sizes[(capsule.direction + 1) % 3], sizes[(capsule.direction + 2) % 3]) + .015f;
        var half = Mathf.Max(0, capsule.height * sizes[capsule.direction] * .5f - radius);
        var axis = capsule.transform.TransformDirection(axes[capsule.direction]);
        var center = capsule.transform.TransformPoint(capsule.center) + from - actor.transform.position + Vector3.up * .07f;
        var bottom = center - axis * half; var top = center + axis * half;
        var offset = to - from;
        var mask = RepairGeometry.Solids | LayerMask.GetMask("character", "character_net");
        var hitCollider = Physics.CapsuleCastAll(bottom, top, radius, offset.normalized, offset.magnitude, mask, QueryTriggerInteraction.Ignore)
            .Where(hit => Other(hit.collider, actor, ignore) && !SeparatingContact(capsule, actor, hit.collider, from, offset))
            .OrderBy(hit => hit.distance).Select(hit => hit.collider).FirstOrDefault();
        if (hitCollider) { reason = "path blocked by " + Obstacle(hitCollider); return false; }
        var overlap = Physics.OverlapCapsule(bottom + offset, top + offset, radius, mask, QueryTriggerInteraction.Ignore)
            .FirstOrDefault(c => Other(c, actor, ignore));
        if (overlap) { reason = "destination blocked by " + Obstacle(overlap); return false; }
        return true;
    }

    private static string Obstacle(Collider collider)
    {
        var view = collider.GetComponentInParent<ZNetView>();
        return view && view.IsValid() ? Utils.GetPrefabName(view.gameObject) + " [" + view.GetZDO().m_uid + "]" : collider.name;
    }

    // A tightly packed tame may already overlap another body's capsule. Allow
    // only movement out of that contact; never ignore a body farther along the
    // route, and still require the destination capsule to be completely clear.
    private static bool SeparatingContact(CapsuleCollider capsule, Character actor, Collider other, Vector3 from, Vector3 offset)
    {
        var tame = other.GetComponentInParent<Character>();
        if (!actor.IsTamed() || !tame || !tame.IsTamed() || !tame.GetComponent<Tameable>()) return false;
        return Physics.ComputePenetration(capsule, capsule.transform.position + from - actor.transform.position,
            capsule.transform.rotation, other, other.transform.position, other.transform.rotation, out var direction, out _)
            && Vector3.Dot(offset.normalized, direction) > .1f;
    }

    private static bool Other(Collider collider, Character actor, Character? ignore)
    {
        var character = collider.GetComponentInParent<Character>();
        return !character || character != actor && character != ignore;
    }

    internal static bool Floor(Vector3 point, float height, out Vector3 floor)
    {
        floor = default;
        if (!Physics.Raycast(new Vector3(point.x, height + .35f, point.z), Vector3.down, out var hit, .8f,
                RepairGeometry.Solids, QueryTriggerInteraction.Ignore) || hit.normal.y < .85f) return false;
        floor = hit.point + Vector3.up * .03f;
        return Mathf.Abs(floor.y - height) < .3f;
    }

    internal static bool CrossesDoor(Vector3 from, Vector3 to)
        => CrossesDoorReason(from, to, out _);

    internal static bool CrossesDoorReason(Vector3 from, Vector3 to, out string reason)
    {
        reason = "no door crossing";
        foreach (var door in MaidDoorWatch.Loaded)
        {
            if (!door) continue;
            var center = door.transform.position;
            var normal = door.transform.forward;
            normal.y = 0;
            if (Mathf.Abs((from.y + to.y) * .5f - center.y) > 3f) continue;
            var a = Vector3.Dot(from - center, normal); var b = Vector3.Dot(to - center, normal);
            if (a * b > 0 || Mathf.Abs(a - b) < .001f) continue;
            var crossing = Vector3.Lerp(from, to, Mathf.Clamp01(a / (a - b))); crossing.y = center.y;
            // Conservative gate width: refuse crossing even an already-open door.
            if ((crossing - center).sqrMagnitude < 36f)
            {
                var view = door.GetComponent<ZNetView>();
                reason = "door/gate crossing guard: " + Utils.GetPrefabName(door.gameObject)
                    + (view && view.IsValid() ? " [" + view.GetZDO().m_uid + "]" : "");
                return true;
            }
        }
        return false;
    }
}

internal sealed class TameCrowd
{
    private readonly NavigationProgress progress = new NavigationProgress();
    private readonly Queue<Vector3> bypass = new Queue<Vector3>();
    private Vector3 goal;
    private Vector3 lastFeet;
    private Character? blocker;
    private float nextPlan, nextRequest, nextScan, blockedFor;
    private float shooUntil, nextAreaPulse;
    internal int Requests, Bypasses;
    internal bool Bypassing => bypass.Count > 0;
    internal float BlockedSeconds;
    internal string State = "none";
    private ZNetView? lastRequestedTame;
    private ZDOID lastRequester;
    private float lastRequestedAt;
    internal string LastShooTame => lastRequestedTame && lastRequestedTame!.IsValid() ? lastRequestedTame.GetZDO().m_uid.ToString() : "none";
    internal float LastShooAge => Requests == 0 ? -1 : Mathf.Max(0, Time.time - lastRequestedAt);
    internal string LastShooOutcome
    {
        get
        {
            if (!lastRequestedTame || !lastRequestedTame!.IsValid()) return Requests == 0 ? "no request" : "tame unloaded";
            if (!lastRequestedTame.IsOwner()) return "handled by remote tame owner; see owner log";
            var receiver = lastRequestedTame.GetComponent<TameYield>();
            if (!receiver) return "receiver missing";
            if (receiver.LastRequester != lastRequester || receiver.LastRequestTime < lastRequestedAt - .01f)
                return "no matching receiver result (or superseded by another maid)";
            return receiver.LastRejection == "accepted" ? "accepted; " + receiver.MovementOutcome : receiver.LastRejection;
        }
    }

    internal void Reset() { bypass.Clear(); blocker = null; progress.Reset(); blockedFor = 0; nextScan = 0; State = "none"; }

    internal bool Tick(Maid maid, MonsterAI ai, Vector3 destination, float delta, bool run, bool allowBypass = true, bool usePath = true)
    {
        if (!Plugin.TameCrowdEnabled.Value || !SessionSupport.Active) { Reset(); return false; }
        if (maid.Stepping) return false;
        var actor = maid.GetComponent<Character>(); var feet = actor.transform.position;
        if ((goal - destination).sqrMagnitude > 1f) { Reset(); goal = destination; nextPlan = 0; }
        if (Time.time < shooUntil && Time.time >= nextAreaPulse)
        {
            nextAreaPulse = Time.time + .5f;
            var pulseHeading = bypass.Count > 0 ? bypass.Peek() - feet : destination - feet;
            if (usePath && bypass.Count == 0)
                foreach (var node in ai.m_path) if ((node - feet).sqrMagnitude > .25f) { pulseHeading = node - feet; break; }
            pulseHeading.y = 0;
            ShooArea(maid, pulseHeading.normalized);
        }
        if (bypass.Count > 0)
        {
            if ((bypass.Peek() - feet).sqrMagnitude < .12f) bypass.Dequeue();
            if (bypass.Count == 0) { ai.m_path.Clear(); ai.m_lastFindPathTime = -100f; State = "bypass complete"; return false; }
            if (!progress.Advance(delta, feet.x, feet.z, destination.x, destination.y, destination.z, 1.5f)
                && CrowdGeometry.Clear(actor, feet, bypass.Peek()))
            { State = "walking around tame"; ai.MoveTowards(bypass.Peek() - feet, run); return true; }
            bypass.Clear(); ai.m_path.Clear(); ai.m_lastFindPathTime = -100f;
            nextPlan = Time.time + 1.5f; blockedFor = Plugin.TameCrowdDelay.Value;
        }
        var heading = destination - feet;
        var segmentGoal = destination;
        if (usePath) foreach (var node in ai.m_path) if ((node - feet).sqrMagnitude > .25f) { heading = node - feet; segmentGoal = node; break; }
        heading.y = 0;
        if (heading.magnitude < .4f) return false;
        heading.Normalize();
        var found = blocker;
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .25f;
            found = Character.GetAllCharacters().Where(c => c && c != actor && !c.GetComponent<Maid>() && c.IsTamed() && !c.IsDead()
                && Mathf.Abs(c.transform.position.y - feet.y) < 1f && (c.transform.position - feet).sqrMagnitude < 9f)
                .Where(c => {
                    var offset = c.transform.position - feet; offset.y = 0;
                    var ahead = Vector3.Dot(offset, heading);
                    return ahead > -.1f && ahead < 2f + c.GetRadius() && Vector3.Cross(offset, heading).magnitude < actor.GetRadius() + c.GetRadius() + .12f;
                }).OrderBy(c => (c.transform.position - feet).sqrMagnitude).FirstOrDefault();
        }
        if (found && (found!.IsDead() || (found.transform.position - feet).sqrMagnitude > 9f)) found = null;
        if (found is null || !found) { blocker = null; blockedFor = 0; State = "clear"; return false; }
        if (found != blocker) { blocker = found; lastFeet = feet; blockedFor = 0; nextPlan = 0; progress.Reset(); }
        // Slow pushing also counts: it need not become complete immobility.
        var movement = feet - lastFeet; movement.y = 0; lastFeet = feet;
        if (movement.magnitude / Mathf.Max(.001f, delta) >= Mathf.Max(.1f, (run ? actor.m_runSpeed : actor.m_speed) * .6f)) blockedFor = 0;
        else blockedFor += Mathf.Min(delta, .25f);
        if (blockedFor > .5f) BlockedSeconds += Mathf.Min(delta, .25f);
        if (Time.time < nextPlan) return false;
        nextPlan = Time.time + .25f;
        var route = new List<Vector3>();
        var reason = "door passage: no bypass";
        var extra = allowBypass ? Bypass(maid, actor, found, heading, segmentGoal, route, out reason) : float.PositiveInfinity;
        State = reason;
        var speed = run ? actor.m_runSpeed : actor.m_speed;
        if (extra > 3f && blockedFor < Plugin.TameCrowdDelay.Value) return false;
        if (Plugin.TameShooEnabled.Value && TameCrowdPolicy.PreferShoo(extra, speed, blockedFor, Plugin.TameCrowdDelay.Value)
            && Time.time >= nextRequest && Time.time >= shooUntil)
        {
            nextRequest = Time.time + 2f;
            shooUntil = Time.time + Plugin.TameShooDuration.Value; nextAreaPulse = Time.time + .5f;
            var affected = ShooArea(maid, heading);
            if (affected > 0) { State = "area shoo requested: " + affected + " tames"; return false; }
        }
        if (route.Count == 0) { State = "tame blocks route: " + reason; return false; }
        foreach (var node in route) bypass.Enqueue(node);
        Bypasses++; progress.Reset();
        ai.MoveTowards(bypass.Peek() - feet, run); State = "walking around tame";
        return true;
    }

    private int ShooArea(Maid maid, Vector3 heading)
    {
        var actor = maid.GetComponent<Character>(); var feet = actor.transform.position;
        var cosine = Mathf.Cos(Plugin.TameShooAngle.Value * .5f * Mathf.Deg2Rad);
        var targets = Character.GetAllCharacters().Where(c => c && c != actor
            && Mathf.Abs(c.transform.position.y - feet.y) < 1.5f
            && TameCrowdPolicy.InShooCone(c.transform.position.x - feet.x, c.transform.position.z - feet.z,
                heading.x, heading.z, Plugin.TameShooRadius.Value, cosine)
            && TameYield.Eligible(c.GetComponent<MonsterAI>()))
            .Where(c => { var v = c.GetComponent<ZNetView>(); return v && v.IsValid()
                && v.GetZDO().GetLong("BH_nextShoo") <= ZNet.instance.GetTime().Ticks; })
            // Let the outer animals start clearing space for those behind them.
            .OrderByDescending(c => (c.transform.position - feet).sqrMagnitude).Take(12).ToArray();
        foreach (var tame in targets)
        {
            lastRequestedTame = tame.GetComponent<ZNetView>();
            lastRequester = maid.View.GetZDO().m_uid; lastRequestedAt = Time.time;
            lastRequestedTame.InvokeRPC(TameYield.AreaRpc, lastRequester, heading);
            Requests++;
        }
        return targets.Length;
    }

    private static float Bypass(Maid maid, Character actor, Character tame, Vector3 heading, Vector3 segmentGoal, List<Vector3> route, out string reason)
    {
        reason = "no bypass";
        var best = float.PositiveInfinity;
        var capsule = tame.GetComponent<CapsuleCollider>();
        if (!capsule) { reason = "bypass: unsupported collider"; return best; }
        var bounds = capsule.bounds;
        var along = actor.GetRadius() + Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(heading.x), 0, Mathf.Abs(heading.z))) + .4f;
        var right = Vector3.Cross(Vector3.up, heading);
        var across = actor.GetRadius() + Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(right.x), 0, Mathf.Abs(right.z))) + .4f;
        var center = bounds.center; center.y = tame.transform.position.y;
        if (!TameCrowdPolicy.WithinSegment(Vector3.Dot(segmentGoal - actor.transform.position, heading),
                Vector3.Dot(center + heading * (along + .3f) - actor.transform.position, heading)))
        { reason = "bypass would overshoot waypoint"; return best; }
        var homeObject = ZNetScene.instance.FindInstance(maid.View.GetZDO().GetZDOID(Maid.HomeKey));
        var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
        if (!home) { reason = "bypass: home not loaded"; return best; }
        foreach (var side in new[] { -1f, 1f })
        {
            var lateral = right * across * side;
            var points = new[] { center - heading * along + lateral,
                center + heading * along + lateral, center + heading * (along + .3f) };
            var previous = actor.transform.position; var length = 0f; var valid = true;
            for (var i = 0; i < points.Length; i++)
            {
                if (!CrowdGeometry.Floor(points[i], previous.y, out points[i])) { reason = "bypass: no floor " + i; valid = false; break; }
                if (!CrowdGeometry.Clear(actor, previous, points[i])) { reason = "bypass: blocked body " + i; valid = false; break; }
                if (!MaintenancePolicy.InRadius((points[i] - home!.transform.position).sqrMagnitude, Plugin.Radius.Value)
                    || !Maid.Allowed(points[i], home.Piece.GetCreator())) { reason = "bypass: permission/radius"; valid = false; break; }
                length += Vector3.Distance(previous, points[i]); previous = points[i];
            }
            var extra = length - Vector3.Distance(actor.transform.position, previous);
            if (!valid || extra >= best) continue;
            best = extra; route.Clear(); route.AddRange(points);
        }
        return best;
    }
}

internal sealed class TameYield : MonoBehaviour
{
    internal const string Rpc = "BH_ExperimentalShoo";
    internal const string AreaRpc = "BH_ExperimentalShooArea";
    internal static readonly Dictionary<MonsterAI, TameYield> Active = new Dictionary<MonsterAI, TameYield>();
    private MonsterAI ai = null!;
    private ZNetView view = null!;
    private Character actor = null!;
    private Vector3 target;
    private float until, cooldown;
    private bool yielding;
    private Character? requester;
    internal string LastRejection = "none";
    internal ZDOID LastRequester;
    internal float LastRequestTime;
    internal Vector3 LastStart, LastTarget;
    private string movementState = "not started";
    private float walkedMeters;
    private ZDOID walkingRequester;
    private Coffin? home;
    private int candidateCursor, replans;
    private float nextReplan;
    private Vector3 blockedTarget;
    private bool hasBlockedTarget;
    private static readonly float[] RetreatAngles = { 0, -35, 35, -70, 70, -110, 110, -145, 145, 180 };
    internal string MovementOutcome => movementState + "; moved " + (yielding ? Vector3.Distance(actor.transform.position, LastStart) : walkedMeters).ToString("F2") + "m";
    private string lastLoggedRejection = "";
    private float nextRejectionLog;

    private void Reject(string reason)
    {
        LastRejection = reason;
        if (reason == lastLoggedRejection && Time.time < nextRejectionLog) return;
        lastLoggedRejection = reason; nextRejectionLog = Time.time + 10f;
        Plugin.Instance.LogInfo("SHOO rejected: " + LastRequester + " -> " + view.GetZDO().m_uid + ": " + reason);
    }

    private void OnDisable() { if (!ReferenceEquals(ai, null)) Active.Remove(ai); yielding = false; }

    private void Awake()
    {
        ai = GetComponent<MonsterAI>(); actor = GetComponent<Character>(); view = GetComponent<ZNetView>();
        if (view && view.IsValid())
        {
            view.Register<ZDOID>(Rpc, Request);
            view.Register<ZDOID, Vector3>(AreaRpc, RequestArea);
        }
    }

    internal static bool Eligible(MonsterAI? ai)
    {
        if (!ai || ai!.GetComponent<Maid>() || !ai.GetComponent<Tameable>()) return false;
        var actor = ai.GetComponent<Character>(); var tame = ai.GetComponent<Tameable>();
        return actor.IsTamed() && !actor.IsDead() && !actor.IsFlying() && !actor.IsSwimming() && !actor.InAttack()
            && !ai.IsAlerted() && !ai.IsSleeping() && !ai.GetTargetCreature() && !ai.GetFollowTarget() && !tame.HaveRider();
    }

    private void Request(long sender, ZDOID maidId)
    {
        var obj = ZNetScene.instance.FindInstance(maidId);
        if (!obj) return;
        var direction = actor.transform.position - obj.transform.position; direction.y = 0;
        RequestArea(sender, maidId, direction.normalized);
    }

    private void RequestArea(long sender, ZDOID maidId, Vector3 heading)
    {
        if (!view.IsValid() || !view.IsOwner()) return;
        // Keep diagnostics bound to an authenticated nearby requester, just as movement is.
        var obj = ZNetScene.instance.FindInstance(maidId);
        var maid = obj ? obj.GetComponent<Maid>() : null;
        if (!maid || !maid!.View.IsValid() || maid.View.GetZDO().GetOwner() != sender || maid.GetComponent<Character>().IsDead()
            || Mathf.Abs(maid.transform.position.y - actor.transform.position.y) > 1.5f
            || !TameCrowdPolicy.InShooCone(actor.transform.position.x - maid.transform.position.x,
                actor.transform.position.z - maid.transform.position.z, heading.x, heading.z, Plugin.TameShooRadius.Value,
                Mathf.Cos(Plugin.TameShooAngle.Value * .5f * Mathf.Deg2Rad))) return;
        LastRequester = maidId; LastRequestTime = Time.time;
        if (!Plugin.TameCrowdEnabled.Value || !Plugin.TameShooEnabled.Value || !SessionSupport.Active) { Reject("disabled or unsupported session"); return; }
        var cooldownSeconds = Mathf.Max(cooldown - Time.time,
            (float)((view.GetZDO().GetLong("BH_nextShoo") - ZNet.instance.GetTime().Ticks) / (double)System.TimeSpan.TicksPerSecond));
        if (cooldownSeconds > 0) { Reject("tame cooldown active"); return; }
        if (!Eligible(ai)) { Reject("ineligible tame: tamed=" + actor.IsTamed() + " alerted=" + ai.IsAlerted() + " sleeping=" + ai.IsSleeping() + " target=" + (bool)ai.GetTargetCreature() + " following=" + (bool)ai.GetFollowTarget()); return; }
        var homeObj = ZNetScene.instance.FindInstance(maid.View.GetZDO().GetZDOID(Maid.HomeKey));
        home = homeObj ? homeObj.GetComponent<Coffin>() : null;
        if (!home) { Reject("coffin not loaded"); return; }
        if (DoorTransit.EnemiesNear(actor, actor.transform.position, maid.transform.position)) { Reject("nearby enemy"); return; }
        requester = maid.GetComponent<Character>();
        candidateCursor = 0; replans = 0; hasBlockedTarget = false; nextReplan = 0;
        if (!PlanRetreat(out var failure)) { Reject(failure); return; }
        LastStart = actor.transform.position;
        until = Time.time + 8f; cooldown = Time.time + Plugin.TameShooCooldown.Value; yielding = true;
        movementState = "walking"; walkedMeters = 0; walkingRequester = maidId;
        LastRejection = "accepted"; Active[ai] = this;
        view.GetZDO().Set("BH_nextShoo", ZNet.instance.GetTime().AddSeconds(Plugin.TameShooCooldown.Value).Ticks);
        view.GetZDO().Set("BH_shooAccepted", view.GetZDO().GetInt("BH_shooAccepted") + 1);
        Plugin.Instance.LogInfo("SHOO accepted: " + maidId + " -> " + view.GetZDO().m_uid
            + " distance=" + Vector3.Distance(actor.transform.position, target).ToString("F1") + " alternate search");
    }

    private bool PlanRetreat(out string failure)
    {
        failure = "requester or coffin unloaded";
        if (!requester || !home || !home!.Piece) return false;
        var start = actor.transform.position;
        var away = start - requester!.transform.position; away.y = 0;
        if (away.sqrMagnitude < .01f) { failure = "overlapping centers"; return false; }
        away.Normalize();
        var failures = new List<string>();
        var distances = new[] { Mathf.Clamp(Plugin.TameShooDistance.Value, 3.5f, 5f), 3.5f, 5f }.Distinct().ToArray();
        var count = RetreatAngles.Length * distances.Length;
        var first = candidateCursor;
        for (var attempt = 0; attempt < count; attempt++)
        {
            var index = (first + attempt) % count;
            var angle = RetreatAngles[index / distances.Length];
            var distance = distances[index % distances.Length];
            var point = start + Quaternion.Euler(0, angle, 0) * away * distance;
            var label = angle + "deg/" + distance.ToString("F1") + "m: ";
            if (hasBlockedTarget && (point - blockedTarget).sqrMagnitude < 1f) continue;
            if (yielding && (point - LastStart).sqrMagnitude > 25f) continue;
            var fromMaid = start - requester.transform.position; var toMaid = point - requester.transform.position;
            if (!TameCrowdPolicy.AvoidsMaid(fromMaid.x, fromMaid.z, toMaid.x, toMaid.z, actor.GetRadius() + requester.GetRadius() + .15f))
            { failures.Add(label + "would cross maid"); continue; }
            if (!CrowdGeometry.Floor(point, actor.transform.position.y, out point)) { failures.Add(label + "no destination floor"); continue; }
            if (!CrowdGeometry.ClearReason(actor, start, point, requester, out var clearance)) { failures.Add(label + clearance); continue; }
            if (CrowdGeometry.CrossesDoorReason(actor.transform.position, point, out var doorReason)) { failures.Add(label + doorReason); continue; }
            if (!Maid.Allowed(point, home!.Piece.GetCreator())) { failures.Add(label + "ward permission at destination"); continue; }
            if (!MaintenancePolicy.InRadius((point - home.transform.position).sqrMagnitude, Plugin.Radius.Value)) { failures.Add(label + "outside coffin work radius"); continue; }
            var permitted = true;
            var direction = (point - start).normalized;
            for (var step = .5f; step < distance; step += .5f)
                if (!Maid.Allowed(start + direction * step, home.Piece.GetCreator())) { permitted = false; break; }
            if (!permitted) { failures.Add(label + "ward on departure route"); continue; }
            if (Active.Values.Any(other => other != this && other.yielding
                && (other.target - point).sqrMagnitude < Mathf.Pow(actor.GetRadius() + other.actor.GetRadius() + .4f, 2)))
            { failures.Add(label + "retreat destination reserved"); continue; }
            target = point; LastTarget = point; candidateCursor = (index + 1) % count;
            failure = "clear"; return true;
        }
        failure = "no safe retreat (" + count + " candidates): " + string.Join("; ", failures.Take(6));
        return false;
    }

    internal void Tick()
    {
        if (!yielding) return;
        if (!Plugin.TameCrowdEnabled.Value || !Plugin.TameShooEnabled.Value || !view.IsValid() || !view.IsOwner())
        { StopYield("cancelled: disabled/session ownership"); return; }
        if (Time.time >= until) { StopYield("cancelled: eight-second deadline"); return; }
        if (!Eligible(ai)) { StopYield("cancelled: tame no longer eligible"); return; }
        if (DoorTransit.EnemiesNear(actor, actor.transform.position, actor.transform.position)) { StopYield("cancelled: nearby enemy"); return; }
        var offset = target - actor.transform.position; offset.y = 0;
        if (offset.magnitude < .35f) { StopYield("completed"); ai.StopMoving(); return; }
        if (!requester || !home) { StopYield("cancelled: requester or coffin unloaded"); return; }
        var fromMaid = actor.transform.position - requester!.transform.position;
        var toMaid = target - requester.transform.position;
        var avoidsMaid = TameCrowdPolicy.AvoidsMaid(fromMaid.x, fromMaid.z, toMaid.x, toMaid.z,
            actor.GetRadius() + requester.GetRadius() + .15f);
        var clearance = "maid moved into retreat path";
        if (!avoidsMaid || !CrowdGeometry.ClearReason(actor, actor.transform.position, target, requester, out clearance))
        {
            ai.StopMoving();
            if (Time.time < nextReplan) return;
            nextReplan = Time.time + .5f;
            if (replans++ >= 3) { StopYield("cancelled: three replans exhausted; " + clearance); return; }
            blockedTarget = target; hasBlockedTarget = true;
            if (!PlanRetreat(out var failure)) { movementState = "waiting for alternate: " + failure; return; }
            movementState = "walking alternate " + replans;
            Plugin.Instance.LogInfo("SHOO replan: " + walkingRequester + " -> " + view.GetZDO().m_uid + " attempt=" + replans);
            return;
        }
        // Native character movement and animations, without fear, fleeing,
        // teleportation, ownership claims or alterations to player commands.
        ai.MoveTowards(offset, false);
    }

    private void StopYield(string reason)
    {
        walkedMeters = Vector3.Distance(actor.transform.position, LastStart);
        movementState = reason; yielding = false; Active.Remove(ai);
        Plugin.Instance.LogInfo("SHOO movement: " + walkingRequester + " -> " + (view.IsValid() ? view.GetZDO().m_uid.ToString() : "unloaded") + ": " + MovementOutcome);
    }
}

[HarmonyPatch(typeof(MonsterAI), "Awake")]
internal static class TameYieldRegistration
{
    private static void Postfix(MonsterAI __instance)
    {
        if (__instance.GetComponent<Tameable>() && !__instance.GetComponent<Maid>() && !__instance.GetComponent<TameYield>())
            __instance.gameObject.AddComponent<TameYield>();
    }
}

[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
internal static class TameYieldMovement
{
    private static void Postfix(MonsterAI __instance)
    {
        if (TameYield.Active.TryGetValue(__instance, out var yield)) yield.Tick();
    }
}
