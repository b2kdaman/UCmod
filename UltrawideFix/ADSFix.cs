using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Harmony;

namespace UmbrellaADS {
 public sealed class FocusReset : MonoBehaviour {
  void OnApplicationFocus(bool focused) { if(!focused) ToggleAim.ResetLatch(); }
  void OnApplicationPause(bool paused) { if(paused) ToggleAim.ResetLatch(); }
 }
 public static class ToggleAim {
  const string PatchId="local.umbrellacorps.toggleads";
  static HarmonyInstance harmony;
  static Type playerType;
  static FieldInfo aimPower,dead,sprintKey;
  static MethodInfo setFlag,equipShot,controlStop;
  static object aimFlag;
  static bool initialized,enabled=true;
  static float nextConfig;
  static DateTime stamp;
  static GameObject focusDriver;
  class State { public bool down,latched; }
  static Dictionary<int,State> states=new Dictionary<int,State>();
  static void Log(string message) { File.AppendAllText("UltrawideFix/ads.log",DateTime.Now.ToString("HH:mm:ss ")+message+"\r\n"); }
  public static void Update() {
   if(!initialized) {
    initialized=true;
    Assembly game=null;
    foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()) if(a.GetName().Name=="Assembly-CSharp") game=a;
    if(game==null) throw new Exception("Game assembly unavailable");
    playerType=game.GetType("K2.PlayerControl",true);
    BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    aimPower=playerType.GetField("AimPower",flags);
    dead=playerType.GetField("IsDead",flags);
    sprintKey=playerType.GetField("SprintKey",flags);
    equipShot=playerType.GetMethod("IsEquipShot",flags);
    setFlag=playerType.GetMethod("SetActionFlag",flags);
    aimFlag=Enum.Parse(setFlag.GetParameters()[0].ParameterType,"AimInput");
    MethodInfo original=playerType.GetMethod("Update_PadInfo_Master",flags);
    foreach(Type t in game.GetTypes()) {
     MethodInfo m=t.GetMethod("IsPlayerControlStop",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
     if(m!=null && m.GetParameters().Length==0) { controlStop=m; break; }
    }
    if(aimPower==null||dead==null||equipShot==null||controlStop==null||original==null) throw new Exception("Required player/input members unavailable");
    harmony=HarmonyInstance.Create(PatchId);
    harmony.Patch(original,null,new HarmonyMethod(typeof(ToggleAim).GetMethod("AfterInput")),null);
    focusDriver=new GameObject("ToggleADSFocusReset");
    UnityEngine.Object.DontDestroyOnLoad(focusDriver);
    focusDriver.AddComponent<FocusReset>();
    Log("Installed toggle ADS on local player input; stop check="+controlStop.DeclaringType.FullName);
   }
   if((bool)controlStop.Invoke(null,null)) ResetLatch();
   if(Time.realtimeSinceStartup<nextConfig) return;
   nextConfig=Time.realtimeSinceStartup+1;
   const string path="UltrawideFix/settings.ini";
   if(File.Exists(path)&&File.GetLastWriteTimeUtc(path)!=stamp) {
    stamp=File.GetLastWriteTimeUtc(path);
    foreach(string raw in File.ReadAllLines(path)) {
     string line=raw.Trim(); if(!line.StartsWith("ToggleADS=",StringComparison.OrdinalIgnoreCase)) continue;
     bool value; if(bool.TryParse(line.Substring(line.IndexOf('=')+1).Trim(),out value)) enabled=value;
    }
    if(!enabled) states.Clear();
    Log("ToggleADS="+enabled);
   }
  }
  public static void ResetLatch() {
   foreach(State state in states.Values) {
    if(state.latched) Log("Aim cleared by pause/focus change");
    state.latched=false;
   }
  }
  public static void AfterInput(object __instance) {
   if(!enabled) return;
   try {
    UnityEngine.Object player=(UnityEngine.Object)__instance;
    int id=player.GetInstanceID(); State state;
    if(!states.TryGetValue(id,out state)) { state=new State(); states[id]=state; }
    bool raw=(float)aimPower.GetValue(__instance)>0.01f;
    bool stop=(bool)controlStop.Invoke(null,null)||(bool)dead.GetValue(__instance)||!(bool)equipShot.Invoke(__instance,null);
    if(sprintKey!=null) {
     object key=sprintKey.GetValue(__instance);
     if(key!=null) stop|=(bool)key.GetType().GetField("On").GetValue(key);
    }
    if(stop) {
     if(state.latched) Log("Aim cleared by gameplay state");
     state.latched=false; state.down=raw; return;
    }
    if(raw&&!state.down) { state.latched=!state.latched; Log("Aim toggled "+(state.latched?"ON":"OFF")); }
    state.down=raw;
    aimPower.SetValue(__instance,state.latched?1f:0f);
    setFlag.Invoke(__instance,new object[]{aimFlag,state.latched});
   } catch(Exception e) { enabled=false; states.Clear(); Log("Disabled after error: "+e); }
  }
  public static void Stop() {
   enabled=false; states.Clear();
   if(harmony!=null) harmony.UnpatchAll(PatchId);
   if(focusDriver!=null) UnityEngine.Object.Destroy(focusDriver);
   Log("Toggle ADS stopped");
  }
 }
}
