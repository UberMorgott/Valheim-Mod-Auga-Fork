using System.Globalization;
using AugaUnity;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Auga
{
    [HarmonyPatch]
    public static class InventoryPanel_Patches
    {
        public static AugaCraftingPanel CraftingPanel;
        public static Transform TopRowInventory;
        public static Transform MainRowsInventory;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
        public static class InventoryGui_Awake_Patch
        {
            [HarmonyPriority(Priority.First)]
            public static void Postfix(InventoryGui __instance)
            {
                Debug.LogWarning($"Starting Auga InventoryGui.Postfix");
                AddItemIconMaterial.IconMaterial = __instance.m_dragItemPrefab.transform.Find("icon").GetComponent<Image>().material;

                __instance.m_playerGrid.m_onSelected = null;
                __instance.m_playerGrid.m_onRightClick = null;
                __instance.m_containerGrid.m_onSelected = null;
                __instance.m_containerGrid.m_onRightClick = null;

                // 1.0.7 nests the container panel under root/Player; lift it to root so replacing
                // Player does not destroy it and root/Container below still finds it.
                __instance.m_container.SetParent(__instance.m_player.parent, false);
                var playerInventory = __instance.Replace("root/Player", Auga.Assets.InventoryScreen, "root/Player");
                __instance.m_player = playerInventory.RectTransform();
                __instance.m_playerGrid = playerInventory.Find("PlayerGrid").GetComponent<InventoryGrid>();
                __instance.m_playerGrid.m_onSelected += __instance.OnSelectedItem;
                __instance.m_playerGrid.m_onRightClick += __instance.OnRightClickItem;
                __instance.m_weight = playerInventory.Find("Weight/Text").GetComponent<TMP_Text>();
                __instance.m_armor = playerInventory.Find("Armor/Text").GetComponent<TMP_Text>();

                var containerInventory = __instance.Replace("root/Container", Auga.Assets.InventoryScreen, "root/Container");
                __instance.m_container = containerInventory.RectTransform();
                __instance.m_containerName = containerInventory.Find("ContainerHeader/Name").GetComponent<TMP_Text>();
                __instance.m_containerGrid = containerInventory.Find("ContainerGrid").GetComponent<InventoryGrid>();
                __instance.m_containerGrid.m_onSelected += __instance.OnSelectedItem;
                __instance.m_containerGrid.m_onRightClick += __instance.OnRightClickItem;
                // 1.0.7 InventoryGui.Awake wires these on the vanilla grids only; UpdateGui calls
                // CanDropDragOntoItem unchecked, so an unwired grid NREs after the first item.
                foreach (var grid in new[] { __instance.m_playerGrid, __instance.m_containerGrid })
                {
                    grid.m_onReleased += __instance.OnReleasedItem;
                    grid.m_onEnter += __instance.OnEnterElement;
                    grid.OnSetTouchSelection += __instance.SetTouchSelection;
                    grid.CanDropDragOntoItem += __instance.CanDropDragOntoItem;
                }
                __instance.m_playerGrid.OnMoveToLowerInventoryGrid += __instance.MoveToLowerInventoryGrid;
                __instance.m_containerGrid.OnMoveToUpperInventoryGrid += __instance.MoveToUpperInventoryGrid;
                __instance.m_containerWeight = containerInventory.Find("Weight/Text").GetComponent<TMP_Text>();
                __instance.m_takeAllButton = containerInventory.Find("TakeAll").GetComponent<ColorButtonText>();
                __instance.m_takeAllButton.onClick.AddListener(__instance.OnTakeAll);
                __instance.m_stackAllButton = containerInventory.Find("StackAll").GetComponent<ColorButtonText>();
                __instance.m_stackAllButton.onClick.AddListener(__instance.OnStackAll);
                
                var oldCraftingPanel = __instance.transform.Find("root/Crafting");
                var craftingPanelSiblingIndex = oldCraftingPanel.GetSiblingIndex();
                oldCraftingPanel.gameObject.SetActive(false);
                Object.Destroy(oldCraftingPanel.gameObject);

                var variantDialog = __instance.Replace("root/VariantDialog", Auga.Assets.InventoryScreen, "root/DummyObjects/DummyVariantDialog");
                __instance.m_variantDialog = variantDialog.GetComponent<VariantDialog>();

                var skillsDialog = __instance.Replace("root/Skills", Auga.Assets.InventoryScreen, "root/RightPanel/TabContent/TabContent_Skills");
                __instance.m_skillsDialog = skillsDialog.GetComponent<SkillsDialog>();
                var dummyContainer = new GameObject("DummyDialogs", typeof(RectTransform));
                dummyContainer.transform.SetParent(skillsDialog.parent);
                variantDialog.SetParent(dummyContainer.transform);
                skillsDialog.SetParent(dummyContainer.transform);
                dummyContainer.SetActive(false);

                var rightPanel = Object.Instantiate(Auga.Assets.InventoryScreen.transform.Find("root/RightPanel"), containerInventory.parent, false);
                Debug.LogWarning($"API AAA is null: {API.GetCraftingControls().Amount == null}");
                Debug.LogWarning($"API InputAmount is null: {API.GetCraftingControls().InputAmount == null}");
                Debug.LogWarning($"API InputAmount is null: {API.GetCraftingControls().CraftButton == null}");
                rightPanel.gameObject.name = "RightPanel";
                rightPanel.SetSiblingIndex(craftingPanelSiblingIndex);
                CraftingPanel = rightPanel.GetComponentInChildren<AugaCraftingPanel>(true);
                CraftingPanel.SetMultiCraftEnabled(Auga.HasMultiCraft);
                __instance.m_playerName = rightPanel.Find("DefaultContent/TitleContainer/PlayerPanelTitle").GetComponent<TMP_Text>();
                __instance.m_pvp = rightPanel.Find("TabContent/TabContent_PVP/Dummy/PVPToggle").GetComponent<Toggle>();
                __instance.m_recipeElementPrefab = CraftingPanel.RecipeItemPrefab;
                __instance.m_recipeListRoot = CraftingPanel.RecipeList;
                __instance.m_recipeListScroll = CraftingPanel.RecipeListScrollbar;
                __instance.m_recipeEnsureVisible = CraftingPanel.RecipeListEnsureVisible;
                __instance.m_recipeListSpace = 34;
                __instance.m_craftingStationName = CraftingPanel.WorkbenchName;
                __instance.m_craftingStationIcon = CraftingPanel.WorkbenchIcon;
                __instance.m_craftingStationLevelRoot = CraftingPanel.WorkbenchLevelRoot;
                __instance.m_craftingStationLevel = CraftingPanel.WorkbenchLevel;
                __instance.m_craftButton = CraftingPanel.CraftButton;
                __instance.m_craftButton.onClick.AddListener(__instance.OnCraftPressed);
                __instance.m_craftCancelButton = CraftingPanel.CraftCancelButton;
                __instance.m_craftCancelButton.onClick.AddListener(__instance.OnCraftCancelPressed);
                __instance.m_craftProgressPanel = CraftingPanel.CraftProgressPanel;
                __instance.m_variantButton = CraftingPanel.VariantButton;
                __instance.m_variantButton.onClick.AddListener(__instance.OnShowVariantSelection);
                __instance.m_variantDialog = CraftingPanel.VariantDialog;
                __instance.m_variantDialog.m_selected += __instance.OnVariantSelected;
                __instance.m_repairButton = CraftingPanel.DefaultRepairButton;
                __instance.m_repairButtonGlow = CraftingPanel.DefaultRepairGlow;
                __instance.m_repairPanel = CraftingPanel.DefaultRepairButton.transform;
                __instance.m_repairButton.onClick.AddListener(__instance.OnRepairPressed);

                __instance.m_recipeIcon = CraftingPanel.DummyIcon;
                __instance.m_recipeName = CraftingPanel.DummyName;
                __instance.m_recipeDecription = CraftingPanel.DummyDescription;
                __instance.m_repairPanelSelection = CraftingPanel.DummyRepairPanelSelection;
                __instance.m_tabCraft = CraftingPanel.DummyCraftTabButton;
                __instance.m_tabUpgrade = CraftingPanel.DummyUpgradeTabButton;
                __instance.m_craftProgressBar = CraftingPanel.DummyCraftProgressBar;
                __instance.m_qualityPanel = CraftingPanel.DummyQualityPanel;
                __instance.m_minStationLevelIcon = CraftingPanel.DummyMinStationLevelIcon;
                CraftingPanel.Initialize(__instance);

                // 1.0.7: vanilla root/Info holds the only entry points to Texts, Trophies and the new
                // Achievements panel (persistent listeners on the live vanilla InventoryGui).
                // Auga has no achievements equivalent, so pass the vanilla Info panel through.
                var vanillaInfo = __instance.transform.Find("root/Info");
                if (vanillaInfo == null)
                    Debug.LogWarning("[Auga] InventoryGui: vanilla root/Info not found; trophies/achievements unreachable");
                else
                {
                    var entries = new System.Collections.Generic.List<string>();
                    foreach (var b in vanillaInfo.GetComponentsInChildren<Button>(true))
                        entries.Add(b.onClick.GetPersistentEventCount() > 0 ? $"{b.name}->{b.onClick.GetPersistentMethodName(0)}" : b.name);
                    Debug.Log($"[Auga] InventoryGui: kept vanilla Info panel, buttons: {string.Join(", ", entries)}");
                }

                var splitDialog = __instance.Replace("root/SplitDialog", Auga.Assets.InventoryScreen, "root/SplitDialog");
                // 1.0.7: split UI is a SplitDialog component; it wires its own listeners in OnEnable.
                // Deactivate before AddComponent so OnEnable does not run with unset fields.
                splitDialog.gameObject.SetActive(false);
                var sd = splitDialog.GetComponent<SplitDialog>();
                if (!sd) sd = splitDialog.gameObject.AddComponent<SplitDialog>();
                sd.m_splitSlider = splitDialog.Find("Dialog/Slider").GetComponent<Slider>();
                sd.m_splitAmount = splitDialog.Find("Dialog/InventoryElement/amount").GetComponent<TMP_Text>();
                sd.m_splitCancelButton = splitDialog.Find("Dialog/ButtonCancel").GetComponent<Button>();
                sd.m_splitOkButton = splitDialog.Find("Dialog/ButtonOk").GetComponent<Button>();
                sd.m_splitIcon = splitDialog.Find("Dialog/InventoryElement/icon").GetComponent<Image>();
                sd.m_splitIconName = splitDialog.Find("Dialog/InventoryElement/DummyText").GetComponent<TMP_Text>();
                sd.m_panel = splitDialog.Find("Dialog") as RectTransform;
                // OnEnable reads these for normal/touch placement; keep Auga's layout for both.
                sd.m_panelNormalPosition = sd.m_panel;
                sd.m_panelTouchPosition = sd.m_panel;
                __instance.m_splitDialog = sd;

                // 1.0.7 indexes m_uiGroups by position: [2] = Info (OnOpenSkills/Texts/Trophies, OnPvpChanged),
                // [3] = crafting (OnSelectedRecipe, OnCraftPressed, tabs; UpdateGamepad runs recipe input at 3).
                // Always length 4: a dummy stands in for a missing Info so [3] stays the crafting panel.
                var infoGroup = vanillaInfo ? vanillaInfo.GetComponent<UIGroupHandler>() : null;
                if (!infoGroup)
                {
                    Debug.LogWarning("[Auga] InventoryGui: no Info UIGroupHandler; using a dummy group at index 2");
                    var dummy = new GameObject("AugaInfoGroupDummy");
                    dummy.transform.SetParent(__instance.transform, false);
                    infoGroup = dummy.AddComponent<UIGroupHandler>();
                }
                __instance.m_uiGroups = new[] {
                    containerInventory.GetComponent<UIGroupHandler>(),
                    playerInventory.GetComponent<UIGroupHandler>(),
                    infoGroup,
                    rightPanel.GetComponent<UIGroupHandler>()
                };
                // Vanilla m_crafting pointed at the destroyed root/Crafting; SetRecipe compares it to
                // m_uiGroups[ActiveGroup].transform for gamepad rumble.
                __instance.m_crafting = (RectTransform)rightPanel;

                var animator = __instance.GetComponent<Animator>();
                var newAnimator = Auga.Assets.InventoryScreen.GetComponent<Animator>();
                animator.runtimeAnimatorController = newAnimator.runtimeAnimatorController;
                animator.Rebind();

                var standardDivider = playerInventory.Find("StandardDivider");
                var trashDivider = playerInventory.Find("TrashDivider");
                standardDivider.gameObject.SetActive(!Auga.UseAugaTrash.Value);
                trashDivider.gameObject.SetActive(Auga.UseAugaTrash.Value);

                Localization.instance.Localize(__instance.transform);
                SetupHelper.LogDeadRefsNextFrame(__instance);
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        public static class InventoryGrid_UpdateGui_Patch
        {
            private static bool _layoutLogged;

            // Runs after EAQS's postfix, which pulls its extra rows out of the grid.
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(InventoryGrid __instance)
            {
                try { Body(__instance); }
                finally { if (__instance.name == "PlayerGrid") FitPlayerPanel(__instance); }
            }

            // The bundle's Main viewport is sized for exactly 3 rows (224px); if the rows left in
            // Main/Grid need more, grow the Player panel instead of showing a scrollbar.
            private static void FitPlayerPanel(InventoryGrid grid)
            {
                var gridRect = MainRowsInventory as RectTransform;
                var viewport = gridRect ? gridRect.parent as RectTransform : null;
                var player = InventoryGui.instance ? InventoryGui.instance.m_player : null;
                if (!viewport || !player) return;

                var content = LayoutUtility.GetPreferredHeight(gridRect);
                var overflow = content - viewport.rect.height;
                if (overflow > 0.5f)
                    player.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, player.rect.height + overflow);

                if (_layoutLogged || !player.gameObject.activeInHierarchy || content <= 0) return;
                _layoutLogged = true;
                var cells = 0;
                foreach (Transform c in gridRect) if (c.gameObject.activeSelf) cells++;
                Debug.Log($"[Auga] PlayerGrid: {cells} main cells, content {content:0}px, viewport {viewport.rect.height:0}px, player panel {player.rect.size}, grew {Mathf.Max(0, overflow):0}px");
                // EAQS (#5 slot alignment): its panel and slot root, in m_player space.
                var eaqsPanel = player.Find("EAQS") as RectTransform;
                var slotRoot = player.Find("EaqsSlotRoot") as RectTransform;
                if (eaqsPanel)
                    Debug.Log($"[Auga] EAQS panel: pos {eaqsPanel.localPosition} pivot {eaqsPanel.pivot} size {eaqsPanel.rect.size}");
                if (slotRoot)
                {
                    Debug.Log($"[Auga] EAQS slotRoot: pos {slotRoot.localPosition} pivot {slotRoot.pivot} size {slotRoot.rect.size} gridRoot pivot {grid.m_gridRoot.pivot}");
                    foreach (RectTransform cell in slotRoot)
                        Debug.Log($"[Auga] EAQS cell {cell.name}: anchored {cell.anchoredPosition} inPlayer {player.InverseTransformPoint(cell.position)} pivot {cell.pivot}");
                }
            }

            private static void Body(InventoryGrid __instance)
            {
                if (__instance.name == "PlayerGrid")
                {
                    if (TopRowInventory == null)
                    {
                        TopRowInventory = __instance.transform.Find("Top");
                        MainRowsInventory = __instance.transform.Find("Main/Grid");
                    }
                }

                //Vector2 startPos = new Vector2(__instance.RectTransform().rect.width / 2f, 0.0f) - new Vector2(__instance.GetWidgetSize().x, 0.0f) * 0.5f;
                foreach (var element in __instance.m_elements)
                {
                    var itemTooltip = element.gameObject.GetComponent<ItemTooltip>();
                    
                    var item = __instance.m_inventory.GetItemAt(element.Position.x, element.Position.y);
                    
                    if (itemTooltip != null && !element.m_used)
                    {
                        itemTooltip.Item = null;
                    }

                    if (element.m_used && itemTooltip != null)
                    {
                        itemTooltip.Item = item;
                    }

                    // Move fresh elements only: other mods (EAQS slot cells) reparent their own.
                    if (__instance.name == "PlayerGrid" && element.transform.parent == __instance.m_gridRoot)
                    {
                        if (element.Position.y == 0)
                        {
                            element.gameObject.transform.SetParent(TopRowInventory);
                        }
                        else
                        {
                            element.gameObject.transform.SetParent(MainRowsInventory);
                            //Vector2 currentPosition = new Vector3(element.Position.x * (__instance.m_elementSpace), (element.Position.y * -__instance.m_elementSpace) - 26);
                            //element.gameObject.RectTransform().anchoredPosition = startPos + currentPosition;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public static class InventoryGui_Show_Patch
        {
            public static void Postfix(InventoryGui __instance)
            {
                var player = Player.m_localPlayer;
                if (player != null)
                {
                    __instance.UpdateContainer(player);
                }
            }
        }

        //CreateItemTooltip
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
        public static class InventoryGrid_CreateItemTooltip_Patch
        {
            public static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, UITooltip tooltip)
            {
                var itemTooltip = tooltip.GetComponent<ItemTooltip>();
                if (itemTooltip != null)
                {
                    itemTooltip.Item = item;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetRecipe))]
        public static class InventoryGui_SetRecipe_Patch
        {
            public static void Postfix(InventoryGui __instance)
            {
                if (CraftingPanel != null)
                {
                    CraftingPanel.SetRecipe(__instance.m_selectedRecipe.Recipe, __instance.m_selectedRecipe.ItemData, __instance.m_selectedVariant);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
        public static class InventoryGui_UpdateRecipe_Patch
        {
            public static void Postfix(InventoryGui __instance)
            {
                if (CraftingPanel != null)
                {
                    CraftingPanel.OnUpdateRecipe(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnVariantSelected))]
        public static class InventoryGui_OnVariantSelected_Patch
        {
            public static void Postfix(InventoryGui __instance)
            {
                if (CraftingPanel != null)
                {
                    CraftingPanel.SetRecipe(__instance.m_selectedRecipe.Recipe, __instance.m_selectedRecipe.ItemData, __instance.m_selectedVariant);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirementList))]
        public static class InventoryGui_SetupRequirementList_Patch
        {
            public static void Postfix(InventoryGui __instance, int quality, Player player, bool allowedQuality)
            {
                if (CraftingPanel != null)
                {
                    CraftingPanel.PostSetupRequirementList(__instance.m_selectedRecipe.Recipe, __instance.m_selectedRecipe.ItemData, quality, player, allowedQuality);
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateCharacterStats))]
        public static class InventoryGui_UpdateCharacterStats_Patch
        {
            public static bool Prefix(InventoryGui __instance, Player player)
            {
                __instance.m_playerName.text = Game.instance.GetPlayerProfile().GetName();
                __instance.m_armor.text = player.GetBodyArmor().ToString(CultureInfo.InvariantCulture);
                return false;
            }
        }

        [HarmonyPatch(typeof(VariantDialog), nameof(VariantDialog.Setup))]
        public static class VariantDialog_Setup_Patch
        {
            public static void Postfix(VariantDialog __instance)
            {
                for (var index = 0; index < __instance.m_elements.Count; index++)
                {
                    var variantElement = __instance.m_elements[index];
                    var selected = index == InventoryGui.instance.m_selectedVariant;

                    var selectedObject = variantElement.transform.Find("selected");
                    if (selectedObject != null)
                    {
                        selectedObject.gameObject.SetActive(selected);
                    }
                }
            }
        }
    }
}
