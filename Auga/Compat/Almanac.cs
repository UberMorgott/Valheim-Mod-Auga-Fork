using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Auga.Compat;

// Almanac (soft dependency): its InventoryGui.Awake postfix (Almanac 3.8 UI\UI_Patches.cs) instantiates its windows
// under IngameGui/HUD - "Almanac" (bundle AlmanacUI), "Almanac Quest" and "Almanac NPC Dialogue" (DialoguePanel),
// "Almanac NPC UI" (NPCCustomization) - after forcing AveriaSerifLibre on every text (FontManager.SetFont), a
// woodpanel_trophys background (SetVanillaPanelBackground) and sprites copied from the inventory's crafting/trophy
// panels (CopySpriteAndMaterial: Auga's or vanilla's depending on which InventoryGui.Awake postfix ran first). The
// form modal (FormPanel._Modal) is instantiated later from its prefab (AlmanacPanel). Running last, every HUD child
// named Almanac* and the FormPanel prefab get AugaStyle.RestyleAll.
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
public static class Almanac
{
    [UsedImplicitly]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(InventoryGui __instance)
    {
        var hud = __instance.transform.parent ? __instance.transform.parent.Find("HUD") : null;
        if (!hud)
            return;
        var found = false;
        foreach (Transform child in hud)
        {
            if (!child.name.StartsWith("Almanac"))
                continue;
            found = true;
            Restyle(child);
        }
        if (!found)
            return;
        var form = AccessTools.TypeByName("Almanac.UI.FormPanel");
        if (form != null && AccessTools.Field(form, "_Modal")?.GetValue(null) is GameObject modal && modal)
            Restyle(modal.transform);
    }

    private static void Restyle(Transform root)
    {
        try
        {
            AugaStyle.RestyleAll(root);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Auga] Almanac {root.name} restyle failed: {e}");
        }
    }
}
