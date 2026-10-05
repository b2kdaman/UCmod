using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
namespace Doorstop {
 public static class RuntimeInspect {
  static bool done;
  static Dictionary<short,OpCode> codes=new Dictionary<short,OpCode>();
  public static void Run() {
   if(done) return; done=true;
   try {
    foreach(FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)) if(f.FieldType==typeof(OpCode)) { OpCode o=(OpCode)f.GetValue(null); codes[o.Value]=o; }
    using(StreamWriter w=new StreamWriter("UltrawideFix/diagnostics/runtime-ads-il.txt")) {
     foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()) if(a.GetName().Name=="Assembly-CSharp") {
      foreach(string name in new string[]{"K2.keyconfig","K2.keyboardControl","K2.PlayerControl","K2.savedataOption+Control","K2.savedata","K2.SystemManager"}) {
       Type t=a.GetType(name); if(t==null) continue;
       w.WriteLine("TYPE "+t.FullName);
       foreach(FieldInfo f in t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)) w.WriteLine("FIELD "+f+" static="+f.IsStatic);
       foreach(MethodInfo m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)) {
        if(!System.Text.RegularExpressions.Regex.IsMatch(m.Name,"AdsSetting|GetOn$|GetTrigger$|GetRelease$|Update_PadInfo_Master|Update_AimInfo|Pad_ResetInfo|CheckAbleADSShift|GetKeyInfo|Update_ADSControl|SetADSAimHold|IsAiming|IsADSMode|IsScopeADSMode")) continue;
        w.WriteLine("METHOD "+m+" static="+m.IsStatic);
        try { Dump(m,w); } catch(Exception e) { w.WriteLine(e); }
       }
      }
     }
    }
   } catch(Exception e) { File.AppendAllText("UltrawideFix/runtime.log",e.ToString()); }
  }
  static void Dump(MethodInfo m,StreamWriter w) {
   MethodBody body=m.GetMethodBody(); if(body==null) return;
   foreach(LocalVariableInfo l in body.LocalVariables) w.WriteLine("LOCAL "+l.LocalIndex+" "+l.LocalType);
   byte[] b=body.GetILAsByteArray(); int i=0;
   while(i<b.Length) {
    int offset=i; short key=b[i++]; if(key==254) key=(short)(0xfe00|b[i++]);
    OpCode op=codes[key]; object operand=""; int token;
    switch(op.OperandType) {
     case OperandType.InlineNone: break;
     case OperandType.ShortInlineI: operand=(sbyte)b[i++]; break;
     case OperandType.ShortInlineVar: operand=b[i++]; break;
     case OperandType.InlineVar: operand=BitConverter.ToUInt16(b,i); i+=2; break;
     case OperandType.InlineI: operand=BitConverter.ToInt32(b,i); i+=4; break;
     case OperandType.InlineI8: operand=BitConverter.ToInt64(b,i); i+=8; break;
     case OperandType.ShortInlineR: operand=BitConverter.ToSingle(b,i); i+=4; break;
     case OperandType.InlineR: operand=BitConverter.ToDouble(b,i); i+=8; break;
     case OperandType.ShortInlineBrTarget: operand=i+1+(sbyte)b[i]; i++; break;
     case OperandType.InlineBrTarget: operand=i+4+BitConverter.ToInt32(b,i); i+=4; break;
     case OperandType.InlineSwitch: int n=BitConverter.ToInt32(b,i); i+=4; operand="switch "+n; i+=n*4; break;
     default:
      token=BitConverter.ToInt32(b,i); i+=4;
      try { operand=op.OperandType==OperandType.InlineString?(object)m.Module.ResolveString(token):m.Module.ResolveMember(token); } catch { operand="token="+token.ToString("X8"); }
      break;
    }
    w.WriteLine(offset.ToString("X4")+" "+op.Name+" "+operand);
   }
  }
 }
 public static class ExtensionLoader {
  static DateTime stamp; static float next; static MethodInfo tick;
  public static void Update() {
   try {
    if(UnityEngine.Time.realtimeSinceStartup>=next) {
     next=UnityEngine.Time.realtimeSinceStartup+1;
     const string path="UltrawideFix/ADSFix.dll";
     if(File.Exists(path) && File.GetLastWriteTimeUtc(path)!=stamp) {
      if(tick!=null) { MethodInfo stop=tick.DeclaringType.GetMethod("Stop"); if(stop!=null) stop.Invoke(null,null); }
      stamp=File.GetLastWriteTimeUtc(path);
      Assembly a=Assembly.Load(File.ReadAllBytes(path));
      tick=a.GetType("UmbrellaADS.ToggleAim").GetMethod("Update");
      File.AppendAllText("UltrawideFix/runtime.log","ADS extension loaded\r\n");
     }
    }
    if(tick!=null) tick.Invoke(null,null);
   } catch(Exception e) { tick=null; File.AppendAllText("UltrawideFix/runtime.log","ADS extension error: "+e+"\r\n"); }
  }
 }
}
