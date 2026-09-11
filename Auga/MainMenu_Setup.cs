using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Main menu stays vanilla (layout, panels, logo); only its TMP texts get Auga's Source Sans Pro font.
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    public static class FejdStartup_Awake_Patch
    {
        private static TMP_FontAsset _font;

        public static void Postfix(FejdStartup __instance)
        {
            HideCinematicsButton(__instance);

            if (_font == null)
            {
                var source = Auga.Assets.SourceSansProSemiBold;
                _font = source != null ? TMP_FontAsset.CreateFontAsset(source) : null;
                if (_font == null)
                {
                    Debug.LogWarning("[Auga] MainMenu: could not create TMP font from SourceSansPro-SemiBold, keeping vanilla font");
                    return;
                }
                if (_font.fallbackFontAssetTable == null)
                    _font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            Apply(__instance.gameObject);
            // List entries are instantiated later from these prefabs.
            Apply(__instance.m_worldListElement);
            var serverList = __instance.GetComponentInChildren<ServerListGui>(true);
            if (serverList != null)
                Apply(serverList.m_serverListElement);
        }

        // The 1.0.7 cinematics list (Black Forest / Locked / Back) is unwanted. Vanilla HideAll already
        // hides m_cinematicsMenuList; the button that opens it has no field, only a prefab onClick -> OnCinematics.
        private static void HideCinematicsButton(FejdStartup instance)
        {
            foreach (var button in instance.GetComponentsInChildren<Button>(true))
            {
                var onClick = button.onClick;
                for (var i = 0; i < onClick.GetPersistentEventCount(); i++)
                {
                    if (onClick.GetPersistentMethodName(i) == nameof(FejdStartup.OnCinematics))
                    {
                        button.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }

        private static void Apply(GameObject root)
        {
            if (root == null)
                return;

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var original = text.font;
                if (original == _font)
                    continue;
                // Glyphs Source Sans Pro lacks (icons, symbols) still render from the vanilla font.
                if (original != null && !_font.fallbackFontAssetTable.Contains(original))
                    _font.fallbackFontAssetTable.Add(original);
                text.font = _font;
                text.fontSharedMaterial = _font.material;
            }
        }
    }
}
