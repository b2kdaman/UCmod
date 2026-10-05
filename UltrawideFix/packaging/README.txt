UMBRELLA CORPS - ULTRAWIDE + TOGGLE ADS
Version 0.1.0-beta.1 | Windows x86 Unity/Mono | 2026-10-05

FEATURES
Full-width world rendering, configurable horizontal FOV, height-fitted UI and
aligned menu input. Toggle aiming uses your configured aim button: press once
to aim, release to remain aimed, and press again to exit.
Defaults: 3440x1440, 120 horizontal FOV, UIScale=1, ToggleADS=true.
Native weapon zoom ratios are preserved, so ADS is narrower than 120 degrees.

INSTALL
1. Close Umbrella Corps. Extract this entire ZIP to a separate folder.
2. Open PowerShell in the extracted folder and run:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GamePath "E:\SteamLibrary\steamapps\common\Umbrella Corps"
   Substitute your own game folder. No administrator access is normally needed.
3. Launch the game normally. Allow approximately 25 seconds for activation.
Do not drag the payload over your game: the installer must first create and
verify mono.original.dll. The ZIP contains the custom loader, not the game's
original runtime. Existing settings.ini is preserved.
The command permits this script for that PowerShell process only; it does not
change your persistent execution policy. Review the included script before use.

REQUIREMENTS / COMPATIBILITY
Requires your own installed Umbrella Corps game: Unity 5.2.3p1, 32-bit Mono.
Installer accepts only the tested original runtime SHA-256:
56a1a25a8472640344915e6cf2a385a2955ad1f50b977586dacd88e12addd453
Other builds and other Mono replacement mods are unsupported. The game's own
Unity/Mono assemblies are prerequisites and are not distributed. Harmony
1.2.0.1 net35 is bundled with its MIT license; no separate mod manager needed.

CONFIGURATION
Edit <game>\UltrawideFix\settings.ini. Changes reload once per second.
Width / Height: target display resolution.
HorizontalFOV: unzoomed horizontal degrees (60-150).
UIScale: 1 is normal; smaller values shrink UI, larger values enlarge it.
ToggleADS=false: return to hold aiming. This is an INI option, not a menu row.
Enabled=false: disable camera/UI changes; ADS is controlled independently.
NativeVerticalFOV=55 and GameplayCamera=Camera should be left at defaults.
The game resolution menu may not list 3440x1440 even when it is active.

UPGRADE / RESTORE
Reinstalling this same build is supported and preserves your settings.
For a different build, first back up settings.ini and restore the original
runtime. Back up/remove conflicting old mod files if the installer identifies
them; it does not silently replace unrelated DLLs or modified files.
To restore stock behavior, close the game, open PowerShell in the game folder:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\UltrawideFix\Restore-Original.ps1
This verifies and restores the original runtime. Remaining mod files are inert.
Keep mono.original.dll until you have verified restoration. Select a native
resolution in the game's PC settings if desired. Saves are not modified.

VALIDATION / KNOWN LIMITATIONS
Beta: installed build tested locally at 3440x1440, 120 horizontal FOV.
Verified menu clicks, full equipment panel, gameplay HUD, and full-width world.
Toggle ADS stayed on after release and exited on a second press, including after
a death/retry in Village (S). User also confirmed these two actions.
Pause/focus, sprint, death and non-gun reset logic is implemented; not all reset
paths have been manually exercised. Other resolutions, weapons, missions and
multiplayer compatibility are unverified.
Some loading/death dimming overlays remain centered at 16:9. Startup logos can
appear before activation. Diagnostic logs and a runtime inspection text file
are generated locally under UltrawideFix; these are not part of the package.
Archive extraction, hashes, installer and restore are checked separately from
these gameplay tests. Installer/restore file operations are exercised in local
fixtures with process enumeration stubbed, including a simulated running-game
guard test. Distribution: github.com/b2kdaman/UCmod and b2kdaman.itch.io/ucmod.

PROVENANCE
Custom camera/UI plugin revision 5, current toggle-ADS plugin with unique build
identity, and custom 777-export Mono forwarding loader. Packaged from the tested
local checkpoint. manifest.json records the source Git commit, exact binary
hashes, platform and validation state. Source is available at
https://github.com/b2kdaman/UCmod; this ZIP is a binary mod package.
This unofficial mod is not affiliated with or endorsed by Capcom.
See THIRD-PARTY-NOTICES.txt for Harmony attribution and license.
