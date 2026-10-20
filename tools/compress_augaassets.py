# Copyright (c) 2026 Morgott
# Licensed under CC BY-NC 4.0, see CC-BY-NC-4.0.txt
"""Re-encode the uncompressed UI textures of the stripped augaassets bundle as BC7.

Run after strip_augaassets.py. The ripped Auga project imported most of its UI art as RGBA32/RGB24, because Unity
refuses to block-compress a texture whose width or height is not a multiple of 4 (107 of them, e.g. 1407x1406):
121 RGBA32 + 6 RGB24 textures held ~95 MiB of the bundle's ~142 MiB GPU memory.

Per RGBA32/RGB24 texture of at least 256x256 texels that is not readable (TMP/legacy font atlases are Alpha8 and
stay untouched; small icons keep RGBA32: their hard 1-px alpha edges show block error most and they total ~2 MiB):
- pad to the next multiple of 4 on the right and on the top (Unity rows run bottom-up, so sprite pixel rects keep
  their origin), with the edge texels repeated: under bilinear filtering with Clamp wrap the padded border samples
  exactly like the clamped edge did. Repeat-wrapped odd-sized textures would change their tiling period, so they are kept as they are.
- encode BC7 (bc7enc via etcpak, uber level 4, perceptual or linear weights, whichever shows less error); mip count, sRGB flag,
  filter/wrap and isReadable (all off) are kept.
- every Sprite on a padded texture keeps its pixel rects (UGUI derives outer/inner UVs from textureRect and the
  texture size at runtime); its baked mesh UV0 (m_RD.m_VertexData, float2) is rescaled by old/new size so tight
  sprite meshes stay on the same texels.
The script prints per texture the worst channel error of the decoded BC7 against the source (unpadded region).

Usage: python tools/compress_augaassets.py <stripped augaassets> <output>
Needs UnityPy + etcpak (pip install UnityPy).
"""
import collections
import math
import pathlib
import struct
import sys

import etcpak
import texture2ddecoder
import UnityPy
from PIL import Image, ImageChops
from UnityPy.streams import EndianBinaryWriter

RGB24, RGBA32, BC7 = 3, 4, 25
CLAMP = 1
MIN_PIXELS = 256 * 256


def bc7_params(perceptual):
    params = etcpak.BC7CompressBlockParams()
    if perceptual:
        params.init_perceptual_weights()
    else:
        params.init_linear_weights()
    params.m_uber_level = 4
    params.m_max_partitions = 64
    params.m_try_least_squares = True
    params.m_mode17_partition_estimation_filterbank = True
    return params


def padded(img, width, height):
    """img in Unity row order (row 0 = bottom); repeat the last column and the top row into the padding."""
    w, h = img.size
    if (w, h) == (width, height):
        return img
    out = Image.new("RGBA", (width, height))
    out.paste(img, (0, 0))
    if width > w:
        out.paste(img.crop((w - 1, 0, w, h)).resize((width - w, h), Image.Resampling.NEAREST), (w, 0))
    if height > h:
        out.paste(out.crop((0, h - 1, width, h)).resize((width, height - h), Image.Resampling.NEAREST), (0, h))
    return out


def encode(raw, fmt, w, h):
    """BC7 with perceptual and with linear weights; keep the one with less visible error."""
    mode = "RGBA" if fmt == RGBA32 else "RGB"
    src = Image.frombytes(mode, (w, h), raw).convert("RGBA")
    width, height = -(-w // 4) * 4, -(-h // 4) * 4
    pixels = padded(src, width, height).tobytes()
    tries = [measure(src, etcpak.compress_bc7(pixels, width, height, bc7_params(p)), w, h, width, height) for p in (True, False)]
    data, worst, psnr = max(tries, key=lambda r: r[2])
    return data, width, height, worst, psnr


def measure(src, data, w, h, width, height):
    decoded = texture2ddecoder.decode_bc7(data, width, height)  # etcpak.decompress_bc7 crashes (0.9.15)
    back = Image.frombytes("RGBA", (width, height), decoded, "raw", "BGRA").crop((0, 0, w, h))
    # What reaches the screen is colour x alpha (UI shaders blend SrcAlpha/OneMinusSrcAlpha), so compare the
    # premultiplied colour plus the alpha itself; colour under alpha 0 is invisible and not counted.
    diff = ImageChops.difference(premultiplied(src), premultiplied(back))
    worst = [hi for _, hi in diff.getextrema()]
    hist = diff.histogram()
    sq = sum(n * (i % 256) ** 2 for i, n in enumerate(hist))
    psnr = 99.0 if sq == 0 else 10 * math.log10(255 * 255 * w * h * 4 / sq)
    return data, worst, psnr


def premultiplied(img):
    r, g, b, a = img.split()
    return Image.merge("RGBA", [ImageChops.multiply(c, a) for c in (r, g, b)] + [a])


def stream_layout(vd):
    """Byte offset + stride per vertex stream (Unity: streams packed in order, each start 16-aligned)."""
    sizes = {0: 4, 1: 2, 2: 1, 3: 1, 4: 2, 5: 2, 6: 1, 7: 1, 8: 2, 9: 2, 10: 4, 11: 4}
    strides = collections.defaultdict(int)
    for ch in vd["m_Channels"]:
        if ch["dimension"]:
            strides[ch["stream"]] = max(strides[ch["stream"]], ch["offset"] + sizes[ch["format"]] * (ch["dimension"] & 0xF))
    starts, pos = {}, 0
    for s in sorted(strides):
        pos += -pos % 16
        starts[s] = pos
        pos += strides[s] * vd["m_VertexCount"]
    return starts, strides, pos


def rescale_uv0(rd, sx, sy):
    vd = rd["m_VertexData"]
    uv = vd["m_Channels"][4]
    if not uv["dimension"]:
        return
    if uv["format"] != 0 or (uv["dimension"] & 0xF) != 2:
        sys.exit(f"sprite UV0 not float2: {uv}")
    starts, strides, total = stream_layout(vd)
    data = bytearray(vd["m_DataSize"])
    if len(data) != total:
        sys.exit(f"vertex data {len(data)} B, layout says {total} B")
    for i in range(vd["m_VertexCount"]):
        at = starts[uv["stream"]] + i * strides[uv["stream"]] + uv["offset"]
        u, v = struct.unpack_from("<ff", data, at)
        struct.pack_into("<ff", data, at, u * sx, v * sy)
    vd["m_DataSize"] = bytes(data)


def main(src, dst):
    env = UnityPy.load(src)
    bundle = env.file
    cab = next(n for n in bundle.files if not n.endswith((".resS", ".resource")))
    objs = bundle.files[cab].objects
    trees = {pid: o.read_typetree() for pid, o in objs.items() if o.type.name in ("Texture2D", "Sprite")}

    new_data, resized = {}, {}
    gpu_before = gpu_after = 0
    for pid, t in trees.items():
        if objs[pid].type.name != "Texture2D" or t["m_TextureFormat"] not in (RGBA32, RGB24):
            continue
        w, h, fmt = t["m_Width"], t["m_Height"], t["m_TextureFormat"]
        if not w or not h:
            continue  # 0x0 "Font Texture" of a dynamic legacy Font, filled at runtime
        if t["m_IsReadable"]:
            print(f"{t['m_Name']:<40} {w}x{h} kept: readable (CPU access, e.g. Cursor.SetCursor needs RGBA32)")
            continue
        if w * h < MIN_PIXELS:
            continue  # small icons: hard 1-px alpha edges show BC7 error most and all of them together are ~2 MiB
        if t["m_MipCount"] != 1:
            print(f"{t['m_Name']:<40} {w}x{h} kept: has {t['m_MipCount']} mips (script encodes mip 0 only)")
            continue
        settings = t["m_TextureSettings"]
        if (w % 4 or h % 4) and (settings["m_WrapU"] != CLAMP or settings["m_WrapV"] != CLAMP):
            print(f"{t['m_Name']:<40} {w}x{h} kept: odd size with Repeat wrap (padding would change the tiling period)")
            continue
        sd = t["m_StreamData"]
        raw = bundle.files[sd["path"].rsplit("/", 1)[-1]].bytes[sd["offset"]:sd["offset"] + sd["size"]] if sd["size"] else bytes(t["image data"])
        if len(raw) != w * h * (4 if fmt == RGBA32 else 3):
            sys.exit(f"{t['m_Name']}: {len(raw)} B for {w}x{h} fmt {fmt}")
        data, width, height, worst, psnr = encode(raw, fmt, w, h)
        print(f"{t['m_Name']:<40} {w}x{h} -> {width}x{height} BC7 psnr {psnr:5.1f} dB max err rgba {worst}")
        gpu_before += w * h * 4
        gpu_after += len(data)
        new_data[pid] = data
        if (width, height) != (w, h):
            resized[pid] = (w / width, h / height)
        t["m_Width"], t["m_Height"], t["m_TextureFormat"] = width, height, BC7
        t["m_CompleteImageSize"] = len(data)
        t["m_IsAlphaChannelOptional"] = False

    for pid, t in trees.items():
        if objs[pid].type.name == "Sprite" and t["m_RD"]["texture"]["m_PathID"] in resized:
            sx, sy = resized[t["m_RD"]["texture"]["m_PathID"]]
            rescale_uv0(t["m_RD"], sx, sy)
            objs[pid].save_typetree(t)

    # Rewrite the texture stream: changed textures get their BC7 bytes, the rest keep theirs.
    streams = collections.defaultdict(list)
    for pid, t in trees.items():
        if objs[pid].type.name == "Texture2D" and t["m_StreamData"]["size"]:
            streams[t["m_StreamData"]["path"].rsplit("/", 1)[-1]].append(pid)
    for pid in new_data:
        if not trees[pid]["m_StreamData"]["size"]:
            sys.exit(f"{trees[pid]['m_Name']}: inline image data, expected a .resS stream")
    for name, pids in streams.items():
        old = bundle.files[name].bytes
        out = bytearray()
        for pid in sorted(pids, key=lambda p: trees[p]["m_StreamData"]["offset"]):
            sd = trees[pid]["m_StreamData"]
            blob = new_data.get(pid, old[sd["offset"]:sd["offset"] + sd["size"]])
            out.extend(b"\0" * (-len(out) % 16))
            sd["offset"], sd["size"] = len(out), len(blob)
            out.extend(blob)
            objs[pid].save_typetree(trees[pid])
        writer = EndianBinaryWriter(bytes(out))
        writer.flags = bundle.files[name].flags
        bundle.files[name] = writer
        print(f"{name}: {len(old)} -> {len(out)} bytes")

    print(f"{len(new_data)} textures -> BC7 ({len(resized)} padded): GPU {gpu_before / 1048576:.1f} -> {gpu_after / 1048576:.1f} MiB")
    data = bundle.save(packer="original")
    pathlib.Path(dst).write_bytes(data)
    print(f"wrote {dst}: {len(data)} bytes")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
