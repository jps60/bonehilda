using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

public sealed class Maid : MonoBehaviour
{
    internal const string HomeKey = "BH_home";
    internal const string ExitingKey = "BH_exiting";
    internal const string ReturningKey = "BH_returning";
    private const string CargoKey = "BH_cargo";
    private const string LastDeliveryKey = "BH_lastDelivery";
    private static readonly MethodInfo RefreshChest = AccessTools.Method(typeof(Container), "CheckForChanges");
    private static readonly FieldInfo WardList = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
    internal ZNetView View { get; private set; } = null!;
    internal bool HasCargo => cargo != null && cargo.GetAllItems().Count > 0;
    internal bool OffDuty => View && View.IsValid() && (View.GetZDO().GetBool(ReturningKey) || View.GetZDO().GetBool("BH_stowing"));
    internal static bool InCoffinPassage(ZDO data) =>
        data.GetBool("BH_entering") || data.GetBool(ExitingKey) || data.GetBool("BH_stowing");
    private Humanoid character = null!;
    private MonsterAI ai = null!;
    private WorkAnimations animations = null!;
    private Inventory cargo = null!;
    private string loadedCargo = "";
    private readonly List<Piece> pieces = new List<Piece>();
    private readonly Dictionary<ZDOID, float> skipped = new Dictionary<ZDOID, float>();
    private readonly Dictionary<string, float> lastSupplied = new Dictionary<string, float>();
    private Job? job;
    private float nextScan;
    private float nextAction;
    private float jobStarted;
    private float nextHomeCheck;
    private bool wasOwner;
    private readonly Housekeeping housekeeping = new Housekeeping();
    private WorkPhase phase;
    private float phaseEnds;
    private int dressLevel = -1;
    private float lastPetRequest = -10f;
    private Coffin? passageHome;
    private readonly List<CollisionPair> passageCollisions = new List<CollisionPair>();
    private sealed class CollisionPair
    {
        internal Collider Maid = null!;
        internal Collider Cabinet = null!;
    }
    private enum WorkPhase { None, Drawing, Repairing, Recovering, Holstering, Supplying, FinishingSupply }

    private sealed class Job
    {
        internal Piece Piece = null!;
        internal WearNTear? Wear;
        internal SupplyTarget? Supply;
        internal ZNetView View = null!;
    }

    private void Awake()
    {
        View = GetComponent<ZNetView>();
        character = GetComponent<Humanoid>();
        ai = GetComponent<MonsterAI>();
        animations = GetComponent<WorkAnimations>();
        cargo = new Inventory("Bonehilda's hands", null!, 1, 1);
        character.m_onDeath += DropCargo;
        View.Register<ZDOID>("BH_ToggleReturn", ToggleReturn);
    }

    private void Start()
    {
        Visuals.Dress(transform);
        // Keep the native interaction/rename behavior, with concise command feedback.
        var tame = GetComponent<Tameable>();
        tame.m_nameBeforeText = false;
        tame.m_tameTextGetter = () => OffDuty ? "On duty" : "Off duty";
        if (View && View.IsValid() && View.IsOwner())
        {
            character.SetTamed(true);
            EnsureDefaultName();
        }
    }

    internal void Bind(Coffin coffin, bool emerge = false)
    {
        View.GetZDO().Set(HomeKey, coffin.View.GetZDO().m_uid);
        View.GetZDO().Set("BH_homePosition", coffin.transform.position);
        character.SetLevel(coffin.Level); // Character level 1/2/3 means zero/one/two stars.
        View.GetZDO().Set(ExitingKey, emerge);
        if (emerge) View.GetZDO().Set("BH_exitStarted", ZNet.instance.GetTime().Ticks);
        character.SetTamed(true);
        EnsureDefaultName();
    }

    private void LateUpdate()
    {
        if (!character || dressLevel == character.GetLevel()) return;
        dressLevel = character.GetLevel();
        DressVisuals.SetTier(transform, dressLevel - 1);
    }

    internal void RequestReturn(Humanoid user) => View.InvokeRPC("BH_ToggleReturn", user.GetZDOID());

    private void ToggleReturn(long sender, ZDOID playerId)
    {
        if (!View.IsValid() || !View.IsOwner() || character.IsDead() || Time.time - lastPetRequest < 0.5f) return;
        var playerObject = ZNetScene.instance.FindInstance(playerId);
        var player = playerObject ? playerObject.GetComponent<Player>() : null;
        if (!player || player!.GetComponent<ZNetView>().GetZDO().GetOwner() != sender || Vector3.Distance(player.transform.position, transform.position) > 4f) return;
        lastPetRequest = Time.time;
        if (View.GetZDO().GetBool("BH_stowing"))
        {
            var homeObject = ZNetScene.instance.FindInstance(View.GetZDO().GetZDOID(HomeKey));
            var coffin = homeObject ? homeObject.GetComponent<Coffin>() : null;
            if (coffin)
            {
                if (!coffin!.View.IsOwner()) coffin.View.ClaimOwnership();
                coffin.Resume();
            }
        }
        SetReturning(!View.GetZDO().GetBool(ReturningKey));
    }

    private void SetReturning(bool returning)
    {
        var data = View.GetZDO();
        data.Set(ReturningKey, returning);
        if (!returning && data.GetBool("BH_entering"))
        {
            data.Set(ExitingKey, true);
            data.Set("BH_exitStarted", ZNet.instance.GetTime().Ticks);
        }
        else data.Set(ExitingKey, false);
        data.Set("BH_entering", false);
        CancelJob();
    }

    internal void SaveStored(ZDO home)
    {
        home.Set("BH_storedHealth", character.GetHealth());
        home.Set("BH_storedName", View.GetZDO().GetString("TamedName"));
        home.Set("BH_storedNameAuthor", View.GetZDO().GetString("TamedNameAuthor"));
        home.Set("BH_storedLastDelivery", View.GetZDO().GetLong(LastDeliveryKey));
    }

    internal void RestoreStored(ZDO home)
    {
        character.SetHealth(Mathf.Clamp(home.GetFloat("BH_storedHealth", character.GetMaxHealth()), 0.1f, character.GetMaxHealth()));
        View.GetZDO().Set("TamedName", home.GetString("BH_storedName", "Bonehilda"));
        View.GetZDO().Set("TamedNameAuthor", home.GetString("BH_storedNameAuthor"));
        View.GetZDO().Set(LastDeliveryKey, home.GetLong("BH_storedLastDelivery"));
    }

    private void EnsureDefaultName()
    {
        var data = View.GetZDO();
        if (data.GetBool("BH_defaultNameSet")) return;
        var current = data.GetString("TamedName");
        if (string.IsNullOrEmpty(current) || Plugin.LegacySkeletonNames.Contains(current))
        {
            data.Set("TamedName", "Bonehilda");
            data.Set("TamedNameAuthor", "host");
        }
        data.Set("BH_defaultNameSet", true);
    }

    private void Update()
    {
        if (!View || !View.IsValid() || !View.IsOwner() || character.IsDead())
        {
            SetCoffinPassage(null);
            wasOwner = false;
            return;
        }
        if (!wasOwner)
        {
            // Discard local plans after an ownership handoff; committed cargo/cooldown live in the ZDO.
            job = null;
            phase = WorkPhase.None;
            animations.SetHammer(false, false);
            housekeeping.Cancel(View);
            wasOwner = true;
        }
        EnsureDefaultName();
        LoadCargo();
        var homeId = View.GetZDO().GetZDOID(HomeKey);
        if (homeId == ZDOID.None) { SetCoffinPassage(null); CancelJob(); return; }
        var homeObject = ZNetScene.instance.FindInstance(homeId);
        if (!homeObject)
        {
            SetCoffinPassage(null);
            CancelJob();
            if (Time.time >= nextHomeCheck)
            {
                nextHomeCheck = Time.time + 10f;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), Network.CheckRpc, View.GetZDO().m_uid, homeId);
            }
            return;
        }
        var home = homeObject.GetComponent<Coffin>();
        if (!home || !home.View.IsValid()) { SetCoffinPassage(null); CancelJob(); return; }
        // A duplicate left by an interrupted spawn can never perform work.
        if (home.View.GetZDO().GetZDOID(Coffin.MaidKey) != View.GetZDO().m_uid)
        {
            SetCoffinPassage(null);
            CancelJob();
            // The two ZDOs can arrive in different packets. Let the server confirm an orphan.
            if (Time.time >= nextHomeCheck)
            {
                nextHomeCheck = Time.time + 10f;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), Network.CheckRpc, View.GetZDO().m_uid, homeId);
            }
            return;
        }
        var delta = Time.deltaTime;
        SetCoffinPassage(InCoffinPassage(View.GetZDO()) ? home : null);
        if (character.GetLevel() != home.Level) character.SetLevel(home.Level);
        if (View.GetZDO().GetBool("BH_stowing"))
        {
            ai.StopMoving();
            if (home.View.GetZDO().GetBool(Coffin.DismissedKey)) return;
            View.GetZDO().Set("BH_stowing", false);
            View.GetZDO().Set(ReturningKey, false);
            View.GetZDO().Set("BH_entering", false);
            View.GetZDO().Set(ExitingKey, true);
            View.GetZDO().Set("BH_exitStarted", ZNet.instance.GetTime().Ticks);
        }
        if (View.GetZDO().GetBool(ReturningKey)) { ReturnToCoffin(home, delta); return; }
        if (View.GetZDO().GetBool(ExitingKey))
        {
            var exit = home.ExitPosition;
            var direction = exit - transform.position;
            direction.y = 0;
            var elapsed = (ZNet.instance.GetTime().Ticks - View.GetZDO().GetLong("BH_exitStarted")) / (double)TimeSpan.TicksPerSecond;
            if (!home.DoorOpen) { ai.StopMoving(); return; }
            if (direction.magnitude > (elapsed > 7 ? .65f : .3f) && Vector3.Distance(transform.position, home.transform.position) < 5f)
            {
                if (elapsed > 7) ai.MoveTo(delta, exit, .25f, false);
                else ai.MoveTowards(direction, false);
                return;
            }
            View.GetZDO().Set(ExitingKey, false);
            SetCoffinPassage(null);
            nextScan = Time.time;
        }
        if (!MaintenancePolicy.InRadius((transform.position - home.transform.position).sqrMagnitude, Plugin.Radius.Value + 1f))
        {
            CancelJob();
            ai.MoveTo(delta, home.transform.position, 2f, false);
            return;
        }

        if (phase != WorkPhase.None)
        {
            UpdateWork(home);
            return;
        }
        if (Time.time < nextAction) { ai.StopMoving(); return; }
        if (job != null && (!Valid(job, home) || Time.time - jobStarted > Plugin.JobTimeout.Value)) CancelJob();
        if (job == null && cargo.GetAllItems().Count > 0)
        {
            var access = WorkPoint(home.Piece);
            if (WalkTo(access, delta) && CanReach(home.Piece, access)) ReturnCargo(home);
            return;
        }
        if (job == null && Time.time >= nextScan)
        {
            nextScan = Time.time + Plugin.ScanInterval.Value;
            job = FindJob(home);
            jobStarted = Time.time;
            if (job != null) housekeeping.Cancel(View);
        }
        if (job == null)
        {
            if (Plugin.Wander.Value)
            {
                ai.m_randomMoveRange = MaintenancePolicy.WanderRange(Plugin.WanderRadius.Value, Plugin.Radius.Value);
                if (housekeeping.Tick(this, ai, home, pieces, delta)) return;
                ai.RandomMovement(delta, home.transform.position, false);
            }
            else
            {
                housekeeping.Cancel(View);
                if (Vector3.Distance(transform.position, home.transform.position) > 3f) ai.MoveTo(delta, home.transform.position, 2f, false);
                else ai.StopMoving();
            }
            return;
        }
        if (job.Supply != null && cargo.GetAllItems().Count == 0)
        {
            var access = WorkPoint(home.Piece);
            if (WalkTo(access, delta) && CanReach(home.Piece, access)) PickUpSupply(home, job.Supply);
            return;
        }
        var port = job.Supply?.Port;
        var point = port ? port!.position : WorkPoint(job.Piece);
        if (!WalkTo(point, delta) || !CanReach(job.Piece, point) || Time.time < nextAction) return;
        ai.LookAt(point);
        if (job.Wear)
        {
            animations.SetHammer(true);
            SetPhase(WorkPhase.Drawing, 1f);
        }
        else if (job.Supply != null && SupplyReady())
        {
            animations.Interact();
            SetPhase(WorkPhase.Supplying, 0.25f);
        }
    }

    private void ReturnToCoffin(Coffin home, float delta)
    {
        if (job != null || phase != WorkPhase.None) CancelJob();
        if (Time.time < nextAction) { ai.StopMoving(); return; }
        var data = View.GetZDO();
        if (!data.GetBool("BH_entering"))
        {
            var front = home.ApproachPosition;
            var distance = front - transform.position; distance.y = 0;
            var local = home.transform.InverseTransformPoint(transform.position);
            // BaseAI stops within at least 0.5 m, even when asked for 0.2 m.
            // Accept a wider staging area and steer directly for the final few steps.
            if (distance.magnitude > 0.85f || Mathf.Abs(local.x) > 0.55f || Mathf.Abs(local.y) > 0.8f)
            {
                // The final navmesh node can end short of a coffin on player-built flooring.
                // Walk the remaining clear stretch, but keep pathfinding around furniture.
                var clearApproach = distance.sqrMagnitude <= 16f && Mathf.Abs(local.y) <= .8f
                    && !Physics.SphereCast(transform.position + Vector3.up * .5f, .28f, distance.normalized,
                        out _, distance.magnitude, LayerMask.GetMask("piece", "terrain", "static_solid"));
                if (clearApproach || MaintenancePolicy.NearCoffinApproach(local.x, local.y, local.z)) ai.MoveTowards(distance, false);
                else ai.MoveTo(delta, front, 0.65f, false);
                return;
            }
            ai.StopMoving();
            if (home.Chest.IsInUse()) return;
            if (cargo.GetAllItems().Count > 0) { ReturnCargo(home); return; }
            if (!home.View.IsOwner()) home.View.ClaimOwnership();
            if (!home.View.IsOwner()) return;
            home.HoldDoor();
            if (!home.DoorOpen) return;
            data.Set("BH_entering", true);
            SetCoffinPassage(home);
        }
        if (!home.View.IsOwner()) home.View.ClaimOwnership();
        if (!home.View.IsOwner()) { ai.StopMoving(); return; }
        home.HoldDoor();
        var direction = home.InteriorPosition - transform.position; direction.y = 0;
        var inside = home.transform.InverseTransformPoint(transform.position);
        if (!MaintenancePolicy.InsideCoffin(inside.x, inside.y, inside.z)) { ai.MoveTowards(direction, false); return; }
        ai.StopMoving();
        home.Store(this);
    }

    private void SetCoffinPassage(Coffin? home)
    {
        if (passageHome == home) return;
        foreach (var pair in passageCollisions)
            if (pair.Maid && pair.Cabinet) Physics.IgnoreCollision(pair.Maid, pair.Cabinet, false);
        passageCollisions.Clear();
        passageHome = home;
        if (!home) return;
        // Imported capsule sizes can exceed the cabinet's interior depth. Only
        // ignore this coffin's frame/door during passage; keep floors, the ramp,
        // terrain, and every other structure solid.
        foreach (var body in GetComponentsInChildren<Collider>())
        {
            if (!body.enabled || body.isTrigger) continue;
            foreach (var cabinet in home!.GetComponentsInChildren<Collider>())
            {
                if (!cabinet.enabled || cabinet.isTrigger || cabinet.name == "BH_CoffinFloor" || cabinet.name == "BH_EntryRamp") continue;
                Physics.IgnoreCollision(body, cabinet, true);
                passageCollisions.Add(new CollisionPair { Maid = body, Cabinet = cabinet });
            }
        }
    }

    private void OnDestroy() => SetCoffinPassage(null);

    private bool SupplyReady() => MaintenancePolicy.SupplyReady(ZNet.instance.GetTime().Ticks,
        View.GetZDO().GetLong(LastDeliveryKey, 0), Plugin.SupplyInterval.Value);

    private void SetPhase(WorkPhase value, float seconds)
    {
        phase = value;
        phaseEnds = Time.time + seconds;
    }

    private void UpdateWork(Coffin home)
    {
        ai.StopMoving();
        if (job == null || !job.Piece || !job.View || !job.View.IsValid()) { CancelJob(); return; }
        var port = job.Supply?.Port;
        var point = port ? port!.position : WorkPoint(job.Piece);
        ai.LookAt(point);
        if (Time.time < phaseEnds) return;
        switch (phase)
        {
            case WorkPhase.Drawing:
                if (!Valid(job, home) || !WithinWorkReach(job.Piece, point)) { CancelJob(); return; }
                animations.Repair();
                SetPhase(WorkPhase.Repairing, 0.25f);
                break;
            case WorkPhase.Repairing:
                // Commit at the swing, after checking the target again. No combat attack is started.
                if (Valid(job, home) && WithinWorkReach(job.Piece, point) && job.Wear!.Repair()) WorkSounds.Repair(job.Piece);
                nextAction = Time.time + Plugin.WorkInterval.Value;
                SetPhase(WorkPhase.Recovering, 0.35f);
                break;
            case WorkPhase.Recovering:
                animations.SetHammer(false);
                SetPhase(WorkPhase.Holstering, 1f);
                break;
            case WorkPhase.Supplying:
                if (Valid(job, home) && WithinWorkReach(job.Piece, point) && SupplyReady()) DeliverSupply(home, job);
                SetPhase(WorkPhase.FinishingSupply, 0.3f);
                break;
            default:
                CancelJob();
                break;
        }
    }

    private void DeliverSupply(Coffin home, Job delivery)
    {
        // Inventory and native owner handler run synchronously on one peer, with capacity rechecked.
        if (home.Chest.IsInUse()) return;
        if (!home.View.IsOwner()) home.View.ClaimOwnership();
        if (!home.View.IsOwner()) return;
        if (!delivery.View.IsOwner()) delivery.View.ClaimOwnership();
        if (!delivery.View.IsOwner() || !Valid(delivery, home)) return;
        var item = cargo.GetAllItems().FirstOrDefault();
        if (item == null || !delivery.Supply!.Matches(item)) return;
        if (!delivery.Supply.Deliver(item)) return;
        cargo.RemoveItem(item, 1);
        SaveCargo();
        View.GetZDO().Set(LastDeliveryKey, ZNet.instance.GetTime().Ticks);
        lastSupplied[SupplyKey(delivery)] = Time.time;
        WorkSounds.Supplied(delivery.Supply);
    }

    private Job? FindJob(Coffin home)
    {
        pieces.Clear();
        Piece.GetAllPiecesInRadius(home.transform.position, Plugin.Radius.Value, pieces);
        Job? best = null;
        var bestDistance = float.MaxValue;
        foreach (var piece in pieces)
        {
            if (!piece || !piece.IsPlacedByPlayer() || !Allowed(piece.transform.position, home.Piece.GetCreator())) continue;
            var view = piece.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) continue;
            if (skipped.TryGetValue(view.GetZDO().m_uid, out var until) && Time.time < until) continue;
            var wear = piece.GetComponent<WearNTear>();
            var candidate = new Job { Piece = piece, View = view };
            candidate.Supply = SupplyTarget.ForPiece(piece)
                .Where(option => Enabled(option) && option.NeedsItem && home.Chest.GetInventory().GetAllItems().Any(option.Matches))
                .OrderBy(option => lastSupplied.TryGetValue(view.GetZDO().m_uid + ":" + option.Kind, out var time) ? time : float.MinValue)
                .FirstOrDefault();
            if (candidate.Supply == null)
            {
                if (Plugin.Repair.Value && wear && MaintenancePolicy.NeedsRepair(wear.GetHealthPercentage())) candidate.Wear = wear;
                else continue;
            }
            var distance = (piece.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        // Bound the temporary cooldown cache in long-running worlds.
        foreach (var key in skipped.Where(x => x.Value < Time.time).Select(x => x.Key).ToArray()) skipped.Remove(key);
        if (lastSupplied.Count > 1024) lastSupplied.Clear();
        return best;
    }

    private bool Valid(Job candidate, Coffin home) => candidate.Piece && candidate.View && candidate.View.IsValid()
        && candidate.Piece.IsPlacedByPlayer()
        && MaintenancePolicy.InRadius((candidate.Piece.transform.position - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
        && Allowed(candidate.Piece.transform.position, home.Piece.GetCreator())
        && ((candidate.Wear && Plugin.Repair.Value && MaintenancePolicy.NeedsRepair(candidate.Wear!.GetHealthPercentage()))
            || (candidate.Supply != null && Enabled(candidate.Supply) && candidate.Supply.NeedsItem));

    private static bool Enabled(SupplyTarget target) => target.Kind == SupplyTarget.SupplyKind.FireplaceFuel
        ? Plugin.Refuel.Value : Plugin.SupplyMachines.Value;
    private static string SupplyKey(Job job) => job.View.GetZDO().m_uid + ":" + job.Supply!.Kind;

    internal static bool Allowed(Vector3 point, long playerId)
    {
        // Evaluate permissions for the coffin's builder, including on a headless server.
        var wards = (List<PrivateArea>)WardList.GetValue(null);
        foreach (var ward in wards)
        {
            if (!ward || !ward.IsEnabled() || (ward.transform.position - point).sqrMagnitude > ward.m_radius * ward.m_radius) continue;
            var piece = ward.GetComponent<Piece>();
            if ((!piece || piece.GetCreator() != playerId) && !ward.IsPermitted(playerId)) return false;
        }
        return true;
    }

    private bool WalkTo(Vector3 point, float dt)
    {
        if (Vector3.Distance(transform.position, point) <= 2.4f)
        {
            ai.StopMoving();
            return true;
        }
        ai.MoveTo(dt, point, 1.5f, false);
        return false;
    }

    private Vector3 WorkPoint(Piece piece)
    {
        var colliders = piece.GetComponentsInChildren<Collider>();
        var point = piece.transform.position;
        var distance = (point - transform.position).sqrMagnitude;
        foreach (var collider in colliders)
        {
            if (!collider.enabled || collider.isTrigger) continue;
            var candidate = collider.ClosestPoint(transform.position + Vector3.up);
            var d = (candidate - transform.position).sqrMagnitude;
            if (d < distance) { point = candidate; distance = d; }
        }
        return point;
    }

    internal bool CanReach(Piece piece, Vector3 point)
    {
        if (Mathf.Abs(point.y - transform.position.y) > 2.8f) return false;
        if (!Physics.Linecast(transform.position + Vector3.up, point, out var hit, LayerMask.GetMask("piece", "terrain", "static_solid"))) return true;
        return hit.collider.GetComponentInParent<Piece>() == piece;
    }

    private bool WithinWorkReach(Piece piece, Vector3 point) =>
        Vector3.Distance(transform.position, point) <= 2.6f && CanReach(piece, point);

    private void PickUpSupply(Coffin home, SupplyTarget supply)
    {
        if (home.Chest.IsInUse()) return;
        if (!home.View.IsOwner()) home.View.ClaimOwnership();
        if (!home.View.IsOwner()) return;
        RefreshChest.Invoke(home.Chest, null);
        var inventory = home.Chest.GetInventory();
        var item = inventory.GetAllItems().FirstOrDefault(supply.Matches);
        if (item == null) { CancelJob(); return; }
        if (cargo.MoveItemToThis(inventory, item, 1, 0, 0))
        {
            SaveCargo();
            home.HoldDoor();
            ai.LookAt(home.transform.position);
            animations.Interact();
            nextAction = Time.time + 0.55f;
            jobStarted = Time.time; // Give the outbound walk its own timeout after collection.
        }
        else CancelJob();
    }

    private void ReturnCargo(Coffin home)
    {
        if (home.Chest.IsInUse()) return;
        if (!home.View.IsOwner()) home.View.ClaimOwnership();
        if (!home.View.IsOwner()) return;
        RefreshChest.Invoke(home.Chest, null);
        var item = cargo.GetAllItems().FirstOrDefault();
        if (item == null) return;
        if (!home.Chest.GetInventory().CanAddItem(item)) { DropCargo(); return; }
        home.Chest.GetInventory().MoveItemToThis(cargo, item);
        SaveCargo();
        home.HoldDoor();
        ai.LookAt(home.transform.position);
        animations.Interact();
        nextAction = Time.time + 0.55f;
    }

    private void CancelJob()
    {
        housekeeping.Cancel(View);
        if (phase == WorkPhase.Drawing || phase == WorkPhase.Repairing || phase == WorkPhase.Recovering)
            animations.SetHammer(false);
        phase = WorkPhase.None;
        if (job != null && job.View && job.View.IsValid()) skipped[job.View.GetZDO().m_uid] = Time.time + Mathf.Max(10f, Plugin.ScanInterval.Value * 2f);
        job = null;
        ai.StopMoving();
    }

    private void LoadCargo()
    {
        var serialized = View.GetZDO().GetString(CargoKey, "");
        if (serialized == loadedCargo) return;
        cargo.RemoveAll();
        if (serialized.Length > 0) cargo.Load(new ZPackage(serialized));
        loadedCargo = serialized;
    }

    private void SaveCargo()
    {
        var package = new ZPackage();
        cargo.Save(package);
        loadedCargo = package.GetBase64();
        View.GetZDO().Set(CargoKey, loadedCargo);
    }

    private void DropCargo()
    {
        if (!View || !View.IsValid() || !View.IsOwner()) return;
        LoadCargo();
        foreach (var item in cargo.GetAllItems().ToArray()) ItemDrop.DropItem(item, item.m_stack, transform.position + Vector3.up, Quaternion.identity);
        cargo.RemoveAll();
        SaveCargo();
    }

    internal void Retire()
    {
        if (!View.IsValid()) return;
        if (!View.IsOwner()) View.ClaimOwnership();
        if (!View.IsOwner()) return;
        DropCargo();
        ZNetScene.instance.Destroy(gameObject);
    }
}
