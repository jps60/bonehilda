using System;
using System.IO;
using System.Linq;
using BepInEx;
using Jotunn.Entities;
using UnityEngine;

namespace Bonehilda;

[Serializable]
internal sealed class MaidActivity
{
    public string id = "", name = "", activity = "", phase = "", target = "", targetId = "", cargo = "", idle = "", door = "", station = "", reservation = "";
    public int stars, pathNodes, repairRetries, skippedJobs;
    public bool localOwner, alerted, hasCargo, hasStation, inReach, targetNeedsItem, recovering, homeLoaded, chestInUse;
    public Vector3 position, targetPosition, workPoint, navigationGoal, recoveryPoint, homePosition;
    public float jobAge, targetDistance, interactionDistance, actionDelay, idleDelay;
    public string repairApproach = "";
    public Vector3 repairStandingPoint, repairSightPoint;
    public int repairProbes;
    public Vector3 eyePosition;
    public float repairReach;
    public int repairRejectedSpace, repairRejectedSight, repairRejectedPath;
    public bool workAccepted;
    public string movementPace = "";
    public string doorBlockedReason = "", doorPlan = "";
    public int doorsOpened, doorsClosed;
    public string supplyApproach = "", animatorClips = "";
    public Vector3 supplyStandingPoint;
    public int supplyRetries;
    public float interactionReach;
    public bool dustingPose, seated;
    public int stepJumps;
}

internal sealed class ActivityDiagnostics : MonoBehaviour
{
    private float nextCapture;
    internal static string SnapshotPath => Path.Combine(Paths.ConfigPath, "Bonehilda.activity.json");
    private static string HistoryPath => Path.Combine(Paths.ConfigPath, "Bonehilda.activity.log");

    private void Update()
    {
        if (!Plugin.ActivityLogging.Value || Time.unscaledTime < nextCapture) return;
        nextCapture = Time.unscaledTime + 2f;
        try { Capture(); }
        catch (Exception e) { Plugin.Instance.LogWarning("Could not capture maid activity: " + e.Message); nextCapture += 15f; }
    }

    internal static string Capture()
    {
        var utc = DateTime.UtcNow.ToString("o");
        var maids = Maid.Loaded.Where(m => m && m.View && m.View.IsValid()).Select(m => m.ReadActivity()).OrderBy(m => m.id).ToArray();
        var records = maids.Select(m => JsonUtility.ToJson(m)).ToArray();
        File.WriteAllText(SnapshotPath, ActivitySnapshotJson.Encode(Plugin.Version, utc, records, pretty: true));
        if (File.Exists(HistoryPath) && new FileInfo(HistoryPath).Length > 2 * 1024 * 1024) File.WriteAllText(HistoryPath, "");
        File.AppendAllText(HistoryPath, ActivitySnapshotJson.Encode(Plugin.Version, utc, records) + Environment.NewLine);
        return string.Join("\n", maids.Select(m => $"{m.name} [{m.id}] {m.activity}/{m.phase}; pace={m.movementPace}; accepted={m.workAccepted}; target={m.target} [{m.targetId}]; distance={m.targetDistance:F1}; reach={m.inReach}; station={m.hasStation}; retries={m.repairRetries}; cargo={m.cargo}; idle={m.idle}; door={m.door}; doorPlan={m.doorPlan}; blocked={m.doorBlockedReason}; opened/closed={m.doorsOpened}/{m.doorsClosed}; claim={m.reservation}"))
            + "\nSnapshot: " + SnapshotPath;
    }
}

internal sealed class ActivityCommand : ConsoleCommand
{
    public override string Name => "bonehilda_status";
    public override string Help => "Write a read-only snapshot of loaded Bonehildas, jobs, paths, and carried items.";
    public override void Run(string[] args) => Console.instance.Print(ActivityDiagnostics.Capture());
}
