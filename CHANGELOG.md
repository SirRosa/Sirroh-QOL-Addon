# Changelog

## 0.1.4 — final sign material implementation trial
- Hook Sign.UpdateText in addition to access-permission completion and read persisted m_currentText with cached Harmony field references.
- Resolve the physical surface from the ZNetView/Piece prefab root, not just Sign component's children; supports MeshRenderer and SkinnedMeshRenderer.
- Use Standard PBR as fallback if the original shader has no albedo texture slot.
- Add explicit log output for renderer discovery, replaced material slots and embedded texture decoding.
- Verify all five actual 256x256 embedded resources in the compiled DLL (Mono.Cecil) in CI.
- Keep the four working gameplay modules and existing Glowing Signs HDR text unchanged.

## 0.1.3
- New sign syntax: `<mat=B>`, `<mat=S>`, `<mat=G>`, `<mat=R>`, `<mat=Y>` for black metal, silver, gold, red and yellow.
- Five unique generated 256x256 PBR albedo textures embedded in the existing QOL DLL; physical sign MeshRenderer materials are replaced with cached clones.
- Markers stay in vanilla ZDO text for multiplayer but are hidden in the local rendered sign text after normal permission filtering. Removing a marker restores the original materials.
- Materials use the existing Valheim shader and UVs, with surface-specific metallic and gloss tuning.
- No alterations to the existing four gameplay modules or GlowingSigns HDR text shader.

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
