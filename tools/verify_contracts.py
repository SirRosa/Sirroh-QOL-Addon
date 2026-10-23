from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
source = {p.stem: p.read_text(encoding="utf-8") for p in (root / "src").glob("*.cs")}
assert len(source) == 16, sorted(source)
assert "sirro.valheim.pracafizyczna" in source["Plugin"]
assert 'Version = "0.1.18"' in source["Plugin"]
for feature in ("AutoRepair", "SafeStamina", "FoodFreeze", "GlowingSigns", "SlopeSafety"):
    assert feature + ".Install(this)" in source["Plugin"]
assert "GetAllItems().Count" in source["AutoRepair"] and "TotalDurability(player) <= before" in source["AutoRepair"]
assert "bool __result" not in source["AutoRepair"]
assert "Player.m_localPlayer.GetCurrentCraftingStation() != __instance" in source["AutoRepair"]
assert "Time.time + 0.25f" in source["SafeStamina"] and "sqrMagnitude" in source["SafeStamina"]
assert "UpdateFood" in source["FoodFreeze"] and "TranspileUpdateFood" in source["FoodFreeze"]
assert "s_statusEffectCampFire" in source["FoodFreeze"]
assert "Food.m_time" in source["FoodFreeze"] and "OpCodes.Sub" in source["FoodFreeze"]
assert "return false;" in source["SlopeSafety"] and "ForceMode.Acceleration" in source["SlopeSafety"]
assert "if (!__instance.IsOnGround())" not in source["SlopeSafety"]
assert "new Material(source)" in source["GlowingSigns"] and "TextMeshPro/Distance Field" in source["GlowingSigns"]
assert 'material.EnableKeyword("GLOW_ON")' in source["GlowingSigns"]
assert 'material.DisableKeyword("OUTLINE_ON")' in source["GlowingSigns"]
assert 'ColorUtility.TryParseHtmlString' in source["GlowingSigns"]
for key in ("SignEmission", "SignGlowStrength", "SignGlowRadius"):
    assert key in source["Plugin"] and key in source["GlowingSigns"]
assert "GlowingSigns.RefreshMaterials()" in source["Plugin"]
assert "SwimmingEquipment.Install(this)" in source["Plugin"]
assert "SwimmingEquipment.Shutdown()" not in source["Plugin"]
assert "[ThreadStatic]" not in source["SwimmingEquipment"]
assert "StackTrace" not in source["SwimmingEquipment"]
assert "Type.EmptyTypes" in source["SwimmingEquipment"]
assert "SwimmingEquipmentMode" not in source["SwimmingEquipment"]
assert "ConfigEntry" not in source["SwimmingEquipment"]
assert "Config.Bind(\"06 Swimming Equipment\"" not in source["Plugin"]
assert "SwimmingEquipmentEnabled" not in source["Plugin"]
assert "SwimmingEquipMode" not in source["Plugin"]
assert "SwimmingCustomAllowlist" not in source["Plugin"]
assert "SwimmingBlacklist" not in source["Plugin"]
assert "Transpile(" in source["SwimmingEquipment"]
assert "code[i].operand = EquipCheck" in source["SwimmingEquipment"]
for caller in ("Humanoid.UpdateEquipment", "Humanoid.EquipItem", "Player.Update"):
    assert caller in source["SwimmingEquipment"]
assert "ConstructionCamera.Install(this)" in source["Plugin"]
assert "ConstructionCamera.Tick()" in source["Plugin"]
assert "ConstructionCamera.Shutdown()" in source["Plugin"]
assert "AccessTools.Field(type, \"Construction\")" in source["ConstructionCamera"]
assert "KeyCode.B" in source["ConstructionCamera"]
assert "GetJoyRightStickX" in source["ConstructionCamera"]
assert "UseHotbarItem" not in source["ConstructionCamera"]
assert "IsBuildToolEquipped" not in source["ConstructionCamera"]
assert "FixedCameraLimit = 60f" in source["ConstructionCamera"]
assert "LowSimulationLimit = 30f" in source["ConstructionCamera"]
assert "50f +" not in source["ConstructionCamera"] and "200f" not in source["ConstructionCamera"]
assert "GetSyncedSimulationDistance" in source["ConstructionCamera"]
assert "RefreshSimulationLimit(true)" in source["ConstructionCamera"]
assert "RefreshSimulationLimit();" in source["ConstructionCamera"]
assert "Mathf.Clamp(_level, 1f, 100f)" in source["ConstructionCamera"]
assert "10f +" in source["ConstructionCamera"] and "90f" in source["ConstructionCamera"]
assert "RemoteCameraPickup.Tick" in source["ConstructionCamera"]
assert 'Hook("Player.LateUpdate"' in source["ConstructionCamera"]
assert 'net.SetReferencePosition(_position)' in source["ConstructionCamera"]
assert 'net.SetReferencePosition(_owner.transform.position)' in source["ConstructionCamera"]
assert "RestorePlayerReference();" in source["ConstructionCamera"]
assert "SetCameraReference();" in source["ConstructionCamera"]
assert "Vector3 offsetFromCamera = drop.transform.position - camera;" in source["RemoteCameraPickup"]
assert "offsetFromCamera.sqrMagnitude > rangeSq" in source["RemoteCameraPickup"]
assert "player.Pickup(drop.gameObject, true, true)" in source["RemoteCameraPickup"]
assert "Hud.IsPieceSelectionVisible()" in source["ConstructionCamera"]
assert "Player.SetMouseLook" in source["ConstructionCamera"]
assert "Player.SetControls" in source["ConstructionCamera"]
assert "GetStationBuildRange" in source["ConstructionCamera"]
assert "UpdatePlacementGhost" in source["ConstructionCamera"]
assert "GetTotalWeight()" in source["RemoteCameraPickup"]
assert "RequestOwn()" in source["RemoteCameraPickup"]
assert "QolRecipeListSafety.Install(this)" in source["Plugin"]
assert "UpdateRecipeList" in source["QolRecipeListSafety"]
assert "ref List<Recipe> __0" in source["QolRecipeListSafety"]
assert "Activator.CreateInstance" in source["QolRecipeListSafety"]
assert "QolRecipeListSafety" not in source["ConstructionCamera"]
assert "private static void OnShow(InventoryGui __instance)" in source["QolStoreSort"]
assert "Ensure(__instance);" in source["QolStoreSort"]
assert "QolFavorites.Install(this)" in source["Plugin"]
assert "QolNearbyInventory.Install(this)" in source["Plugin"]
assert "QolTrashReclaim.Install(this)" in source["Plugin"]
assert "QolStoreSort.Install(this)" in source["Plugin"]
assert "QolStoreSort.Shutdown()" in source["Plugin"]
assert "QolFavorites.IsFavoriteSlot" in source["QolStoreSort"]
assert "StartStoreCurrent" in source["QolNearbyInventory"]
assert "m_gridPos.y == 0" in source["QolStoreSort"]
assert "QolNearbyInventory.Tick();" in source["Plugin"]
assert "FrameBudgetMs = 2.0" in source["QolNearbyInventory"]
assert "private static long CellOf" in source["QolNearbyInventory"]
assert "ClearContentIndex" in source["QolNearbyInventory"]
assert "IndexInventory" in source["QolNearbyInventory"]
assert "if (!Relevant(dest,quick)) continue;" in source["QolNearbyInventory"]
assert "Nearby.Sort" not in source["QolNearbyInventory"].replace("// No Nearby.Sort:","")
assert "ReadSprite(\"ui_sort\")" in source["QolFavorites"]
assert "ReadSprite(\"ui_recycle\")" in source["QolFavorites"]
assert "QolFavorites.SortIcon" in source["QolStoreSort"]
assert "QolFavorites.RecycleIcon" in source["QolTrashReclaim"]
assert "QolFavorites.WoodPanel" in source["QolStoreSort"]
assert "ReadSprite(\"ui_wood\")" in source["QolFavorites"]
assert "QolStoreSort.PositionRecycle" in source["QolTrashReclaim"]
assert "QolStoreSort.Tick();" in source["Plugin"]
assert "_actionsWood" not in source["QolStoreSort"]
for key in ("_sortWood", "_quickWood", "_restockWood"):
    assert f"PositionScreen({key}," in source["QolStoreSort"]
assert "ScreenBounds(panel)" in source["QolStoreSort"]
assert "ScreenBounds(weight)" in source["QolStoreSort"]
assert "rightEdge=weightTab.xMax;" in source["QolStoreSort"]
assert "playerBottom=inventory.yMin;" in source["QolStoreSort"]
assert "weightBottom=weightTab.yMin;" in source["QolStoreSort"]
assert "float slot=available/3f;" in source["QolStoreSort"]
assert "float lower=playerBottom+4f*scale;" in source["QolStoreSort"]
assert "float upper=weightBottom-3f*scale;" in source["QolStoreSort"]
assert "float woodWidth=84f*scale;" in source["QolStoreSort"]
assert "exposed+16f*scale" in source["QolStoreSort"]
assert "new Vector2(centerX,bottom),woodWidth,boardHeight" in source["QolStoreSort"]
assert "_recycleCenter=new Vector2(iconCenterX,weightTop+nativeTabHeight*.5f" in source["QolStoreSort"]
assert "PositionAt(" not in source["QolStoreSort"]
# Regression geometry from the 1920x1080 game screenshot:
# inventory bottom 475px, Weight bottom 300px.
upper = 1080 - 300 - 3
lower = 1080 - 475 + 4
slot = (upper - lower) / 3
centers_top = [1080-(upper-slot*(i+0.5)) for i in range(3)]
assert all(300 < y < 475 for y in centers_top), centers_top
assert centers_top[-1] + (slot-1)/2 <= 475 - 3, centers_top
assert (684 - (84/2) + 84/2) == 684
assert "private const float ActionHitSize = 52f" in source["QolStoreSort"]
assert "ActionIconInset = 9f" in source["QolStoreSort"]
assert "new Vector2(8f,8f)" in source["QolTrashReclaim"]
assert "private static string PrefabId" in source["QolTrashReclaim"]
assert "db.GetRecipe(item)" in source["QolTrashReclaim"]
assert "Recipe prefabMatch = null" in source["QolTrashReclaim"]
assert "if (item.m_customData != null && item.m_customData.Count > 0)" not in source["QolTrashReclaim"]
assert "PositionRecycle(_buttonRect)" in source["QolTrashReclaim"]
assert "QolFavorites.FavoriteIcon,0,ToggleFavorite" in source["QolStoreSort"]
assert "SpriteMeshType.FullRect" in source["QolFavorites"]
assert "dfc004814b5c9d0a3b8d063034953eac3b18feb580db3342f7cf86c4664c7f54" in (root / "tools/generate_qol_assets.py").read_text()
assert len(list((root / "assets/supplied_64").glob("ui_atlas.bz2.b64.part*"))) == 6
assert "QolIconHover" in source["QolStoreSort"]
assert "KeyCode.Q" in source["QolNearbyInventory"] and "KeyCode.R" in source["QolNearbyInventory"]
assert "RangeSq = 100f * 100f" in source["QolNearbyInventory"]
assert "MoveItemToThis" in source["QolNearbyInventory"]
assert "ClaimOwnership()" in source["QolNearbyInventory"]
assert "QolGameAccess.CheckAccess(chest, player.GetPlayerID())" in source["QolNearbyInventory"]
assert "QolFavorites.IsFavorite" in source["QolNearbyInventory"]
assert "m_gridPos.y == 0" in source["QolNearbyInventory"]
assert "QolGameAccess.ButtonPosition" in source["QolFavorites"]
assert "SirrohQOLFavorites_" in source["QolFavorites"]
assert "BinaryFormatter" not in source["QolFavorites"]
assert "Czy na pewno chcesz zniszczyć ten przedmiot?" in source["QolTrashReclaim"]
assert '"Tak"' in source["QolTrashReclaim"] and '"Nie"' in source["QolTrashReclaim"]
assert "GetEmptySlots()" in source["QolTrashReclaim"]
assert "FindFreeStackSpace" in source["QolTrashReclaim"]
assert "100% materiałów" in source["QolTrashReclaim"]
assert "m_gridPos.y == 0" in source["QolTrashReclaim"]
assert "SignMaterials" not in "".join(source.values())
assert "SignMaterialMarkup" not in "".join(source.values())
manifest = json.loads((root / "manifest.json").read_text())
assert manifest["version_number"] == "0.1.18"
assert manifest["name"] == "SirrohQOLAddon"
print("Source and Thunderstore manifest contracts: PASS")

assert "VisibleRailInset = 6f" in source["QolStoreSort"]
assert "float iconCenterX=centerX+Mathf.Min(VisibleRailInset*scale,woodWidth*.18f);" in source["QolStoreSort"]
assert "PositionScreen(_sort,new Vector2(iconCenterX,top),iconHit,iconHit)" in source["QolStoreSort"]
assert "PositionScreen(_quick,new Vector2(iconCenterX,middle),iconHit,iconHit)" in source["QolStoreSort"]
assert "PositionScreen(_restock,new Vector2(iconCenterX,bottom),iconHit,iconHit)" in source["QolStoreSort"]
assert "_recycleCenter=new Vector2(iconCenterX" in source["QolStoreSort"]
assert "background.color = Color.clear;" in source["QolStoreSort"]
assert "hit.color = Color.clear;" in source["QolStoreSort"]
assert "hit.color = Color.clear;" in source["QolTrashReclaim"]
assert "background.raycastTarget = true;" in source["QolStoreSort"]
assert "hit.raycastTarget = true;" in source["QolTrashReclaim"]
assert "ArmorShieldX = 643f" in source["QolStoreSort"]
assert "ArmorShieldY = 80f" in source["QolStoreSort"]
assert "armor.Find(\"armor_icon\") as RectTransform" in source["QolStoreSort"]
assert "PositionFavorite(gui);" in source["QolStoreSort"]
assert "QolFavorites.FavoriteIcon,0,ToggleFavorite" in source["QolStoreSort"]
assert "_favoriteImage.rectTransform.sizeDelta = new Vector2(17f,17f)" in source["QolStoreSort"]
assert ".012f" not in source["QolStoreSort"] and ".012f" not in source["QolTrashReclaim"]
# Screenshot geometry: board approx x=600..684, exposed x=628..684.
# Its visible midpoint is 656; center of the hit regions moves from x642 to 655.
board_right = 684.0
wood_width = 84.0
previous_center = board_right - wood_width/2
new_center = previous_center + min(6.0,wood_width*0.18)
assert abs(new_center - 648) < 0.01
assert new_center < 655
assert new_center+26 <= board_right

# No popup tooltip, per-frame hover polling or visible clickbox background.
ui = source["QolStoreSort"]
trash = source["QolTrashReclaim"]
assert 'hover.Initialize(display, false);' in ui
assert 'hover.Initialize(image, sprite == QolFavorites.FavoriteIcon);' in ui
assert 'hover.Initialize(image, false);' in trash
assert 'private bool _hovered;' in ui
assert 'bool gold = _hovered || QolFavorites.FavoriteMode;' in ui
assert '_favoriteHover.Refresh();' in ui
assert '_graphic.gameObject.AddComponent<Shadow>()' in ui
assert '_glow.enabled = _hovered;' in ui
assert 'new Color(.73f, .49f, .30f, 1f)' in ui
assert 'new Color(1f, .92f, .62f, 1f)' in ui
assert 'void OnPointerEnter(PointerEventData eventData)' in ui
assert 'void OnPointerExit(PointerEventData eventData)' in ui
assert 'SirrohQOLTooltip' not in ui
assert 'TextMeshProUGUI' not in ui
assert 'void Hover(string caption)' not in ui
assert 'void Unhover()' not in ui

# Native Valheim UI dimming: no duplicated EnvMan/time/ambient calculations.
lighting = source["QolNativeUiLighting"]
assert 'LitShaderName = "Custom/LitGui"' in lighting
assert "FindLitMaterial(gui.m_player)" in lighting
assert "FindLitMaterial(gui.transform)" in lighting
assert "Shader.Find(LitShaderName)" in lighting
assert 'image.material = material;' in lighting
assert 'QolNativeUiLighting.Sync(__instance, _woodRoot, _overlay, _favoritePanel);' in source["QolStoreSort"]
assert 'QolNativeUiLighting.Sync(gui, _woodRoot, _overlay, _favoritePanel);' in source["QolStoreSort"]
assert "QolNativeUiLighting.Apply(image);" in source["QolTrashReclaim"]
assert "QolNativeUiLighting.Apply(overlay);" in source["QolFavorites"]
assert "QolNativeUiLighting.Shutdown();" in source["Plugin"]
assert "GetComponentsInChildren<Image>(true)" in lighting
assert "img.color.a < .00001f" in lighting
assert "Update()" not in lighting
assert "EnvMan.instance" not in lighting
assert "IsNight" not in lighting
assert "IsDay" not in lighting
assert "RenderSettings" not in lighting
assert "SetGlobalColor" not in lighting
assert "SetGlobalFloat" not in lighting
assert "Mathf.Lerp" not in lighting

# Only the user's six 64px pixel assets receive gamma lift.
# Scene/night dimming still runs exclusively through the native LitGui shader.
art = (root / "tools/generate_qol_assets.py").read_text(encoding="utf-8")
assert '"wood": 0.87, "favorite": 0.94' in art
assert '.get(name, 0.90)' in art
assert 'red.point(curve), green.point(curve), blue.point(curve), alpha' in art
assert 'icon.getchannel("A").tobytes() == original_pixels[3::4]' in art
assert 'after > before and after < before * 1.28' in art
assert 'dfc004814b5c9d0a3b8d063034953eac3b18feb580db3342f7cf86c4664c7f54' in art
assert 'Shader.Find(LitShaderName)' in source["QolNativeUiLighting"]
assert 'LitShaderName = "Custom/LitGui"' in source["QolNativeUiLighting"]
assert "Mathf.Lerp" not in source["QolNativeUiLighting"]
assert 'Color.clear;' in source["QolStoreSort"]
