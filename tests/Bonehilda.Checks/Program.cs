using Bonehilda;

var checks = new (string Name, bool Passed)[]
{
    ("stable home identity survives runtime ID changes", HomeIdentityPolicy.HomeMatches("home-guid", "home-guid", 10000)),
    ("foreign home token cannot match merely by position", !HomeIdentityPolicy.HomeMatches("old-guid", "new-guid", 0)),
    ("legacy maids can recover their unique saved home position", HomeIdentityPolicy.HomeMatches("", "", .01f)),
    ("legacy migration does not adopt a distant coffin", !HomeIdentityPolicy.HomeMatches("", "", 1)),
    ("missing legacy home position cannot be adopted", !HomeIdentityPolicy.HomeMatches("", "", float.PositiveInfinity)),
    ("tokenless legacy maid can migrate to an identified home", HomeIdentityPolicy.MaidMatches("", "home-guid", 0)),
    ("identified maid cannot attach to a different legacy home", !HomeIdentityPolicy.MaidMatches("other-guid", "", 0)),
    ("ambiguous or absent recovery candidates are never selected", !HomeIdentityPolicy.Unique(0) && !HomeIdentityPolicy.Unique(2) && HomeIdentityPolicy.Unique(1)),
    ("stocked accepted machine continues without a success cooldown", SupplyContinuation.Keep(true, true, int.MaxValue, 3)),
    ("equal-priority alternatives do not force an idle break", SupplyContinuation.Keep(true, true, 3, 3)),
    ("empty coffin ends the supply chain", !SupplyContinuation.Keep(true, false, int.MaxValue, 3)),
    ("full or reserved destination ends the supply chain", !SupplyContinuation.Keep(false, true, int.MaxValue, 3)),
    ("critical repairs can preempt continued supplying", !SupplyContinuation.Keep(true, true, 0, 3)),
    ("needed fuel can preempt continued processor input", !SupplyContinuation.Keep(true, true, 2, 3)),
    ("quarter-capacity trigger refuels slow-burning fixtures", FuelPolicy.Start(2, 10, 3600, .25f, 120)),
    ("exact quarter capacity alone does not trigger a topoff", !FuelPolicy.Start(2.5f, 10, 3600, .25f, 120)),
    ("short runway triggers even above quarter capacity", FuelPolicy.Start(8, 10, 10, .25f, 120)),
    ("exact two-minute runway alone does not trigger", !FuelPolicy.Start(8, 10, 15, .25f, 120)),
    ("healthy fuel reserves do not start unnecessary topoffs", !FuelPolicy.Start(8, 10, 600, .25f, 120)),
    ("damage-driven fuel uses capacity rather than invented burn time", !FuelPolicy.Start(8, 10, 0, .25f, 120) && FuelPolicy.Start(2, 10, 0, .25f, 120)),
    ("full items must fit before a refueling run starts", !FuelPolicy.Start(9.5f, 10, 1, .25f, 120)),
    ("zero fuel remains urgent with disabled thresholds", FuelPolicy.Start(0, 10, 0, 0, 0)),
    ("needed fuel outranks ordinary repair even during repair preference", RoutinePolicy.WorkPriority(true, false, false, false, true) < RoutinePolicy.WorkPriority(false, false, false, false)),
    ("needed fuel outranks processor inputs", RoutinePolicy.WorkPriority(true, false, false, true, true) < RoutinePolicy.WorkPriority(true, false, false, true)),
    ("close threshold does not prove swing clearance", !DoorPolicy.OutsideSwing(0, .75f, 1.5f, .4f)),
    ("whole body outside swing is eligible for closing", DoorPolicy.OutsideSwing(0, 2.3f, 1.5f, .4f)),
    ("wrong-side arrival does not count as crossing", !DoorPolicy.ReachedSide(-1, 1, .4f)),
    ("standing fully beyond the intended side counts as crossing", DoorPolicy.ReachedSide(1, 1, .4f)),
    ("normal doorway crossing does not require two-meter clear runups", DoorPolicy.CrossingDistance(.4f, .1f) == .75f),
    ("wide but thin gates keep a short threshold crossing", DoorPolicy.CrossingDistance(.4f, .1f) < 1f),
    ("thick doors retain body clearance on both sides", DoorPolicy.CrossingDistance(.5f, .6f) >= 1.29f),
    ("failed-repair grace interval leaves selection to supplies", !RoutinePolicy.RepairSearchAllowed(12, 15)),
    ("repair eligibility returns when its grace interval expires", RoutinePolicy.RepairSearchAllowed(15, 15)),
    ("normal repair selection is available without prior failure", RoutinePolicy.RepairSearchAllowed(0, 0)),
    ("80 percent continuation includes rolls below threshold", IdleDeck.ContinueSitting(.7999f, .8f)),
    ("continuation stops at the exact threshold", !IdleDeck.ContinueSitting(.8f, .8f)),
    ("summoned skeletts are social acquaintances", SocialPolicy.Classify("Skeleton_Friendly(Clone)", false, true, false, false, false, false) == SocialKind.Skeleton),
    ("skelette prefab variants are recognized", SocialPolicy.Classify("Skelette(Clone)", false, true, false, false, false, false) == SocialKind.Skeleton),
    ("wild hostile skeletons are never greeted", SocialPolicy.Classify("Skeleton", false, false, false, true, false, false) == SocialKind.None),
    ("tamed wolves get affectionate gestures", SocialPolicy.Classify("Wolf", false, true, false, false, false, false) == SocialKind.Tame),
    ("idle maids use coworker greetings", SocialPolicy.Classify("BH_Bonehilda", true, true, false, false, false, false) == SocialKind.Maid),
    ("working or dismissed maids are left alone", SocialPolicy.Classify("BH_Bonehilda", true, true, false, false, false, true) == SocialKind.None),
    ("dead tames are never visited", SocialPolicy.Classify("Wolf", false, true, false, false, true, false) == SocialKind.None),
    ("player characters are excluded", SocialPolicy.Classify("Player", false, true, true, false, false, false) == SocialKind.None),
    ("ordinary wild animals are excluded", SocialPolicy.Classify("Deer", false, false, false, false, false, false) == SocialKind.None),
    ("sleeping or alerted tames are excluded", SocialPolicy.Classify("Wolf", false, true, false, false, false, true) == SocialKind.None),
    ("small acquaintance movements are tolerated", SocialPolicy.InPlace(.99f, .5f)),
    ("departing acquaintances are not chased", !SocialPolicy.InPlace(1.01f, 0)),
    ("targets on another floor end the visit", !SocialPolicy.InPlace(0, .76f)),
    ("social cooldown remains active before expiry", SocialPolicy.CoolingDown(99, 100)),
    ("social cooldown expires at its deadline", !SocialPolicy.CoolingDown(100, 100)),
    ("two gestures always alternate", RoutinePolicy.Gesture(2, 0, 0) == 1 && RoutinePolicy.Gesture(2, 1, .99f) == 0),
    ("first gesture can choose either option", RoutinePolicy.Gesture(2, -1, 0) == 0 && RoutinePolicy.Gesture(2, -1, .99f) == 1),
    ("one-option gestures remain valid", RoutinePolicy.Gesture(1, 0, .99f) == 0),
    ("critical damage outranks depleted fuel", RoutinePolicy.WorkPriority(false, true, false, true) < RoutinePolicy.WorkPriority(true, false, true, true)),
    ("depleted fuel outranks an ordinary preferred repair", RoutinePolicy.WorkPriority(true, false, true, false) < RoutinePolicy.WorkPriority(false, false, false, false)),
    ("three successful supplies switch ordinary preference to repairs", !RoutinePolicy.NextPreference(true, 3)),
    ("repair bouts retain preference until third success", !RoutinePolicy.NextPreference(false, 2) && RoutinePolicy.NextPreference(false, 3)),
    ("failed actions do not advance work streak policy", RoutinePolicy.NextPreference(true, 0)),
    ("occasional favorite rest visits outweigh a modest distance difference", RoutinePolicy.RestScore(10, true, true) < RoutinePolicy.RestScore(2, false, true)),
    ("ordinary rest choices keep distance ordering", RoutinePolicy.RestScore(10, true, false) == 10),
    ("small player-sized threshold is stepped over", StepPolicy.Walk(.25f, 1) && !StepPolicy.Jump(.25f, 1)),
    ("forty centimeters remains a walking step", StepPolicy.Walk(.4f, 1) && !StepPolicy.Jump(.4f, 1)),
    ("larger ledge retains the jump animation", !StepPolicy.Walk(.5f, 1) && StepPolicy.Jump(.5f, 1)),
    ("tall walls do not become step shortcuts", !StepPolicy.Walk(.7f, 1) && !StepPolicy.Jump(.7f, 1)),
    ("steep support rejects both traversal modes", !StepPolicy.Walk(.25f, .6f) && !StepPolicy.Jump(.5f, .6f)),
    ("normal floor causes no step lift", !StepPolicy.Walk(.02f, 1)),
    ("small wooden thresholds use walking recovery", StepPolicy.Walk(.08f, 1)),
    ("step begins at the current position", StepPolicy.Rise(0) == 0 && StepPolicy.Forward(0) == 0),
    ("step lifts before crossing the vertical riser", StepPolicy.Rise(.2f) > 0 && StepPolicy.Forward(.2f) == 0),
    ("foot is fully raised before moving over the riser", StepPolicy.Rise(.35f) == 1 && StepPolicy.Forward(.35f) == 0),
    ("step continues walking at the landing height", StepPolicy.Rise(.7f) == 1 && StepPolicy.Forward(.7f) > 0),
    ("step ends exactly at the validated landing", StepPolicy.Rise(1) == 1 && StepPolicy.Forward(1) == 1),
    ("slow idle gait does not prolong a step excessively", StepPolicy.Duration(1.05f, .5f) <= .65f),
    ("restrained jump clears a half-meter ledge without a huge leap", Math.Abs(StepPolicy.JumpSpeed(.5f, 10) * StepPolicy.JumpSpeed(.5f, 10) / 20f - .68f) < .001f),
    ("unreachable repair candidates keep the idle pace", MovementPolicy.Select(false, true, false) == MovementPace.Idle),
    ("accepted repairs use urgent travel", MovementPolicy.Select(true, true, false) == MovementPace.Repair),
    ("supply pickup uses player walking pace before carrying", MovementPolicy.Select(true, false, false) == MovementPace.Supply),
    ("supply delivery uses player walking pace", MovementPolicy.Select(true, false, true) == MovementPace.Supply),
    ("returning leftover cargo keeps supply pace", MovementPolicy.Select(false, false, true) == MovementPace.Supply),
    ("completed work restores slow idle pace", MovementPolicy.Select(false, false, false) == MovementPace.Idle),
    ("repairs run across a clear longer approach", MovementPolicy.Run(MovementPace.Repair, 100, false)),
    ("repair stance final two meters are walked", !MovementPolicy.Run(MovementPace.Repair, 4, false)),
    ("repair stance just beyond two meters permits running", MovementPolicy.Run(MovementPace.Repair, 4.01f, false)),
    ("door or coffin precision overrides repair urgency", !MovementPolicy.Run(MovementPace.Repair, 100, true)),
    ("supplies never use running paths", !MovementPolicy.Run(MovementPace.Supply, 900, false)),
    ("idle visits never use running paths", !MovementPolicy.Run(MovementPace.Idle, 900, false)),
    ("empty torch accepts fuel", MaintenancePolicy.NeedsFuel(0, 10, false)),
    ("one full free slot accepts one item", MaintenancePolicy.NeedsFuel(9, 10, false)),
    ("partially burned torch with a free slot accepts fuel", MaintenancePolicy.NeedsFuel(8.75f, 10, false)),
    ("partial slot does not waste a whole resource", !MaintenancePolicy.NeedsFuel(9.25f, 10, false)),
    ("full fire preserves resources", !MaintenancePolicy.NeedsFuel(10, 10, false)),
    ("overfull fire preserves resources", !MaintenancePolicy.NeedsFuel(11, 10, false)),
    ("infinite fuel is never replenished", !MaintenancePolicy.NeedsFuel(0, 10, true)),
    ("zero capacity is ignored", !MaintenancePolicy.NeedsFuel(0, 0, false)),
    ("negative capacity is ignored", !MaintenancePolicy.NeedsFuel(0, -10, false)),
    ("invalid fuel does not consume resources", !MaintenancePolicy.NeedsFuel(float.NaN, 10, false)),
    ("negative fuel does not consume resources", !MaintenancePolicy.NeedsFuel(-1, 10, false)),
    ("invalid capacity is ignored", !MaintenancePolicy.NeedsFuel(0, float.NaN, false)),
    ("damaged building is repaired", MaintenancePolicy.NeedsRepair(0.5f)),
    ("nearly destroyed building is repaired", MaintenancePolicy.NeedsRepair(0.001f)),
    ("full health is ignored", !MaintenancePolicy.NeedsRepair(1)),
    ("destroyed building is ignored", !MaintenancePolicy.NeedsRepair(0)),
    ("negative health is ignored", !MaintenancePolicy.NeedsRepair(-1)),
    ("invalid health is ignored", !MaintenancePolicy.NeedsRepair(float.NaN)),
    ("floating point noise does not cause repair loops", !MaintenancePolicy.NeedsRepair(0.9999f)),
    ("repair surface just inside player reach is accepted", MaintenancePolicy.InRepairReach(4.99f * 4.99f, 5f)),
    ("native player repair boundary is strict", !MaintenancePolicy.InRepairReach(25f, 5f)),
    ("surface beyond player reach is rejected", !MaintenancePolicy.InRepairReach(25.01f, 5f)),
    ("repair reach includes vertical distance", !MaintenancePolicy.InRepairReach(4f * 4f + 4f * 4f, 5f)),
    ("repair reach accepts overhead surfaces within player range", MaintenancePolicy.InRepairReach(3.5f * 3.5f + 1f, 5f)),
    ("invalid reach does not permit repairs", !MaintenancePolicy.InRepairReach(1f, float.NaN) && !MaintenancePolicy.InRepairReach(1f, 0f)),
    ("invalid target distance does not permit repairs", !MaintenancePolicy.InRepairReach(float.NaN, 5f) && !MaintenancePolicy.InRepairReach(-1f, 5f)),
    ("three repair retries exhaust even when paths are replanned", BlockedRepairExhaustsRetries()),
    ("a new repair receives its own retry budget", NewRepairResetsRetries()),
    ("enemy near the door prevents opening even if far from the maid", DoorPolicy.EnemyNear(25, 400, 10)),
    ("enemy near the maid prevents opening even if far from the door", DoorPolicy.EnemyNear(400, 25, 10)),
    ("enemy radius boundary is included", DoorPolicy.EnemyNear(100, 400, 10)),
    ("enemies beyond both radii do not prevent opening", !DoorPolicy.EnemyNear(101, 121, 10)),
    ("a retreat has time to move away before closing", !DoorPolicy.RetreatExpired(5.999)),
    ("a blocked retreat closes at six seconds", DoorPolicy.RetreatExpired(6)),
    ("a resumed overdue retreat cannot hold a gate open", DoorPolicy.RetreatExpired(20)),
    ("a job across the doorway can use a door bridge", DoorPolicy.CrossesPlane(3, -8)),
    ("door routing works from the opposite side", DoorPolicy.CrossesPlane(-3, 8)),
    ("a job on this side does not detour through the door", !DoorPolicy.CrossesPlane(3, 8)),
    ("opposite approach direction still excludes same-side jobs", !DoorPolicy.CrossesPlane(-3, -8)),
    ("a door-plane repair does not open the door unnecessarily", !DoorPolicy.CrossesPlane(3, 0)),
    ("a near-surface door repair does not open the door unnecessarily", !DoorPolicy.CrossesPlane(3, -.35f)),
    ("a nearby point on the far side permits crossing", DoorPolicy.CrossesPlane(3, -.36f)),
    ("an invalid door normal never authorizes a crossing", !DoorPolicy.CrossesPlane(float.NaN, -8)),
    ("low supported ledge permits a recovery jump", DoorPolicy.SmallLedge(.25f, 1)),
    ("upper ledge height limit is included", DoorPolicy.SmallLedge(.65f, .65f)),
    ("normal floor does not trigger a recovery jump", !DoorPolicy.SmallLedge(.05f, 1)),
    ("a drop is not a ledge to jump onto", !DoorPolicy.SmallLedge(-.3f, 1)),
    ("a high wall is not a small ledge", !DoorPolicy.SmallLedge(.66f, 1)),
    ("steep landing prevents a ledge jump", !DoorPolicy.SmallLedge(.3f, .64f)),
    ("ordinary supplies begin ahead of rain damage", RoutinePolicy.WorkPriority(true, false, false, true) < RoutinePolicy.WorkPriority(false, false, false, true)),
    ("ordinary repairs get a turn ahead of top-offs", RoutinePolicy.WorkPriority(false, false, false, false) < RoutinePolicy.WorkPriority(true, false, false, false)),
    ("equally urgent destinations retain oldest-first fairness", TaskClaimPolicy.Prefer(20, 50, 30, 1)),
    ("equally recent destinations retain nearest ordering", TaskClaimPolicy.Prefer(0, 2, 0, 10)),
    ("shorter idle interval schedules activities after eight seconds", IdleTiming.Delay(8, true, 0) == 8),
    ("idle variation caps the new default at twelve seconds", IdleTiming.Delay(8, true, 1) == 12),
    ("upper floor within player interaction reach can be supplied", MaintenancePolicy.InRepairReach(4.9f * 4.9f, 5)),
    ("supply interaction still rejects beyond-player reach", !MaintenancePolicy.InRepairReach(5.1f * 5.1f, 5)),
    ("installed player fuel reach accepts a visible surface just inside 3.5m", MaintenancePolicy.InRepairReach(3.49f * 3.49f, 3.5f)),
    ("installed player fuel reach rejects its exact boundary", !MaintenancePolicy.InRepairReach(3.5f * 3.5f, 3.5f)),
    ("fuel reach uses surface distance rather than a farther object center", MaintenancePolicy.InRepairReach(3.4f * 3.4f, 3.5f) && !MaintenancePolicy.InRepairReach(4f * 4f, 3.5f)),
    ("native hover offset extends fuel interaction reach", MaintenancePolicy.InRepairReach(4f * 4f, 3.5f + 1f)),
    ("inside radius", MaintenancePolicy.InRadius(29 * 29, 30)),
    ("radius boundary is inclusive", MaintenancePolicy.InRadius(30 * 30, 30)),
    ("outside radius", !MaintenancePolicy.InRadius(30.1f * 30.1f, 30)),
    ("coffin center is in radius", MaintenancePolicy.InRadius(0, 30)),
    ("vertical distance counts toward radius", !MaintenancePolicy.InRadius(20 * 20 + 25 * 25, 30)),
    ("empty smelter accepts an input", MaintenancePolicy.NeedsInput(0, 10)),
    ("last queue slot accepts an input", MaintenancePolicy.NeedsInput(9, 10)),
    ("full input queue preserves resources", !MaintenancePolicy.NeedsInput(10, 10)),
    ("overfull queue preserves resources", !MaintenancePolicy.NeedsInput(11, 10)),
    ("zero input capacity is ignored", !MaintenancePolicy.NeedsInput(0, 0)),
    ("invalid queue is ignored", !MaintenancePolicy.NeedsInput(-1, 10)),
    ("empty ballista accepts a new ammo type", MaintenancePolicy.CanLoadAmmo(0, 20, "WoodBolt", "IronBolt")),
    ("loaded ballista accepts matching ammunition", MaintenancePolicy.CanLoadAmmo(5, 20, "IronBolt", "IronBolt")),
    ("loaded ballista never mixes ammunition", !MaintenancePolicy.CanLoadAmmo(5, 20, "WoodBolt", "IronBolt")),
    ("full ballista does not take ammunition", !MaintenancePolicy.CanLoadAmmo(20, 20, "IronBolt", "IronBolt")),
    ("cooking stations are excluded", MaintenancePolicy.ExcludedConsumer(true, false)),
    ("obliterators are excluded", MaintenancePolicy.ExcludedConsumer(false, true)),
    ("windmills and fermenters stay eligible", !MaintenancePolicy.ExcludedConsumer(false, false)),
    ("wandering uses requested distance", MaintenancePolicy.WanderRange(10, 30) == 10),
    ("wandering can use the whole work radius", MaintenancePolicy.WanderRange(50, 30) == 30),
    ("small work radius also constrains wandering", MaintenancePolicy.WanderRange(10, 5) == 5),
    ("zero idle radius follows the full work radius", MaintenancePolicy.WanderRange(0, 30) == 30),
    ("automatic idle radius follows a changed work radius", MaintenancePolicy.WanderRange(0, 60) == 60),
    ("explicit smaller idle radius stays respected", MaintenancePolicy.WanderRange(5, 60) == 5),
    ("work area edge is available for idle destinations", MaintenancePolicy.InRadius(900, MaintenancePolicy.WanderRange(0, 30))),
    ("idle destinations outside work area stay rejected", !MaintenancePolicy.InRadius(901, MaintenancePolicy.WanderRange(0, 30))),
    ("nearby acquaintance far from coffin is eligible", SocialPolicy.InRange(16, 841, 10, 30)),
    ("acquaintance near coffin but far from maid is excluded", !SocialPolicy.InRange(256, 81, 10, 30)),
    ("nearby acquaintance outside work area is excluded", !SocialPolicy.InRange(4, 961, 10, 30)),
    ("both search boundaries include their exact edge", SocialPolicy.InRange(100, 900, 10, 30)),
    ("large social search remains independent of coffin distance", SocialPolicy.InRange(1600, 625, 50, 30)),
    ("small social search still limits nearby detection", !SocialPolicy.InRange(100.1f, 400, 10, 30)),
    ("long idle walks receive time to finish at the slow gait", MaintenancePolicy.IdleTravelBudget(30, 16) >= 59.9f),
    ("nearby idle visits keep their minimum budget", MaintenancePolicy.IdleTravelBudget(1, 20) == 20),
    ("idle path attempts remain bounded", MaintenancePolicy.IdleTravelBudget(100, 20) == 120),
    ("negative fermenter hashes still accept the item", MaintenancePolicy.AcceptedSupply(0, -185094678, true)),
    ("positive fermenter hashes accept the item", MaintenancePolicy.AcceptedSupply(0, 185094678, true)),
    ("rejected fermenter delivery preserves the item", !MaintenancePolicy.AcceptedSupply(0, 0, true)),
    ("accepted machine delivery consumes one carried item", MaintenancePolicy.AcceptedSupply(3, 4, false)),
    ("rejected machine delivery preserves carried item", !MaintenancePolicy.AcceptedSupply(3, 3, false)),
    ("first delivery has no artificial delay", MaintenancePolicy.SupplyReady(100_000_000, 0, 1)),
    ("no burst in the same frame", !MaintenancePolicy.SupplyReady(100_000_000, 100_000_000, 1)),
    ("one second default blocks early delivery", !MaintenancePolicy.SupplyReady(109_999_999, 100_000_000, 1)),
    ("one second boundary allows one delivery", MaintenancePolicy.SupplyReady(110_000_000, 100_000_000, 1)),
    ("longer configured interval is honored", !MaintenancePolicy.SupplyReady(120_000_000, 100_000_000, 5)),
    ("fractional configured interval is honored", MaintenancePolicy.SupplyReady(105_000_000, 100_000_000, 0.5f)),
    ("clock rewind cannot bypass the cooldown", !MaintenancePolicy.SupplyReady(90_000_000, 100_000_000, 1)),
    ("persisted timestamp permits work after elapsed time", MaintenancePolicy.SupplyReady(160_000_000, 100_000_000, 5)),
    ("zero star keeps exactly two empty slots", MaintenancePolicy.StorageRows(0, 2, 1) == 1),
    ("one star keeps four slots", MaintenancePolicy.StorageRows(4, 4, 1) == 1),
    ("two star keeps eight slots", MaintenancePolicy.StorageRows(8, 4, 2) == 2),
    ("old 24 stacks remain accessible in six rows", MaintenancePolicy.StorageRows(24, 4, 2) == 6),
    ("partially filled legacy row remains accessible", MaintenancePolicy.StorageRows(9, 4, 2) == 3),
    ("removing overflow restores eight slots", MaintenancePolicy.StorageRows(7, 4, 2) == 2),
    ("nearby native stopping point allows final approach", MaintenancePolicy.NearCoffinApproach(0, 0, 2.2f)),
    ("sideways approach is allowed in front of coffin", MaintenancePolicy.NearCoffinApproach(1.1f, .2f, 1.7f)),
    ("behind the coffin uses normal pathfinding", !MaintenancePolicy.NearCoffinApproach(0, 0, -.6f)),
    ("distant approach uses normal pathfinding", !MaintenancePolicy.NearCoffinApproach(0, 0, 4)),
    ("another floor cannot use threshold assistance", !MaintenancePolicy.NearCoffinApproach(0, 2, 1.7f)),
    ("coffin center is a valid entry", MaintenancePolicy.InsideCoffin(0, .2f, .04f)),
    ("walking just inside the door leaves the dress in its path", !MaintenancePolicy.InsideCoffin(0, .2f, .23f)),
    ("dismissal waits for the maid to clear the side wall", !MaintenancePolicy.InsideCoffin(.24f, .2f, .04f)),
    ("standing outside the door cannot complete dismissal", !MaintenancePolicy.InsideCoffin(0, 0, .6f)),
    ("standing beside the coffin cannot complete dismissal", !MaintenancePolicy.InsideCoffin(.6f, 0, .04f)),
    ("standing behind the coffin cannot complete dismissal", !MaintenancePolicy.InsideCoffin(0, 0, -.5f)),
    ("standing above the coffin cannot complete dismissal", !MaintenancePolicy.InsideCoffin(0, 2, .04f)),
    ("stationary navigation retries after the configured observation time", StalledRouteRetries()),
    ("slow forward progress avoids false stuck detection", MovingRouteDoesNotRetry()),
    ("a changed destination gets its own stuck timer", ChangedRouteResetsTimer()),
    ("ownership handoff clears a pending stuck timeout", ResetClearsTimer()),
    ("a single long frame is not evidence of a blocked route", LongFrameDoesNotRetry()),
    ("small physics jitter does not hide an obstruction", JitterStillRetries()),
};
checks = checks.Concat(IdleChecks()).ToArray();
checks = checks.Concat(ReservationChecks()).ToArray();
checks = checks.Concat(SnapshotChecks()).ToArray();
checks = checks.Concat(LinkChecks()).ToArray();
checks = checks.Concat(ApproachChecks()).ToArray();
checks = checks.Concat(SupplyFailureChecks()).ToArray();
foreach (var check in checks)
    if (!check.Passed) throw new Exception($"FAILED: {check.Name}");
Console.WriteLine($"Passed {checks.Length} maintenance policy checks.");

static IEnumerable<(string Name, bool Passed)> SupplyFailureChecks()
{
    var history = new SupplyFailures();
    yield return ("first failed delivery is deferred for a minute", history.Failed("brazier") == 60);
    yield return ("repeated failure doubles retry delay", history.Failed("brazier") == 120);
    yield return ("unrelated consumers keep their own cooldown", history.Failed("smelter") == 60);
    for (var i = 0; i < 20; i++) history.Failed("brazier");
    yield return ("failed delivery backoff is bounded at ten minutes", history.Failed("brazier") == 600);
    history.Succeeded("brazier");
    yield return ("successful delivery clears repeated failure history", history.Failed("brazier") == 60);
    var destination = new RepairRetries(); var coffin = new RepairRetries();
    destination.TryAgain(); destination.TryAgain(); coffin.TryAgain(); coffin.Reset();
    yield return ("coffin recovery cannot replenish destination retry budget", destination.Count == 2 && destination.TryAgain() && !destination.TryAgain());
}

static IEnumerable<(string Name, bool Passed)> ApproachChecks()
{
    yield return ("hanging fixture native offset includes lower standing floors", RepairApproachPolicy.SearchBottom(87.5f, 3.5f + 3f) <= 80f);
    yield return ("base reach alone would miss the same lower floor", RepairApproachPolicy.SearchBottom(87.5f, 3.5f) > 80f);
    yield return ("short local walks retain continuous level support", RepairApproachPolicy.SupportsStep(80, 80, 1));
    yield return ("short local walks tolerate a shallow step", RepairApproachPolicy.SupportsStep(80, 80.3f, .9f));
    yield return ("short local walks reject a drop to another floor", !RepairApproachPolicy.SupportsStep(80, 78, 1));
    yield return ("short local walks reject a high riser", !RepairApproachPolicy.SupportsStep(80, 80.6f, 1));
    yield return ("short local walks reject steep support surfaces", !RepairApproachPolicy.SupportsStep(80, 80, .4f));
    yield return ("invalid support does not qualify as a local walking route", !RepairApproachPolicy.SupportsStep(80, float.NaN, 1));
    yield return ("a path ending on the requested floor qualifies", RepairApproachPolicy.EndpointMatches(.2f * .2f, .1f));
    yield return ("a path snapped to another floor is rejected despite matching XZ", !RepairApproachPolicy.EndpointMatches(0, 2));
    yield return ("a path stopped short of the standing spot is rejected", !RepairApproachPolicy.EndpointMatches(1, 0));
    yield return ("small navmesh height and position offsets are tolerated", RepairApproachPolicy.EndpointMatches(.64f * .64f, .49f));
    yield return ("a point below the requested floor is also rejected", !RepairApproachPolicy.EndpointMatches(0, -.51f));
    yield return ("route length rather than straight-line proximity chooses a standing spot", RepairApproachPolicy.Cost(4, 0) < RepairApproachPolicy.Cost(18, 0));
    yield return ("similar routes gently favor the current floor", RepairApproachPolicy.Cost(4, 0) < RepairApproachPolicy.Cost(4, 2));
    yield return ("a shorter route can still use another floor", RepairApproachPolicy.Cost(4, 2) < RepairApproachPolicy.Cost(10, 0));
    yield return ("an alternate point too close to a failed stance is rejected", RepairApproachPolicy.SameFailedSpot(.4f * .4f, .1f));
    yield return ("a different floor is not the same failed stance", !RepairApproachPolicy.SameFailedSpot(0, 2));
    yield return ("a stance around another corner can be tried", !RepairApproachPolicy.SameFailedSpot(1, 0));
    var budget = new RepairRetries();
    var attempts = 1;
    while (budget.TryAgain()) attempts++;
    yield return ("standing-spot changes share a total of three recovery retries", attempts == 4 && budget.Count == 3 && !budget.TryAgain());
}

static IEnumerable<(string Name, bool Passed)> LinkChecks()
{
    long At(int seconds) => seconds * TimeSpan.TicksPerSecond;
    var guard = new LinkConfirmation();
    yield return ("a maid with no home gets time for initial binding", !guard.Confirm("unbound", "none", 1, false, At(10)));
    yield return ("an unbound maid is confirmed orphaned on a later check", guard.Confirm("unbound", "none", 1, false, At(20)));
    guard.Confirm("loading", "coffin", 1, false, At(10));
    yield return ("a delayed reciprocal link cancels pending cleanup", !guard.Confirm("loading", "coffin", 1, true, At(14)));
    yield return ("a subsequent break starts a fresh observation", !guard.Confirm("loading", "coffin", 1, false, At(30)));
    yield return ("one bad packet cannot trigger immediate cleanup", !guard.Confirm("loading", "coffin", 1, false, At(34)));
    yield return ("a persistently missing coffin is confirmed", guard.Confirm("loading", "coffin", 1, false, At(40)));
    guard.Confirm("duplicate", "coffin-with-another-maid", 1, false, At(10));
    yield return ("a duplicate with a persistent backlink mismatch is confirmed", guard.Confirm("duplicate", "coffin-with-another-maid", 1, false, At(20)));
    yield return ("a valid server link stays valid even if its scene object is unloaded", !guard.Confirm("unloaded", "coffin", 1, true, At(30)) && !guard.Confirm("unloaded", "coffin", 1, true, At(50)));
    guard.Confirm("handoff", "coffin", 1, false, At(10));
    yield return ("an ownership handoff resets orphan evidence", !guard.Confirm("handoff", "coffin", 2, false, At(20)));
    yield return ("a repaired link after handoff is kept", !guard.Confirm("handoff", "coffin", 2, true, At(21)));
    guard.Confirm("rebound", "old", 1, false, At(10));
    yield return ("rebinding to another coffin resets orphan evidence", !guard.Confirm("rebound", "new", 1, false, At(20)));
    guard.Confirm("rewind", "coffin", 1, false, At(100));
    yield return ("a time rewind cannot confirm stale evidence", !guard.Confirm("rewind", "coffin", 1, false, At(90)));
    guard.Confirm("stale", "coffin", 1, false, At(100));
    yield return ("an old observation after unloading must be repeated", !guard.Confirm("stale", "coffin", 1, false, At(170)));
    guard.Confirm("scene", "coffin", 1, false, At(200));
    guard.Clear();
    yield return ("changing scenes clears pending orphan observations", !guard.Confirm("scene", "coffin", 1, false, At(210)));
}

static IEnumerable<(string Name, bool Passed)> SnapshotChecks()
{
    var records = new[] {
        "{\"id\":\"maid0\",\"targetId\":\"roof1\",\"position\":{\"x\":1,\"y\":2,\"z\":3},\"inReach\":false}",
        "{\"id\":\"maid1\",\"targetId\":\"roof2\",\"reservation\":\"repair roof2 (15s)\"}",
        "{\"id\":\"maid2\",\"targetId\":\"stair\",\"cargo\":\"Coal x1\"}"
    };
    foreach (var pretty in new[] { false, true })
    {
        var json = ActivitySnapshotJson.Encode("0.4.11", "2026-11-21T22:29:45Z", records, pretty);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        var maids = root.GetProperty("maids");
        yield return ($"complete snapshot exports all three maids (pretty={pretty})", maids.GetArrayLength() == 3);
        yield return ($"snapshot retains distinct roof target IDs (pretty={pretty})", maids[0].GetProperty("targetId").GetString() != maids[1].GetProperty("targetId").GetString());
        yield return ($"snapshot retains movement and reach diagnostics (pretty={pretty})", maids[0].GetProperty("position").GetProperty("x").GetInt32() == 1 && !maids[0].GetProperty("inReach").GetBoolean());
        yield return ($"snapshot retains reservations and cargo (pretty={pretty})", maids[1].GetProperty("reservation").GetString() == "repair roof2 (15s)" && maids[2].GetProperty("cargo").GetString() == "Coal x1");
        yield return ($"snapshot retains version and timestamp (pretty={pretty})", root.GetProperty("version").GetString() == "0.4.11" && root.GetProperty("utc").GetString() == "2026-11-21T22:29:45Z");
        if (!pretty) yield return ("history snapshots occupy a single JSONL line", !json.Contains('\n') && !json.Contains('\r'));
    }
    using var empty = System.Text.Json.JsonDocument.Parse(ActivitySnapshotJson.Encode("test", "now", Array.Empty<string>()));
    yield return ("no loaded maids produces an explicit empty array", empty.RootElement.GetProperty("maids").GetArrayLength() == 0);
    using var escaped = System.Text.Json.JsonDocument.Parse(ActivitySnapshotJson.Encode("a\"b\\c\n\t", "now", records));
    yield return ("snapshot metadata is safely JSON escaped", escaped.RootElement.GetProperty("version").GetString() == "a\"b\\c\n\t");
}

static IEnumerable<(string Name, bool Passed)> ReservationChecks()
{
    var claims = new List<TaskClaim>();
    var now = 100L;
    // Simulate three independently scanning tiers: each sees claims as soon as
    // another maid chooses a destination, before either starts its animation.
    var selected = new List<string>();
    foreach (var maid in new[] { "tier0", "tier1", "tier2" })
    {
        var target = new[] { "wall", "smelter", "torch" }.First(t => TaskClaimPolicy.Available(claims, t, maid, true, now));
        selected.Add(target);
        claims.Add(new TaskClaim(maid, target, "work", true, now, 115));
    }
    yield return ("three tiers choose three different available jobs", selected.Distinct().Count() == 3);
    yield return ("a claimed job stays out of another maid's selection", !TaskClaimPolicy.Available(claims, "wall", "tier1", true, now));
    yield return ("the current worker retains her own job", TaskClaimPolicy.Available(claims, "wall", "tier0", true, now));
    yield return ("a busy destination cannot attract a cosmetic visit", !TaskClaimPolicy.Available(claims, "smelter", "idle", false, now));
    yield return ("expiry frees an abandoned job at its boundary", TaskClaimPolicy.Available(claims, "wall", "other", true, 115));
    yield return ("an unexpired job is still reserved", !TaskClaimPolicy.Available(claims, "wall", "other", true, 114));
    claims.RemoveAll(c => c.Maid == "tier0");
    yield return ("release on cancel or death frees the job immediately", TaskClaimPolicy.Available(claims, "wall", "other", true, now));
    var idle = new TaskClaim("idle", "chair", "sit", false, 90, 115);
    yield return ("an approach reserves a chair before sitting", !TaskClaimPolicy.Available(new[] { idle }, "chair", "other", false, now));
    yield return ("real work can preempt an idle visit", TaskClaimPolicy.Available(new[] { idle }, "chair", "worker", true, now));
    var work = new TaskClaim("worker", "chair", "repair", true, 100, 115);
    yield return ("a repair outranks an older idle visit", TaskClaimPolicy.Winner(new[] { idle, work }, "chair", now)?.Maid == "worker");
    var first = new TaskClaim("b", "wall", "repair", true, 95, 115);
    var second = new TaskClaim("a", "wall", "repair", true, 96, 115);
    yield return ("an earlier active worker wins a remote claim race", TaskClaimPolicy.Winner(new[] { second, first }, "wall", now)?.Maid == "b");
    var tie = new TaskClaim("a", "wall", "repair", true, 95, 115);
    yield return ("simultaneous claims have a peer-independent winner", TaskClaimPolicy.Winner(new[] { first, tie }, "wall", now)?.Maid == "a"
        && TaskClaimPolicy.Winner(new[] { tie, first }, "wall", now)?.Maid == "a");
    yield return ("a disconnected owner's expiry admits a replacement", TaskClaimPolicy.Winner(new[] { first, new TaskClaim("new", "wall", "repair", true, 115, 130) }, "wall", 116)?.Maid == "new");
    yield return ("a future claim after clock rewind cannot block work", TaskClaimPolicy.Available(new[] { first }, "wall", "other", true, 94));
    yield return ("claims for another structure do not interfere", TaskClaimPolicy.Available(new[] { first }, "smelter", "other", true, now));
    yield return ("an untouched task outranks a nearby recently started task", TaskClaimPolicy.Prefer(0, 900, 95, 1));
    yield return ("recently started tasks move behind untouched tasks", !TaskClaimPolicy.Prefer(95, 1, 0, 900));
    yield return ("least recently started tasks lead when all were visited", TaskClaimPolicy.Prefer(90, 900, 95, 1));
    yield return ("equal recency retains nearest-job selection", TaskClaimPolicy.Prefer(0, 1, 0, 900));
}

static bool StalledRouteRetries()
{
    var route = new NavigationProgress();
    route.Advance(.1f, 0, 0, 10, 0, 0, 6);
    for (var i = 0; i < 59; i++)
        if (route.Advance(.1f, 0, 0, 10, 0, 0, 6)) return false;
    var retries = 0;
    for (var i = 0; i < 3; i++)
        if (route.Advance(.1f, 0, 0, 10, 0, 0, 6)) retries++;
    return retries == 1;
}

static bool MovingRouteDoesNotRetry()
{
    var route = new NavigationProgress();
    for (var i = 0; i < 300; i++)
        if (route.Advance(.1f, i * .015f, 0, 10, 0, 0, 6)) return false;
    return true;
}

static bool ChangedRouteResetsTimer()
{
    var route = new NavigationProgress();
    for (var i = 0; i < 55; i++) route.Advance(.1f, 0, 0, 10, 0, 0, 6);
    for (var i = 0; i < 20; i++)
        if (route.Advance(.1f, 0, 0, 0, 0, 10, 6)) return false;
    return true;
}

static bool ResetClearsTimer()
{
    var route = new NavigationProgress();
    for (var i = 0; i < 55; i++) route.Advance(.1f, 0, 0, 10, 0, 0, 6);
    route.Reset();
    for (var i = 0; i < 20; i++)
        if (route.Advance(.1f, 0, 0, 10, 0, 0, 6)) return false;
    return true;
}

static bool LongFrameDoesNotRetry()
{
    var route = new NavigationProgress();
    route.Advance(.1f, 0, 0, 10, 0, 0, 6);
    return !route.Advance(20, 0, 0, 10, 0, 0, 6);
}

static bool JitterStillRetries()
{
    var route = new NavigationProgress();
    for (var i = 0; i < 70; i++)
        if (route.Advance(.1f, (i % 2 == 0 ? .03f : -.03f), 0, 10, 0, 0, 6)) return true;
    return false;
}

static bool BlockedRepairExhaustsRetries()
{
    var progress = new NavigationProgress();
    var budget = new RepairRetries();
    var retries = 0;
    for (var frame = 0; frame < 400; frame++)
    {
        if (!progress.Advance(.1f, 0, 0, 10, 0, 0, 6)) continue;
        if (!budget.TryAgain()) return retries == 3;
        retries++;
        // Replanning the route resets observations, but not the job's budget.
        progress.Reset();
    }
    return false;
}

static bool NewRepairResetsRetries()
{
    var budget = new RepairRetries();
    for (var i = 0; i < 3; i++) if (!budget.TryAgain()) return false;
    if (budget.TryAgain()) return false;
    budget.Reset();
    for (var i = 0; i < 3; i++) if (!budget.TryAgain()) return false;
    return !budget.TryAgain();
}

static IEnumerable<(string Name, bool Passed)> IdleChecks()
{
    yield return ("performed activities respect the configured delay", IdleTiming.Delay(15, true, 0) == 15 && IdleTiming.Delay(15, true, 1) == 22.5f);
    yield return ("failed approaches retry quickly even with a long configured delay", IdleTiming.Delay(90, false, 0) == 3 && IdleTiming.Delay(90, false, 1) == 4.5f);
    var now = 100L * TimeSpan.TicksPerSecond;
    var oldPending = IdleTiming.Next(now, 90, true, 1);
    var shortened = IdleTiming.ClampPending(now, oldPending, 15);
    yield return ("lowering the interval caps an old saved cooldown", shortened == now + (long)(22.5 * TimeSpan.TicksPerSecond));
    yield return ("an elapsed cooldown stays ready", IdleTiming.ClampPending(now, now - 1, 15) < now);
    yield return ("idle polling does not keep postponing a pending visit", IdleTiming.ClampPending(now + TimeSpan.TicksPerSecond, shortened, 15) == shortened);
    yield return ("failed approach cooldown expires before a full activity delay", IdleTiming.Next(now, 15, false, 1) < IdleTiming.Next(now, 15, true, 0));
    var dresses = new[] { "ArmorDress10", "ArmorDress1", "ArmorDress6" };
    for (var tier = 0; tier < 3; tier++)
    {
        var recipe = CoffinRecipes.ForTier(tier);
        yield return ($"tier {tier} has exactly one skeleton trophy", recipe.Single(r => r.Item == "TrophySkeleton").Amount == 1);
        yield return ($"tier {tier} has one progressing Hildir dress", recipe.Count(r => r.Item.StartsWith("ArmorDress")) == 1 && recipe.Single(r => r.Item == dresses[tier]).Amount == 1);
        yield return ($"tier {tier} uses ectoplasm without eitr or wisps", recipe.Single(r => r.Item == "Ectoplasm").Amount == (2 << tier) && !recipe.Any(r => r.Item is "Eitr" or "Wisp" or "YggdrasilWood"));
        yield return ($"tier {tier} fits the recipe UI", recipe.Length <= 6 && recipe.All(r => r.Amount > 0));
    }
    var remaining = 0;
    var seen = 0;
    var previous = 0;
    var noRepeat = true;
    for (var i = 0; i < 3; i++)
    {
        var choice = IdleDeck.Choose(7, ref remaining, previous, .4f);
        noRepeat &= (seen & choice) == 0;
        seen |= choice; previous = choice;
    }
    yield return ("idle deck visits all available activities before refilling", noRepeat && seen == 7 && remaining == 0);
    yield return ("refilled deck avoids immediate repetition", IdleDeck.Choose(7, ref remaining, previous, .99f) != previous);
    remaining = 0; seen = 0; previous = 0; noRepeat = true;
    for (var i = 0; i < 4; i++)
    {
        var choice = IdleDeck.Choose(15, ref remaining, previous, .7f);
        noRepeat &= choice != 0 && (seen & choice) == 0;
        seen |= choice; previous = choice;
    }
    yield return ("social visits participate in a complete four-activity deck", noRepeat && seen == 15 && remaining == 0);
    remaining = 0;
    yield return ("a lone social opportunity can be selected", IdleDeck.Choose(IdleDeck.Social, ref remaining, 0, 0) == IdleDeck.Social);
    foreach (var kind in new[] { SocialKind.Skeleton, SocialKind.Tame, SocialKind.Maid, SocialKind.Odin })
        yield return ($"social gestures exist for {kind}", SocialPolicy.Emotes.TryGetValue(kind, out var gestures) && gestures.Length == 2 && gestures.Distinct().Count() == 2);
    remaining = 7;
    yield return ("disabled activities are never selected", IdleDeck.Choose(1, ref remaining, 0, .99f) == 1);
    yield return ("an unavailable deck is empty", IdleDeck.Choose(0, ref remaining, 0, .5f) == 0);
    remaining = 0;
    yield return ("one available activity can repeat", IdleDeck.Choose(4, ref remaining, 4, 0) == 4);
    yield return ("sitting gently favors continuing", IdleDeck.ContinueSitting(.5999f, .6f) && !IdleDeck.ContinueSitting(.6f, .6f));
    yield return ("zero sitting chance always leaves", !IdleDeck.ContinueSitting(0, 0));
    foreach (var boss in new[] { "TrophyEikthyr", "TrophyTheElder", "TrophyBonemass", "TrophyDragonQueen", "TrophyGoblinKing", "TrophySeekerQueen", "TrophyFader", "TrophyHatchling" })
        yield return ($"boss reaction exists: {boss}", TrophyEmotes.Map.TryGetValue(boss, out var emotes) && emotes.Length is >= 1 and <= 2);
    yield return ("mounted skeleton trophies get pointing", TrophyEmotes.Map["TrophySkeleton"].Contains("point"));
}
