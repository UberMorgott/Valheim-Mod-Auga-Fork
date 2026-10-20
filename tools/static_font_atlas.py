# Copyright (c) 2026 Morgott
# Licensed under CC BY-NC 4.0, see CC-BY-NC-4.0.txt
"""Freeze a Dynamic TMP font asset of the augaassets bundle into a Static one with a non-readable atlas.

A Dynamic TMP_FontAsset keeps its atlas readable (TMP refuses to add glyphs to a non-readable texture), so the
bundle's "SourceSansPro-Bold SDF" (4096x4096 Alpha8, 584 baked characters, only 18 of them Cyrillic) holds a 16 MiB
CPU copy of the atlas next to the GPU one. Freezing it needs every glyph text can ask for already in the atlas:

1. prepare: dump the asset's atlas, glyph/character tables and packer rects, plus the source font's cmap minus the
   baked characters (the codepoints to add).
2. tools/unity/FontAtlasBake.cs (Unity 6000.0.75f1, batchmode, any project with com.unity.ugui 2.0 and the TMP
   shaders): rebuilds the asset with its creation settings, injects the dumped state and adds the codepoints through
   TMP_FontAsset.TryAddCharacters, i.e. the same FontEngine path the game takes at runtime for a Dynamic asset.
   Baked glyphs keep their pixels, rects and metrics; new ones go into the free rects.
3. splice: write the result back: glyph/character tables and rects replaced, AtlasPopulationMode Static, source font
   reference cleared (what TMP's editor setter does for Static), atlas pixels replaced and m_IsReadable off. Font
   feature tables (kerning, ligatures, marks) already cover the whole font and stay as they are.

Usage:
  python tools/static_font_atlas.py prepare <bundle> "<font asset name>" <font ttf> <workdir>
  (copy the font ttf into the bake project's Assets, tools/unity/FontAtlasBake.cs into Assets/Editor)
  Unity.exe -batchmode -quit -nographics -projectPath <bake project> -executeMethod FontAtlasBake.Run
            -bakeOut <workdir> -bakeFont Assets/<font ttf>
  python tools/static_font_atlas.py splice <bundle> "<font asset name>" <workdir> <output bundle>
Needs UnityPy + fontTools.
"""
import json
import pathlib
import sys

import UnityPy

STATIC = 0


def load(src, name):
    env = UnityPy.load(src)
    bundle = env.file
    cab = next(n for n in bundle.files if not n.endswith((".resS", ".resource")))
    objs = bundle.files[cab].objects
    for o in objs.values():
        if o.type.name == "MonoBehaviour":
            t = o.read_typetree()
            if t.get("m_Name") == name and "m_AtlasPopulationMode" in t:
                return bundle, objs, o, t
    sys.exit(f"no TMP font asset named {name!r}")


def rect(r):
    return {"x": r["m_X"], "y": r["m_Y"], "w": r["m_Width"], "h": r["m_Height"]}


def unrect(r):
    return {"m_X": r["x"], "m_Y": r["y"], "m_Width": r["w"], "m_Height": r["h"]}


def prepare(src, name, ttf, work):
    from fontTools.ttLib import TTFont

    work = pathlib.Path(work)
    work.mkdir(parents=True, exist_ok=True)
    _, objs, _, fa = load(src, name)
    if fa["m_AtlasPopulationMode"] == STATIC:
        sys.exit(f"{name} is already Static")
    if len(fa["m_AtlasTextures"]) != 1 or fa["m_IsMultiAtlasTexturesEnabled"]:
        sys.exit(f"{name}: expected one atlas texture without multi-atlas")
    tex = objs[fa["m_AtlasTextures"][0]["m_PathID"]].read_typetree()
    if tex["m_TextureFormat"] != 1 or tex["m_StreamData"]["size"]:
        sys.exit(f"{name}: expected an inline Alpha8 atlas")
    (work / "old_atlas.bin").write_bytes(bytes(tex["image data"]))
    m = lambda g: g["m_Metrics"]
    cs = fa["m_CreationSettings"]
    inject = {
        "settings": {"pointSize": int(cs["pointSize"]), "padding": int(cs["padding"]), "renderMode": int(cs["renderMode"]),
                     "width": fa["m_AtlasWidth"], "height": fa["m_AtlasHeight"]},
        "glyphs": [{"i": g["m_Index"], "w": m(g)["m_Width"], "h": m(g)["m_Height"], "bx": m(g)["m_HorizontalBearingX"],
                    "by": m(g)["m_HorizontalBearingY"], "adv": m(g)["m_HorizontalAdvance"], "r": rect(g["m_GlyphRect"]),
                    "s": g["m_Scale"], "a": g["m_AtlasIndex"]} for g in fa["m_GlyphTable"]],
        "chars": [{"u": c["m_Unicode"], "g": c["m_GlyphIndex"], "s": c["m_Scale"]} for c in fa["m_CharacterTable"]],
        "used": [rect(r) for r in fa["m_UsedGlyphRects"]],
        "free": [rect(r) for r in fa["m_FreeGlyphRects"]],
    }
    (work / "inject.json").write_text(json.dumps(inject))
    baked = {c["m_Unicode"] for c in fa["m_CharacterTable"]}
    add = [u for u in sorted(TTFont(ttf).getBestCmap()) if u not in baked]
    (work / "order.txt").write_text(" ".join(map(str, add)))
    print(f"{name}: {tex['m_Width']}x{tex['m_Height']} readable={tex['m_IsReadable']} {len(baked)} chars baked, "
          f"{len(add)} cmap codepoints to add; bake with {cs['pointSize']} pt pad {cs['padding']} render mode {cs['renderMode']}")


def splice(src, name, work, dst):
    work = pathlib.Path(work)
    bundle, objs, fobj, fa = load(src, name)
    bake = json.loads((work / "bake.json").read_text())
    atlas = (work / "new_atlas.bin").read_bytes()
    tobj = objs[fa["m_AtlasTextures"][0]["m_PathID"]]
    tex = tobj.read_typetree()
    if (bake["tex"]["w"], bake["tex"]["h"], bake["tex"]["fmt"]) != (tex["m_Width"], tex["m_Height"], "Alpha8"):
        sys.exit(f"baked atlas {bake['tex']} does not match {tex['m_Width']}x{tex['m_Height']}")
    if len(atlas) != tex["m_Width"] * tex["m_Height"]:
        sys.exit(f"baked atlas has {len(atlas)} B")
    face = fa["m_FaceInfo"]
    for k, v in (("m_PointSize", "pointSize"), ("m_LineHeight", "lineHeight"), ("m_AscentLine", "ascentLine"), ("m_DescentLine", "descentLine")):
        if abs(face[k] - bake["face"][v]) > 1e-3:
            sys.exit(f"face info {k} {face[k]} != baked {bake['face'][v]}")

    old_glyphs = {g["m_Index"]: g for g in fa["m_GlyphTable"]}
    old_chars = {c["m_Unicode"]: c["m_GlyphIndex"] for c in fa["m_CharacterTable"]}
    glyphs = []
    for g in bake["glyphs"]:
        w, h, bx, by, adv = g["m"]
        new = {"m_Index": g["i"], "m_Metrics": {"m_Width": w, "m_Height": h, "m_HorizontalBearingX": bx, "m_HorizontalBearingY": by,
                                               "m_HorizontalAdvance": adv},
               "m_GlyphRect": unrect(g["r"]), "m_Scale": g["s"], "m_AtlasIndex": g["a"], "m_ClassDefinitionType": 0}
        old = old_glyphs.get(g["i"])
        if old is not None:
            if (old["m_Metrics"], old["m_GlyphRect"], old["m_Scale"], old["m_AtlasIndex"]) != (new["m_Metrics"], new["m_GlyphRect"], new["m_Scale"], new["m_AtlasIndex"]):
                sys.exit(f"glyph {g['i']} changed in the bake")
            new["m_ClassDefinitionType"] = old["m_ClassDefinitionType"]
        glyphs.append(new)
    chars = [{"m_ElementType": 1, "m_Unicode": c["u"], "m_GlyphIndex": c["g"], "m_Scale": c["s"]} for c in bake["chars"]]
    if any(old_chars[c["m_Unicode"]] != c["m_GlyphIndex"] for c in chars if c["m_Unicode"] in old_chars) or len(set(old_chars) - {c["m_Unicode"] for c in chars}):
        sys.exit("baked character table lost or remapped a baked character")
    old_atlas = bytes(tex["image data"])
    width = tex["m_Width"]
    for g in fa["m_UsedGlyphRects"]:
        for y in range(g["m_Y"], g["m_Y"] + g["m_Height"]):
            row = slice(y * width + g["m_X"], y * width + g["m_X"] + g["m_Width"])
            if old_atlas[row] != atlas[row]:
                sys.exit(f"baked atlas changed pixels of used rect {g}")

    fa["m_GlyphTable"] = glyphs
    fa["m_CharacterTable"] = chars
    fa["m_UsedGlyphRects"] = [unrect(r) for r in bake["used"]]
    fa["m_FreeGlyphRects"] = [unrect(r) for r in bake["free"]]
    fa["m_AtlasPopulationMode"] = STATIC
    fa["m_SourceFontFile"] = {"m_FileID": 0, "m_PathID": 0}
    fobj.save_typetree(fa)
    tex["image data"] = atlas
    tex["m_CompleteImageSize"] = len(atlas)
    tex["m_IsReadable"] = False
    tobj.save_typetree(tex)

    cyr = sum(1 for c in chars if 0x400 <= c["m_Unicode"] <= 0x52F)
    print(f"{name}: {len(old_chars)} -> {len(chars)} chars ({cyr} Cyrillic), {len(old_glyphs)} -> {len(glyphs)} glyphs, Static; "
          f"atlas {tex['m_Width']}x{tex['m_Height']} Alpha8 {len(atlas)} B readable -> non-readable; "
          f"cmap codepoints not baked: {[hex(u) for u in bake['missing']]}")
    data = bundle.save(packer="original")
    pathlib.Path(dst).write_bytes(data)
    print(f"wrote {dst}: {len(data)} bytes")


if __name__ == "__main__":
    if len(sys.argv) == 6 and sys.argv[1] == "prepare":
        prepare(*sys.argv[2:])
    elif len(sys.argv) == 6 and sys.argv[1] == "splice":
        splice(*sys.argv[2:])
    else:
        sys.exit(__doc__)
