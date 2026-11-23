using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

// Private startup probe only: substitute handshake data and an inactive ZNet,
// then restore every global. No socket, live world or real server is involved.
internal static class SessionRuntimeChecks
{
    private static int notices;
    private static bool CaptureMessage(MessageHud.MessageType __0, string __1)
    {
        if (__0 != MessageHud.MessageType.Center || __1 != "Bonehilda is not supported on this server.")
            throw new Exception("Unsupported notice must use the fading center message");
        notices++;
        return false;
    }

    internal static void Run(Assembly assembly)
    {
        var support = assembly.GetType("Bonehilda.SessionSupport", true);
        var active = support.GetProperty("Active", BindingFlags.Static | BindingFlags.NonPublic);
        var plugin = assembly.GetType("Bonehilda.Plugin", true);
        var attribute = plugin.GetCustomAttributesData().Single(a => a.AttributeType == typeof(NetworkCompatibilityAttribute));
        if ((int)attribute.ConstructorArguments[0].Value != (int)CompatibilityLevel.ClientMustHaveMod || (int)attribute.ConstructorArguments[1].Value != (int)VersionStrictness.Patch)
            throw new Exception("Session compatibility must allow an absent server mod and enforce exact versions");
        var compat = typeof(ModCompatibility);
        var jotunn = compat.Assembly;
        var module = jotunn.GetType("Jotunn.Utils.ModModule", true);
        var moduleData = jotunn.GetType("Jotunn.Utils.ModuleVersionData", true);
        var serverData = jotunn.GetType("Jotunn.Utils.ServerVersionData", true);
        var listType = typeof(List<>).MakeGenericType(module);
        Func<string, IList> modules = version => {
            var list = (IList)Activator.CreateInstance(listType);
            if (version != null) list.Add(Activator.CreateInstance(module, "com.bonehilda.valheim", "Bonehilda", new System.Version(version),
                CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Patch));
            return list;
        };
        Func<IList, object> data = list => Activator.CreateInstance(moduleData, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { list }, null);
        var compare = AccessTools.Method(compat, "CompareVersionData");
        var versionNow = (string)plugin.GetField("Version").GetRawConstantValue();
        var current = modules(versionNow);
        var absent = modules(null);
        if (!(bool)compare.Invoke(null, new[] { data(absent), data(current) })) throw new Exception("Absent server mod was rejected");
        if (!(bool)compare.Invoke(null, new[] { data(current), data(current) })) throw new Exception("Matching version was rejected");
        if ((bool)compare.Invoke(null, new[] { data(current), data(modules("0.4.29")) })) throw new Exception("Old client version was accepted");
        if ((bool)compare.Invoke(null, new[] { data(modules("0.4.29")), data(current) })) throw new Exception("Old server version was accepted");
        if ((bool)compare.Invoke(null, new[] { data(current), data(absent) })) throw new Exception("Missing client mod was accepted");

        var instance = AccessTools.Field(typeof(ZNet), "m_instance");
        var isServer = AccessTools.Field(typeof(ZNet), "m_isServer");
        var remote = AccessTools.Field(compat, "LastServerVersionData");
        var oldInstance = instance.GetValue(null); var oldServer = isServer.GetValue(null); var oldRemote = remote.GetValue(null);
        var root = new GameObject("Private session compatibility checks"); root.SetActive(false);
        var net = root.AddComponent<ZNet>();
        var table = root.AddComponent<PieceTable>();
        var player = PrefabManager.Instance.GetPrefab("Player").GetComponent<Player>();
        var vanilla = PrefabManager.Instance.GetPrefab("piece_chest_wood");
        var coffin = PieceManager.Instance.GetPiece("BH_Coffin").PiecePrefab;
        table.m_pieces = new List<GameObject> { vanilla, coffin };
        var original = table.m_pieces;
        Func<IList, object> server = list => Activator.CreateInstance(serverData, BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { list }, null);
        try
        {
            instance.SetValue(null, net); isServer.SetValue(null, false);
            // No Jotunn handshake (vanilla server) and a Jotunn server without
            // Bonehilda must both fail closed for gameplay.
            remote.SetValue(null, Activator.CreateInstance(serverData, true));
            if ((bool)active.GetValue(null, null)) throw new Exception("Missing handshake enabled gameplay");
            remote.SetValue(null, server(absent));
            if ((bool)active.GetValue(null, null)) throw new Exception("Unsupported server enabled gameplay");
            table.UpdateAvailable(new HashSet<string>(), player, false, true);
            if (((HashSet<Piece>)AccessTools.Field(typeof(PieceTable), "m_enabledPieces").GetValue(table)).Contains(coffin.GetComponent<Piece>()) || !((HashSet<Piece>)AccessTools.Field(typeof(PieceTable), "m_enabledPieces").GetValue(table)).Contains(vanilla.GetComponent<Piece>()))
                throw new Exception("Unsupported menu failed to hide only coffins, including no-cost mode");
            if (!ReferenceEquals(table.m_pieces, original) || original.Count != 2) throw new Exception("Menu filter lost registered assets");
            if (player.HaveRequirements(coffin.GetComponent<Piece>(), Player.RequirementMode.IsKnown)) throw new Exception("Unsupported coffin requirements passed");
            if (player.TryPlacePiece(coffin.GetComponent<Piece>())) throw new Exception("Unsupported coffin placement passed");
            VerifyNotice(assembly, root, net, player);
            remote.SetValue(null, server(current));
            if (!(bool)active.GetValue(null, null)) throw new Exception("Supported server did not reactivate gameplay");
            table.UpdateAvailable(new HashSet<string>(), player, false, true);
            if (!((HashSet<Piece>)AccessTools.Field(typeof(PieceTable), "m_enabledPieces").GetValue(table)).Contains(coffin.GetComponent<Piece>())) throw new Exception("Supported menu did not restore coffin");
            remote.SetValue(null, server(absent));
            isServer.SetValue(null, true);
            if (!(bool)active.GetValue(null, null)) throw new Exception("Local world inherited remote unsupported state");
            table.UpdateAvailable(new HashSet<string>(), player, false, true);
            if (!((HashSet<Piece>)AccessTools.Field(typeof(PieceTable), "m_enabledPieces").GetValue(table)).Contains(coffin.GetComponent<Piece>())) throw new Exception("Local menu did not restore coffin");
        }
        finally
        {
            instance.SetValue(null, oldInstance); isServer.SetValue(null, oldServer); remote.SetValue(null, oldRemote);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyNotice(Assembly assembly, GameObject root, ZNet net, Player player)
    {
        var localPlayer = AccessTools.Field(typeof(Player), "m_localPlayer");
        var hudInstance = AccessTools.Field(typeof(MessageHud), "m_instance");
        var connection = AccessTools.Field(typeof(ZNet), "m_connectionStatus");
        var external = AccessTools.Field(typeof(ZNet), "m_externalError");
        var oldPlayer = localPlayer.GetValue(null); var oldHud = hudInstance.GetValue(null);
        var oldConnection = connection.GetValue(null); var oldExternal = external.GetValue(null);
        var hud = root.AddComponent<MessageHud>();
        var notice = root.AddComponent(assembly.GetType("Bonehilda.SessionNotice", true));
        var update = AccessTools.Method(notice.GetType(), "Update");
        var harmony = new Harmony("com.bonehilda.private-notice-check");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(MessageHud), nameof(MessageHud.ShowMessage)), prefix: new HarmonyMethod(typeof(SessionRuntimeChecks), nameof(CaptureMessage)));
            localPlayer.SetValue(null, player); hudInstance.SetValue(null, hud);
            external.SetValue(null, ZNet.ConnectionStatus.None);
            connection.SetValue(null, ZNet.ConnectionStatus.Connecting);
            notices = 0;
            update.Invoke(notice, null);
            if (notices != 0) throw new Exception("Notice appeared before successful login");
            connection.SetValue(null, ZNet.ConnectionStatus.Connected);
            update.Invoke(notice, null); update.Invoke(notice, null);
            if (notices != 1) throw new Exception("Login notice missing or repeated every frame");
            AccessTools.Field(typeof(ZNet), "m_instance").SetValue(null, null);
            update.Invoke(notice, null);
            AccessTools.Field(typeof(ZNet), "m_instance").SetValue(null, net);
            update.Invoke(notice, null);
            if (notices != 2) throw new Exception("New session did not receive its own login notice");
        }
        finally
        {
            harmony.UnpatchSelf();
            localPlayer.SetValue(null, oldPlayer); hudInstance.SetValue(null, oldHud);
            connection.SetValue(null, oldConnection); external.SetValue(null, oldExternal);
        }
    }
}
