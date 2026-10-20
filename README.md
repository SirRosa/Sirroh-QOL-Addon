# Sirroh QOL Addon — 0.1.4 (last test release)

One BepInEx DLL with five independently configurable features for **Sirroh Overhaul 0.22.65+** on Valheim 1.0.

| Module | Behavior | Safety |
| --- | --- | --- |
| Auto Repair | On valid crafting-station interaction, repairs eligible inventory items using the game's own UI repair methods. | Valheim 1.0 false Interact return handled; station check, next-frame GUI, bounded vanilla repairs. |
| Safe Stamina | Local player spends no stamina when there are no hostile creatures within the configured 25 m radius. | 0.25 s cached enemy scan; squared distances, no NPC or remote-player mutation. |
| Food Freeze | Food duration pauses while Resting or near a lit fireplace. | Targeted timer patch leaves vanilla food stats and regeneration active. |
| Glowing Signs | Emissive letters plus five new sign surface materials through `<mat=X>` tags. | Five 256px sign textures generated as RGBA resources and embedded in the DLL; locally swapped physical sign renderers, not global materials. |
| Slope Safety | Prevents player sliding when grounded on slopes. | Cancels ApplySlide and compensates slope gravity while idle; leaves swimming, riding, ships and NPCs alone. |

## Materials for signs

Type a marker anywhere in the sign text, including at the beginning, e.g. `<mat=G>Moja tabliczka`. The marker remains in the normal multiplayer ZDO text but is stripped from **visible** text for clients with Sirroh QOL Addon. These five variants replace the *physical sign mesh's material albedo* with a custom surface texture and adjust metallic/smoothness while preserving Valheim UVs and physical geometry:

| Marker | Polish | Finish |
| --- | --- | --- |
| `<mat=B>` | Czarny metal | Dark forged metal |
| `<mat=S>` | Srebrny | Brushed silver |
| `<mat=G>` | Złoty | Warm polished gold |
| `<mat=R>` | Czerwony | Glossy deep-red lacquer |
| `<mat=Y>` | Żółty | Yellow enamel |

Changing to another marker updates the physical sign material; deleting it restores the original material. Last valid marker wins. Invalid markers are kept in text. Color tags such as `<color=#ffff00>` continue to style the glyphs independently.

The marker is deliberately saved in the ordinary sign text; all players who should see the custom surfaces need the addon installed. The mod does not create prefabs, modify ZDO fields, or replace global font assets.

Glowing Signs settings: `EmissionIntensity` (default 1.75), `GlowStrength` (default 0.68), `GlowRadius` (default 0.13). Existing sign materials update when values change; glow/bloom visibility also depends on the game's shader variants and post-processing.

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
4. Enter `<mat=B>`, `<mat=S>`, `<mat=G>`, `<mat=R>` and `<mat=Y>` on different signs, compare entire mesh surfaces and emissive text, then remove the tag and verify the original material returns. Compare on multiple multiplayer clients.
5. Stand on a steep hill, walk downhill, jump/fall, then ride a mount and ship. Only grounded player sliding should be disabled.

**Test build**: Source is compiled and Thunderstore ZIP is uploaded by the GitHub Actions `Build Thunderstore` workflow. Runtime behavior still requires in-game verification.


## Surface troubleshooting (0.1.4)
The mod now finds MeshRenderers by searching the complete ZNetView/Piece prefab hierarchy rather than only Sign's direct children, and reacts to Sign.UpdateText as well as permission callbacks. It logs each tagged sign once per successful material change, e.g. `[Info:Sirroh QOL Addon] SignMaterials: sign=..., variant=G, foundRenderers=2, replacedMaterials=2 [OK]`.

When a mesh cannot be found or a surface shader has no supported texture slot, it logs `NO MESH` or `NO REPLACEMENT` instead of silently reporting success. Remove the marker to restore the original sign. The four gameplay modules and existing Glowing Signs HDR text behavior are unmodified.
