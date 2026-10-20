from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
source = {p.stem: p.read_text(encoding="utf-8") for p in (root / "src").glob("*.cs")}
assert len(source) == 6, sorted(source)
assert "sirro.valheim.pracafizyczna" in source["Plugin"]
assert 'Version = "0.1.0"' in source["Plugin"]
for feature in ("AutoRepair", "SafeStamina", "FoodFreeze", "GlowingSigns", "SlopeSafety"):
    assert feature + ".Install(this)" in source["Plugin"]
assert "GetAllItems().Count" in source["AutoRepair"] and "after <= before" in source["AutoRepair"]
assert "Time.time + 0.25f" in source["SafeStamina"] and "sqrMagnitude" in source["SafeStamina"]
assert "UpdateFood" in source["FoodFreeze"] and "AfterUpdate" in source["FoodFreeze"]
assert "return false;" in source["SlopeSafety"]
assert "new Material(source)" in source["GlowingSigns"]
manifest = json.loads((root / "manifest.json").read_text())
assert manifest["version_number"] == "0.1.0"
assert manifest["name"] == "SirrohQOLAddon"
print("Source and Thunderstore manifest contracts: PASS")
