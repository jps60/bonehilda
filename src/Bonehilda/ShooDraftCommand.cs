using System.Linq;
using Jotunn.Entities;
using UnityEngine;

namespace Bonehilda;

internal sealed class ShooDraftCommand : ConsoleCommand
{
    public override string Name => "bonehilda_shoo";
    public override string Help => "Review the nearest owned maid's upper-body gesture: left, right, both (within 8 m). Work and passage animations take priority.";
    public override void Run(string[] args)
    {
        var name = args.Length == 0 ? "left" : args[0].ToLowerInvariant();
        var variant = name == "left" ? 0 : name == "right" ? 1 : name == "both" ? 2 : -1;
        if (variant < 0) { Console.instance.Print("Use bonehilda_shoo left|right|both"); return; }
        if (!Player.m_localPlayer) return;
        var player = Player.m_localPlayer;
        var maid = Maid.Loaded.Where(m => m && m.View && m.View.IsValid() && m.View.IsOwner()
            && (m.transform.position - player.transform.position).sqrMagnitude < 64f)
            .OrderBy(m => (m.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
        if (!maid || !ShooGesture.Begin(maid, variant, true))
        { Console.instance.Print("No nearby owned maid available for a gesture. Let her finish work/interactions first."); return; }
        Console.instance.Print("Draft gesture: " + name + ". Hammer drawn: the left arm is used instead. Please review wrist/palm orientation and the skeleton rattle.");
    }
}
