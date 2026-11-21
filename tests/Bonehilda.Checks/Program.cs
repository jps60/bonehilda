using Bonehilda;

var checks = new (string Name, bool Passed)[]
{
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
    ("wandering keeps a margin within the work radius", MaintenancePolicy.WanderRange(50, 30) == 28),
    ("small radius also constrains wandering", MaintenancePolicy.WanderRange(10, 5) == 3),
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
foreach (var check in checks)
    if (!check.Passed) throw new Exception($"FAILED: {check.Name}");
Console.WriteLine($"Passed {checks.Length} maintenance policy checks.");

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
