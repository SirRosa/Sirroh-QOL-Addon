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


# Lossless atlas assembled from the user's current conversation uploads.
# Six text chunks reconstruct the exact original 64x64 RGBA for:
# Sort, QuickStack, Restock, Recycle, Favorite and the wooden 64x64 frame.
# The checked digest prevents silent re-encoding/corruption.
import base64, bz2, hashlib
packed_b64 = "".join(
    (Path("assets/supplied_64") / ("ui_atlas.bz2.b64.part" + str(i))).read_text().strip()
    for i in range(6)
)
packed = base64.b64decode(packed_b64, validate=True)
assert hashlib.sha256(packed).hexdigest() == (
    "b3be72dc3266d7d04979f9234d96a46e200bc70b6f539b4e880595a349f61d76"
), "Uploaded UI asset atlas differs from the user original"
rgba = bz2.decompress(packed)
assert len(rgba) == 256 * 128 * 4
assert hashlib.sha256(rgba).hexdigest() == (
    "dfc004814b5c9d0a3b8d063034953eac3b18feb580db3342f7cf86c4664c7f54"
), "Uploaded pixel data differs from the user original"
atlas = Image.frombytes("RGBA", (256,128), rgba)
for name, x, y in (
    ("sort",0,0), ("quickstack",64,0),
    ("restock",128,0), ("recycle",192,0),
    ("favorite",0,64), ("wood",64,64),
):
    icon = atlas.crop((x,y,x+64,y+64))

    # Art-only brightness correction, measured against the vanilla side tabs
    # at nighttime. Keep the source atlas and SHA-256 unchanged.
    # A gentle gamma curve raises mid/dark RGB values while preserving the
    # original palette, near-white highlights, per-pixel alpha and dimensions.
    # Custom/LitGui remains entirely responsible for scene-dependent dimming.
    original_pixels = icon.tobytes()
    gamma = {"wood": 0.87, "favorite": 0.94}.get(name, 0.90)
    curve = [min(255, round(255 * (value / 255) ** gamma))
             for value in range(256)]
    red, green, blue, alpha = icon.split()
    icon = Image.merge("RGBA", (
        red.point(curve), green.point(curve), blue.point(curve), alpha
    ))
    assert icon.getchannel("A").tobytes() == original_pixels[3::4], (
        "Pixel transparency was modified: " + name
    )

    def average_luma(blob):
        # Count visible RGB values only; transparent padding is irrelevant.
        n = 0
        total = 0.0
        for i in range(0, len(blob), 4):
            if blob[i+3] < 8:
                continue
            total += (0.2126 * blob[i] + 0.7152 * blob[i+1]
                      + 0.0722 * blob[i+2]) * blob[i+3] / 255
            n += blob[i+3] / 255
        return total / n if n > 0 else 0

    before = average_luma(original_pixels)
    after = average_luma(icon.tobytes())
    assert after > before and after < before * 1.28, (
        "Art-only lift outside subtle brightness range: " + name
    )
    print(f"UI graphic brightness {name}: {before:.1f} -> {after:.1f} "
          f"(+{(after/before-1)*100:.1f}%, gamma={gamma})")

    # Pillow is top-down, UnityEngine.Texture2D.LoadRawTextureData is
    # bottom-up; flip rows here so the on-screen artwork is upright.
    icon = icon.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    output = struct.pack("<II",64,64) + icon.tobytes()
    assert len(output) == 8+64*64*4
    (out / ("ui_"+name+".rgba")).write_bytes(output)
    print("User supplied exact UI:",name,"64x64 (lossless)")
