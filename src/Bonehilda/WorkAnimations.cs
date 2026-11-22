using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace Bonehilda;

// Retarget the player's humanoid controller onto the skeleton's own avatar.
// The hammer is equipment scenery, rather than an extra lootable inventory item.
internal sealed class WorkAnimations : MonoBehaviour
{
    private const string DrawnKey = "BH_hammerDrawn";
    private ZNetView view = null!;
    private ZSyncAnimation sync = null!;
    private GameObject held = null!;
    private GameObject belted = null!;
    private string swing = "swing_hammer";
    private int hammerState;
    private const string EmoteKey = "BH_idleEmote", EmoteId = "BH_idleEmoteId", EmoteEnd = "BH_idleEmoteEnd";
    private int seenEmote;
    private Animator animator = null!;
    private string booleanEmote = "";
    internal bool HammerDrawn
    {
        get
        {
            if (!view) view = GetComponent<ZNetView>();
            return view && view.IsValid() && view.GetZDO().GetBool(DrawnKey);
        }
    }

    internal static void Configure(GameObject maid, GameObject player)
    {
        var animator = maid.GetComponentInChildren<Animator>(true);
        var source = player.GetComponentInChildren<Animator>(true);
        if (!animator.avatar.isHuman || !source.runtimeAnimatorController)
            throw new InvalidOperationException("Player animations require a humanoid skeleton avatar.");
        animator.runtimeAnimatorController = source.runtimeAnimatorController;
        CoffinPerspective.Attach(animator);
        var sync = maid.GetComponent<ZSyncAnimation>();
        var playerSync = player.GetComponent<ZSyncAnimation>();
        sync.m_syncBools = new List<string>(playerSync.m_syncBools);
        sync.m_syncFloats = new List<string>(playerSync.m_syncFloats);
        sync.m_syncInts = new List<string>(playerSync.m_syncInts);

        var equipment = maid.GetComponent<VisEquipment>();
        var playerEquipment = player.GetComponent<VisEquipment>();
        equipment.m_rightHand = CopySocket(maid.transform, playerEquipment.m_rightHand, "BH_HammerHand");
        equipment.m_backTool = CopySocket(maid.transform, playerEquipment.m_backTool, "BH_HammerBelt");
        maid.AddComponent<WorkAnimations>();
    }

    private static Transform CopySocket(Transform root, Transform source, string name)
    {
        var bone = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == source.parent.name);
        if (!bone) throw new InvalidOperationException("Missing equipment bone: " + source.parent.name);
        var socket = new GameObject(name).transform;
        socket.SetParent(bone, false);
        socket.localPosition = source.localPosition;
        socket.localRotation = source.localRotation;
        socket.localScale = source.localScale;
        return socket;
    }

    private void Start()
    {
        view = GetComponent<ZNetView>();
        sync = GetComponent<ZSyncAnimation>();
        animator = GetComponentInChildren<Animator>();
        animator.SetBool("wakeup", false);
        var equipment = GetComponent<VisEquipment>();
        var hammer = PrefabManager.Instance.GetPrefab("Hammer");
        var data = hammer.GetComponent<ItemDrop>().m_itemData;
        swing = data.m_shared.m_attack.m_attackAnimation;
        hammerState = (int)data.m_shared.m_animationState;
        held = MakeTool(hammer, equipment.m_rightHand, false);
        belted = MakeTool(hammer, equipment.m_backTool, true);
        RefreshTool();
    }

    private static GameObject MakeTool(GameObject hammer, Transform socket, bool back)
    {
        var visual = back ? hammer.transform.Find("attach_back") : null;
        if (!visual) visual = hammer.transform.Find("attach");
        if (!visual) throw new InvalidOperationException("Hammer equipment visual is missing.");
        // Match VisEquipment.AttachItem: imported skeleton bones have model-unit
        // scale. Parenting with worldPositionStays=false multiplies the hammer
        // by that scale, turning its handle into an enormous wooden beam.
        var tool = Instantiate(visual!.gameObject);
        tool.transform.SetParent(socket, true);
        tool.name = back ? "BH_BeltHammer" : "BH_HeldHammer";
        tool.transform.localPosition = Vector3.zero;
        tool.transform.localRotation = Quaternion.identity;
        var offset = hammer.transform.Find("equipoffset");
        if (offset)
        {
            tool.transform.localPosition += offset.position;
            tool.transform.localRotation *= offset.rotation;
        }
        foreach (var collider in tool.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        tool.SetActive(false);
        return tool;
    }

    private void LateUpdate()
    {
        RefreshTool();
        if (!view || !view.IsValid() || !animator) return;
        var data = view.GetZDO();
        if (view.IsOwner() && data.GetString(EmoteKey).Length != 0 && ZNet.instance.GetTime().Ticks >= data.GetLong(EmoteEnd)) StopEmote();
        var revision = data.GetInt(EmoteId);
        if (seenEmote == revision) return;
        seenEmote = revision;
        if (booleanEmote.Length != 0) animator.SetBool(booleanEmote, false);
        booleanEmote = "";
        animator.SetTrigger("emote_stop");
        var emote = data.GetString(EmoteKey);
        if (emote.Length == 0 || ZNet.instance.GetTime().Ticks >= data.GetLong(EmoteEnd)) return;
        animator.ResetTrigger("emote_stop");
        var parameter = animator.parameters.FirstOrDefault(p => p.name == "emote_" + emote);
        if (parameter != null && parameter.type == AnimatorControllerParameterType.Bool)
        {
            booleanEmote = parameter.name;
            animator.SetBool(booleanEmote, true);
        }
        else animator.SetTrigger("emote_" + emote);
    }

    internal void Emote(string name, float seconds)
        => BeginEmote(name, ZNet.instance.GetTime().Ticks + (long)(seconds * TimeSpan.TicksPerSecond));

    internal void RestEmote(string name) => BeginEmote(name, long.MaxValue);

    private void BeginEmote(string name, long until)
    {
        SetHammer(false, false);
        var data = view.GetZDO();
        data.Set(EmoteKey, name);
        data.Set(EmoteEnd, until);
        data.Set(EmoteId, data.GetInt(EmoteId) + 1);
    }

    internal void StopEmote()
    {
        if (!view) view = GetComponent<ZNetView>();
        if (!view || !view.IsValid() || !view.IsOwner()) return;
        var data = view.GetZDO();
        if (data.GetString(EmoteKey).Length == 0) return;
        data.Set(EmoteKey, "");
        data.Set(EmoteId, data.GetInt(EmoteId) + 1);
    }

    private void RefreshTool()
    {
        if (!view || !view.IsValid() || !held || !belted) return;
        var drawn = view.GetZDO().GetBool(DrawnKey);
        held.SetActive(drawn);
        belted.SetActive(!drawn);
    }

    internal void SetHammer(bool drawn, bool animate = true)
    {
        // Start order between components is unspecified; obtain these on demand too.
        if (!view) view = GetComponent<ZNetView>();
        if (!sync) sync = GetComponent<ZSyncAnimation>();
        view.GetZDO().Set(DrawnKey, drawn);
        sync.SetFloat("statef", drawn ? hammerState : 0);
        sync.SetInt("statei", drawn ? hammerState : 0);
        if (animate) sync.SetTrigger("equip_hip"); // The same trigger used by the player's R key.
        RefreshTool();
    }

    internal void Repair() => sync.SetTrigger(swing);
    internal void Interact() => sync.SetTrigger("interact");
}
