import pathlib, pefile, sys
root=pathlib.Path(__file__).parent
if len(sys.argv)!=2: raise SystemExit('Usage: python build_proxy.py <original-mono.dll>')
exports=pefile.PE(sys.argv[1]).DIRECTORY_ENTRY_EXPORT.symbols
lines=[]; defs=['LIBRARY mono','EXPORTS']; initial=[]
for n,s in enumerate(exports):
    lines.append(f'static FARPROC target{n};\n__declspec(naked) void proxy{n}(void) {{ __asm jmp dword ptr [target{n}] }}')
    name=s.name.decode() if s.name else f'ordinal{s.ordinal}'
    defs.append(f' {name}=proxy{n} @{s.ordinal}'+(' NONAME' if s.name is None else ''))
    symbol=f'"{name}"' if s.name else f'(LPCSTR){s.ordinal}'
    initial.append(f'target{n}=GetProcAddress(original,{symbol});')
lines.append('static void init_proxies(HMODULE original) {'+'\n'.join(initial)+'}')
(root/'proxy_stubs.h').write_text('\n'.join(lines))
(root/'proxy.def').write_text('\n'.join(defs))
print('Generated',len(exports),'forwarders to mono.original.dll')
