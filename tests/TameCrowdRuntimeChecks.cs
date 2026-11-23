using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

internal static class TameCrowdRuntimeChecks
{
    private static Vector3 Origin;
    [Serializable] private class Result {
        public string scenario, mode; public int trial, requests, bypasses, accepted;
        public bool completed; public float seconds, pathMeters, stationarySeconds, wolfMeters, finalDistance;
        public Vector3 maidEnd, wolfEnd; public float wolfYieldMeters, wolfYieldTargetDistance; public int safetyChecks; public string crowdState, reject;
    }
    private static GameObject Box(string name, Vector3 center, Vector3 size) {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
        obj.transform.position=center; obj.transform.localScale=size; obj.layer=LayerMask.NameToLayer("piece");
        return obj;
    }
    private static void Config(Assembly assembly, string name, bool value) {
        var entry=assembly.GetType("Bonehilda.Plugin").GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        entry.GetType().GetProperty("Value").SetValue(entry,value,null);
    }
    private static void Remove(GameObject obj) {
        if(!obj)return; var view=obj.GetComponent<ZNetView>();
        if(view&&view.IsValid())ZNetScene.instance.Destroy(obj);else UnityEngine.Object.Destroy(obj);
    }
    // Isolated probes only: creates temporary actors/geometry and moves the private observer.
    internal static IEnumerator Run(Vector3 origin) {
        var routine=RunCore(origin);
        while(true) {
            bool next; object value;
            try { next=routine.MoveNext(); value=next?routine.Current:null; }
            catch(Exception error) { Debug.LogError("SHOO BENCH FAIL: "+error); Application.Quit(2); throw; }
            if(!next)yield break; yield return value;
        }
    }
    private static IEnumerator RunCore(Vector3 origin) {
        if (!Environment.GetCommandLineArgs().Contains("-nographics") || !Environment.GetCommandLineArgs().Contains("-savedir")) throw new Exception("A private headless savedir is required for this destructive test fixture");
        Origin=origin;
        while(!Player.m_localPlayer)yield return null;
        Player.m_localPlayer.SetGodMode(true);
        var floor=Box("Private crowd benchmark floor",Origin+new Vector3(0,-.1f,2),new Vector3(24,.2f,24));
        Player.m_localPlayer.TeleportTo(Origin+new Vector3(-8,1,-8),Quaternion.identity,true);
        ZNet.instance.SetReferencePosition(Origin);
        yield return new WaitForSecondsRealtime(5);
        var maidPrefab=ZNetScene.instance.GetPrefab("BH_Bonehilda");
        var assembly=maidPrefab.GetComponent("Maid").GetType().Assembly;
        var workerType=assembly.GetType("Bonehilda.Maid");
        var navigate=AccessTools.Method(workerType,"Navigate");
        var crowdField=AccessTools.Field(workerType,"crowd");
        var results=new List<Result>();
        var output=Path.Combine(Paths.ConfigPath,"shoo-benchmark-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".jsonl");
        foreach(var scenario in new[]{"clear","open-tame","narrow-tame"})
        for(var trial=0;trial<(File.Exists(Path.Combine(Paths.ConfigPath,"shoo-short.test"))?1:3);trial++)
        for(var order=0;order<3;order++) {
            var mode=(order+trial)%3; // Rotate execution order across repetitions.
            UnityEngine.Random.InitState(1729+trial);
            Config(assembly,"TameCrowdEnabled",mode>0); Config(assembly,"TameShooEnabled",mode==2);
            var walls=new List<GameObject>();
            if(scenario=="narrow-tame") {
                walls.Add(Box("Private left wall",Origin+new Vector3(-1.05f,1.25f,2),new Vector3(.2f,2.5f,14)));
                walls.Add(Box("Private right wall",Origin+new Vector3(1.05f,1.25f,2),new Vector3(.2f,2.5f,14)));
            }
            var coffin=UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("BH_Coffin_0Star"),Origin+new Vector3(8,.05f,0),Quaternion.identity);
            ((MonoBehaviour)coffin.GetComponent("Coffin")).enabled=false;
            var setCreator=AccessTools.Method(typeof(Piece),"SetCreator"); var creatorArgs=setCreator.GetParameters().Select(p=>p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray(); creatorArgs[0]=Player.m_localPlayer.GetPlayerID(); setCreator.Invoke(coffin.GetComponent<Piece>(),creatorArgs);
            var worker=UnityEngine.Object.Instantiate(maidPrefab,Origin+Vector3.up*.05f,Quaternion.identity);
            var maid=(MonoBehaviour)worker.GetComponent("Maid"); maid.enabled=false;
            AccessTools.Method(workerType,"Bind").Invoke(maid,new object[]{coffin.GetComponent("Coffin"),false});
            AccessTools.Method(workerType,"SetPace").Invoke(maid,new object[]{Enum.Parse(assembly.GetType("Bonehilda.MovementPace"),"Supply")});
            coffin.GetComponent<ZNetView>().GetZDO().Set("BH_maid",worker.GetComponent<ZNetView>().GetZDO().m_uid);
            GameObject wolf=null;
            if(scenario!="clear") {
                wolf=UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("Wolf"),Origin+new Vector3(0,.05f,2.1f),Quaternion.identity);
                wolf.GetComponent<Character>().SetTamed(true);
                var ai=wolf.GetComponent<MonsterAI>(); ai.m_randomMoveInterval=10000;ai.m_randomMoveRange=0;
                AccessTools.Field(typeof(BaseAI),"m_reachedRandomMoveTarget").SetValue(ai,true);
                AccessTools.Field(typeof(BaseAI),"m_randomMoveUpdateTimer").SetValue(ai,10000f);
                if(!wolf.GetComponent("TameYield"))throw new Exception("Wolf lacks shoo RPC registration");
            }
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.7f);
            var goal=Origin+new Vector3(0,0,4.5f);
            var result=new Result{scenario=scenario,trial=trial,mode=new[]{"native","bypass","shoo"}[mode]};
            var started=Time.realtimeSinceStartup;var previous=worker.transform.position;var wolfStart=wolf?wolf.transform.position:Vector3.zero;
            while(Time.realtimeSinceStartup-started<12f) {
                ZNet.instance.SetReferencePosition(Origin);
                var offset=worker.transform.position-goal;offset.y=0;
                if(offset.magnitude<.5f){result.completed=true;break;}
                navigate.Invoke(maid,new object[]{goal,Time.fixedDeltaTime,.3f,false,null});
                var delta=worker.transform.position-previous;delta.y=0;
                result.pathMeters+=delta.magnitude;
                if(Time.realtimeSinceStartup-started>.5f&&delta.magnitude<.001f)result.stationarySeconds+=Time.fixedDeltaTime;
                previous=worker.transform.position;
                yield return new WaitForFixedUpdate();
            }
            result.seconds=Time.realtimeSinceStartup-started;
            worker.GetComponent<MonsterAI>().StopMoving();
            if(wolf&&mode==2&&scenario=="narrow-tame") {
                // Finish the pet's walk after stopping the maid. Keep route timing
                // separate from this observation and the following safety checks.
                yield return new WaitForSecondsRealtime(4f);
                var yielding=wolf.GetComponent("TameYield");var yt=yielding.GetType();
                var start=(Vector3)AccessTools.Field(yt,"LastStart").GetValue(yielding);
                var target=(Vector3)AccessTools.Field(yt,"LastTarget").GetValue(yielding);
                var accepted=wolf.GetComponent<ZNetView>().GetZDO().GetInt("BH_shooAccepted");
                if(accepted>0) {
                    result.wolfYieldMeters=Vector3.Distance(wolf.transform.position,start);
                    result.wolfYieldTargetDistance=Vector3.Distance(target,start);
                    if(result.wolfYieldMeters<3||result.wolfYieldMeters>5.1f)throw new Exception("Shoo walk did not finish within 3-5 m: "+result.wolfYieldMeters);
                    var request=AccessTools.Method(yt,"Request");var view=wolf.GetComponent<ZNetView>();var mid=worker.GetComponent<ZNetView>().GetZDO().m_uid;
                    request.Invoke(yielding,new object[]{view.GetZDO().GetOwner(),mid});
                    if(view.GetZDO().GetInt("BH_shooAccepted")!=accepted)throw new Exception("Per-tame cooldown failed");
                    result.safetyChecks++;
                    AccessTools.Field(yt,"cooldown").SetValue(yielding,0f);view.GetZDO().Set("BH_nextShoo",0L);
                    request.Invoke(yielding,new object[]{long.MaxValue,mid});
                    if(view.GetZDO().GetInt("BH_shooAccepted")!=accepted)throw new Exception("Foreign requester was accepted");
                    result.safetyChecks++;
                }
                var eligible=AccessTools.Method(yt,"Eligible");var ai=wolf.GetComponent<MonsterAI>();
                wolf.GetComponent<Character>().SetTamed(false);
                if((bool)eligible.Invoke(null,new object[]{ai}))throw new Exception("Wild animal can be shooed");
                wolf.GetComponent<Character>().SetTamed(true);result.safetyChecks++;
                ai.SetFollowTarget(Player.m_localPlayer.gameObject);
                if((bool)eligible.Invoke(null,new object[]{ai}))throw new Exception("Following pet can be shooed");
                ai.SetFollowTarget(null);result.safetyChecks++;
                AccessTools.Method(typeof(MonsterAI),"SetAlerted").Invoke(ai,new object[]{true});
                if((bool)eligible.Invoke(null,new object[]{ai}))throw new Exception("Alerted pet can be shooed");
                AccessTools.Method(typeof(MonsterAI),"SetAlerted").Invoke(ai,new object[]{false});result.safetyChecks++;
                var clear=AccessTools.Method(assembly.GetType("Bonehilda.CrowdGeometry"),"Clear");
                var barrier=Box("Private safety obstruction",wolf.transform.position+Vector3.forward*2+Vector3.up, new Vector3(3,2,.3f));
                Physics.SyncTransforms();
                if((bool)clear.Invoke(null,new object[]{wolf.GetComponent<Character>(),wolf.transform.position,wolf.transform.position+Vector3.forward*4,null}))throw new Exception("Shoo route passed a solid barrier");
                UnityEngine.Object.DestroyImmediate(barrier);result.safetyChecks++;
                var door=UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("wood_door"),Origin+new Vector3(0,0,8),Quaternion.identity);
                var crosses=AccessTools.Method(assembly.GetType("Bonehilda.CrowdGeometry"),"CrossesDoor");
                foreach(var state in new[]{0,1}) {
                    door.GetComponent<ZNetView>().GetZDO().Set("state",state);
                    if(!(bool)crosses.Invoke(null,new object[]{Origin+Vector3.forward*6,Origin+Vector3.forward*10}))throw new Exception("Door crossing not rejected");
                    result.safetyChecks++;
                }
                Remove(door);
            }
            result.finalDistance=Vector3.Distance(worker.transform.position,goal); result.maidEnd=worker.transform.position-Origin;
            if(wolf){result.accepted=wolf.GetComponent<ZNetView>().GetZDO().GetInt("BH_shooAccepted");result.wolfMeters=Vector3.Distance(wolf.transform.position,wolfStart);result.wolfEnd=wolf.transform.position-Origin; var y=wolf.GetComponent("TameYield");result.reject=(string)AccessTools.Field(y.GetType(),"LastRejection").GetValue(y);}
            var crowd=crowdField.GetValue(maid);result.requests=(int)AccessTools.Field(crowd.GetType(),"Requests").GetValue(crowd);
            result.bypasses=(int)AccessTools.Field(crowd.GetType(),"Bypasses").GetValue(crowd);
            result.crowdState=(string)AccessTools.Field(crowd.GetType(),"State").GetValue(crowd);
            results.Add(result);var json=JsonUtility.ToJson(result);File.AppendAllText(output,json+Environment.NewLine);Debug.Log("SHOO BENCH "+json);
            Remove(worker);Remove(wolf);Remove(coffin);foreach(var wall in walls)UnityEngine.Object.Destroy(wall);
            yield return new WaitForSecondsRealtime(.5f);
        }
        UnityEngine.Object.Destroy(floor);
        Debug.Log("SHOO BENCH COMPLETE: "+output+" rows="+results.Count);
    }
}