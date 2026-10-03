using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using AugaUnity;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Auga
{
    public class StoreMethods
    {
        public StoreGui SetupAugaStoreGui(StoreGui instance)
        {
            if (instance.name.StartsWith("Auga")) return instance;

            try
            {
                var originalTransform = instance.transform;
                var parent = originalTransform.parent;
                var siblingIndex = parent.GetSiblingIndex();
                var newStoreGui = GetAugaStoreGui(parent);
                newStoreGui.transform.SetAsLastSibling();
                SetupHelper.WrapInRootCanvasOf(newStoreGui.transform, instance.gameObject);

                if (string.Equals(instance.transform.name, "Store_Screen", StringComparison.Ordinal) && string.Equals(instance.m_rootPanel.name, "Store", StringComparison.Ordinal))
                {
                    originalTransform.gameObject.SetActive(false);
                    instance = newStoreGui;
                    return instance;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error In Store: {e.Message}");
            }

            return instance;
        }

        private StoreGui GetAugaStoreGui(Transform parent)
        {
            var newStore = Object.Instantiate(Auga.Assets.StoreGui, parent, false);
            var newStoreGui = newStore.GetComponent<StoreGui>();

            newStoreGui.m_coinPrefab = ObjectDB.instance.GetItemPrefab("Coins").GetComponent<ItemDrop>();
            newStoreGui.transform.Find("Store").gameObject.AddComponent<MovableHudElement>().Init(TextAnchor.UpperLeft, 140, -180);
            AddVanillaStorePaths(newStoreGui);

            return newStoreGui;
        }

        // Epic Loot 0.14.13 TemperPanel.LoadImageSprites (Hildir's tempering panel, built under the shown StoreGui
        // by its StoreGui.Show finalizer) copies art from vanilla Store_Screen paths: Store/SellPanel (frame
        // material) and Store/ItemList/Items/ItemElement/bkg|icon|selected (row sprite, icon material, selection
        // colour). Auga's store has neither path, so the lookup threw and TemperPanel.Awake stopped there: white
        // sprite-less Sundial and Temper button, English titles, no position from its config. Inactive stand-ins
        // carrying Auga's own row art (the store's m_listElement) give it those values; nothing draws them.
        private static void AddVanillaStorePaths(StoreGui store)
        {
            var root = store.m_rootPanel ? store.m_rootPanel.transform : store.transform.Find("Store");
            if (!root || root.Find("SellPanel"))
                return;
            var panel = root.Find("AugaPanelBase/Background");
            Stub("SellPanel", root).material = panel && panel.TryGetComponent<Image>(out var bg) ? bg.material : null;

            var items = Stub("ItemList", root).transform;
            items = Stub("Items", items).transform;
            var element = Stub("ItemElement", items).transform;
            var template = store.m_listElement ? store.m_listElement.transform : null;
            foreach (var name in new[] { "bkg", "icon", "selected" })
            {
                var src = template ? template.Find(name) : null;
                var dst = Stub(name, element);
                if (src && src.TryGetComponent<Image>(out var image))
                    AugaStyle.CopyImage(dst, image);
            }
        }

        private static Image Stub(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Image>();
        }

    }

    [HarmonyPatch]
    public class Store_Setup
    {

        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Awake))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Awake_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var instrs = instructions.ToList();

            var counter = 0;

            CodeInstruction LogMessage(CodeInstruction instruction)
            {
                //Debug.LogWarning($"IL_{counter}: Opcode: {instruction.opcode} Operand: {instruction.operand}");
                return instruction;
            }

            for (int i = 0; i < instrs.Count; ++i)
            {
                if (i == 0)
                {
                    yield return LogMessage(new CodeInstruction(OpCodes.Ldarg_0));
                    counter++;

                    yield return LogMessage(new CodeInstruction(OpCodes.Ldarg_0));
                    counter++;

                    yield return LogMessage(new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(StoreMethods), nameof(StoreMethods.SetupAugaStoreGui))));
                    counter++;

                    //skip next this;
                    i++;

                }

                yield return LogMessage(instrs[i]);
                counter++;
            }
        }

        // Epic Loot builds its trader panels in its own StoreGui.Show finalizer (StoreGui_Patch.OpenPanelFor):
        // TemperPanel at Hildir, MerchantPanel (secret stash, gamble, treasure maps, bounties) at Haldor, both
        // instantiated from its bundle under the StoreGui. Priority.Last runs this one after it. Both take Epic
        // Loot's woodpanel art and vanilla fonts (Epic Loot's own Auga branch never runs: its HasAuga is never set);
        // Restyle gives them the Auga panel, rows, buttons and fonts once (the frame's AugaCorner ornaments mark a
        // styled panel). MerchantPanel's Awake already filled its lists (AddComponent on an active object), so the
        // restyle covers the live rows and the inactive ItemElement templates later rows are cloned from.
        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Show))]
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        public static void Show_Finalizer(StoreGui __instance)
        {
            if (!__instance)
                return;
            RestyleOnce(__instance.transform.Find("TemperPanel"), "Frame/AugaCorner");
            RestyleOnce(__instance.transform.Find("MerchantPanel"), "AugaCorner");
        }

        private static void RestyleOnce(Transform panel, string styledMarker)
        {
            if (!panel || panel.Find(styledMarker))
                return;
            try
            {
                AugaStyle.Restyle(panel);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Auga] Epic Loot {panel.name} restyle failed: {e}");
            }
        }

        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.FillList))]
        [HarmonyPostfix]
        public static void FillList_Postfix(StoreGui __instance)
        {
            if (Auga.HasBetterTrader)
            {
                return;
            }

            var items = __instance.m_trader.GetAvailableItems();
            for (var index = 0; index < __instance.m_itemList.Count; index++)
            {
                var item = items[index];
                var itemElement = __instance.m_itemList[index];
                itemElement.GetComponent<ItemTooltip>().Item = item.m_prefab.m_itemData;
            }
        }
    }
}
