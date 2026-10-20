# Changelog

## 0.1.1
- Auto Repair: fix successful CraftingStation.Interact returning false in Valheim 1.0. Verify matching player station; perform bounded vanilla repair operations.
- Food Freeze: use native CampFire/Resting status to recognize fire exposure. Replace unsafe timer snapshot/restore with narrow IL patch on food time subtraction only; keep regeneration and stat updates untouched.
- Glowing Signs: restore original legacy distance-field material logic; remove forced thick outlines and artificially configured glow.
- No Slope Slide: clear vanilla slippage without IsOnGround check (which is false on steep slopes); counter projected slope gravity for stationary grounded player.
- Safe Stamina remains unchanged.

## 0.1.0
- First source release of five independent QOL modules.
