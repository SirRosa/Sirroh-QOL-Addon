# Sirroh QOL Addon — 0.1.2 (test release)

One BepInEx DLL with five independently configurable features for **Sirroh Overhaul 0.22.65+** on Valheim 1.0.

| Module | Behavior | Safety |
| --- | --- | --- |
| Auto Repair | On valid crafting-station interaction, repairs eligible inventory items using the game's own UI repair methods. | Valheim 1.0 false Interact return handled; station check, next-frame GUI, bounded vanilla repairs. |
| Safe Stamina | Local player spends no stamina when there are no hostile creatures within the configured 25 m radius. | 0.25 s cached enemy scan; squared distances, no NPC or remote-player mutation. |
| Food Freeze | Food duration pauses while Resting or near a lit fireplace. | Targeted timer patch leaves vanilla food stats and regeneration active. |
| Glowing Signs | Color-matched emissive text and a soft glow, without the hard dark outline. | HDR face and GLOW_ON on sign-only cached TMP materials; shared fonts unchanged. |
| Slope Safety | Prevents player sliding when grounded on slopes. | Cancels ApplySlide and compensates slope gravity while idle; leaves swimming, riding, ships and NPCs alone. |

Glowing Signs settings: `EmissionIntensity` (default 1.75), `GlowStrength` (default 0.68), `GlowRadius` (default 0.13). Existing sign materials update when values change; glow/bloom visibility also depends on the game's shader variants and post-processing.\n\nEach module can be toggled in `BepInEx/config/sirro.valheim.qoladdon.cfg`. Modules are installed independently: failure to find an optional game method disables only its hook and is logged.

## Requirements

- Valheim 1.0; BepInExPack 5.4.2351.
- Sirroh Overhaul **0.22.65 or later** installed (BepInEx dependency GUID `sirro.valheim.pracafizyczna`).
- Do not simultaneously load the five old reference mods; they are not required.

The five decompiled mods in `Sirroh-Compatibility-Builder` were consulted as **behavioral soft references only**. This project contains new code, not embedded copies of their DLLs.

## Test cases

1. Open a suitable workbench/forge with damaged repairable and nonrepairable equipment. Verify only eligible items are repaired once.
2. Sprint, swim, jump, mine and place pieces without nearby enemies; then spawn a hostile within 25 m and verify normal stamina consumption resumes.
3. Monitor a food timer while Resting, then while near a burning fire, then away from both. Verify no sudden expiration or missed stat updates.
4. Compare glowing signs by day and night, with plain and <color=#FF0000>colored</color> text. Check soft emissive glow without any sharp outline. Adjust GlowStrength to verify live material updates.
5. Stand on a steep hill, walk downhill, jump/fall, then ride a mount and ship. Only grounded player sliding should be disabled.

**Test build**: Source is compiled and Thunderstore ZIP is uploaded by the GitHub Actions `Build Thunderstore` workflow. Runtime behavior still requires in-game verification.
