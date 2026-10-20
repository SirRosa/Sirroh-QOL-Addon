# Changelog

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
