using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Main menu stays vanilla (layout, panels, logo). Only button labels get the font of Auga's old
    // main-menu buttons (bundle TMP "Norsebold SDF", has Cyrillic). It is a display font, so body and
    // list texts (world/server/character names) keep the vanilla font.
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    public static class FejdStartup_Awake_Patch
    {
        public static void Postfix(FejdStartup __instance)
        {
            HideCinematicsButton(__instance);

            var font = Auga.Assets.NorseboldTMP;
            if (font == null)
            {
                Debug.LogWarning("[Auga] MainMenu: Norsebold SDF missing, keeping vanilla font");
                return;
            }

            foreach (var text in __instance.GetComponentsInChildren<TMP_Text>(true))
            {
                var original = text.font;
                if (original == font || text.GetComponentInParent<Button>(true) == null)
                    continue;
                // Glyphs Norsebold lacks (icons, symbols) still render from the vanilla font.
                if (original != null && !font.fallbackFontAssetTable.Contains(original))
                    font.fallbackFontAssetTable.Add(original);
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
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
    }
}
