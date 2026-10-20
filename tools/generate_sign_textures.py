"""Generate five deterministic 256x256 sign surface PNGs for embedding in the QOL DLL.

Pure Python: no Pillow, no external asset files and no non-deterministic RNG.
Each texture has directionally brushed grain, subtle surface variation and fine
worn highlights. The in-game renderer retains its own mesh and UV mapping.
"""
from pathlib import Path
import math
import struct
import zlib

SIZE = 256
OUT = Path("assets/sign_materials")
# Albedo RGB, grain strength, micro-scratch intensity. Metallic properties live in C#.
PALETTES = {
    "B": ((41, 49, 59), 18.0, 12.0),    # forged black metal
    "S": ((172, 181, 191), 22.0, 16.0), # brushed silver
    "G": ((204, 153, 58), 21.0, 13.0),  # warm gold
    "R": ((157, 39, 43), 15.0, 6.0),    # deep red lacquer
    "Y": ((220, 174, 45), 17.0, 7.0),   # yellow enamel
}

def rng(x, y, salt=0):
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 65535) / 65535.0

def chunk(kind, payload):
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)

def write_texture(code, color, amplitude, scratches):
    pixels = bytearray()
    for y in range(SIZE):
        pixels.append(0)  # PNG unfiltered scanline
        # Slanted directional brush grain with multiple scales, tile on the edges.
        for x in range(SIZE):
            u = x / SIZE
            v = y / SIZE
            grain = (
                math.sin(2 * math.pi * (v * 31 + 0.15 * math.sin(2 * math.pi * u * 3))) * .25
                + math.sin(2 * math.pi * v * 83) * .10
                + math.sin(2 * math.pi * v * 6) * .30
            )
            noise = (rng(x, y, ord(code)) - .5) * .65
            waviness = math.sin(2 * math.pi * (u * 5 + v * 2)) * 0.12
            streak = (rng(y // 2, x // 48, ord(code)) - .5) * scratches * 0.32
            # Very occasional hairline scratches; no repeating nails or seams.
            if code in "BSG" and y % 47 == 0:
                streak += scratches * 0.75
            delta = amplitude * (grain + noise + waviness) + streak
            # Golden and red/yellow materials have warmer highlights than steel.
            for channel, base in enumerate(color):
                multiplier = (1.10, 1.00, 0.90)[channel] if code in "GRY" else (0.92, 1.00, 1.08)[channel]
                pixels.append(max(4, min(250, round(base + delta * multiplier))))
            pixels.append(255)  # fully opaque
    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(pixels, 9)) + chunk(b"IEND", b"")
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / (code + ".png")
    path.write_bytes(png)
    rgba = bytearray()
    for line in range(SIZE):
        start = line * (SIZE * 4 + 1) + 1
        rgba.extend(pixels[start : start + SIZE * 4])
    raw = OUT / (code + ".rgba")
    raw.write_bytes(rgba)
    print(f"{code}: {path} ({len(png)} bytes), {raw} ({len(rgba)} bytes)")

for code, args in PALETTES.items():
    write_texture(code, *args)
