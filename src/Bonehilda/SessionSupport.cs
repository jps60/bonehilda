using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace Bonehilda;

// Keep registered assets intact across world changes. Only the current server
// decides whether gameplay is available; a missing handshake fails closed.
internal static class SessionSupport
{
    internal const string UnsupportedMessage = "Bonehilda is not supported on this server.";
    internal static bool Active => !ZNet.instance || ZNet.instance.IsServer()
        || ModCompatibility.IsModuleOnServer(Plugin.Guid);

    internal static List<GameObject> BuildPieces(List<GameObject> pieces) => Active ? pieces
        : pieces.Where(piece => !piece || !piece.GetComponent<Coffin>()).ToList();

    internal static void Notify()
    {
        // Valheim 1.0 added optional flags to the message API. Resolve its native
        // defaults so both the reference-assembly and installed signatures work.
        var show = AccessTools.Method(typeof(MessageHud), nameof(MessageHud.ShowMessage));
        var args = show.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        args[0] = MessageHud.MessageType.Center;
        args[1] = UnsupportedMessage;
        show.Invoke(MessageHud.instance, args);
    }
}

internal sealed class SessionNotice : MonoBehaviour
{
    private ZNet? session;
    private bool shown;

    private void Update()
    {
        var current = ZNet.instance;
        if (current != session) { session = current; shown = false; }
        if (!current || current.IsServer() || shown || !Player.m_localPlayer || !MessageHud.instance
            || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected) return;
        shown = true;
        if (!SessionSupport.Active)
        {
            SessionSupport.Notify();
            Plugin.Instance.LogInfo(SessionSupport.UnsupportedMessage);
        }
    }
}

// Filter the input to native menu generation, including no-cost mode, favorites
// and categories. Restore the registered list even if another patch throws.
[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
internal static class SessionBuildMenu
{
    private static void Prefix(PieceTable __instance, out List<GameObject> __state)
    {
        __state = __instance.m_pieces;
        __instance.m_pieces = SessionSupport.BuildPieces(__state);
    }

    private static Exception? Finalizer(PieceTable __instance, List<GameObject> __state, Exception? __exception)
    {
        __instance.m_pieces = __state;
        return __exception;
    }
}
