using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

public sealed class Maid : MonoBehaviour
{
    internal static readonly HashSet<Maid> Loaded = new HashSet<Maid>();
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
    private float repairChainUntil;
    private float nextHomeCheck;
    private bool wasOwner;
    private bool retiring;
    private readonly NavigationProgress navigation = new NavigationProgress();
    private bool recoveringRoute;
    private Vector3 recoveryPoint, recoveryGoal;
    private float recoveryUntil;
    private int recoveryAttempts;
    private Vector3 navigationGoal;
    private readonly RepairRetries repairRetries = new RepairRetries();
    private readonly RepairApproach repairApproach = new RepairApproach();
    private bool repairRouteFailed;
    private float idleSpeed;
    private MovementPace pace;
    private readonly IdleActivities idle = new IdleActivities();
    private readonly DoorTransit doors = new DoorTransit();
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
    private void OnEnable() => Loaded.Add(this);
    private void OnDisable() { TaskClaims.Release(this); Loaded.Remove(this); }

    private sealed class Job
    {
        internal Piece Piece = null!;
        internal WearNTear? Wear;
        internal SupplyTarget? Supply;
        internal ZNetView View = null!;
        internal bool Accepted;
    }

    private void Awake()
    {
        View = GetComponent<ZNetView>();
        character = GetComponent<Humanoid>();
        idleSpeed = character.m_speed;
        ai = GetComponent<MonsterAI>();
        animations = GetComponent<WorkAnimations>();
        cargo = new Inventory("Bonehilda's hands", null!, 1, 1);
        character.m_onDeath += () => { TaskClaims.Release(this); DropCargo(); };
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
        StopNavigation();
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
        home.Set("BH_storedNextDust", View.GetZDO().GetLong(IdleActivities.NextVisitKey));
    }

    internal void RestoreStored(ZDO home)
    {
        character.SetHealth(Mathf.Clamp(home.GetFloat("BH_storedHealth", character.GetMaxHealth()), 0.1f, character.GetMaxHealth()));
        View.GetZDO().Set("TamedName", home.GetString("BH_storedName", "Bonehilda"));
        View.GetZDO().Set("TamedNameAuthor", home.GetString("BH_storedNameAuthor"));
        View.GetZDO().Set(LastDeliveryKey, home.GetLong("BH_storedLastDelivery"));
        View.GetZDO().Set(IdleActivities.NextVisitKey, home.GetLong("BH_storedNextDust"));
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
        if (retiring || !View || !View.IsValid() || !View.IsOwner() || character.IsDead())
        {
            StopNavigation();
            SetCoffinPassage(null);
            wasOwner = false;
            return;
        }
        if (!wasOwner)
        {
            nextHomeCheck = Time.time + 10f;
            TaskClaims.Release(this);
            StopNavigation();
            ai.m_randomMoveInterval = Plugin.WanderInterval.Value;
            ai.ResetRandomMovement();
            // Discard local plans after an ownership handoff; committed cargo/cooldown live in the ZDO.
            job = null;
            phase = WorkPhase.None;
            animations.SetHammer(false, false);
            idle.Cancel(this);
            wasOwner = true;
        }
        ClearCombatState();
        EnsureDefaultName();
        LoadCargo();
        SetPace(OffDuty ? MovementPace.Idle : MovementPolicy.Select(job?.Accepted == true, job?.Wear, HasCargo));
        TaskClaims.Refresh(this);
        var homeId = View.GetZDO().GetZDOID(HomeKey);
        // Validate every maid, including unbound legacy/spawned instances and
        // those occupied with a door. Only the server knows unloaded links.
        if (Time.time >= nextHomeCheck)
        {
            nextHomeCheck = Time.time + 10f;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), Network.CheckRpc, View.GetZDO().m_uid, homeId);
            if (retiring || !View || !View.IsValid()) return;
        }
        // Finish a door she opened before resuming work, idle, or dismissal.
        if (doors.Tick(this, ai, Time.deltaTime)) return;
        if (homeId == ZDOID.None) { SetCoffinPassage(null); CancelJob(); return; }
        var homeObject = ZNetScene.instance.FindInstance(homeId);
        if (!homeObject)
        {
            SetCoffinPassage(null);
            CancelJob();
            return;
        }
        var home = homeObject.GetComponent<Coffin>();
        if (!home || !home.View.IsValid()) { SetCoffinPassage(null); CancelJob(); return; }
        // A duplicate left by an interrupted spawn can never perform work.
        if (home.View.GetZDO().GetZDOID(Coffin.MaidKey) != View.GetZDO().m_uid)
        {
            SetCoffinPassage(null);
            CancelJob();
            return;
        }
        var delta = Time.deltaTime;
        SetCoffinPassage(InCoffinPassage(View.GetZDO()) ? home : null);
        if (character.GetLevel() != home.Level) character.SetLevel(home.Level);
        if (View.GetZDO().GetBool("BH_stowing"))
        {
            StopNavigation();
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
            if (!home.DoorOpen) { StopNavigation(); ai.StopMoving(); return; }
            if (direction.magnitude > (elapsed > 7 ? .65f : .3f) && Vector3.Distance(transform.position, home.transform.position) < 5f)
            {
                Navigate(exit, delta, .25f, elapsed <= 7, home);
                return;
            }
            View.GetZDO().Set(ExitingKey, false);
            StopNavigation();
            SetCoffinPassage(null);
            nextScan = Time.time;
        }
        if (!MaintenancePolicy.InRadius((transform.position - home.transform.position).sqrMagnitude, Plugin.Radius.Value + 1f))
        {
            CancelJob();
            Navigate(home.ApproachPosition, delta, 1f);
            return;
        }

        if (phase != WorkPhase.None)
        {
            StopNavigation();
            UpdateWork(home);
            // A repair can hand off directly to the next damaged piece.
            if (phase != WorkPhase.None) return;
        }
        if (job != null && (repairRouteFailed || !Valid(job, home) || Time.time - jobStarted > Plugin.JobTimeout.Value))
        {
            var preserveIdle = !job.Accepted;
            if (!job.Wear || !ContinueRepairs(home)) CancelJob(preserveIdle);
        }
        // Repair cooldown limits swings, not the walk to the next repair.
        // Supply pickups and transfers still finish their interaction pause.
        if (Time.time < nextAction && (job == null || !job.Wear)) { StopNavigation(); ai.StopMoving(); return; }
        if (job == null && cargo.GetAllItems().Count > 0)
        {
            idle.Cancel(this);
            var access = WorkPoint(home.Piece);
            if (WalkTo(access, delta, home.Piece)) ReturnCargo(home);
            return;
        }
        if (job == null && Time.time >= nextScan)
        {
            nextScan = Time.time + Plugin.ScanInterval.Value;
            job = FindJob(home);
            jobStarted = Time.time;
            ResetRepairRoute();
            if (job?.Accepted == true) idle.Cancel(this);
        }
        if (job != null && job.Wear && !job.Accepted)
        {
            // A damaged piece is only a candidate until there is a usable
            // sightline and route. Keep idle animations/claims during searches.
            var ready = WithinRepairReach(job.Piece, RepairPoint(job.Piece));
            var route = ready ? RepairApproach.Result.Ready : repairApproach.Tick(this, ai, home, job.Piece, out _);
            if (route == RepairApproach.Result.Ready && TaskClaims.TryClaim(this, job.Piece, "repair", work: true))
            {
                job.Accepted = true;
                jobStarted = Time.time;
                idle.Cancel(this);
                StopNavigation();
            }
            else
            {
                if (route == RepairApproach.Result.Failed) RetryRepairApproach();
                // Keep the hammer between chained repairs while a brief route
                // search completes, but stow it before resuming cosmetic idles.
                if (animations.HammerDrawn)
                {
                    if (!repairRouteFailed && Time.time < repairChainUntil) { ai.StopMoving(); return; }
                    animations.SetHammer(false);
                }
                TickIdle(home, delta);
                return;
            }
        }
        if (job == null)
        {
            TickIdle(home, delta);
            return;
        }
        SetPace(MovementPolicy.Select(job.Accepted, job.Wear, HasCargo));
        if (job.Supply != null && cargo.GetAllItems().Count == 0)
        {
            var access = WorkPoint(home.Piece);
            if (WalkTo(access, delta, home.Piece)) PickUpSupply(home, job.Supply);
            return;
        }
        var port = job.Supply?.Port;
        var point = job.Wear ? RepairPoint(job.Piece) : port ? port!.position : WorkPoint(job.Piece);
        if (!WalkTo(point, delta, job.Piece, job.Wear) || Time.time < nextAction) return;
        ai.LookAt(point);
        if (job.Wear)
        {
            if (animations.HammerDrawn)
            {
                var facing = point - transform.position; facing.y = 0;
                if (facing.sqrMagnitude > .01f && Vector3.Dot(transform.forward, facing.normalized) < .65f) return;
                SwingHammer();
            }
            else
            {
                animations.SetHammer(true);
                SetPhase(WorkPhase.Drawing, 1f);
            }
        }
        else if (job.Supply != null && SupplyReady())
        {
            animations.Interact();
            SetPhase(WorkPhase.Supplying, 0.25f);
        }
    }

    private void TickIdle(Coffin home, float delta)
    {
        SetPace(MovementPace.Idle);
        if (Plugin.Wander.Value)
        {
            ai.m_randomMoveRange = MaintenancePolicy.WanderRange(Plugin.WanderRadius.Value, Plugin.Radius.Value);
            ai.m_randomMoveInterval = Plugin.WanderInterval.Value;
            if (idle.Tick(this, ai, home, pieces, delta)) return;
            StopNavigation();
            // Our AI patch skips BaseAI.UpdateAI, which advances this timer.
            ai.m_randomMoveUpdateTimer = Mathf.Max(0f, ai.m_randomMoveUpdateTimer - delta);
            ai.RandomMovement(delta, home.transform.position, false);
            character.SetRun(false);
            if (doors.TryBegin(this, ai, ai.m_randomMoveTarget)) ai.StopMoving();
        }
        else
        {
            idle.Cancel(this);
            if (Vector3.Distance(transform.position, home.transform.position) > 3f) Navigate(home.ApproachPosition, delta, 1f);
            else { StopNavigation(); ai.StopMoving(); }
        }
    }

    private void SetPace(MovementPace value)
    {
        pace = value;
        character.m_speed = value == MovementPace.Supply ? Plugin.PlayerWalkSpeed : idleSpeed;
        character.SetRun(false);
    }

    private void ReturnToCoffin(Coffin home, float delta)
    {
        if (job != null || phase != WorkPhase.None) CancelJob();
        if (Time.time < nextAction) { StopNavigation(); ai.StopMoving(); return; }
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
                Navigate(front, delta, .65f, clearApproach || MaintenancePolicy.NearCoffinApproach(local.x, local.y, local.z), home);
                return;
            }
            ai.StopMoving();
            StopNavigation();
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
        if (!home.View.IsOwner()) { StopNavigation(); ai.StopMoving(); return; }
        home.HoldDoor();
        var inside = home.transform.InverseTransformPoint(transform.position);
        if (!MaintenancePolicy.InsideCoffin(inside.x, inside.y, inside.z)) { Navigate(home.InteriorPosition, delta, .02f, true, home); return; }
        StopNavigation();
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

    private void ClearCombatState()
    {
        if (ai.IsAlerted() || View.GetZDO().GetBool("alert"))
        {
            ai.SetAlerted(false);
            // BaseAI's skipped update can leave the network flag different
            // from its local field after a save/load or ownership handoff.
            View.GetZDO().Set("alert", false);
            // Alerted RandomMovement runs and immediately picks another route
            // on arrival. Discard that route when recovering an existing maid.
            ai.ResetRandomMovement();
            ai.StopMoving();
        }
        ai.m_targetCreature = null!;
        ai.m_targetStatic = null!;
        if (ai.HaveTarget()) ai.SetTargetInfo(ZDOID.None);
    }

    private void UpdateWork(Coffin home)
    {
        ai.StopMoving();
        if (job == null || !job.Piece || !job.View || !job.View.IsValid()) { CancelJob(); return; }
        var port = job.Supply?.Port;
        var point = job.Wear ? RepairPoint(job.Piece) : port ? port!.position : WorkPoint(job.Piece);
        ai.LookAt(point);
        if (Time.time < phaseEnds) return;
        switch (phase)
        {
            case WorkPhase.Drawing:
                if (!Valid(job, home) || !WithinRepairReach(job.Piece, point)) { CancelJob(); return; }
                SwingHammer();
                break;
            case WorkPhase.Repairing:
                // Commit at the swing, after checking the target again. No combat attack is started.
                if (Valid(job, home) && WithinRepairReach(job.Piece, point) && job.Wear!.Repair()) WorkSounds.Repair(job.Piece);
                nextAction = Time.time + Plugin.WorkInterval.Value;
                SetPhase(WorkPhase.Recovering, 0.35f);
                break;
            case WorkPhase.Recovering:
                if (ContinueRepairs(home)) break;
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

    private void SwingHammer()
    {
        animations.Repair();
        SetPhase(WorkPhase.Repairing, .25f);
    }

    private bool ContinueRepairs(Coffin home)
    {
        // Do not immediately revisit the piece just attempted (including rain
        // damage or a rejected repair). Keep the existing retry cooldown.
        var wasWorking = job?.Accepted == true;
        SkipCurrentJob();
        var next = FindJob(home, repairsOnly: true);
        nextScan = Time.time;
        if (next == null) return false;
        job = next;
        jobStarted = Time.time;
        if (wasWorking) repairChainUntil = Time.time + 1f;
        ResetRepairRoute();
        phase = WorkPhase.None;
        if (wasWorking) StopNavigation();
        return true;
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

    private Job? FindJob(Coffin home, bool repairsOnly = false)
    {
        pieces.Clear();
        Piece.GetAllPiecesInRadius(home.transform.position, Plugin.Radius.Value, pieces);
        TaskClaims.ObserveRecent();
        Job? best = null;
        var bestDistance = float.MaxValue;
        var bestRecent = long.MaxValue;
        foreach (var piece in pieces)
        {
            if (!piece || !piece.IsPlacedByPlayer() || !Allowed(piece.transform.position, home.Piece.GetCreator())) continue;
            var view = piece.GetComponent<ZNetView>();
            if (!view || !view.IsValid()) continue;
            if (skipped.TryGetValue(view.GetZDO().m_uid, out var until) && Time.time < until) continue;
            if (!TaskClaims.Available(this, piece, work: true)) continue;
            var wear = piece.GetComponent<WearNTear>();
            var candidate = new Job { Piece = piece, View = view };
            candidate.Supply = repairsOnly ? null : SupplyTarget.ForPiece(piece)
                .Where(option => Enabled(option) && option.NeedsItem && home.Chest.GetInventory().GetAllItems().Any(option.Matches))
                .OrderBy(option => lastSupplied.TryGetValue(view.GetZDO().m_uid + ":" + option.Kind, out var time) ? time : float.MinValue)
                .FirstOrDefault();
            if (candidate.Supply == null)
            {
                if (Plugin.Repair.Value && wear && MaintenancePolicy.NeedsRepair(wear.GetHealthPercentage()) && HasRepairStation(piece)) candidate.Wear = wear;
                else continue;
            }
            var distance = (piece.transform.position - transform.position).sqrMagnitude;
            var recent = TaskClaims.Recent(piece);
            if (!TaskClaimPolicy.Prefer(recent, distance, bestRecent, bestDistance)) continue;
            best = candidate;
            bestDistance = distance;
            bestRecent = recent;
        }
        // Bound the temporary cooldown cache in long-running worlds.
        foreach (var key in skipped.Where(x => x.Value < Time.time).Select(x => x.Key).ToArray()) skipped.Remove(key);
        if (lastSupplied.Count > 1024) lastSupplied.Clear();
        if (best == null || best.Wear) return best;
        best.Accepted = TaskClaims.TryClaim(this, best.Piece, "supply: " + best.Supply!.Kind, work: true);
        return best.Accepted ? best : null;
    }

    private bool Valid(Job candidate, Coffin home) => candidate.Piece && candidate.View && candidate.View.IsValid()
        && (candidate.Accepted ? TaskClaims.Holds(this, candidate.Piece, work: true) : TaskClaims.Available(this, candidate.Piece, work: true))
        && candidate.Piece.IsPlacedByPlayer()
        && MaintenancePolicy.InRadius((candidate.Piece.transform.position - home.transform.position).sqrMagnitude, Plugin.Radius.Value)
        && Allowed(candidate.Piece.transform.position, home.Piece.GetCreator())
        && ((candidate.Wear && Plugin.Repair.Value && MaintenancePolicy.NeedsRepair(candidate.Wear!.GetHealthPercentage()) && HasRepairStation(candidate.Piece))
            || (candidate.Supply != null && Enabled(candidate.Supply) && candidate.Supply.NeedsItem));

    internal bool HasRepairStation(Piece piece) => HasRepairStation(piece, transform.position);
    internal bool HasRepairStation(Piece piece, Vector3 position) => !piece.m_craftingStation
        || CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, position);

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

    private bool WalkTo(Vector3 point, float dt, Piece piece, bool repairing = false)
    {
        if (repairing ? WithinRepairReach(piece, point) : Vector3.Distance(transform.position, point) <= 2.4f && CanReach(piece, point))
        {
            StopNavigation();
            ai.StopMoving();
            return true;
        }
        if (repairing)
        {
            var homeObject = ZNetScene.instance.FindInstance(View.GetZDO().GetZDOID(HomeKey));
            var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
            if (!home) return false;
            var result = repairApproach.Tick(this, ai, home!, piece, out var standing);
            if (result == RepairApproach.Result.Ready)
            {
                var offset = standing - transform.position; offset.y = 0;
                Navigate(standing, dt, .25f, repairApproach.Direct || offset.magnitude < 1.2f && RepairGeometry.ClearLastStep(this, standing));
            }
            else
            {
                ai.StopMoving();
                if (result == RepairApproach.Result.Failed) RetryRepairApproach();
            }
            return false;
        }
        Navigate(point, dt, 1.5f);
        return false;
    }

    internal void StopNavigation()
    {
        navigation.Reset();
        recoveringRoute = false;
    }

    internal void Navigate(Vector3 point, float dt, float stoppingDistance, bool direct = false, Coffin? passage = null)
    {
        navigationGoal = point;
        var doorBlocked = doors.TryBegin(this, ai, point);
        if (View.GetZDO().GetZDOID("BH_openDoor") != ZDOID.None) return;
        var feet = transform.position;
        if (recoveringRoute && (point - recoveryGoal).sqrMagnitude > 1f) StopNavigation();
        if (recoveringRoute)
        {
            var offset = recoveryPoint - feet;
            offset.y = 0;
            if (offset.magnitude <= .5f || Time.time >= recoveryUntil)
            {
                StopNavigation();
                ResetPath();
            }
            else
            {
                if (ClearRecoveryRoute(recoveryPoint, passage)) ai.MoveTowards(offset, false);
                else ai.MoveTo(dt, recoveryPoint, .5f, false);
                return;
            }
        }
        if (navigation.Advance(dt, feet.x, feet.z, point.x, point.y, point.z, Plugin.StuckTime.Value))
        {
            if (job?.Accepted == true && job.Wear)
            {
                RetryRepairApproach();
                return;
            }
            ResetPath();
            if (TryBackOff(point, passage))
            {
                recoveryGoal = point;
                recoveringRoute = true;
                recoveryUntil = Time.time + 4f;
                // Move away on this frame instead of waiting at the obstruction.
                Navigate(point, dt, stoppingDistance, direct, passage);
                return;
            }
        }
        if (doorBlocked) { ai.StopMoving(); return; }
        var run = MovementPolicy.Run(pace, (point - feet).sqrMagnitude, passage || recoveringRoute);
        if (direct)
        {
            var direction = point - feet; direction.y = 0;
            ai.MoveTowards(direction, run);
        }
        else ai.MoveTo(dt, point, stoppingDistance, run);
    }

    private void ResetPath()
    {
        // BaseAI otherwise reuses a failed route to the same point for five seconds.
        ai.m_lastFindPathTime = -100f;
        ai.m_path.Clear();
    }

    private bool TryBackOff(Vector3 goal, Coffin? passage)
    {
        var feet = transform.position;
        var away = passage ? passage!.transform.forward : feet - goal;
        away.y = 0;
        if (away.sqrMagnitude < .01f) away = -transform.forward;
        away.Normalize();
        var alternate = (++recoveryAttempts % 2 == 0 ? -1f : 1f);
        foreach (var angle in new[] { 30f * alternate, -45f * alternate, 80f * alternate, -95f * alternate, 0f })
        {
            var candidate = feet + Quaternion.Euler(0, angle, 0) * away * 2f;
            if (!RecoveryFloor(candidate, feet.y, out candidate)) continue;
            var middle = (feet + candidate) * .5f;
            if (!RecoveryFloor(middle, feet.y, out _)) continue;
            if (!ClearRecoveryRoute(candidate, passage) && !ai.HavePath(candidate)) continue;
            recoveryPoint = candidate;
            return true;
        }
        return false;
    }

    private static bool RecoveryFloor(Vector3 point, float currentY, out Vector3 floor)
    {
        var solids = LayerMask.GetMask("piece", "terrain", "static_solid");
        if (Physics.Raycast(point + Vector3.up * .8f, Vector3.down, out var hit, 1.6f, solids, QueryTriggerInteraction.Ignore)
            && hit.normal.y >= .65f && hit.point.y - currentY <= .45f && currentY - hit.point.y <= .75f)
        {
            floor = hit.point;
            return true;
        }
        floor = default;
        return false;
    }

    private bool ClearRecoveryRoute(Vector3 point, Coffin? passage)
    {
        var direction = point - transform.position;
        var solids = LayerMask.GetMask("piece", "terrain", "static_solid");
        foreach (var hit in Physics.SphereCastAll(transform.position + Vector3.up * .75f, .25f,
                     direction.normalized, direction.magnitude, solids, QueryTriggerInteraction.Ignore))
            if (!passage || hit.collider.GetComponentInParent<Coffin>() != passage) return false;
        return true;
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

    private Vector3 RepairPoint(Piece piece) => RepairGeometry.Aim(piece, character.GetEyePoint());

    private bool WithinRepairReach(Piece piece, Vector3 point)
    {
        var eye = character.GetEyePoint();
        if (!MaintenancePolicy.InRepairReach((point - eye).sqrMagnitude, Plugin.RepairReach)) return false;
        return !Physics.Linecast(eye, point, out var hit, RepairGeometry.SightMask, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<Piece>() == piece;
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

    private void CancelJob(bool preserveIdle = false)
    {
        if (!preserveIdle) idle.Cancel(this);
        // Chained repairs can be walking with the hammer drawn and no active
        // animation phase. Dismissal, invalid targets, and timeouts still stow it.
        if (animations.HammerDrawn) animations.SetHammer(false);
        phase = WorkPhase.None;
        SkipCurrentJob();
        job = null;
        ResetRepairRoute();
        if (!preserveIdle) ai.StopMoving();
    }

    private void ResetRepairRoute()
    {
        repairRetries.Reset();
        repairApproach.Reset();
        repairRouteFailed = false;
    }

    private void RetryRepairApproach()
    {
        if (!repairRetries.TryAgain()) repairRouteFailed = true;
        else jobStarted = Time.time;
        repairApproach.Retry();
        if (job?.Accepted == true)
        {
            TaskClaims.Release(this, job.Piece);
            job.Accepted = false;
            StopNavigation(); ResetPath(); ai.StopMoving();
        }
    }

    internal void DoorApproachFailed()
    {
        if (job?.Accepted == true && job.Wear) repairRouteFailed = true;
        else if (job?.Accepted == true) jobStarted = Time.time - Plugin.JobTimeout.Value - 1f;
    }

    private void SkipCurrentJob()
    {
        if (job?.Accepted == true && job.Piece) TaskClaims.Release(this, job.Piece);
        if (job != null && job.View && job.View.IsValid())
            skipped[job.View.GetZDO().m_uid] = Time.time + Mathf.Max(repairRouteFailed ? 60f : 10f, Plugin.ScanInterval.Value * 2f);
    }

    internal MaidActivity ReadActivity()
    {
        var data = View.GetZDO();
        var state = new MaidActivity { id = data.m_uid.ToString(), name = data.GetString("TamedName", "Bonehilda"),
            localOwner = View.IsOwner(), stars = character.GetLevel() - 1, position = transform.position, phase = phase.ToString(),
            alerted = ai.IsAlerted(), hasCargo = HasCargo, cargo = string.Join(", ", cargo.GetAllItems().Select(i => Utils.GetPrefabName(i.m_dropPrefab) + " x" + i.m_stack)),
            activity = data.GetBool(ReturningKey) ? "returning" : data.GetBool(ExitingKey) ? "exiting" : job == null ? HasCargo ? "returning cargo" : "idle" : job.Wear ? job.Accepted ? "repair" : "idle / checking repair" : "supply",
            movementPace = pace.ToString(), workAccepted = job?.Accepted == true,
            jobAge = job == null ? 0 : Time.time - jobStarted, pathNodes = ai.m_path.Count, repairRetries = repairRetries.Count, skippedJobs = skipped.Count,
            navigationGoal = navigationGoal, recovering = recoveringRoute, recoveryPoint = recoveryPoint, idle = idle.Describe,
            door = doors.Describe(this),
            reservation = TaskClaims.Describe(this),
            repairApproach = repairApproach.State, repairStandingPoint = repairApproach.Position,
            repairSightPoint = repairApproach.SightPoint, repairProbes = repairApproach.Probes,
            eyePosition = character.GetEyePoint(), repairReach = Plugin.RepairReach,
            repairRejectedSpace = repairApproach.RejectedSpace, repairRejectedSight = repairApproach.RejectedSight,
            repairRejectedPath = repairApproach.RejectedPath,
            homePosition = data.GetVec3("BH_homePosition", transform.position), actionDelay = Mathf.Max(0, nextAction - Time.time),
            idleDelay = Mathf.Max(0, (float)((data.GetLong(IdleActivities.NextVisitKey) - ZNet.instance.GetTime().Ticks) / (double)TimeSpan.TicksPerSecond)) };
        var homeObject = ZNetScene.instance.FindInstance(data.GetZDOID(HomeKey));
        var home = homeObject ? homeObject.GetComponent<Coffin>() : null;
        state.homeLoaded = home;
        state.chestInUse = home && home!.Chest.IsInUse();
        if (job != null && job.Piece)
        {
            state.target = Utils.GetPrefabName(job.Piece.gameObject);
            state.targetId = job.View && job.View.IsValid() ? job.View.GetZDO().m_uid.ToString() : "unloaded";
            state.targetPosition = job.Piece.transform.position;
            state.targetDistance = Vector3.Distance(transform.position, state.targetPosition);
            state.workPoint = job.Wear ? RepairPoint(job.Piece) : job.Supply?.Port ? job.Supply!.Port!.position : WorkPoint(job.Piece);
            state.interactionDistance = Vector3.Distance(job.Wear ? character.GetEyePoint() : transform.position, state.workPoint);
            state.inReach = job.Wear ? WithinRepairReach(job.Piece, state.workPoint) : WithinWorkReach(job.Piece, state.workPoint);
            state.station = job.Piece.m_craftingStation ? job.Piece.m_craftingStation.m_name : "none";
            state.hasStation = HasRepairStation(job.Piece);
            state.targetNeedsItem = job.Supply != null && job.Supply.NeedsItem;
        }
        return state;
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
        if (retiring || !View.IsValid()) return;
        if (!View.IsOwner()) View.ClaimOwnership();
        if (!View.IsOwner()) return;
        retiring = true;
        TaskClaims.Release(this);
        DropCargo();
        ZNetScene.instance.Destroy(gameObject);
    }
}
