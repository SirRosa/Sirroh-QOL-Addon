# Sirroh QOL Addon — 0.1.24 (test release)

One BepInEx DLL with configurable gameplay, inventory and UI features, including a skill-scaled Build Camera, for Valheim 1.0.

| Module | Behavior | Safety |
| --- | --- | --- |
On valid crafting-station interaction, repairs eligible inventory items using the game's own UI repair methods. | Valheim 1.0 false Interact return handled; station check, next-frame GUI, bounded vanilla repairs. |
Local player spends no stamina when there are no hostile creatures within the configured 25 m radius. | 0.25 s cached enemy scan; squared distances, no NPC or remote-player mutation. |
Food duration pauses while Resting or near a lit fireplace. | Targeted timer patch leaves vanilla food stats and regeneration active. |
Color-matched emissive text and a soft glow, without the hard dark outline. | HDR face and GLOW_ON on sign-only cached TMP materials; shared fonts unchanged. |
Prevents player sliding when grounded on slopes. | Cancels ApplySlide and compensates slope gravity while idle; leaves swimming, riding, ships and NPCs alone. |
B toggles free build camera with no equipped-tool requirement. Scales with Construction skill. | Original player stays fixed; tool swaps do not exit, local-only camera; bounded pickup via vanilla item pickup. |
Wield/change equipped items while swimming. | Exact IL call-site substitution in Humanoid.EquipItem, Humanoid.UpdateEquipment and Player.Update; swimming physics and stamina unaffected. |

Glowing Signs settings: `EmissionIntensity` (default 1.75), `GlowStrength` (default 0.68), `GlowRadius` (default 0.13). Existing sign materials update when values change; glow/bloom visibility also depends on the game's shader variants and post-processing.\n\nEach module can be toggled in `BepInEx/config/sirro.valheim.qoladdon.cfg`. Modules are installed independently: failure to find an optional game method disables only its hook and is logged.

## Requirements

- Valheim 1.0; BepInExPack 5.4.2351.
- Required BepInEx dependency GUID: `sirro.valheim.pracafizyczna` (version 0.22.65 or later).

## Test cases

1. Open a suitable workbench/forge with damaged repairable and nonrepairable equipment. Verify only eligible items are repaired once.
2. Sprint, swim, jump, mine and place pieces without nearby enemies; then spawn a hostile within 25 m and verify normal stamina consumption resumes.
3. Monitor a food timer while Resting, then while near a burning fire, then away from both. Verify no sudden expiration or missed stat updates.
4. Compare glowing signs by day and night, with plain and <color=#FF0000>colored</color> text. Check soft emissive glow without any sharp outline. Adjust GlowStrength to verify live material updates.
5. Stand on a steep hill, walk downhill, jump/fall, then ride a mount and ship. Only grounded player sliding should be disabled.
6. Enter deep water WITH a sword/shield or hammer already equipped. Confirm items remain visible. Change weapons/tools while swimming and verify formerly stowed equipment returns. Verify stamina drain, drowning, animations and manual weapon stowing are otherwise normal.

**Test build**: Source is compiled and Thunderstore ZIP is uploaded by the GitHub Actions `Build Thunderstore` workflow. Runtime behavior still requires in-game verification.

## Swimming equipment (v0.1.3)

Rebuilt on top of the **known working 0.1.2 code and shader**. The failed `<mat=X>` physical sign material feature and all associated files have been removed.

Swimming equipment is **always on**, with no configuration, restrictions or blacklist. Every item is allowed.

The previous v0.1.3 attempt patched `Character.IsSwimming` using a thread-local flag only during two Humanoid methods. In Valheim 1.0, the game also guards **restoring hidden items** in `Player.Update`, leaving equipment stowed. v0.1.5 changes only the three relevant calls to `IsSwimming` inside those game methods. Swimming movement, physics and stamina still use normal `IsSwimming`.

If upgrading from 0.1.3, `06 Swimming Equipment` options in old BepInEx configuration files are ignored. No new keys are created. In-game validation is required.

## Build Camera — Construction-based (v0.1.6)
- Press **B** to enter or leave free camera mode, without requiring a hammer. Tool/hotbar swaps retain free camera.
- Keyboard **WASD**/game movement bindings move the camera; mouse looks; jump/crouch raise/lower; run accelerates. Controller analog sticks supported.
- Camera range from the stationary player: **60 m**, independent of Construction. When Valheim reports near simulation distance=1, it is **30 m** as a safety cap.nstruction 1** to **250 m at Construction 100**, linear interpolation based on `Sirro.PracaFizyczna.AdditionalSkills.Construction`.
- Auto-pickup of dropped objects near the camera: **10 m at Construction 1** to **100 m at Construction 100**. Uses the game's pickup and ownership APIs, weight, inventory and normal drop eligibility; scans up to 32 loaded drops once per 0.25 seconds.
- Remote placement and terrain-building updates use a temporary player placement-distance override scoped to the corresponding methods; original value is restored by a Harmony finalizer.
- Changing to a hammer, hoe, shovel, rake or any other tool with Valheim build/terrain placement ability does **not** force the camera to exit. Tools that don't support vanilla placement do not gain fake recipes or capabilities.
- There is no tool restriction or additional build-camera configuration.
- All previous QOL modules retained without implementation changes.

**Manual test required**: activate with empty hands; move beyond 50 m at low skill, confirm clamp; swap among hammer/hoe/rake without camera snapping back; inspect distant placement and pickup at known Construction levels 1, 50, 100. Test multiplayer pickups and disconnect while active. The game is not launched by CI.

## Camera streaming and remote pickup (v0.1.7)

When B is active, the Valheim `ZNet.SetReferencePosition` used by ZoneSystem (terrain chunks) and ZNetScene (networked objects) follows the Build Camera instead of the character's position. The player stays physically at the original location, including in multiplayer, and normal streaming reference is restored when B is turned off. This enables rendering and dropped-item discovery near the camera, but does not increase the game's simulation-distance settings and remote data may arrive with network latency.

Pickup range is measured **only from the camera location** (10m at Construction level 1, scaling to 100m at level 100), not the player's location. Pickup uses Valheim's standard inventory/weight/ownership logic and active ward access; excludes fish. The scanner processes at most 64 loaded drops every 0.25 seconds, not the entire world's drops. The first successful camera-based pickup is logged once.

**Multiplayer caveat:** Valheim has a single streaming reference per peer. While build camera is active far from the body, chunks near the *player body* may be unloaded locally. Always test this feature on a backup world, particularly at the 250m maximum distance; restored player streaming may need a short load when B is switched off.

## v0.1.8: player despawn diagnosis and safe camera limit
The Valheim 1.0 IL audit found the literal `Local player destroyed` inside `Player.OnDestroy()`. When the ZNet streaming reference follows a distant camera, `ZNetScene.CreateDestroyObjects` enumerates the camera's active sectors and `ZNetScene.RemoveObjects` destroys no-longer-relevant object instances. A stationary player's ZNetView can be removed too.

Valheim sectors measure 64m. On the standard network simulation distance near=2, `PointInsideActiveArea` uses a maximum Chebyshev distance of 1.5 sectors (96m) from camera sector center, and (non-classic mode) a tighter radial distance of 1.75 sectors (112m). The camera may be 32m away from its sector center, so a 100m camera radius is not guaranteed safe. The addon now imposes a fixed **60m** bound at near>=2 or a **30m** bound at near=1 (the latter can arise from lower server/graphics simulation settings). This is a *movement limit*, not a patch preventing removal of out-of-range world entities. The 10–100m pickup radius still scales with Construction, centered on the camera.

## Quickstack / Restock + Trash / Reclaim (v0.1.9)

- **Q** — move all eligible non-hotbar, non-favorited stackable items to accessible chests within **100 m**, but only into chests already containing that kind of item.
- **R** — fill existing non-hotbar incomplete stacks from accessible chests within **100 m**; never creates new item types in inventory, never removes hotbar contents.
- **Favorites** — hold **LeftAlt** and left-click an item in the player inventory to toggle favoriting the item type; hold **LeftAlt** and right-click a slot to toggle favoriting the slot. Both protect from Quickstack and Trash, indicated by a gold/blue inventory border. Preferences persist per player in BepInEx config as text (replaces BinaryFormatter).
- **Trash / Reclaim** — drag an item to a single icon in your inventory. When a valid crafting recipe exists and cumulative inventory slots and carrying capacity suffice, full ingredient yield including upgrades is returned. No separate reclaim button in chests. If the item has no recoverable recipe, a Polish **Tak/Nie** modal confirms deletion. When a reclaimable item doesn't fit, nothing is deleted; the operation is refused.
- Five inventory UI PNG assets (favorite border, trash icon and mask, reclaim border, recycling panel) are embedded in one DLL. No separate asset files are required.
- Loaded, accessible chests are acquired via vanilla ZNetView ownership and processed in bounded batches. Avoids continuous world scans and preserves private/ward access.
- Both hotkeys are intentionally fixed, without extra configuration.

**Critical in-game QA:** Q/R near multiple accessible and private chests; large inventories, hotbar, favorite items and slots; source stack partial counts; reclaim quality upgrades; full inventory rejection; Tak/nie non-recyclable item; multiplayer ownership and ward rules. All untested gameplay behavior must be validated before treating this as a stable release.

- Inventory actions also include **Sort**, **Store All**, **Quickstack Q**, **Restock R** buttons. Sort reorders only unprotected inventory slots, without moving the hotbar, equipped gear or favorited slots/items. Store All sends eligible non-hotbar, non-favorited items to the currently opened chest (including types not already in it), with the same network ownership and ward checks.
- Unlike Quickstack, Restock may refill an existing hotbar stack; nothing is *taken away* from the hotbar.

## v0.1.10 UI and performance pass
- New supplied icons physically resampled: Sort/Favorite/Quickstack/Restock **32×32**, Recycle **48×48**. On-screen sizes: **21×21** within 26×26 mini buttons, Recycle **31×31** inside a 38×38 drop/click target. All alpha is retained. Five UI textures are included in the DLL.
- Anchored under the player's **Weight** block (not a 380px button strip). Icons: Sort, Quickstack, Restock, Favorite mode, context-only Store All (Quickstack icon with A overlay); compact Recycle icon beside the row. Tooltips appear only on hover; Favorite toggles Alt-like favoriting mode.
- Chest selection uses a **32m spatial hash**, limiting searches to neighboring loaded cells within 100m. No per-command global chest scan or sorting. Chests are indexed by item type before expensive ownership RPCs; only relevant chests may be claimed.
- Restock uses pooled item lists by type, built once per chest (instead of repeated chest inventory scans per player item). Transfers are spread across frames with a **2ms cooperative budget**. Every loaded eligible chest within 100m is processed; no arbitrary 128-chest cutoff. Multiplayer permissions and wards remain checked before mutation.
- In-game checking remains necessary for performance and potential UI overlap. Track console log/errors, a dense chest room, multiplayer and small/large inventory interactions.

## v0.1.11 Inventory UI startup fixes
- Corrected the QolStoreSort Show Harmony postfix injection parameter from `gui` to `__instance`. The incorrect parameter prevented Harmony from installing the icon toolbar hook.
- Added guarded recipe-list validation before vanilla `InventoryGui.UpdateRecipeList`: skips only invalid/null recipe entries **for display only**; does not modify global ObjectDB crafting recipe sources, crafting recipes, or ingredients. Repairs missing temporary UI lists. If the inventory UI lacks the recipe list root, it emits an explicit warning and skips only that list update rather than crashing the entire inventory UI.
- Five custom supplied icons remain embedded at 32x32 (Recycle 48x48). Chest operations, skill scaling and earlier gameplay modules remain unchanged.
- Diagnose in BepInEx LogOutput.log via `RecipeListSafety:` if crafting panel still does not appear; UI layout updates occur only when a compatible root exists.

## v0.1.12 — exact 1920×1080 inventory placement and new sprites

The user's reference screen is 1920×1080 with marker centers:
- Recycle (red): (645, 186.5), 64×64
- Sort (yellow): (645, 326.5), 64×64
- Quickstack (blue): (645, 384.5), 64×64
- Restock (green): (645, 442.5), 64×64

Coordinates are converted to screen/Canvas space, scaling uniformly to other screen resolutions. The original Favorite control stays anchored under Weight; Store All stays with it. The supplied wood panel sprite is used as a 9-sliced decorative background, behind the player inventory panel (two separate protruding tabs for separated red vs three lower icons), with no raycasts. UI objects are cached and hidden when inventory closes.


## v0.1.13 — compact aligned UI and broader armor recipe matching
- Based on the actual 1920×1078 screenshot, the UI controls now match the existing inventory's narrow shield/weight rail. Action hitboxes are 52×52 with 34×34 rendered icons; Recycle is 54×54 with a 38×38 icon. Wooden backing has been reduced from 84px wide to 60px and aligned to the rail.
- Target centers at 1920 reference resolution: Recycle (645,190), Sort (645,331), Quickstack (645,396), Restock (645,461). Favorite stays in its existing location, as requested. Screen/canvas scaling remains supported.
- Reclaim no longer rejects ALL items containing custom metadata (many ordinary armor items, including leggings, carry metadata). Prefab identity takes precedence over shared display name and `ObjectDB.GetRecipe(item)` is used as a fallback when necessary; quality-upgrade material yields and inventory-capacity checks are preserved.
- Missing recipes emit a diagnostic message containing the prefab and quality. No usable recipe still opens the explicit delete confirmation; the source is not removed without that confirmation.

## v0.1.14 — three side tabs fitted to actual inventory height
- Sort, Quickstack, Restock now each have their **own wooden background panel**; no single 200px-long lower wood block.
- At runtime, positions are calculated from `InventoryGui.m_player` and its `Weight` RectTransform. All three panels divide the available vertical gap evenly between the bottom of Weight and the bottom of the inventory; 4px bottom clearance ensures the last tab **never extends below** the inventory.
- Wooden tabs extend ~16px beneath the existing inventory edge while matching the exact outer right edge of vanilla weight/shield tabs. They are sized to the available 3-slot height, with 1px separation and 52px bounded icon-hit targets.
- A proportional screenshot-based fallback is retained for UI replacement mods that do not expose suitable Weight transform bounds. Recycle stays above Weight. Favorite and Store All remain unchanged.
- All Q/R transfer, Reclaim, icons, and other modules remain untouched.

## v0.1.15 — transparent hitboxes, optical alignment, Favorite on shield
- The 0.012-alpha neutral UI Image layers were still visible on top of wood. All five clickable Image backgrounds are now **Color.clear** with `raycastTarget=true`; the icons and transparent click surfaces remain usable.
- Four side-rail icon and hitbox centers now shift **13 reference pixels to the right** from the old full-wood center; wooden backing and heights are untouched. The exposed tab face is centered at about x656 versus former x642, based on the supplied 1920×1080 screenshot.
- Favorite star is removed from the Weight action row and placed above the player's **armor shield badge** (approximately x643/y80 at 1920×1080). It follows the actual `Armor/armor_icon` RectTransform when available, with screenshot-relative fallback. Store All retains its existing position. Favorite type/slot persistence is unchanged.

## v0.1.16 — final icon hover and Favorite coloring
- Rail icon center adjustment: 13px -> **6px**, without moving any wooden boards.
- Hovering over icons gives subtle brighter artwork and a faint warm gold edge shadow. Popup text and tooltip objects are removed; hover changes only occur on pointer events.
- Favorite on the armor shield: **brown** normally, **gold** when hovered or when favorite-editing mode is active. Clicking once enables gold editing mode; clicking again disables it and returns brown after pointer exit.

## v0.1.17 — Native Valheim day/night UI lighting

- **No custom day/night algorithm.** Valheim exposes its own `Custom/LitGui` shader (with lighting, brightness, saturation and shadow parameters); the game `EnvMan` sets global ambient/sun shader lighting values as environments change.
- At InventoryGui.Show, the addon looks for an existing `Custom/LitGui` material on the **actual vanilla inventory UI images** and shares that material across the addon wood tabs, action glyphs, Favorite, Store All, Recycle, and Favorite slot borders. This preserves vanilla material-level lighting settings and automatically receives native shader light/dark changes without polling.
- With full UI-overhaul replacements that lack an existing LitGui material, the addon looks up Valheim's loaded `Custom/LitGui` shader and creates a single fallback material. If even the shader is unavailable, it leaves existing graphics unchanged and logs a warning. It **never** substitutes a homemade clock-based brightness calculation.
- Fully transparent clickable image surfaces remain on Unity's unlit UI shader, avoiding unnecessary draw/material changes. Visual highlight tint/colors remain independent and multiply through native LitGui lighting.
- Lighting material discovery occurs once on UI creation/open and re-checks after the next frame to pick up dynamically initialized icons. No per-frame shader searches, no extra color polling.
- Confirm in Valheim at daytime, sunset/night, inside caves and near artificial lights; actual native shader lighting response must be validated on the client.

## v0.1.18 — artwork-only brightening (native LitGui unchanged)

The user's night screenshot showed that the new side-rail tabs/icons looked darker than comparable vanilla UI. The *source graphics* are now gently lightened **before embedding** at build time with per-channel gamma curves: wood (0.87), Sort/Quickstack/Restock/Recycle (0.90), Favorite star (0.94). This lifts primarily dark and medium pixels, retaining highlights and each asset's color relationships, transparency and native 64×64 layout. The original lossless source atlas is untouched and SHA-256 verified.

This is **not** a second lighting system: it does not alter `Custom/LitGui`, copy/modify Unity materials, change any shader parameters, adjust per-frame sprite tints, or detect darkness independently. Day/night fades continue to come entirely from Valheim. The embedded graphics are slightly brighter in all environments.

The build reports before/after pixel brightness for each of the six assets and fails if alpha differs or the increase is excessive.


## AzuAutoStore compatibility (0.1.19)

When the optional \`Azumatt.AzuAutoStore\` mod is installed, use
\`BepInEx/config/sirro.valheim.qoladdon.cfg\` → \`[07 Compatibility]\` →
\`QuickStackEngine = Sirroh\` (default) or \`AzuAutoStore\`.

- **Sirroh**: the Q key and Sirroh QuickStack icon run the 100 m indexed / cooperative Sirroh implementation. Azu's **manual Store Shortcut** is suppressed to avoid two overlapping actions. Azu's automatic chest collection, YAML rules, item drawers, backpacks, quivers, and its other functions are **not** disabled.
- **AzuAutoStore**: the Q key and Sirroh QuickStack icon call Azu's native parameterless \`Functions.TryStore()\` exactly once. Azu's own Store Shortcut remains available except a duplicate shortcut bound to Q. The native 100 m Sirroh QuickStack coroutine is not started, and Azu controls the effective range (default **5 m**), rules, ownership and feedback.
- **Restock, Sort and Store All** remain Sirroh features in either mode. Choosing Azu does *not* replace Restock with a different operation.
- Favorite decisions are the union of Sirroh and Azu saved favorites. Azu manual/automatic deposits and Sirroh QuickStack/Store All respect favorites marked in the other mod; Sirroh's single border shows Azu marked items and empty favorite slots (Azu's redundant border graphics are hidden without disabling its favorite editing). Sirroh's Alt+click favorite toggles both via Azu's own public save API, so items can also be unmarked consistently. Existing favorites files are **not migrated or overwritten**; using Azu's independent shortcuts can still leave separate favorite flags until the Sirroh favorite toggle is used.
- Without a compatible Azu installation the Sirroh backend is used, regardless of the preference. The external DLL is never required as a compile-time dependency.

Recycle_N_Reclaim compatibility is **not** part of this update. Runtime behavior must be verified in a multiplayer world before treating this as a stable release.

## v0.1.20 — correct native LitGui material brightness for readable nighttime UI

The UI was still too dark despite earlier 0.1.18 art-only gamma correction. The problem was sharing the native **dark panel material preset** directly across **gold foreground icons** and **wood backgrounds**. Valheim's own `Custom/LitGui` shader includes both `_Brightness` and `_Lighting`; adjusting the former on **clones** can make our UI readable while preserving the latter and the game's changing day/night lighting.

This release creates just two cached material variants by copying the vanilla LitGui material: wood uses `_Brightness` = 1.25 × max(1, native setting), gold icons (Recycle/Sort/Quickstack/Restock, Favorite, Store All and favorite borders) use 2.40 × max(1, native setting), capped at 4.0. These are fixed **material brightness calibrations, not a second lighting system**; Unity still applies Valheim's dynamic scene/dungeon lighting through the original shader. Vanilla materials, art pixels/alpha, colors, hitboxes, game mechanics and the AzuAutoStore compatibility module are unchanged.

For diagnosis, BepInEx LogOutput shows: `QOL LitGui wood: native _Brightness=... QOL _Brightness=...` and `QOL LitGui glyphs: ...`. Compare the addon wood to vanilla shield/weight tabs and icon legibility at dusk/night and indoors. If the native shader is missing, the addon preserves existing UI material behavior and logs a warning rather than substituting a custom day/night formula.

## v0.1.21 — restrained native UI brightness, visible Favorite

The previous v0.1.20 mistakenly **forced the original Valheim LitGui brightness up to at least 1** before multiplying, which made the new wooden side tabs visibly lighter than the native Armor/Weight boards, while the brown Favorite tint was still very dark.

The correction **preserves the actual native `_Brightness` baseline** (even below 1). The wood material is now 16% of the way from that baseline to the previous 1.25 target, normal symbols are 40% toward 2.40, and Favorite alone 62% toward 2.40. These **are interpolation fractions, not multipliers**. The expected result is restrained wooden tabs close to vanilla while keeping symbols legible. It still uses Valheim's native Custom/LitGui shader and its runtime lighting; no clock, shader globals or per-frame computations.

The Favorite star on the steel Armor shield has an additional warmer, brighter bronze tint (RGB 1.0 / .78 / .52) when inactive and soft gold (1.0 / .96 / .76) when hovered or selected. Clicking again returns to bronze when the pointer leaves. It uses its own cached LitGui material, avoiding changes to other icons or the vanilla UI.

For troubleshooting, the BepInEx log records the actual native and addon _Brightness of wood, glyphs and favorite. These values allow fine adjustments to the game material instead of large guess-based PNG modifications. Source graphics, layout, hover glows, favorites behavior and Q/R/Reclaim mechanics are unchanged.

## v0.1.22 — consistent Recycle button and visible hover effects

**Recycle dimensions now match the three lower side rail actions:** 52×52 clickable rectangle, 9px inset on all sides resulting in a 34×34 visible icon. Its 9-sliced wood tab shares the same computed `boardHeight` as Sort, Quickstack and Restock, but remains above the native Weight panel. All 64×64 source assets are preserved.

**Hover:** the previous tiny one-sided `Shadow` (1px offset, .22 opacity) was too faint under Valheim's nighttime lighting. Icons now use a pre-cached four-sided Unity UI `Outline` at 1.6px/78% alpha, a warmer golden image tint and an 8% icon-only scale lift while hovered. Every control remains the same size for hit-testing, with transparent backgrounds and no hover text; click/drag/favorite interactions are unchanged. `Outline.enabled` toggles only on enter/exit, keeping CPU and GC overhead negligible. Native `Custom/LitGui` day/night shading is untouched.

## v0.1.23 — highly visible Favorite edit-mode star

The Armor-shield Favorite indicator now changes **the actual rendered image**, not only the tint of a dark texture. A separate 64×64 sprite is generated in the build pipeline: every nontransparent pixel of the original supplied Favorite star is replaced with solid gold `RGB(255,208,52)`; every alpha pixel and original shape is preserved exactly. The original brown icon remains embedded unchanged.

**Inactive:** original muted bronze, with the same Valheim `Custom/LitGui` night-darkness shader. **Hover while inactive:** mild highlight but still the original brown art. **Active (Favorite editing):** entire star is gold even with no hover; persistent bright gold outline, subtle 12% enlargement, and UI/Default **unlit** rendering for an emission-like indicator visible at night. **Click again:** return to brown, native LitGui lighting and no persistent halo. When hovering an active star, an additional small scale lift highlights the cursor.

This is an emission-like **unlit UI** effect, not a new HDR emission/bloom shader or second environmental lighting system. All existing favorite item logic, Alt controls, UI positioning, Q/R/Reclaim, Azu integration, resource caching and performance behavior are unchanged.

## v0.1.24 — one Favorite star, no displaced duplicate outlines

The previous Unity `Outline` multiplied the 19px Favorite star silhouette into four offset vertex copies. This looked like several misaligned gold stars. Favorite now has **no UI Outline component**. The unlit solid gold sprite remains intact and a separate 30px circular radial alpha-gradient behind the single gold star produces a soft glow in active editing mode. On hover without editing there is only a weak halo; the icon remains original bronze with native `Custom/LitGui` dimming. Active gold remains visible at night. The halo uses one 64px alpha-blended texture (not the star silhouette), is not clickable, cached, and uses zero per-frame computations. The normal Sort/Quickstack/Restock/Recycle hover outlines are unchanged. Favorite persists/synchronizes exactly as before, including AzuAutoStore support.
