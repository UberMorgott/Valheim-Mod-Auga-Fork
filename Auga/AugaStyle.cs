using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Restyle in place: copy the look of an Auga bundle element onto a live vanilla element.
    // Visual properties only; RectTransforms, layout, components and vanilla fields stay untouched.
    public static class AugaStyle
    {
        public static T FromPrefab<T>(GameObject prefab, string path) where T : Component
        {
            var t = prefab ? prefab.transform.Find(path) : null;
            var c = t ? t.GetComponent<T>() : null;
            if (c == null)
                Debug.LogError($"[Auga] bundle prefab {(prefab ? prefab.name : "null")}: {path} ({typeof(T).Name}) missing");
            return c;
        }

        public static void CopyImage(Image dst, Image src)
        {
            if (!dst || !src)
                return;
            dst.sprite = src.sprite;
            dst.type = src.type;
            dst.color = src.color;
            dst.material = src.material;
            dst.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        }

        public static void CopyText(TMP_Text dst, TMP_Text src)
        {
            if (!dst || !src)
                return;
            SetFont(dst, src.font);
            dst.fontSharedMaterial = src.fontSharedMaterial;
            dst.color = src.color;
            dst.fontStyle = src.fontStyle;
            dst.characterSpacing = src.characterSpacing;
        }

        // Glyphs the Auga font lacks (icons, symbols) still render from the vanilla font.
        public static void SetFont(TMP_Text text, TMP_FontAsset font)
        {
            var original = text.font;
            if (!font || original == font)
                return;
            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (original && !font.fallbackFontAssetTable.Contains(original))
                font.fallbackFontAssetTable.Add(original);
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
    }
}
