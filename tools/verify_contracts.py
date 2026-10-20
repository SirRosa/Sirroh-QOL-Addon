from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
source = {p.stem: p.read_text(encoding="utf-8") for p in (root / "src").glob("*.cs")}
assert len(source) == 6, sorted(source)
assert "sirro.valheim.pracafizyczna" in source["Plugin"]
assert 'Version = "0.1.2"' in source["Plugin"]
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
manifest = json.loads((root / "manifest.json").read_text())
assert manifest["version_number"] == "0.1.2"
assert manifest["name"] == "SirrohQOLAddon"
print("Source and Thunderstore manifest contracts: PASS")
