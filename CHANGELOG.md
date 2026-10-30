# Changelog

## 0.1.32 (test)
- Add optional AzuCraftyBoxes 1.8.28 compatibility for PlanBuild-imported placeable chests missing creator metadata. The shared Azu registration gate supplies both crafting and hammer building; no separate build patch or changes to Azu's recipe/consumption logic.
- Keep vanilla chest/ward checks and Azu enable, pull-toggle, range, in-use, MineOnly and YAML rules intact.
- Preserve the 0.1.31 main-branch Glowing Signs anti-aliasing fix and all other QOL modules unchanged.

## 0.1.31 (test)
- Glowing Signs: push the existing sign-text Canvas 0.03 units forward in its local Z once during Sign.Awake to reduce anti-aliasing flicker when moving toward/away from signs. Never accumulate the offset on permission callbacks.
- Preserve the existing TMP HDR face color, emissive GLOW_ON, sign colors, cache and all Glow settings. No global anti-aliasing changes, per-frame updates or unrelated QOL changes.

## 0.1.30 (test)
- Quickstack and Restock now allow player-accessible chests reconstructed from PlanBuild blueprints even if the original Piece creator flag was not preserved. Vanilla Container.CheckAccess, ward checks, InUse and network ownership remain enforced; moving ship containers remain excluded.
- Report breakdown of rejected containers (invalid state, access, ward, in-use, ship and ownership timeout) once per operation in BepInEx logs. No extra continuous scanning.
- Rename BepInEx display to Sirroh QOL [Addon] and use Thunderstore-compliant name Sirroh_QOL_Addon (Thunderstore forbids brackets/spaces).
- Use the supplied 256x256 transparent PNG as the Thunderstore icon; the packager no longer replaces it with a generated placeholder.
- No changes to stack-size settings or other QOL modules.

## 0.1.29
- Optional ExtraSlots compatibility: Sirroh QuickStack and Sort now operate only on ordinary inventory rows, preserving equipment, ammo, food, quick, misc and custom slots.
- Restock remains unchanged and continues to replenish stacks in ExtraSlots.
- If ExtraSlots is loaded but its API boundary is unavailable, abort QuickStack/Sort safely without moving items. No hard ExtraSlots dependency.

## 0.1.28
- Keep Quickstack, Restock, Sort, Recycle and Favorite inventory tabs below other window layers and hide them while Valheim skills, journal, trophies, achievements, split-stack or variant dialogs are open.
- Prevent hidden Recycle hitboxes from accepting a drag during a modal dialog.
- Replace custom grey destruction-confirmation rectangles with Valheim's native localized YesNoPopup. Preserve explicit confirmation, inventory validation and no-delete cancellation.
- Discard pending delete state if the native popup is dismissed with Escape/back. Other gameplay, item-reclaim and UI appearance logic is unchanged.


## 0.1.27
- Fix Sirroh QuickStack chest VFX failing because Azu's optional PingContainer is gated by external settings/existing components: directly instantiate the identical vfx_Potion_health_medium per modified destination and temporarily highlight WearNTear.
- Eliminate Harmony missing-field warning for Localization.instance by resolving field/property once with normal reflection.
- Avoid probing missing ObjectDB.UpdateRecipes with noisy Harmony AccessTools: enumerate only existing lifecycle hooks.
- Fix Reclaim confirmation's missing TMP font warning by assigning a native InventoryGui font before activating text controls.
- Downgrade expected non-craftable-item Reclaim diagnostics to Debug, and intentional 1920x1080 proportional fallback to Info.
- Other mods' warnings and unidentified RPC method hashes are unchanged.


## 0.1.26
- Fixed empty-inventory Reclaim failures caused by confusing character weight with inventory capacity; Valheim allows overweight inventory.
- When the true slot limit is exceeded, safely stage and drop up to 64 returned material stacks beside the player, then remove the source. Rollback staged objects on failure. No spawning on normal reclaim.
- Synchronize Azu RMB/left-click favorite operations to Sirroh favorites and continue syncing Sirroh edits into Azu; prevent duplicate toggles when edit modes overlap.
- Redirect Azu middle-click (single-item shortcut) and manual-store shortcuts into Sirroh QuickStack with Sirroh selected; retain Azu native behavior when Azu backend is selected.
- Play Azu native destination chest PingContainer VFX + highlight for each changed chest, or equivalent vanilla ping without Azu. Display translated single-item or total-count transfer messages.


## 0.1.25
- Lazy prefab-first recipe cache plus fallback to the Valheim recipe registry and shared item names. Cached across reclaim operations and invalidated when ObjectDB/recipe list changes.
- Handle dynamically registered mod recipes and upgrade-only material requirements. Aggregate duplicate yield prefabs before adding materials.
- Epic Loot: official optional API integration (rarity + actual enchant costs), with once-bound delegates and on-demand JSON decoding.
- Jewelcrafting: optional API integration for socketed gems; return corresponding prefab items.
- Abort with source preserved on unresolved external material/API errors; no new per-frame work.


## 0.1.24
- Fix overlapping/misaligned Favorite stars caused by Unity UI Outline creating four offset copies of the 19px star mesh (particularly the 2.5px active-state outline). Never add Outline to Favorite; disable any stale one.
- Draw exactly ONE existing source-alpha gold star and a separate, smooth, circular gold gradient behind it. The gradient is a cached 64x64 raw resource displayed at 30x30, with zero alpha at its edges; unlike Outline it does not duplicate the star silhouette. No per-frame processing or raycasts.
- Keep the distinct states: bronze original with native LitGui when idle; brighter bronze with subtle radial glow on hover; a fully gold unlit star with a persistent warm radial glow when Favorite editing is on.
- Reduce active star scaling to 6% (instead of 12-18%). All other QOL icon hover outlines and inventory UI layout remain unchanged.
- Embed one additional UI glow resource (13 total) and verify it in CI.

## 0.1.23
- Fully golden Favorite in edit mode: create an additional 64x64 solid RGB(255,208,52) sprite from the exact alpha of the existing Favorite artwork (no new image generated and no change to the original bronze sprite).
- Editing ON uses the full gold sprite on Unity UI/Default (unlit, emission-like), with a persistent gold halo; normal hover does not switch to the full-gold sprite. Editing OFF returns to the bronze image with native LitGui shader/dimming and no persistent halo. Increase shield star from 17px to 19px for legibility.
- Adds exactly one embedded image resource (12 total). CI verifies resource completeness, matching alpha and consistent mode handling. No per-frame polling, new gameplay options, changes to favorite storage/Azu support, other UI elements or game systems.

## 0.1.22
- Normalize Recycle button geometry to match Sort, Quickstack and Restock: identical 52x52 clickable area and 34x34 icon artwork, centered with 9px inset. The Recycle wooden background matches the other buttons' height, instead of following the taller 70px Weight tab. Keep right-edge alignment and its position above Weight.
- Replace almost invisible 1px, alpha=.22 one-directional Unity UI Shadow with an outlined, warm-gold, four-sided highlight on the existing icon Image; alpha=.78, offset 1.6px, an 8% scale lift, and clearer warm icon tint on hover. Enabled only while pointer is over an icon. No extra animations, tooltips, polling or material allocations per frame.
- Favorite retains its established bronze/gold state colors and lights up via the same hover outline; gameplay, native LitGui shading, AzuAutoStore compatibility and Quickstack/Reclaim logic unchanged.

## 0.1.21
- Correct excessive v0.1.20 panel brightness: do not force the original Valheim `Custom/LitGui` `_Brightness` to 1 before calibration. Interpolate from the ACTUAL native material level toward the previous upper target by only 16% for wood and 40% for general glyphs. The previous high values remain only as upper reference points, not multipliers.
- Favorite receives one separately cached LitGui material with a 62% blend and a brighter, muted bronze tint when inactive / pale gold when hovered or selected. Improves readability on the dark steel armor shield without turning wooden tabs into bright blocks.
- Keep original vanilla materials unmodified, day/night shader lighting, all alpha, 64x64 source images, hover behavior, AzuAutoStore compatibility and other QOL systems unchanged.
- Regression checks prevent reintroducing the minimum-1.0 brightness override and enforce distinct wood/glyph/favorite material variants.

## 0.1.20
- Fix unreadably dark addon tabs/icons at night by calibrating Valheim's own `Custom/LitGui` `_Brightness` property, instead of further overexposing original PNG images. The shader explicitly supports independent _Brightness and _Lighting properties.
- Clone the actual vanilla LitGui material exactly once per graphic category: wood uses 1.25x neutral brightness, glyphs/favorite borders use 2.40x. The original shared material remains untouched; no per-image material allocations.
- Keep the game's native LitGui dynamic lighting and all environment shader globals intact. No custom time-of-day/brightness polling, no `_Lighting` overrides, no global shader changes.
- Draw backgrounds with the wood variant and foreground symbols with the brighter glyph variant; leave transparent click hitboxes on UI/Default. Log original and addon shader brightness values for in-game diagnostics.
- Preserve all 0.1.19 AzuAutoStore compatibility, performance optimizations, UI layout, Reclaim behavior, alpha and existing image assets.

## 0.1.19
- Add soft AzuAutoStore 3.1.7 integration: configuration selects the Sirroh (default) or Azu manual QuickStack backend.
- Suppress only the overlapping Azu manual store when Sirroh is selected; retain all native Azu automatic storage, configured container types and background collection.
- When Azu is selected, delegate the Q key and QuickStack UI to Azu Functions.TryStore() once, avoiding parallel Sirroh operations and duplicate feedback. Preserve Sirroh Restock, Sort and Store All.
- Respect both mods' favorites in either backend through a cached, bidirectional runtime query bridge. Preserve marked empty Azu slots during Sirroh Sort, synchronize explicit Sirroh favorite toggles via Azu's native API, and hide only Azu's duplicate favorite-border renderer. No manual editing of either mod's binary favorites files.
- No work on Recycle_N_Reclaim in this release.

## 0.1.18
- Brighten only the six user-supplied 64x64 UI image assets in the build pipeline: wooden tab texture (gamma 0.87), four action icons (gamma 0.90), Favorite star (gamma 0.94). Darker/midtone pixels receive a mild luminance boost, with no flattening of highlights or forced recoloring.
- Original user pixel atlas SHA-256 verification remains intact; pixel alpha, dimensions and original source images are preserved exactly. CI asserts every visible asset receives a bounded luminance increase and the alpha channel is unchanged.
- Keep native Custom/LitGui day/night shading, all layout and hover effects, and game mechanics unchanged.

## 0.1.17
- Use the native Valheim `Custom/LitGui` material/shader for every visible QOL icon, wood tab and Favorite slot border. Reuse the inventory's existing shader material when possible.
- The native EnvMan shader globals and LitGui GPU shading provide day/night and dark-area shading; there is no separate brightness tick, time interpolation or manual environment sampling in our mod.
- Graceful fallback to the game's loaded LitGui shader when UI replacements remove native LitGui images; warnings without altering base game visuals when the shader is absent.
- Preserve icon layout, hover glows, Favorite selection colors, and all QOL gameplay mechanics from v0.1.16.

## 0.1.16
- Reduce four action icons' horizontal offset to 6px (previously 13px); wooden tabs and heights unchanged.
- Eliminate hover text popups. Event-driven modest brightening and subtle golden shadow highlight the icon itself on pointer enter, resetting on pointer exit, without Update work.
- Favorite star stays on Armor shield, brown when inactive, gold on hover or while favorite editing mode is active; clicking again restores the inactive brown when cursor leaves.
- Keep transparent hitboxes, game mechanics and all QOL modules unchanged.

## 0.1.15
- Remove the semi-transparent square behind Sort, Quickstack, Restock, Recycle and Favorite by using fully transparent raycast-enabled Button Image surfaces. Hit targets and icon artwork remain unchanged.
- Shift only the four action icon/clickable regions right by 13px at reference 1920x1080, matching the visible part of the wooden side tabs (not their hidden underlay). Keep wooden tabs and their vertical spacing intact.
- Reposition Favorite into the center of the gray armor-shield icon above the Armor value, preferring the actual Armor/armor_icon RectTransform with a screenshot-derived X643/Y80 fallback. Reduce its on-screen artwork to 17px and preserve its toggle functionality.
- All gameplay modules, hotkeys, and previous Q/R/Reclaim mechanics remain unchanged.

## 0.1.14
- Replace the combined 200px StackWoodTab with three separately sized and positioned wooden tabs (Sort, Quickstack, Restock).
- Anchor them to actual runtime inventory/Weight rect geometry, divide available vertical space into equal thirds and stop the third tab above the inventory bottom.
- Extend left under the native inventory and reach exactly to the existing Weight/shield tab outer right edge.
- Preserve Recycle position above Weight, Favorite and Store All, existing icon dimensions and all existing QOL systems.
- Add CI geometry assertions against the supplied 1920x1080 screenshot.

## 0.1.13
- Scale down and align the four addon icons to the vanilla right-side inventory bars: 34px artwork on 52px action hitboxes (38px Recycle on 54px); 60px width 9-slice wood underlay.
- Adjust placement to Recycle x645/y190, Sort x645/y331, Quickstack x645/y396, Restock x645/y461 at 1920 design resolution. Keep Favorite where it was.
- Reclaim recipe bug: metadata on armor no longer causes it to be rejected. Resolve by exact prefab first, ObjectDB recipe lookup second, shared-name fallback last. Preserve inventory-capacity protection and full base recipe quality ingredient refund.
- Keep all existing gameplay, Q/R performance fixes and icon resources unchanged.

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
