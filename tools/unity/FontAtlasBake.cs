// Copyright (c) 2026 Morgott
// Licensed under CC BY-NC 4.0, see CC-BY-NC-4.0.txt
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

// Step 2 of tools/static_font_atlas.py (editor script, Unity 6000.0.75f1, com.unity.ugui 2.0 + TMP shaders).
// Unity.exe -batchmode -quit -nographics -projectPath <p> -executeMethod FontAtlasBake.Run -bakeOut <workdir> -bakeFont Assets/<ttf>
// <workdir> holds old_atlas.bin, inject.json and order.txt from "prepare". Recreates the bundle font asset's state
// (creation settings, atlas pixels, glyph/character tables, packer rects) in a Dynamic asset, adds the codepoints of
// order.txt through TryAddCharacters (the FontEngine path the game uses at runtime) and writes new_atlas.bin + bake.json.
public static class FontAtlasBake
{
    [Serializable] public class R { public int x, y, w, h; }
    [Serializable] public class G { public uint i; public float w, h, bx, by, adv; public R r; public float s; public int a; }
    [Serializable] public class C { public uint u, g; public float s; }
    [Serializable] public class S { public int pointSize, padding, renderMode, width, height; }
    [Serializable] public class Inject { public S settings; public List<G> glyphs; public List<C> chars; public List<R> used, free; }

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : throw new ArgumentException("missing " + name);
    }

    static FieldInfo Field(string name) => typeof(TMP_FontAsset).GetField(name, Any) ?? throw new MissingFieldException(name);
    static GlyphRect Rect(R r) => new GlyphRect(r.x, r.y, r.w, r.h);

    public static void Run()
    {
        var dir = Arg("-bakeOut");
        // CreateFontAsset reads TMP_Settings; a bake project needs no TMP Essential Resources for that.
        typeof(TMP_Settings).GetField("s_Instance", Any).SetValue(null, ScriptableObject.CreateInstance<TMP_Settings>());
        var inj = JsonUtility.FromJson<Inject>(File.ReadAllText(Path.Combine(dir, "inject.json")));
        var add = File.ReadAllText(Path.Combine(dir, "order.txt")).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => uint.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var st = inj.settings;

        var font = AssetDatabase.LoadAssetAtPath<Font>(Arg("-bakeFont"));
        var fa = TMP_FontAsset.CreateFontAsset(font, st.pointSize, st.padding, (GlyphRenderMode)st.renderMode, st.width, st.height,
            AtlasPopulationMode.Dynamic, false);
        var tex = fa.atlasTexture;
        if (tex.width != st.width || tex.height != st.height)
            tex.Reinitialize(st.width, st.height);
        tex.LoadRawTextureData(File.ReadAllBytes(Path.Combine(dir, "old_atlas.bin")));
        tex.Apply(false, false);

        var glyphs = inj.glyphs.Select(g => new Glyph(g.i, new GlyphMetrics(g.w, g.h, g.bx, g.by, g.adv), Rect(g.r), g.s, g.a)).ToList();
        var byIndex = glyphs.ToDictionary(g => g.index);
        Field("m_GlyphTable").SetValue(fa, glyphs);
        Field("m_CharacterTable").SetValue(fa, inj.chars.Select(c => new TMP_Character(c.u, fa, byIndex[c.g]) { scale = c.s }).ToList());
        Field("m_UsedGlyphRects").SetValue(fa, inj.used.Select(Rect).ToList());
        Field("m_FreeGlyphRects").SetValue(fa, inj.free.Select(Rect).ToList());
        fa.ReadFontAssetDefinition();

        bool all = fa.TryAddCharacters(add, out uint[] missing, false);
        missing ??= new uint[0];
        Debug.Log($"FontAtlasBake: {inj.chars.Count} -> {fa.characterTable.Count} chars, all={all}, not added={missing.Length}");

        File.WriteAllBytes(Path.Combine(dir, "new_atlas.bin"), tex.GetRawTextureData());
        var ci = CultureInfo.InvariantCulture;
        string F(float f) => f.ToString("R", ci);
        string Rs(GlyphRect r) => $"{{\"x\":{r.x},\"y\":{r.y},\"w\":{r.width},\"h\":{r.height}}}";
        var fi = fa.faceInfo;
        var sb = new StringBuilder();
        sb.Append($"{{\"tex\":{{\"w\":{tex.width},\"h\":{tex.height},\"fmt\":\"{tex.format}\"}},");
        sb.Append($"\"face\":{{\"pointSize\":{F(fi.pointSize)},\"lineHeight\":{F(fi.lineHeight)},\"ascentLine\":{F(fi.ascentLine)},\"descentLine\":{F(fi.descentLine)}}},");
        sb.Append("\"missing\":[").Append(string.Join(",", missing.Select(m => m.ToString(ci)))).Append("],");
        sb.Append("\"glyphs\":[").Append(string.Join(",", fa.glyphTable.Select(g =>
            $"{{\"i\":{g.index},\"m\":[{F(g.metrics.width)},{F(g.metrics.height)},{F(g.metrics.horizontalBearingX)},{F(g.metrics.horizontalBearingY)},{F(g.metrics.horizontalAdvance)}],\"r\":{Rs(g.glyphRect)},\"s\":{F(g.scale)},\"a\":{g.atlasIndex}}}")));
        sb.Append("],\"chars\":[").Append(string.Join(",", fa.characterTable.Select(c => $"{{\"u\":{c.unicode},\"g\":{c.glyphIndex},\"s\":{F(c.scale)}}}")));
        sb.Append("],\"used\":[").Append(string.Join(",", ((List<GlyphRect>)Field("m_UsedGlyphRects").GetValue(fa)).Select(Rs)));
        sb.Append("],\"free\":[").Append(string.Join(",", ((List<GlyphRect>)Field("m_FreeGlyphRects").GetValue(fa)).Select(Rs))).Append("]}");
        File.WriteAllText(Path.Combine(dir, "bake.json"), sb.ToString());
    }
}
