from pathlib import Path
import struct, zlib

root = Path(__file__).resolve().parents[1]
textures = root / "assets/sign_materials"
colors = {}
for code in "BSGRY":
    data = (textures / (code + ".png")).read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    size, typ = struct.unpack(">I4s", data[8:16])
    assert size == 13 and typ == b"IHDR"
    width, height, depth, mode = struct.unpack(">IIBB", data[16:26])
    assert (width, height, depth, mode) == (256, 256, 8, 6)
    assert len(data) > 1000
    colors[code] = data
assert len({zlib.crc32(value) for value in colors.values()}) == 5
print("PASS: five distinct 256x256 RGBA sign texture variants")
