from pathlib import Path
import struct
import zlib

size = 256
raw = bytearray()
for y in range(size):
    raw.append(0)
    for x in range(size):
        a, b = abs(x-128), abs(y-128)
        shield = b < 91 and a < (94 - max(0, y - 137) * 0.8)
        stripe = (abs(x-y) < 13 or abs(x + y - 255) < 13) and shield
        accent = shield and (abs(x-128) < 13 or abs(y-112) < 12)
        rgb = (24, 29, 38)
        if shield: rgb = (79, 93, 110)
        if stripe: rgb = (143, 157, 168)
        if accent: rgb = (220, 226, 226)
        raw.extend(rgb)

def chunk(name, data):
    return struct.pack(">I", len(data)) + name + data + struct.pack(">I", zlib.crc32(name + data) & 0xffffffff)

png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
Path("icon.png").write_bytes(png)
print("Wrote 256x256 Thunderstore icon")
