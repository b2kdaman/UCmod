# Building UCmod

The release uses the tested binary checkpoint recorded in `UltrawideFix/packaging/checkpoint.json`. Rebuilding source does not automatically make a build gameplay-validated.

## Requirements

- Your own Umbrella Corps installation (Unity 5.2.3p1, x86 Mono).
- Windows .NET Framework C# compiler at `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe`.
- For the native loader: Microsoft C++ Build Tools, x86 target.
- For packaging/export checks: Python 3 with `pefile` installed.

## Managed plugins

```powershell
.\UltrawideFix\Build-Camera.ps1 -GamePath "E:\SteamLibrary\steamapps\common\Umbrella Corps"
.\UltrawideFix\Build-ADS.ps1 -GamePath "E:\SteamLibrary\steamapps\common\Umbrella Corps"
```

Outputs are written to ignored `build/`; game files are not modified. The ADS assembly receives a fresh identity on each build so Mono can reload it correctly.

## Native forwarding loader

Open an **x86 Native Tools Command Prompt** for Visual Studio, change to `UltrawideFix\tools`, and run:

```bat
cl /nologo /LD /O2 /MT bootstrap.c /link /DEF:proxy.def /OUT:mono.dll
```

The checked-in export stubs forward the tested original runtime's 777 exports. Regenerate only when deliberately targeting another verified runtime:

```powershell
python .\UltrawideFix\tools\build_proxy.py "<path-to-original-mono.dll>"
```

Never distribute `mono.original.dll`, `uc.exe`, or the game's managed assemblies. The custom proxy is MIT licensed; its export declarations refer to the Mono API. Harmony 1.2.0.1 net35 is bundled in `uc_Data/Managed/0Harmony.dll` with its upstream license.

## Package the validated checkpoint

```powershell
python .\UltrawideFix\packaging\Build-Release.py --game-root "E:\SteamLibrary\steamapps\common\Umbrella Corps"
```

The installed game must provide a verified `uc_Data/Mono/mono.original.dll`, or an unmodified supported `mono.dll`. No installation is performed on the live game. Tests operate on temporary fixtures under the ignored artifact directory, with process discovery stubbed. A simulated running-process check covers installer refusal. The builder checks the pinned binary hashes, export ABI, archive allowlist and extraction, installer, settings preservation, restore, and rejection cases.

Output: `UltrawideFix/artifacts/`. Source commit and binary hashes are recorded in the manifest. Update the version/checkpoint and manual test notes only after testing a changed build. Publish the ZIP and its SHA-256 file; fixture files and logs are not release assets.
