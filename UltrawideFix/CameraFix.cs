using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Doorstop {
 public static class Entrypoint {
  public static void Start() {
   File.AppendAllText("UltrawideFix/runtime.log", "Doorstop entry reached\r\n");
   Thread t = new Thread(Boot); t.IsBackground=true; t.Start();
  }
  static void Boot() {
   Thread.Sleep(10000);
   try { CameraProbe.Attach(); }
   catch(Exception e) { File.AppendAllText("UltrawideFix/runtime.log", e.ToString()+"\r\n"); }
  }
 }
 public sealed class FrameRestore : MonoBehaviour {
  void Update() { CameraProbe.UpdateUI(); ExtensionLoader.Update(); }
  IEnumerator Start() {
   while(true) {
    yield return new WaitForEndOfFrame();
    CameraProbe.RestoreAll();
   }
  }
 }
 public static class CameraProbe {
  static HashSet<int> seen=new HashSet<int>();
  static HashSet<int> canvasSeen=new HashSet<int>();
  static Dictionary<int, Saved> saved=new Dictionary<int, Saved>();
  static Dictionary<int, Saved> uiOriginal=new Dictionary<int, Saved>();
  static Dictionary<int, LayoutSaved> layouts=new Dictionary<int, LayoutSaved>();
  static float nextCheck;
  static float nextAudit;
  static DateTime configTime;
  static bool enabled=true;
  static int width=3440,height=1440;
  static float horizontal=120f,nativeFov=55f;
  static float uiScale=1f;
  static string cameraName="Camera";
  static bool restoreStarted;
  struct Saved { public Camera camera; public float fov,aspect; public Rect rect; }
  struct LayoutSaved { public CanvasScaler scaler; public CanvasScaler.ScreenMatchMode mode; public float match; public Vector2 reference; }
  static void Log(string s) { File.AppendAllText("UltrawideFix/runtime.log",s+"\r\n"); }
  static void Settings() {
   if(Time.realtimeSinceStartup<nextCheck) return;
   nextCheck=Time.realtimeSinceStartup+1f;
   foreach(Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) {
    CanvasScaler layout=canvas.GetComponent<CanvasScaler>();
    if(canvas.renderMode!=RenderMode.WorldSpace && layout!=null && layout.uiScaleMode==CanvasScaler.ScaleMode.ScaleWithScreenSize && !layouts.ContainsKey(layout.GetInstanceID())) {
     layouts[layout.GetInstanceID()]=new LayoutSaved { scaler=layout,mode=layout.screenMatchMode,match=layout.matchWidthOrHeight,reference=layout.referenceResolution };
    }
    if(canvasSeen.Add(canvas.GetInstanceID())) {
     CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
     Log("Canvas="+canvas.name+" mode="+canvas.renderMode+" scale="+canvas.scaleFactor+" camera="+(canvas.worldCamera==null?"none":canvas.worldCamera.name)+" scaler="+(scaler==null?"none":scaler.uiScaleMode+" reference="+scaler.referenceResolution+" match="+scaler.matchWidthOrHeight+" screenMatch="+scaler.screenMatchMode));
    }
   }
   const string path="UltrawideFix/settings.ini";
   if(File.Exists(path) && File.GetLastWriteTimeUtc(path)!=configTime) {
    foreach(string raw in File.ReadAllLines(path)) {
     string line=raw.Trim(); if(line.StartsWith("#")||!line.Contains("=")) continue;
     string[] parts=line.Split(new char[]{'='},2); string k=parts[0].Trim(),v=parts[1].Trim();
     if(k=="Enabled") enabled=bool.Parse(v);
     if(k=="Width") width=Math.Max(640,Math.Min(16384,int.Parse(v)));
     if(k=="Height") height=Math.Max(480,Math.Min(8640,int.Parse(v)));
     if(k=="HorizontalFOV") horizontal=Math.Max(60,Math.Min(150,float.Parse(v,CultureInfo.InvariantCulture)));
     if(k=="NativeVerticalFOV") nativeFov=Math.Max(1,Math.Min(150,float.Parse(v,CultureInfo.InvariantCulture)));
     if(k=="GameplayCamera") cameraName=v;
     if(k=="UIScale") uiScale=Math.Max(0.5f,Math.Min(2f,float.Parse(v,CultureInfo.InvariantCulture)));
    }
    configTime=File.GetLastWriteTimeUtc(path);
    Log("Settings: "+width+"x"+height+" horizontal FOV="+horizontal+" gameplay camera="+cameraName+" native="+nativeFov+" enabled="+enabled);
   }
   if(enabled && (Screen.width!=width||Screen.height!=height)) {
    Screen.SetResolution(width,height,Screen.fullScreen);
    Log("Requested resolution "+width+"x"+height);
   }
  }
  public static void UpdateUI() {
   try {
    Settings();
    foreach(LayoutSaved original in layouts.Values) {
     CanvasScaler scaler=original.scaler; if(scaler==null) continue;
     bool fit=enabled && (float)Screen.width/Screen.height>16f/9f;
     scaler.screenMatchMode=fit?CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:original.mode;
     scaler.matchWidthOrHeight=fit?1f:original.match;
     scaler.referenceResolution=fit?original.reference/uiScale:original.reference;
    }
    if(enabled) foreach(Saved original in uiOriginal.Values) {
     if(original.camera==null) continue;
     original.camera.rect=new Rect(0,0,1,1);
     original.camera.aspect=(float)Screen.width/Screen.height;
    }
   } catch(Exception e) { if(enabled) Log(e.ToString()); enabled=false; }
  }
  public static void Attach() {
   Camera.onPreCull += BeforeCamera;
   Camera.onPreRender += Audit;
   File.AppendAllText("UltrawideFix/runtime.log", "Camera callback attached\r\n");
  }
  static void BeforeCamera(Camera cam) {
   try {
   Settings();
   if(!restoreStarted) {
    RuntimeInspect.Run();
    GameObject driver=new GameObject("UltrawideFrameRestore");
    UnityEngine.Object.DontDestroyOnLoad(driver);
    driver.AddComponent<FrameRestore>();
    restoreStarted=true;
    Log("UI viewport revision 5; height-based canvas layout with native UI FOV");
   }
   if(seen.Add(cam.GetInstanceID())) {
    File.AppendAllText("UltrawideFix/runtime.log", "Camera="+cam.name+" fov="+cam.fieldOfView+" aspect="+cam.aspect+" rect="+cam.rect+" ortho="+cam.orthographic+" target="+(cam.targetTexture==null?"screen":cam.targetTexture.name)+" resolution="+Screen.width+"x"+Screen.height+"\r\n");
   }
   if(!enabled) {
    foreach(Saved initial in uiOriginal.Values) {
     if(initial.camera!=null) {
      initial.camera.fieldOfView=initial.fov; initial.camera.aspect=initial.aspect; initial.camera.rect=initial.rect;
     }
    }
    uiOriginal.Clear();
    return;
   }
   if(cam.targetTexture!=null) return;
   int id=cam.GetInstanceID();
   if(saved.ContainsKey(id)) return;
   Saved original=new Saved { camera=cam,fov=cam.fieldOfView,aspect=cam.aspect,rect=cam.rect };
   float aspect=(float)Screen.width/Screen.height;
   if(cam.name.StartsWith("ui_",StringComparison.OrdinalIgnoreCase)) {
    Saved initial;
    if(!uiOriginal.TryGetValue(id,out initial)) { initial=original; uiOriginal[id]=initial; }
    // Use native UI projection and fix CanvasScaler instead of rendering at a
    // different scale than Unity's layout and GraphicRaycaster expect.
    cam.rect=new Rect(0,0,1,1); cam.aspect=aspect;
    // Keep the viewport through Update for matching UI raycast/input alignment.
   } else if(!cam.orthographic) {
    saved[id]=original;
    cam.rect=new Rect(0,0,1,1); cam.aspect=aspect;
    if(cam.name==cameraName) {
     double targetTan=Math.Tan(horizontal*Math.PI/360)/aspect;
     double zoomRatio=Math.Tan(original.fov*Math.PI/360)/Math.Tan(nativeFov*Math.PI/360);
     cam.fieldOfView=(float)(Math.Atan(targetTan*zoomRatio)*360/Math.PI);
    }
   }
   } catch(Exception e) { if(enabled) Log(e.ToString()); enabled=false; }
  }
  public static void RestoreAll() {
   // onPostRender runs before image effects. Restore only after the entire frame.
   foreach(Saved original in saved.Values) {
    Camera cam=original.camera;
    if(cam!=null) { cam.fieldOfView=original.fov; cam.aspect=original.aspect; cam.rect=original.rect; }
   }
   saved.Clear();
  }
  static void Audit(Camera cam) {
   if(cam.name!=cameraName||cam.targetTexture!=null||Time.realtimeSinceStartup<nextAudit) return;
   nextAudit=Time.realtimeSinceStartup+5f;
   float horizontalActual=(float)(Math.Atan(Math.Tan(cam.fieldOfView*Math.PI/360)*cam.aspect)*360/Math.PI);
   Log("Render camera="+cam.name+" screen="+Screen.width+"x"+Screen.height+" vFOV="+cam.fieldOfView+" hFOV="+horizontalActual+" aspect="+cam.aspect+" rect="+cam.rect+" matrix="+cam.projectionMatrix.m00+","+cam.projectionMatrix.m11);
  }
 }
}
