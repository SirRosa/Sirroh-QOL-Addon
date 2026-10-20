from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
source = {p.stem: p.read_text(encoding="utf-8") for p in (root / "src").glob("*.cs")}
assert len(source) == 8, sorted(source)
assert "sirro.valheim.pracafizyczna" in source["Plugin"]
assert 'Version = "0.1.4"' in source["Plugin"]
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
assert "SignMaterials.Install(this)" in source["Plugin"]
assert "SignMaterials.Shutdown()" in source["Plugin"]
assert "OnCheckPermissionCompleted" in source["SignMaterials"]
assert "renderer.sharedMaterials" in source["SignMaterials"] or "b.Renderer.sharedMaterials" in source["SignMaterials"]
assert "Sirroh.Qol.Signs." in source["SignMaterials"]
assert "EnableKeyword" in source["GlowingSigns"]
assert "ZDOVars" not in source["SignMaterials"] and "ZDO::Set" not in source["SignMaterials"]
assert "StringComparison.OrdinalIgnoreCase" in source["SignMaterialMarkup"]
assert 'nameof(AfterUpdateText)' in source["SignMaterials"]
assert 'GetComponentInParent<ZNetView>()' in source["SignMaterials"]
assert 'OriginalText(sign)' in source["SignMaterials"]
assert 'Viewable(sign)' in source["SignMaterials"]
assert 'Shader.Find("Standard")' in source["SignMaterials"]
for code in ("'B'", "'S'", "'G'", "'R'", "'Y'"):
    assert code in source["SignMaterialMarkup"] and code in source["SignMaterials"]
manifest = json.loads((root / "manifest.json").read_text())
assert manifest["version_number"] == "0.1.4"
assert manifest["name"] == "SirrohQOLAddon"
print("Source and Thunderstore manifest contracts: PASS")
