from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
source = {p.stem: p.read_text(encoding="utf-8") for p in (root / "src").glob("*.cs")}
assert len(source) == 9, sorted(source)
assert "sirro.valheim.pracafizyczna" in source["Plugin"]
assert 'Version = "0.1.7"' in source["Plugin"]
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
assert "50f +" in source["ConstructionCamera"] and "200f" in source["ConstructionCamera"]
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
assert "SignMaterials" not in "".join(source.values())
assert "SignMaterialMarkup" not in "".join(source.values())
manifest = json.loads((root / "manifest.json").read_text())
assert manifest["version_number"] == "0.1.7"
assert manifest["name"] == "SirrohQOLAddon"
print("Source and Thunderstore manifest contracts: PASS")
