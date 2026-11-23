using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal sealed class DoorTransit
{
    private const string PendingKey = "BH_openDoor", StageKey = "BH_doorStage", StartKey = "BH_doorStart";
    private const string FromKey = "BH_doorFrom", ExitKey = "BH_doorExit", FailedKey = "BH_doorFailed";
    private const string MaidKey = "BH_doorMaid", LeaseKey = "BH_doorLease";
    private static readonly System.Reflection.MethodInfo CanUse = AccessTools.Method(typeof(Door), "CanInteract");
    private float nextCheck;
    private bool blocked;
    private readonly DoorSwing swing = new DoorSwing();
    private readonly List<Vector3> escapePath = new List<Vector3>();
    private readonly Dictionary<ZDOID, float> avoid = new Dictionary<ZDOID, float>();
    private ZDOID swingDoor;
    private float nextEscape;
    private Vector3 escape;
    private bool hasEscape;
    private float escapeUntil;
    private readonly List<Vector3> failedEscapes = new List<Vector3>();
    internal string BlockedReason { get; private set; } = "";

    internal string Describe(Maid maid)
    {
        var data = maid.View.GetZDO();
        return data.GetZDOID(PendingKey) == ZDOID.None ? "none" : data.GetZDOID(PendingKey) + " " + data.GetString(StageKey);
    }

    internal bool TryBegin(Maid maid, MonsterAI ai, Vector3 goal)
    {
        if (!Plugin.UseDoors.Value) return false;
        if (maid.View.GetZDO().GetZDOID(PendingKey) != ZDOID.None) return true;
        if (Time.time < nextCheck) return blocked;
        nextCheck = Time.time + .2f; blocked = false; BlockedReason = "";
        var point = goal;
        foreach (var node in ai.m_path)
            if ((node - maid.transform.position).sqrMagnitude > .25f) { point = node; break; }
        var direction = point - maid.transform.position; direction.y = 0;
        if (direction.sqrMagnitude < .04f) return false;
        foreach (var hit in Physics.SphereCastAll(maid.transform.position + Vector3.up * .8f, .22f,
                     direction.normalized, Mathf.Min(2.8f, direction.magnitude + .5f), LayerMask.GetMask("piece", "piece_nonsolid", "static_solid"), QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            var door = hit.collider.GetComponentInParent<Door>();
            // A wall in front of a door is not a route through that door.
            if (!door) return false;
            var view = door.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || State(view) != 0) continue;
            blocked = true;
            BlockedReason = "closed " + Utils.GetPrefabName(door.gameObject);
            if (avoid.TryGetValue(view.GetZDO().m_uid, out var retryAt) && Time.time < retryAt)
            { BlockedReason += ": failed crossing deferred"; return true; }
            var piece = door.GetComponentInParent<Piece>();
            var homeObject = ZNetScene.instance.FindInstance(maid.View.GetZDO().GetZDOID(Maid.HomeKey));
            var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
            if (!piece || !piece.IsPlacedByPlayer() || !home || door.m_keyItem || door.m_canNotBeClosed
                || !Maid.Allowed(door.transform.position, home!.Piece.GetCreator()))
            { BlockedReason += ": locked or no permission"; return true; }
            if (!Usable(door) || !Settled(door, "closed")) { BlockedReason += ": animating"; return true; }
            if (EnemiesNear(maid.GetComponent<Character>(), door.transform.position, maid.transform.position))
            { BlockedReason += ": enemy nearby"; return true; }
            var leased = view.GetZDO().GetZDOID(MaidKey);
            if (leased != ZDOID.None && leased != maid.View.GetZDO().m_uid && view.GetZDO().GetLong(LeaseKey) > Now)
            { BlockedReason += ": another maid crossing"; return true; }
            Geometry(door, out var center, out var normal, out var halfDepth);
            var clearance = DoorPolicy.CrossingDistance(maid.GetComponent<Character>().GetRadius(), halfDepth);
            var side = Vector3.Dot(maid.transform.position - center, normal) >= 0 ? 1f : -1f;
            var from = center + normal * side * clearance;
            var exit = center - normal * side * clearance;
            if (!IdleActivities.Floor(from, maid.transform.position.y, out from)
                || !IdleActivities.Floor(exit, maid.transform.position.y, out exit)
                || !MaintenancePolicy.InRadius((exit - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
                || !Maid.Allowed(exit, home.Piece.GetCreator()) || !RepairGeometry.ClearDoorway(maid, from, exit, door))
            { BlockedReason += ": blocked or unsupported threshold"; return true; }
            if (!view.IsOwner()) view.ClaimOwnership();
            if (!view.IsOwner() || State(view) != 0 || EnemiesNear(maid.GetComponent<Character>(), center, maid.transform.position)) return true;
            var data = maid.View.GetZDO();
            swing.Capture(door); swingDoor = view.GetZDO().m_uid; hasEscape = false; nextEscape = 0; failedEscapes.Clear();
            data.Set("BH_doorCenter", center); data.Set("BH_doorNormal", normal); data.Set("BH_doorExpectedSide", -side);
            data.Set(PendingKey, view.GetZDO().m_uid); data.Set(StageKey, "opening"); data.Set(StartKey, Now);
            data.Set(FromKey, from); data.Set(ExitKey, exit); data.Set(FailedKey, false);
            view.GetZDO().Set(MaidKey, data.m_uid);
            Refresh(view);
            maid.StopNavigation(); ai.StopMoving();
            maid.GetComponent<WorkAnimations>().Interact();
            view.InvokeRPC("UseDoor", Vector3.Dot(door.transform.forward, maid.transform.position - door.transform.position) < 0);
            return true;
        }
        return false;
    }

    internal bool Tick(Maid maid, MonsterAI ai, float delta)
    {
        var data = maid.View.GetZDO();
        var id = data.GetZDOID(PendingKey);
        if (id == ZDOID.None) return false;
        var instance = ZNetScene.instance.FindInstance(id);
        var door = instance ? instance.GetComponent<Door>() : null;
        if (!door) { Finish(maid, false); return false; }
        var view = door!.GetComponent<ZNetView>();
        if (!view || !view.IsValid()) { Finish(maid, false); return false; }
        if (!view.IsOwner()) view.ClaimOwnership();
        if (!view.IsOwner()) { ai.StopMoving(); return true; }
        Refresh(view);
        if (data.GetFloat("BH_doorExpectedSide") == 0)
        {
            var from = data.GetVec3(FromKey, maid.transform.position);
            var exit = data.GetVec3(ExitKey, door.transform.position);
            var normal = door.transform.forward; normal.y = 0; normal.Normalize();
            data.Set("BH_doorCenter", (from + exit) * .5f); data.Set("BH_doorNormal", normal);
            data.Set("BH_doorExpectedSide", Vector3.Dot(exit - from, normal) >= 0 ? 1f : -1f);
        }
        if (swingDoor != id) { swing.Capture(door); swingDoor = id; hasEscape = false; }
        var stage = data.GetString(StageKey);
        var elapsed = (Now - data.GetLong(StartKey)) / (double)TimeSpan.TicksPerSecond;
        if (State(view) == 0)
        {
            ai.StopMoving();
            if (!Settled(door, "closed") || (stage == "opening" && elapsed < 3)) return true;
            var center = data.GetVec3("BH_doorCenter", door.transform.position);
            var normal = data.GetVec3("BH_doorNormal", door.transform.forward);
            var sideOk = DoorPolicy.ReachedSide(Vector3.Dot(maid.transform.position - center, normal),
                data.GetFloat("BH_doorExpectedSide"), maid.GetComponent<Character>().GetRadius());
            Finish(maid, data.GetBool(FailedKey) || !sideOk);
            return false;
        }
        if (stage == "opening")
        {
            ai.StopMoving();
            if (!Settled(door, "open")) return true;
            data.Set(StageKey, "crossing"); data.Set(StartKey, Now); stage = "crossing";
            data.Set("BH_doorsOpened", data.GetInt("BH_doorsOpened") + 1);
            Log(maid, door, "opened");
            elapsed = 0;
            ai.m_path.Clear(); ai.m_lastFindPathTime = -100f;
        }
        if (stage == "crossing" && (elapsed > 10 || EnemiesNear(maid.GetComponent<Character>(), door.transform.position, maid.transform.position)))
        {
            var from = data.GetVec3(FromKey, maid.transform.position); var exit = data.GetVec3(ExitKey, maid.transform.position);
            data.Set(ExitKey, (maid.transform.position - from).sqrMagnitude < (maid.transform.position - exit).sqrMagnitude ? from : exit);
            var center = data.GetVec3("BH_doorCenter", door.transform.position);
            var normal = data.GetVec3("BH_doorNormal", door.transform.forward);
            data.Set("BH_doorExpectedSide", Mathf.Sign(Vector3.Dot(data.GetVec3(ExitKey, from) - center, normal)));
            data.Set(StageKey, "retreating"); data.Set(StartKey, Now); data.Set(FailedKey, true); stage = "retreating";
            elapsed = 0;
        }
        if (stage == "retreating" && DoorPolicy.RetreatExpired(elapsed))
        {
            data.Set(StageKey, "clearing"); stage = "clearing"; hasEscape = false;
        }
        if (stage == "crossing" || stage == "retreating")
        {
            if (maid.Stepping) { ai.StopMoving(); return true; }
            var target = data.GetVec3(ExitKey, maid.transform.position);
            var offset = target - maid.transform.position; offset.y = 0;
            if (offset.magnitude > .25f)
            {
                maid.StepToward(target, delta);
                var clear = ClearWalk(maid.transform.position, target);
                if (clear) ai.MoveTowards(offset, true); else ai.MoveTo(delta, target, .2f, true);
                return true;
            }
            ai.StopMoving(); data.Set(StageKey, "clearing"); stage = "clearing"; hasEscape = false;
        }
        if (stage == "clearing" || stage == "closing")
        {
            var center = data.GetVec3("BH_doorCenter", door.transform.position);
            var normal = data.GetVec3("BH_doorNormal", door.transform.forward);
            var correctSide = DoorPolicy.ReachedSide(Vector3.Dot(maid.transform.position - center, normal),
                data.GetFloat("BH_doorExpectedSide"), maid.GetComponent<Character>().GetRadius());
            if (!swing.Clear(maid, maid.transform.position) || !correctSide)
            {
                data.Set(StageKey, "clearing");
                if (hasEscape && Time.time >= escapeUntil) { failedEscapes.Add(escape); hasEscape = false; }
                if ((!hasEscape || (escape - maid.transform.position).sqrMagnitude < .09f) && Time.time >= nextEscape)
                {
                    nextEscape = Time.time + 1f;
                    hasEscape = FindEscape(maid, ai, door, center, normal, data.GetFloat("BH_doorExpectedSide"));
                    escapeUntil = Time.time + 6f;
                }
                if (hasEscape)
                {
                    maid.StepToward(escape, delta);
                    if (maid.Stepping) { ai.StopMoving(); return true; }
                    if (RepairGeometry.ClearWalk(maid, escape)) ai.MoveTowards(escape - maid.transform.position, true);
                    else ai.MoveTo(delta, escape, .1f, true);
                }
                else { ai.StopMoving(); BlockedReason = "clearing: no supported stance outside door swing"; }
                return true;
            }
            data.Set(StageKey, "closing");
        }
        ai.StopMoving(); maid.StopNavigation(); ai.LookAt(door.transform.position);
        if (Settled(door, "open") && State(view) != 0)
        {
            maid.GetComponent<WorkAnimations>().Interact();
            view.InvokeRPC("UseDoor", false);
            if (State(view) == 0)
            {
                data.Set("BH_doorsClosed", data.GetInt("BH_doorsClosed") + 1);
                Log(maid, door, "closed");
            }
        }
        return true;
    }

    private static long Now => ZNet.instance.GetTime().Ticks;
    private bool FindEscape(Maid maid, MonsterAI ai, Door door, Vector3 center, Vector3 normal, float side)
    {
        var homeObject = ZNetScene.instance.FindInstance(maid.View.GetZDO().GetZDOID(Maid.HomeKey));
        var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
        if (!home) return false;
        for (var pass = 0; pass < 2; pass++)
        {
            var toward = normal * (pass == 0 ? side : -side);
            foreach (var distance in new[] { 1f, 1.5f, 2f, 3f, swing.Extent + 1f })
                foreach (var angle in new[] { 0f, 35f, -35f, 65f, -65f })
                {
                    var point = center + Quaternion.Euler(0, angle, 0) * toward * distance;
                    if (!IdleActivities.Floor(point, maid.transform.position.y, out point) || !swing.Clear(maid, point)
                        || !DoorPolicy.ReachedSide(Vector3.Dot(point - center, normal), pass == 0 ? side : -side, maid.GetComponent<Character>().GetRadius())
                        || failedEscapes.Any(p => (p - point).sqrMagnitude < .25f)
                        || !MaintenancePolicy.InRadius((point - home!.transform.position).sqrMagnitude, Plugin.Radius.Value)
                        || !Maid.Allowed(point, home.Piece.GetCreator()) || !RepairGeometry.Fits(maid, point)
                        || !DoorRoutePlanner.Segment(maid, ai, maid.transform.position, point, escapePath)) continue;
                    escape = point;
                    if (pass != 0)
                    {
                        maid.View.GetZDO().Set("BH_doorExpectedSide", -side);
                        maid.View.GetZDO().Set(FailedKey, true);
                    }
                    ai.m_path.Clear(); ai.m_path.AddRange(escapePath); ai.m_lastFindPathTarget = point;
                    ai.m_lastFindPathTime = Time.time; ai.m_lastFindPathResult = true;
                    return true;
                }
        }
        return false;
    }
    private static int State(ZNetView view) => view.GetZDO().GetInt("state");
    private static bool Usable(Door door) => (bool)CanUse.Invoke(door, null);
    private static bool Settled(Door door, string tag)
    {
        var animator = door.GetComponentInChildren<Animator>();
        return animator && !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsTag(tag);
    }
    internal static bool CanPlan(Maid maid, Coffin home, Door door)
    {
        var view = door.GetComponent<ZNetView>();
        var piece = door.GetComponentInParent<Piece>();
        return view && view.IsValid() && State(view) == 0 && piece && piece.IsPlacedByPlayer()
            && !door.m_keyItem && !door.m_canNotBeClosed
            && MaintenancePolicy.InRadius((door.transform.position - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
            && Maid.Allowed(door.transform.position, home.Piece.GetCreator());
    }
    private static void Log(Maid maid, Door door, string action)
    {
        if (Plugin.ActivityLogging.Value)
            Plugin.Instance.LogInfo("Bonehilda " + maid.View.GetZDO().m_uid + " " + action + " "
                + Utils.GetPrefabName(door.gameObject) + " [" + door.GetComponent<ZNetView>().GetZDO().m_uid + "]");
    }
    private static void Refresh(ZNetView view)
    {
        if (view.IsOwner() && view.GetZDO().GetLong(LeaseKey) < Now + 10L * TimeSpan.TicksPerSecond)
            view.GetZDO().Set(LeaseKey, Now + 15L * TimeSpan.TicksPerSecond);
    }
    private void Finish(Maid maid, bool failed)
    {
        if (failed) avoid[maid.View.GetZDO().GetZDOID(PendingKey)] = Time.time + 30f;
        hasEscape = false;
        maid.View.GetZDO().Set(PendingKey, ZDOID.None); maid.View.GetZDO().Set(StageKey, "");
        blocked = false; nextCheck = 0; BlockedReason = "";
        maid.StopNavigation();
        if (failed) maid.DoorApproachFailed();
    }

    internal static bool EnemiesNear(Character maid, Vector3 door, Vector3 feet)
    {
        foreach (var other in Character.GetAllCharacters())
            if (other && !other.IsDead() && DoorPolicy.EnemyNear((other.transform.position - door).sqrMagnitude,
                    (other.transform.position - feet).sqrMagnitude, Plugin.DoorEnemyRadius.Value) && BaseAI.IsEnemy(maid, other)) return true;
        return false;
    }

    internal static void Geometry(Door door, out Vector3 center, out Vector3 normal, out float halfDepth)
    {
        var colliders = door.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
        var bounds = colliders.Length == 0 ? new Bounds(door.transform.position + Vector3.up, new Vector3(1, 2, .3f)) : colliders[0].bounds;
        foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
        center = bounds.center; normal = door.transform.forward; normal.y = 0; normal.Normalize();
        halfDepth = 0;
        foreach (var collider in colliders)
        {
            var extent = Vector3.Dot(collider.bounds.extents, new Vector3(Mathf.Abs(normal.x), 0, Mathf.Abs(normal.z)));
            // Project local collider axes so a rotated wide gate does not gain
            // fictitious thickness from its world axis-aligned bounding box.
            Vector3 local;
            if (collider is BoxCollider box) local = box.size * .5f;
            else if (collider is MeshCollider mesh && mesh.sharedMesh) local = mesh.sharedMesh.bounds.extents;
            else { halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(collider.bounds.center - center, normal)) + extent); continue; }
            extent = Mathf.Abs(Vector3.Dot(collider.transform.TransformVector(Vector3.right * local.x), normal))
                + Mathf.Abs(Vector3.Dot(collider.transform.TransformVector(Vector3.up * local.y), normal))
                + Mathf.Abs(Vector3.Dot(collider.transform.TransformVector(Vector3.forward * local.z), normal));
            halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(collider.bounds.center - center, normal)) + extent);
        }
    }

    private static bool ClearWalk(Vector3 from, Vector3 to)
    {
        if (!IdleActivities.Floor((from + to) * .5f, from.y, out _)) return false;
        var offset = to - from;
        return !Physics.SphereCast(from + Vector3.up * .8f, .25f, offset.normalized, out _, offset.magnitude,
            LayerMask.GetMask("piece", "terrain", "static_solid"), QueryTriggerInteraction.Ignore);
    }

    internal static void Watch(Door door)
    {
        var view = door.GetComponent<ZNetView>();
        if (!view || !view.IsValid() || view.GetZDO().GetZDOID(MaidKey) == ZDOID.None) return;
        if (State(view) == 0 && Settled(door, "closed"))
        {
            if (view.IsOwner()) view.GetZDO().Set(MaidKey, ZDOID.None);
            return;
        }
        if (view.GetZDO().GetLong(LeaseKey) > Now || !Usable(door)) return;
        if (!view.IsOwner()) view.ClaimOwnership();
        if (view.IsOwner() && State(view) != 0) view.InvokeRPC("UseDoor", false);
    }
}

internal sealed class MaidDoorWatch : MonoBehaviour
{
    internal static readonly HashSet<Door> Loaded = new HashSet<Door>();
    private void OnEnable() => Loaded.Add(GetComponent<Door>());
    private void OnDisable() => Loaded.Remove(GetComponent<Door>());
    private float nextCheck;
    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 1f;
        DoorTransit.Watch(GetComponent<Door>());
    }
}

[HarmonyPatch(typeof(Door), "Awake")]
internal static class MaidDoorWatchPatch
{
    private static void Postfix(Door __instance)
    {
        if (!__instance.GetComponent<MaidDoorWatch>()) __instance.gameObject.AddComponent<MaidDoorWatch>();
    }
}
