# Changelog

## 0.1.12
- Position 64×64 Recycle, Sort, Quickstack, Restock at the exact supplied 1920×1080 reference image coordinates. Uniform resolution-aware scaling; preserve Favorite near Weight and Store All unchanged.
- Place supplied wooden 64×64 textured backplate as 9-sliced UI geometry behind inventory, projecting from the edge in red-button and three-button groupings. Cache and hide overlays with inventory.
- Replace previous icons with all five new 64×64 user PNGs, preserving lossless RGBA, including texture row-order correction for Unity. Store artwork with verified full-atlas SHA-256.
- Keep all Q/R network ownership and batch-performance fixes unchanged.

## 0.1.11
- Fix missing icon toolbar: Harmony InventoryGui.Show postfix parameter was incorrectly named 'gui' instead of required '__instance'.
- Guard InventoryGui.UpdateRecipeList against malformed item recipes and missing UI data; keep ObjectDB unchanged and log which recipe is broken instead of breaking inventory opening.
- Preserve all icon assets, 2ms quickstack frame budget and gameplay modules.

## 0.1.10
- Replace 380px wide labeled buttons with 26px icon-only mini-controls arranged under the vanilla Weight anchor like QuickStackStore. Favorite has a toggle state, Store All only appears for an opened container. Reclaim receives its own compact 38px drop target. Add mouseover tooltips.
- Incorporate all five user-supplied icons: Sort, Favorite, QuickStack, Restock at 32x32, Recycle at 48x48 (source 64x64); embed PNG-derived RGBA resources in the single DLL. Retain the five previously embedded compatibility icons/resources.
- Optimize Q/R in large bases: 32-meter spatial hash for known containers, no full-world container scan or distance sort; precheck relevant stacks BEFORE claiming network ownership; create a pooled per-container name-to-stacks index once per chest rather than searching it repeatedly; 2ms cooperative main-thread budget including transfer loops; no fixed 128-chest truncation.
- Reclaim and protected favorites behavior unchanged. Existing Q/R keybindings and 100m radius unchanged.

## 0.1.9
- Add integrated Recycle N Reclaim and QuickStackStore modules (no extra plugin DLLs). Q: Quickstack into nearby accessible chests within 100m; R: Restock existing non-hotbar stacks from nearby chests within 100m.
- One Trash/Reclaim UI target: dragging a supported recipe-based item returns 100% ingredient yield, including quality upgrades, only after preflighting cumulative inventory stack/slot/weight capacity; non-recyclable items ask for explicit "Czy na pewno chcesz zniszczyć ten przedmiot?" Tak/Nie before deleting.
- Protect favorites and the whole hotbar; LeftAlt+left click toggles item-name favorites, LeftAlt+right click toggles slot favorites. Save to per-player plain text config; no insecure BinaryFormatter.
- Preserve original PNGs from the two extracted mods as embedded RGBA sprites (border, trash, trashmask, trashingborder, RecyclingPanel), and keep natural Valheim GUI feedback.
- Avoid expensive continuous chest queries: maintain awake-registered chest set; query only on Q/R, pre-index current item names, process bounded chest batches, check privacy/ward/ownership/in-use, await ownership before moving items and save once per chest.
- Add the original manual Sort and Store All actions as compact inventory buttons alongside Q/R, with sorting that preserves favorite slots and hotbar; Store All writes only to the player's open chest and respects ownership and favorites.
- Restock can TOP UP hotbar stacks (but never removes them); Quickstack/Sort/Store/Trash never remove hotbar items.
- Exclude ship/mobile inventory for safety in multiplayer. No artificial global ZDO scanning or forced remote ownership under other player's in-use status.
- Existing seven modules unmodified aside from integration entry points.

## 0.1.8
- Fix critical Build Camera player destruction when camera streaming reference moves too far from the character's original sector. This was ZNetScene.RemoveObjects removing the local player ZNetView after its ZDO dropped outside the synced near area, not a visual rendering optimization.
- Uncouple camera distance completely from Construction level. Default synced near-area >=2: fixed 60m camera radius. Low near-area 1: fixed 30m radius. Dynamic network-distance read once per 2 seconds, never allow more than 60m.
- Derive limits from Valheim 1.0 decompiled IL: sectors 64m, near2 point-active-area Chebyshev <=96m and nonclassic spherical <=112m; near1 Chebyshev <=64m. The camera-sector-center can be as much as 32m from the camera, making 100m unsafe at sector boundaries.
- Keep Construction skill dependent camera-local pickup range 10m at level 1 to 100m at level 100.
- Leave ZNetScene's normal destruction path untouched. Keep all other QOL modules and free-camera behavior unchanged.

## 0.1.7
- Build Camera: streaming interest position follows the free camera, not the stationary player body. Valheim ZoneSystem and ZNetScene now stream terrain and nearby ZDOs around the camera (including on remote multiplayer hosts subject to latency).
- Hook Player.LateUpdate, which normally resets ZNet reference position to the player body; reassert camera position after that update and after camera movement.
- Restore the player body's original streaming reference immediately when camera is disabled, on focus loss, disconnect or plugin shutdown. Player transform and network identity remain unchanged.
- Keep remote pickup strictly camera-centered, expand the bounded loaded-drop scan to 64 items / 0.25s, and log the first successful camera-relative pickup.
- The six existing modules and all existing configs remain unmodified.

## 0.1.6
- New Build Camera (based on RunicBuildCamera): toggle with B; no hammer/tool required to activate; hotbar/tool changes never cause camera exit.
- Construction level controls camera max distance 50m (level 1) to 250m (level 100), and remote pickup radius 10m to 100m.
- Camera controls support mouse and controller, WASD, vertical controls and run acceleration.
- Placement/ghost ranges are adjusted only inside relevant methods and restored through Harmony finalizers; no player teleport, no local-player reassignment, and no permanently modified game fields.
- Bounded loaded-item scan for pickup every 0.25 seconds through vanilla item checks and pickup operation; no global item scans each frame.
- Swimming Equipment and all five established QOL modules unchanged.

## 0.1.5
- Swimming Equipment: fixed the instant weapon-stowing behavior. Valheim also checks IsSwimming in Player.Update before restoring hand items; the previous version missed this third call site.
- Replace only the IsSwimming calls in Humanoid.UpdateEquipment, Humanoid.EquipItem and Player.Update through exact IL transpilers; other uses of IsSwimming remain untouched.
- Remove ALL swimming module configuration. All items are enabled by default, no exclusions or mode selection.
- No stack traces or per-frame character scans, no changes to the five already-confirmed QOL modules or to their settings.

## 0.1.3
- Reverted all code and resources introduced for `<mat=B/S/G/R/Y>` sign surface variants; 0.1.2 glowing text preserved exactly.
- Added Wield Equipment While Swimming based on the supplied extracted 1.1.4 reference; scoped `Character.IsSwimming` override for `Humanoid.EquipItem` and `Humanoid.UpdateEquipment`.
- Added All, WeaponsOnly, ToolsOnly, Custom modes with prefab custom/blacklist, cached and refreshed when settings change.
- Replaced old StackTrace call-site checks with inexpensive thread-local equipment scope and exception-safe cleanup; swimming physics unchanged.

## 0.1.2
- Glowing Signs: explicitly activate GLOW_ON on the legacy TMP distance-field shader; 0.1.1 did not enable its emissive effect.
- HDR glyph brightness with configurable EmissionIntensity; soft color-matched glow with configurable GlowStrength and GlowRadius.
- Dedicated material cache per font and tint, no hard outline, no mutations to shared font materials.
- Four confirmed-good QOL modules unchanged.

## 0.1.1
- Auto Repair: fix successful CraftingStation.Interact returning false in Valheim 1.0. Verify matching player station; perform bounded vanilla repair operations.
- Food Freeze: use native CampFire/Resting status to recognize fire exposure. Replace unsafe timer snapshot/restore with narrow IL patch on food time subtraction only; keep regeneration and stat updates untouched.
- Glowing Signs: restore original legacy distance-field material logic; remove forced thick outlines and artificially configured glow.
- No Slope Slide: clear vanilla slippage without IsOnGround check (which is false on steep slopes); counter projected slope gravity for stationary grounded player.
- Safe Stamina remains unchanged.

## 0.1.0
- First source release of five independent QOL modules.
