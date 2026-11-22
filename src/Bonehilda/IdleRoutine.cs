using UnityEngine;

namespace Bonehilda;

internal static class IdleRoutine
{
    internal static string Gesture(Maid maid, string category, string[] choices)
    {
        var data = maid.View.GetZDO();
        var key = "BH_gesture_" + category;
        var index = RoutinePolicy.Gesture(choices.Length, data.GetInt(key, -1), Random.value);
        data.Set(key, index);
        return choices[index];
    }

    internal static bool Favorite(Maid maid, Piece piece) => piece.GetComponent<ZNetView>() is ZNetView view && view.IsValid()
        && maid.View.GetZDO().GetZDOID("BH_favoriteRest") == view.GetZDO().m_uid;

    internal static void RememberRest(Maid maid, Piece piece, Vector3 point)
    {
        if (!Plugin.NaturalRoutines.Value || !piece) return;
        var view = piece.GetComponent<ZNetView>();
        if (!view || !view.IsValid()) return;
        var data = maid.View.GetZDO();
        // Keep a familiar place, with an occasional new favorite.
        if (data.GetZDOID("BH_favoriteRest") != ZDOID.None && !Favorite(maid, piece) && Random.value > .2f) return;
        data.Set("BH_favoriteRest", view.GetZDO().m_uid);
        data.Set("BH_favoriteRestPoint", piece.transform.InverseTransformPoint(point));
    }
}
