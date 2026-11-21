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
}

internal sealed class ActivityDiagnostics : MonoBehaviour
{
    [Serializable]
    private sealed class Snapshot
    {
        public string version = Plugin.Version, utc = "";
        public MaidActivity[] maids = Array.Empty<MaidActivity>();
    }
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
        var snapshot = new Snapshot { utc = DateTime.UtcNow.ToString("o"),
            maids = Maid.Loaded.Where(m => m && m.View && m.View.IsValid()).Select(m => m.ReadActivity()).OrderBy(m => m.id).ToArray() };
        File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snapshot, true));
        if (File.Exists(HistoryPath) && new FileInfo(HistoryPath).Length > 2 * 1024 * 1024) File.WriteAllText(HistoryPath, "");
        File.AppendAllText(HistoryPath, JsonUtility.ToJson(snapshot) + Environment.NewLine);
        return string.Join("\n", snapshot.maids.Select(m => $"{m.name} [{m.id}] {m.activity}/{m.phase}; target={m.target}; distance={m.targetDistance:F1}; retries={m.repairRetries}; cargo={m.cargo}; idle={m.idle}; door={m.door}"))
            + "\nSnapshot: " + SnapshotPath;
    }
}

internal sealed class ActivityCommand : ConsoleCommand
{
    public override string Name => "bonehilda_status";
    public override string Help => "Write a read-only snapshot of loaded Bonehildas, jobs, paths, and carried items.";
    public override void Run(string[] args) => Console.instance.Print(ActivityDiagnostics.Capture());
}
