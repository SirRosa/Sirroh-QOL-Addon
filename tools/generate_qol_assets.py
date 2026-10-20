"""Fetch original extracted QuickStack/Reclaim icon PNGs and embed as Unity raw RGBA.
Run only in CI; the assets are from the user's referenced GitHub repository.
"""
from pathlib import Path
from urllib.parse import quote
from urllib.request import Request, urlopen
from PIL import Image
from io import BytesIO
import struct

source = "https://raw.githubusercontent.com/SirRosa/Sirroh-Compatibility-Builder/main/"
base = "Extracted/QOL/To Recreate/"
items = {
    "border": base + "QuickStackStore__71ccc644da/csharp/QuickStackStore.Resources.border.png",
    "trash": base + "QuickStackStore__71ccc644da/csharp/QuickStackStore.Resources.trash.png",
    "trashmask": base + "QuickStackStore__71ccc644da/csharp/QuickStackStore.Resources.trashmask.png",
    "trashingborder": base + "Recycle_N_Reclaim__362e0a417c/csharp/Recycle_N_Reclaim.Assets.trashingborder.png",
    "RecyclingPanel": base + "Recycle_N_Reclaim__362e0a417c/csharp/Recycle_N_Reclaim.Assets.RecyclingPanel.png",
}
out = Path(".build-refs/assets")
out.mkdir(parents=True, exist_ok=True)
for name, relative in items.items():
    url = source + quote(relative, safe="/")
    with urlopen(Request(url, headers={"User-Agent":"Sirroh-QOL-Addon-Builder"}), timeout=40) as response:
        raw = response.read()
    with Image.open(BytesIO(raw)) as source_image:
        image = source_image.convert("RGBA")
        width, height = image.size
        output = struct.pack("<II", width, height) + image.tobytes()
    assert len(output) == 8 + width * height * 4
    (out / (name + ".rgba")).write_bytes(output)
    print(f"Embedded original {name}: {width}x{height}, {len(raw)} PNG bytes")
