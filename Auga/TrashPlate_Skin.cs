using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // MorgottTweaks [Inventory] TrashSlot clones the vanilla Armor plate (m_player/Armor, restyled in place by
    // PlayerInventory_Setup) as m_player/MT_Trash. Its icon becomes Auga's own trash sprite: "Trash", referenced by
    // the old Auga trasher plate Inventory_screen/root/Player/TrashDivider/Content/Icon (bundle augaassets; the sprite
    // is not a named bundle asset, so it is read from that prefab).
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class TrashPlate_Skin
    {
        private static Sprite _trash;

        [UsedImplicitly]
        public static void Postfix(InventoryGui __instance)
        {
            var icon = __instance.m_player ? __instance.m_player.Find("MT_Trash/armor_icon") : null;
            if (!icon || !icon.TryGetComponent<Image>(out var image)) return;
            if (!_trash && Auga.Assets.InventoryScreen)
            {
                var source = Auga.Assets.InventoryScreen.transform.Find("root/Player/TrashDivider/Content/Icon");
                if (source && source.TryGetComponent<Image>(out var sourceImage)) _trash = sourceImage.sprite;
            }
            if (_trash && image.sprite != _trash) image.sprite = _trash;
        }
    }
}
