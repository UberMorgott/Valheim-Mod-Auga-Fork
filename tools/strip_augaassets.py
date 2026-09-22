# Copyright (c) 2026 Morgott
# Licensed under CC BY-NC 4.0, see CC-BY-NC-4.0.txt
"""Losslessly strip the Unity-built augaassets bundle down to what AugaSkin loads.

Kept: every object reachable (PPtr walk) from the container entries that
Auga/Auga.cs loads by name (the Load<T>("...") calls), plus the AssetBundle
object. Every kept object keeps its bytes; the only edits are resource offsets
(Texture2D/AudioClip data repacked into the new .resS/.resource), the
AssetBundle container/preload table, and the CJK font removal below.

CJK fonts (only English and Russian are shipped): the SourceHanSansCN and
Noto Sans CJK Fonts and their TMP font assets are dropped. Edits: their entries
in legacy Font m_FallbackFonts and TMP m_FallbackFontAssetTable lists, and the
two TMP texts that use them directly (HotKeyElement/binding in HUD,
InventoryElement/quality in Inventory_screen; both prefabs are only read for
art, never instantiated) move to the same-weight Source Sans Pro font asset and
its material. Cyrillic stays covered: every kept dynamic font asset has a source
font with all 66 Russian letters, and AugaStyle.SetFont adds the vanilla font
as a runtime fallback.

Usage: python tools/strip_augaassets.py <unity-built augaassets> <output>
Re-run on every new Unity build of the bundle. Needs UnityPy (pip install UnityPy).
"""
import collections
import pathlib
import re
import sys

import UnityPy
from UnityPy.streams import EndianBinaryWriter

DROP_FONTS = {
    "SourceHanSansCN-Normal", "SourceHanSansCN-Medium", "SourceHanSansCN-Regular", "SourceHanSansCN-Bold",
    "Noto Sans CJK Regular",
}
# TMP font asset -> kept Latin/Cyrillic font asset of the same weight for texts that use it directly.
DROP_TMP = {
    "SourceHanSansCN-Bold SDF": "SourceSansPro-Bold SDF",
    "SourceHanSansCN-Regular SDF": "SourceSansPro-Regular SDF",
    "Noto Sans CJK Regular SDF": None,
}
STREAM_KEYS = {
    "m_StreamData": ("path", "offset", "size"),
    "m_Resource": ("m_Source", "m_Offset", "m_Size"),
}
AUGA_CS = pathlib.Path(__file__).resolve().parent.parent / "Auga" / "Auga.cs"


def pptrs(tree):
    found = []

    def walk(node):
        if isinstance(node, dict):
            if "m_PathID" in node and "m_FileID" in node:
                if node["m_FileID"] == 0 and node["m_PathID"]:
                    found.append(node["m_PathID"])
            else:
                for value in node.values():
                    walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(tree)
    return found


def asset_key(container_name):
    return container_name.rsplit("/", 1)[-1].rsplit(".", 1)[0].lower()


def main(src, dst):
    used = {n.lower() for n in re.findall(r'Load<[\w.]+>\("([^"]+)"\)', AUGA_CS.read_text(encoding="utf8"))}
    env = UnityPy.load(src)
    bundle = env.file
    cab = next(n for n in bundle.files if not n.endswith((".resS", ".resource")))
    objs = bundle.files[cab].objects
    trees = {pid: o.read_typetree() for pid, o in objs.items()}
    ab_obj = next(o for o in objs.values() if o.type.name == "AssetBundle")
    ab = trees[ab_obj.path_id]

    def closure(roots):
        seen, stack = set(), list(roots)
        while stack:
            pid = stack.pop()
            if pid in seen or pid not in objs:
                continue
            seen.add(pid)
            stack.extend(pptrs(trees[pid]))
        return seen

    roots = {info["asset"]["m_PathID"] for name, info in ab["m_Container"] if asset_key(name) in used}
    missing = used - {asset_key(name) for name, _ in ab["m_Container"]}
    if missing:
        sys.exit(f"assets loaded by Auga.cs not in bundle: {sorted(missing)}")

    font_names = {pid: trees[pid]["m_Name"] for pid in objs if objs[pid].type.name == "Font"}
    tmp_names = {pid: t["m_Name"] for pid, t in trees.items()
                 if objs[pid].type.name == "MonoBehaviour" and "m_AtlasPopulationMode" in t}
    tmp_by_name = {n: pid for pid, n in tmp_names.items()}
    dropped_tmp = {pid for pid, n in tmp_names.items() if n in DROP_TMP}
    dropped_mat = {trees[pid]["m_Material"]["m_PathID"]: pid for pid in dropped_tmp}

    def edit(pid, key, value):
        trees[pid][key] = value
        objs[pid].save_typetree(trees[pid])

    for pid, tree in trees.items():
        if pid in font_names:
            fallbacks = tree.get("m_FallbackFonts", [])
            kept = [f for f in fallbacks if font_names.get(f["m_PathID"]) not in DROP_FONTS]
            if len(kept) != len(fallbacks):
                edit(pid, "m_FallbackFonts", kept)
        elif pid in tmp_names:
            fallbacks = tree.get("m_FallbackFontAssetTable", [])
            kept = [f for f in fallbacks if f["m_PathID"] not in dropped_tmp]
            if len(kept) != len(fallbacks):
                edit(pid, "m_FallbackFontAssetTable", kept)
        elif isinstance(tree.get("m_fontAsset"), dict) and tree["m_fontAsset"]["m_PathID"] in dropped_tmp:
            # TMP text using a dropped CJK font asset directly: same-weight Source Sans Pro and its material.
            old = tree["m_fontAsset"]["m_PathID"]
            new = tmp_by_name[DROP_TMP[tmp_names[old]]]
            if dropped_mat.get(tree["m_sharedMaterial"]["m_PathID"]) != old:
                sys.exit(f"text {pid} uses a non-default material of {tmp_names[old]}")
            tree["m_fontAsset"] = {"m_FileID": 0, "m_PathID": new}
            tree["m_sharedMaterial"] = {"m_FileID": 0, "m_PathID": trees[new]["m_Material"]["m_PathID"]}
            objs[pid].save_typetree(tree)

    reach = closure(roots) | {ab_obj.path_id}
    if any(font_names.get(pid) in DROP_FONTS for pid in reach) or reach & dropped_tmp or reach & set(dropped_mat):
        sys.exit("a dropped font, font asset or its material is still referenced")
    dead = [pid for pid in objs if pid not in reach]
    print("dropped objects:", dict(collections.Counter(objs[p].type.name for p in dead)))

    streams = collections.defaultdict(list)
    for pid in reach:
        for key, (path_key, off_key, size_key) in STREAM_KEYS.items():
            data = trees[pid].get(key)
            if isinstance(data, dict) and data.get(size_key):
                name = data[path_key].rsplit("/", 1)[-1]
                streams[name].append((data[off_key], data[size_key], pid, key, off_key))
    for name, entries in streams.items():
        old = bundle.files[name].bytes
        new = bytearray()
        for offset, size, pid, key, off_key in sorted(entries):
            new.extend(b"\0" * (-len(new) % 16))
            trees[pid][key][off_key] = len(new)
            new.extend(old[offset:offset + size])
            objs[pid].save_typetree(trees[pid])
        writer = EndianBinaryWriter(bytes(new))
        writer.flags = bundle.files[name].flags
        bundle.files[name] = writer
        print(f"{name}: {len(old)} -> {len(new)} bytes")

    preload = ab["m_PreloadTable"]
    new_preload, ranges, container = [], {}, []
    for name, info in ab["m_Container"]:
        if info["asset"]["m_FileID"] == 0 and info["asset"]["m_PathID"] not in reach:
            continue
        span = (info["preloadIndex"], info["preloadSize"])
        if span not in ranges:
            items = [p for p in preload[span[0]:span[0] + span[1]] if p["m_FileID"] != 0 or p["m_PathID"] in reach]
            ranges[span] = (len(new_preload), len(items))
            new_preload.extend(items)
        info["preloadIndex"], info["preloadSize"] = ranges[span]
        container.append((name, info))
    ab["m_Container"], ab["m_PreloadTable"] = container, new_preload
    ab_obj.save_typetree(ab)
    for pid in dead:
        del objs[pid]

    data = bundle.save(packer="original")
    pathlib.Path(dst).write_bytes(data)
    print(f"wrote {dst}: {len(data)} bytes")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
