"""Validate the supplied mod icon; never overwrite user-supplied artwork."""
from pathlib import Path
from PIL import Image

icon = Path("icon.png")
with Image.open(icon) as image:
    assert image.format == "PNG", "Thunderstore icon must be PNG"
    assert image.size == (256, 256), "Thunderstore icon must be exactly 256x256"
    assert image.mode in ("RGBA", "P"), "Thunderstore icon must retain alpha/palette"
print("Validated supplied 256x256 Thunderstore icon")
