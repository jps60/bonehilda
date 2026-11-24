using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
internal static class BuildHudRuntimeChecks
{
    internal static IEnumerator Run()
    {
        var root=new GameObject("Private HUD capacity test");root.SetActive(false);
        var hud=root.AddComponent<Hud>();
        hud.m_requirementItems=new GameObject[6];
        for(var i=0;i<6;i++) { var entry=new GameObject("Requirement "+i,typeof(RectTransform));entry.transform.SetParent(root.transform,false);entry.transform.localPosition=Vector3.down*(40*i);hud.m_requirementItems[i]=entry; }
        var assembly=ZNetScene.instance.GetPrefab("BH_Coffin").GetComponent("Coffin").GetType().Assembly;
        var ensure=AccessTools.Method(assembly.GetType("Bonehilda.CoffinBuildHud"),"EnsureCapacity");
        foreach(var name in new[]{"BH_Coffin_0Star","BH_Coffin_1Star","BH_Coffin"}) {
            var piece=ZNetScene.instance.GetPrefab(name).GetComponent<Piece>();
            ensure.Invoke(null,new object[]{hud,piece});var entries=hud.m_requirementItems;
            var count=piece.m_resources.Length+(piece.m_craftingStation?1:0);
            if(entries.Length<count)throw new Exception("Insufficient HUD capacity: "+name);
            for(var index=0;index<count;index++)if(!entries[index])throw new Exception("Missing HUD entry: "+index);
            for(var frame=0;frame<300;frame++)ensure.Invoke(null,new object[]{hud,piece});
            if(!ReferenceEquals(entries,hud.m_requirementItems))throw new Exception("Per-frame HUD reallocation");
            if(piece.m_craftingStation && !entries[piece.m_resources.Length])throw new Exception("Station slot missing");
            Debug.Log("BUILD HUD PASS: "+name+" materials="+piece.m_resources.Length+" capacity="+entries.Length);
        }
        Debug.Log("BUILD HUD COMPLETE: real coffin recipes, station-slot bounds and 903 capacity checks; rendered HUD requires gameplay validation");
        UnityEngine.Object.Destroy(root);
        yield break;
    }
}