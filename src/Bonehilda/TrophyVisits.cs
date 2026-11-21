using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Bonehilda;

internal sealed class TrophyVisits
{
    private static readonly MethodInfo AttachedItem = AccessTools.Method(typeof(ItemStand), "GetAttachedItem");
    private static readonly FieldInfo VisualItem = AccessTools.Field(typeof(ItemStand), "m_visualItem");
    private ItemStand? target;
    private string item = "";
    private Vector3 standingPoint;
    private float deadline;
    private bool reacting;
    internal bool Performed { get; private set; }
    internal string Describe => target ? item + " " + standingPoint.ToString("F2") + (reacting ? " emoting" : " approaching") : "none";

    internal static void Configure()
    {
        foreach (var boss in Resources.FindObjectsOfTypeAll<Character>().Where(c => c.m_boss))
        {
            var drops = boss.GetComponent<CharacterDrop>();
            if (!drops) continue;
            foreach (var drop in drops.m_drops)
                if (drop.m_prefab && drop.m_prefab.name.StartsWith("Trophy", System.StringComparison.Ordinal))
                    TrophyEmotes.AddBoss(drop.m_prefab.name);
        }
    }

    internal bool TryBegin(Maid maid, Coffin home, System.Collections.Generic.List<Piece> pieces)
    {
        target = null;
        Performed = false;
        var best = float.PositiveInfinity;
        var previous = maid.View.GetZDO().GetZDOID("BH_lastTrophy");
        foreach (var piece in pieces)
        {
            if (!Eligible(piece, home)) continue;
            var stand = piece.GetComponent<ItemStand>();
            if (!stand) continue;
            var name = ItemName(stand);
            if (!TrophyEmotes.Map.ContainsKey(name)) continue;
            var aim = LookPoint(stand, out var front);
            var point = aim + front * Plugin.TrophyDistance.Value;
            if (!IdleActivities.Floor(point, aim.y - 1f, out point, 4f)) continue;
            if (Mathf.Abs(aim.y - point.y) > 2.8f
                || (point - home.transform.position).sqrMagnitude > Plugin.Radius.Value * Plugin.Radius.Value) continue;
            if (Physics.Linecast(point + Vector3.up, aim, out var obstruction,
                    LayerMask.GetMask("piece", "terrain", "static_solid"), QueryTriggerInteraction.Ignore)
                && obstruction.collider.GetComponentInParent<Piece>() != piece) continue;
            var score = (point - maid.transform.position).sqrMagnitude;
            // Prefer a different display without turning a single trophy into a dead end.
            var view = piece.GetComponent<ZNetView>();
            if (view && view.IsValid() && view.GetZDO().m_uid == previous) score += 10000f;
            if (score >= best) continue;
            best = score; target = stand; item = name; standingPoint = point;
        }
        deadline = Time.time + Mathf.Min(30f, Plugin.JobTimeout.Value);
        reacting = false;
        return target;
    }

    internal bool Tick(Maid maid, MonsterAI ai, Coffin home, float delta)
    {
        if (!target || Time.time >= deadline || ItemName(target!) != item || !Eligible(target!.GetComponent<Piece>(), home)) return false;
        var aim = LookPoint(target, out var front);
        var offset = standingPoint - maid.transform.position; offset.y = 0;
        var toMaid = maid.transform.position - aim; toMaid.y = 0;
        var close = offset.magnitude <= .45f && toMaid.magnitude <= Plugin.TrophyDistance.Value + .5f
            && Vector3.Dot(toMaid.normalized, front) >= .7f && maid.CanReach(target.GetComponent<Piece>(), aim);
        if (!close)
        {
            if (reacting) return false;
            maid.Navigate(standingPoint, delta, .3f, offset.magnitude < 1f && ClearPath(maid, standingPoint));
            return true;
        }
        maid.StopNavigation(); ai.StopMoving(); ai.LookAt(aim);
        var facing = aim - maid.transform.position; facing.y = 0;
        if (!reacting && Vector3.Dot(maid.transform.forward, facing.normalized) > .9f)
        {
            var choices = TrophyEmotes.Map[item];
            maid.GetComponent<WorkAnimations>().Emote(choices[Random.Range(0, choices.Length)], 5f);
            maid.View.GetZDO().Set("BH_lastTrophy", target.GetComponent<ZNetView>().GetZDO().m_uid);
            reacting = true;
            Performed = true;
            deadline = Time.time + 5f;
        }
        return true;
    }

    internal void Cancel(Maid maid)
    {
        if (reacting) maid.GetComponent<WorkAnimations>().StopEmote();
        target = null; reacting = false;
    }

    private static bool Eligible(Piece piece, Coffin home) => piece && piece.IsPlacedByPlayer()
        && (piece.transform.position - home.transform.position).sqrMagnitude <= Plugin.Radius.Value * Plugin.Radius.Value
        && Maid.Allowed(piece.transform.position, home.Piece.GetCreator());

    private static string ItemName(ItemStand stand)
    {
        var value = AttachedItem.Invoke(stand, null);
        // Valheim 1.0 stores item hashes; older versions returned a prefab name.
        if (value is string name) return name;
        var prefab = value is int hash && hash != 0 ? ObjectDB.instance.GetItemPrefab(hash) : null;
        return prefab ? prefab!.name : "";
    }

    private static Vector3 LookPoint(ItemStand stand, out Vector3 front)
    {
        var anchor = stand.m_attachOther ? stand.m_attachOther : stand.transform;
        front = anchor.position - stand.transform.position; front.y = 0;
        if (front.sqrMagnitude < .001f) { front = stand.transform.forward; front.y = 0; }
        if (front.sqrMagnitude < .001f) { front = stand.transform.up; front.y = 0; }
        front.Normalize();
        var point = anchor.position;
        var visual = VisualItem.GetValue(stand) as GameObject;
        if (visual)
        {
            var renderers = visual!.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length != 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var extent = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(front.x), 0, Mathf.Abs(front.z)));
                point = bounds.center + front * extent;
            }
        }
        return point;
    }

    private static bool ClearPath(Maid maid, Vector3 point)
    {
        var offset = point - maid.transform.position;
        return !Physics.SphereCast(maid.transform.position + Vector3.up * .75f, .25f,
            offset.normalized, out _, offset.magnitude, LayerMask.GetMask("piece", "terrain", "static_solid"), QueryTriggerInteraction.Ignore);
    }
}
