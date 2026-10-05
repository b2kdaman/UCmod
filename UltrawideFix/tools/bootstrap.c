#include <windows.h>
#include <stdio.h>
#include <stdarg.h>

static char baseDir[MAX_PATH];
static HMODULE runtimeModule;
static void logline(const char *fmt, ...) {
 char path[MAX_PATH], msg[2048];
 _snprintf_s(path,sizeof(path),_TRUNCATE,"%sUltrawideFix\\bootstrap_%lu.log",baseDir,GetCurrentProcessId());
 va_list args; va_start(args,fmt); _vsnprintf_s(msg,sizeof(msg),_TRUNCATE,fmt,args); va_end(args);
 HANDLE file=CreateFileA(path,FILE_APPEND_DATA,FILE_SHARE_READ|FILE_SHARE_WRITE,NULL,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);
 if(file!=INVALID_HANDLE_VALUE) { DWORD written; WriteFile(file,msg,(DWORD)strlen(msg),&written,NULL); CloseHandle(file); }
}
static DWORD WINAPI worker(void *unused) {
 HMODULE mono=NULL;
 logline("Waiting for game's Mono runtime\r\n");
 for(int i=0;i<1200;i++) { mono=runtimeModule; if(mono) break; Sleep(100); }
 if(!mono) { logline("Mono module not found within 120 seconds\r\n"); return 0; }
 logline("Mono found at %p\r\n",mono);
 void *(__cdecl *get_root)(void)=(void*)GetProcAddress(mono,"mono_get_root_domain");
 void *(__cdecl *attach)(void*)=(void*)GetProcAddress(mono,"mono_thread_attach");
 void (__cdecl *detach)(void*)=(void*)GetProcAddress(mono,"mono_thread_detach");
 void *(__cdecl *open_assembly)(void*,const char*)=(void*)GetProcAddress(mono,"mono_domain_assembly_open");
 void *(__cdecl *get_image)(void*)=(void*)GetProcAddress(mono,"mono_assembly_get_image");
 void *(__cdecl *get_class)(void*,const char*,const char*)=(void*)GetProcAddress(mono,"mono_class_from_name");
 void *(__cdecl *get_method)(void*,const char*,int)=(void*)GetProcAddress(mono,"mono_class_get_method_from_name");
 void *(__cdecl *invoke)(void*,void*,void**,void**)=(void*)GetProcAddress(mono,"mono_runtime_invoke");
 if(!get_root||!attach||!detach||!open_assembly||!get_image||!get_class||!get_method||!invoke) { logline("Required Mono export missing\r\n"); return 0; }
 void *domain=NULL;
 for(int i=0;i<1200;i++) { domain=get_root(); if(domain) break; Sleep(100); }
 if(!domain) { logline("Mono domain not ready\r\n"); return 0; }
 Sleep(15000);
 void *thread=attach(domain);
 char dll[MAX_PATH]; _snprintf_s(dll,sizeof(dll),_TRUNCATE,"%sUltrawideFix\\CameraFix.dll",baseDir);
 logline("Loading %s\r\n",dll);
 void *assembly=open_assembly(domain,dll);
 if(!assembly) { logline("Camera assembly failed to load\r\n"); detach(thread); return 0; }
 void *klass=get_class(get_image(assembly),"Doorstop","Entrypoint");
 void *method=klass?get_method(klass,"Start",0):NULL;
 void *exception=NULL;
 if(method) { invoke(method,NULL,NULL,&exception); logline("Camera entry invoked; exception=%p\r\n",exception); }
 else logline("Camera entry not found\r\n");
 detach(thread); return 0;
}

#include "proxy_stubs.h"

BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved) {
 if(reason==DLL_PROCESS_ATTACH) {
  DisableThreadLibraryCalls(instance);
  GetModuleFileNameA(NULL,baseDir,MAX_PATH);
  char *end=strrchr(baseDir,'\\'); if(end) end[1]=0;
  wchar_t systemDll[MAX_PATH]; GetModuleFileNameW(instance,systemDll,MAX_PATH);
  wchar_t *last=wcsrchr(systemDll,L'\\'); if(last) last[1]=0;
  wcscat_s(systemDll,MAX_PATH,L"mono.original.dll");
  HMODULE original=LoadLibraryW(systemDll);
  if(!original) return FALSE;
  runtimeModule=original;
  init_proxies(original);
  HANDLE thread=CreateThread(NULL,0,worker,NULL,0,NULL);
  if(thread) CloseHandle(thread);
 }
 return TRUE;
}
