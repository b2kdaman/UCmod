"""Package only the tested binaries; exercise installation in disposable fixtures."""
from pathlib import Path, PurePosixPath
import hashlib, json, shutil, subprocess, zipfile, datetime, os, argparse

root = Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--game-root',type=Path,required=True,help='Installed game used only for original-runtime validation fixtures')
game_root=parser.parse_args().game_root.resolve()
mod = root / 'UltrawideFix'
version = '0.1.0-beta.1'
label = 'UmbrellaCorps-Ultrawide-ToggleADS-' + version
artifacts = mod / 'artifacts'
run = artifacts / ('verify-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f'))
stage = run / 'stage'
stage.mkdir(parents=True)
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
original_hash = '56a1a25a8472640344915e6cf2a385a2955ad1f50b977586dacd88e12addd453'
original = game_root / 'uc_Data/Mono/mono.original.dll'
if not original.is_file(): original=game_root/'uc_Data/Mono/mono.dll'
assert sha(original) == original_hash
checkpoint=json.loads((mod/'packaging/checkpoint.json').read_text(encoding='utf-8'))
for relative,digest in checkpoint.items():
    assert sha(root/relative)==digest, 'Binary changed since the validated checkpoint: '+relative

allowlist = {
    'Install.ps1': mod / 'packaging/Install.ps1',
    'README.txt': mod / 'packaging/README.txt',
    'THIRD-PARTY-NOTICES.txt': mod / 'packaging/THIRD-PARTY-NOTICES.txt',
    'payload/uc_Data/Mono/mono.dll': root / 'uc_Data/Mono/mono.dll',
    'payload/uc_Data/Managed/0Harmony.dll': root / 'uc_Data/Managed/0Harmony.dll',
    'payload/UltrawideFix/CameraFix.dll': mod / 'CameraFix.dll',
    'payload/UltrawideFix/ADSFix.dll': mod / 'ADSFix.dll',
    'payload/UltrawideFix/settings.ini': mod / 'packaging/settings.ini',
    'payload/UltrawideFix/Restore-Original.ps1': mod / 'Restore-Original.ps1',
}
# Prove the native loader is custom and preserves the original export ABI.
import pefile
loader = pefile.PE(str(allowlist['payload/uc_Data/Mono/mono.dll']))
stock = pefile.PE(str(original))
assert loader.FILE_HEADER.Machine == 0x14c
assert {(x.name,x.ordinal) for x in loader.DIRECTORY_ENTRY_EXPORT.symbols} == {(x.name,x.ordinal) for x in stock.DIRECTORY_ENTRY_EXPORT.symbols}
assert [x.dll for x in loader.DIRECTORY_ENTRY_IMPORT] == [b'KERNEL32.dll']
assert sha(allowlist['payload/uc_Data/Mono/mono.dll']) == sha(mod / 'tools/mono.dll')
assert sha(allowlist['payload/uc_Data/Mono/mono.dll']) != original_hash
assert sha(allowlist['payload/uc_Data/Managed/0Harmony.dll']) == checkpoint['uc_Data/Managed/0Harmony.dll']
for relative, source in allowlist.items():
    target = stage / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)

manifest = {
    'name':'Umbrella Corps Ultrawide + Toggle ADS', 'version':version,
    'label':label, 'releaseDate':'2026-10-05',
    'platform':{'os':'Windows','architecture':'x86','engine':'Unity 5.2.3p1 Mono'},
    'sourceCommits':{'UCmod':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()}, 'checkpoint':'Tested camera revision 5 plus toggle ADS and unique reload identity',
    'requiredOriginalMonoSha256':original_hash,
    'acceptance':{
        'automated':'Archive allowlist, extraction, hashes, export ABI, fixture install/reinstall/settings preservation, restore and invalid-input rejection verified by Build-Release.py before success. Fixture tests stub process enumeration, including a simulated running-game guard test.',
        'manual':'3440x1440, 120 horizontal FOV; menu click alignment, equipment and HUD fit; ADS tap/release/second tap verified after mission retry; user confirmed',
        'remaining':['Other resolutions/weapons/missions/multiplayer unverified','Not all pause/focus/sprint/non-gun reset paths exercised','Some transition/death dimming overlays remain 16:9'],
        'publication':'Public beta package; publication visibility tracked on GitHub and itch.io'
    },
    'files':[{'path':p,'size':(stage/p).stat().st_size,'sha256':sha(stage/p)} for p in sorted(allowlist)]
}
(stage/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
hashed = sorted([*allowlist, 'manifest.json'])
(stage/'CHECKSUMS.sha256').write_text(''.join(f'{sha(stage/p)}  {p}\n' for p in hashed),encoding='ascii')
archive = run / (label+'.zip')
expected = set(allowlist) | {'manifest.json','CHECKSUMS.sha256'}
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for relative in sorted(expected): z.write(stage/relative,relative)
extracted = run/'extracted'
with zipfile.ZipFile(archive) as z:
    assert set(z.namelist()) == expected and len(z.namelist()) == len(expected)
    for name in z.namelist():
        p=PurePosixPath(name)
        assert not p.is_absolute() and '..' not in p.parts and ':' not in name and '\\' not in name
    z.extractall(extracted)
assert {p.relative_to(extracted).as_posix() for p in extracted.rglob('*') if p.is_file()} == expected
for relative in expected: assert sha(extracted/relative) == sha(stage/relative)
readback=json.loads((extracted/'manifest.json').read_text(encoding='utf-8'))
assert readback==manifest and readback['version']==version
for f in readback['files']:
    assert (extracted/f['path']).stat().st_size==f['size'] and sha(extracted/f['path'])==f['sha256']

tests=[]
def run_ps(script,*args,success=True,running_game=None):
    quote=lambda s: "'"+str(s).replace("'","''")+"'"
    # Isolated file-operation fixtures stub process discovery. Never install
    # into the live game while validating the package.
    process_result='return' if running_game is None else '[PSCustomObject]@{Path='+quote(running_game/'uc.exe')+'}'
    stub='function Get-Process { [CmdletBinding()] param([string]$Name) '+process_result+' }\n'
    command=stub+'& '+quote(script)+' '+ ' '.join(str(a) if str(a).startswith('-') else quote(a) for a in args)
    test_env=os.environ.copy()
    test_env['PSModulePath']=str(Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/Modules')
    r=subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-Command',command],capture_output=True,text=True,env=test_env)
    if (r.returncode==0)!=success: raise RuntimeError(r.stdout+'\n'+r.stderr)
    return r.stdout+r.stderr
def fixture(name):
    p=run/name
    (p/'uc_Data/Mono').mkdir(parents=True)
    (p/'uc.exe').write_bytes(b'Packaging test marker; not an executable')
    shutil.copy2(original,p/'uc_Data/Mono/mono.dll')
    return p

game=fixture('test-game')
guard=run_ps(extracted/'Install.ps1','-GamePath',game,success=False,running_game=game)
assert 'Close Umbrella Corps before installing' in guard
assert not (game/'uc_Data/Mono/mono.original.dll').exists()
tests.append('Running-game guard: simulated target process rejected before mutation')
run_ps(extracted/'Install.ps1','-GamePath',game)
for rel in allowlist:
    if rel.startswith('payload/'):
        assert sha(game/rel[8:])==sha(extracted/rel)
assert sha(game/'uc_Data/Mono/mono.original.dll')==original_hash
assert (game/'UltrawideFix/diagnostics').is_dir()
tests.append('Clean install: all six payload files, verified original backup, diagnostics directory')
settings=game/'UltrawideFix/settings.ini'
settings.write_text(settings.read_text()+'\n# Preserve fixture settings\n',encoding='ascii')
saved=sha(settings)
run_ps(extracted/'Install.ps1','-GamePath',game)
assert sha(settings)==saved
tests.append('Reinstall: custom settings preserved')
run_ps(game/'UltrawideFix/Restore-Original.ps1')
assert sha(game/'uc_Data/Mono/mono.dll')==original_hash
tests.append('Restore: original runtime restored and hash verified')
run_ps(extracted/'Install.ps1','-GamePath',game)
assert sha(game/'uc_Data/Mono/mono.dll')==sha(extracted/'payload/uc_Data/Mono/mono.dll')
tests.append('Reinstall after restore')
bad=fixture('unsupported-game')
(bad/'uc_Data/Mono/mono.dll').write_bytes(b'Unsupported runtime')
result=run_ps(extracted/'Install.ps1','-GamePath',bad,success=False)
assert 'Unsupported or modified' in result and not (bad/'UltrawideFix').exists()
tests.append('Unsupported runtime rejected without installation')
bad=fixture('conflicting-backup')
(bad/'uc_Data/Mono/mono.original.dll').write_bytes(b'Bad backup')
result=run_ps(extracted/'Install.ps1','-GamePath',bad,success=False)
assert 'backup failed verification' in result and sha(bad/'uc_Data/Mono/mono.dll')==original_hash
tests.append('Conflicting original backup rejected')
bad=fixture('conflicting-harmony')
(bad/'uc_Data/Managed').mkdir()
(bad/'uc_Data/Managed/0Harmony.dll').write_bytes(b'Other mod dependency')
result=run_ps(extracted/'Install.ps1','-GamePath',bad,success=False)
assert 'Existing file differs' in result and not (bad/'uc_Data/Mono/mono.original.dll').exists()
tests.append('Conflicting Harmony rejected before changes')
tampered=run/'tampered'
shutil.copytree(extracted,tampered)
(tampered/'payload/UltrawideFix/ADSFix.dll').write_bytes(b'Tampered file')
bad=fixture('tampered-target')
result=run_ps(tampered/'Install.ps1','-GamePath',bad,success=False)
assert 'Package verification failed' in result and not (bad/'uc_Data/Mono/mono.original.dll').exists()
tests.append('Tampered payload rejected before changes')
final=artifacts/archive.name
shutil.copy2(archive,final)
assert sha(final)==sha(archive)
report={'archive':str(final),'sha256':sha(final),'size':final.stat().st_size,'archiveFiles':len(expected),'installPayloadFiles':6,'checks':tests,'verificationDirectory':str(run)}
(artifacts/(label+'.verification.json')).write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
(artifacts/(label+'.zip.sha256')).write_text(f'{sha(final)}  {final.name}\n',encoding='ascii')
print(json.dumps(report,indent=2))
