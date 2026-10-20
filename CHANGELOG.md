# Changelog

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
