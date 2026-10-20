# Sirroh QOL Addon — 0.1.0 (test release)

One BepInEx DLL with five independently configurable features for **Sirroh Overhaul 0.22.65+** on Valheim 1.0.

| Module | Behavior | Safety |
| --- | --- | --- |
| Auto Repair | On valid crafting-station interaction, repairs eligible inventory items using the game's own UI repair methods. | Next-frame GUI check, inventory-count loop limit, durability-progress guard. |
| Safe Stamina | Local player spends no stamina when there are no hostile creatures within the configured 25 m radius. | 0.25 s cached enemy scan; squared distances, no NPC or remote-player mutation. |
| Food Freeze | Food duration is paused while Resting or within range of a lit fireplace. | `UpdateFood` still executes; only temporary food durations are restored. |
| Glowing Signs | Higher-contrast sign text materials, with a subtle bright edge. | Material clone per font; avoids edits to global font assets or continuous polling. |
| Slope Safety | Prevents player sliding when grounded on slopes. | Does not touch airborne, attached, riding, swimming or ship movement. |

Each module can be toggled in `BepInEx/config/sirro.valheim.qoladdon.cfg`. Modules are installed independently: failure to find an optional game method disables only its hook and is logged.

## Requirements

- Valheim 1.0; BepInExPack 5.4.2351.
- Sirroh Overhaul **0.22.65 or later** installed (BepInEx dependency GUID `sirro.valheim.pracafizyczna`).
- Do not simultaneously load the five old reference mods; they are not required.

The five decompiled mods in `Sirroh-Compatibility-Builder` were consulted as **behavioral soft references only**. This project contains new code, not embedded copies of their DLLs.

## Test cases

1. Open a suitable workbench/forge with damaged repairable and nonrepairable equipment. Verify only eligible items are repaired once.
2. Sprint, swim, jump, mine and place pieces without nearby enemies; then spawn a hostile within 25 m and verify normal stamina consumption resumes.
3. Monitor a food timer while Resting, then while near a burning fire, then away from both. Verify no sudden expiration or missed stat updates.
4. Compare new sign text in darkness and daylight; verify no other TMP UI or fonts are changed.
5. Stand on a steep hill, walk downhill, jump/fall, then ride a mount and ship. Only grounded player sliding should be disabled.

**Test build**: Source is compiled and Thunderstore ZIP is uploaded by the GitHub Actions `Build Thunderstore` workflow. Runtime behavior still requires in-game verification.
