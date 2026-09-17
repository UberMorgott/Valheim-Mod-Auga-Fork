using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AugaUnity
{
    public class CraftingRequirementsPanel : MonoBehaviour
    {
        public GameObject[] RequirementList = new GameObject[0];
        public Image Icon;
        public Image UpgradedIcon;
        public Image WorkbenchIcon;
        public TMP_Text WorkbenchLevel;
        public TMP_Text OriginalQualityLevel;
        public TMP_Text NewQualityLevel;
        public TMP_Text ItemCraftType;
        public UpgradeRequirementsWireFrame WireFrame;

        public void Activate(InventoryGui inventoryGui, ComplexTooltip itemInfo)
        {
            inventoryGui.m_recipeRequirementList = RequirementList;
            itemInfo.Icon = Icon;
            inventoryGui.m_minStationLevelBasecolor = new Color32(0xEA, 0xE1, 0xD9, 0xFF);
            inventoryGui.m_minStationLevelText = WorkbenchLevel;
            inventoryGui.m_itemCraftType = ItemCraftType;
            Update();
        }

        public void Update()
        {
            var inventoryGui = InventoryGui.instance;

            if (UpgradedIcon != null && Icon != null)
            {
                UpgradedIcon.enabled = Icon.enabled;
                UpgradedIcon.sprite = Icon.sprite;
            }

            if (Player.m_localPlayer != null)
            {
                var workbench = Player.m_localPlayer.GetCurrentCraftingStation();

                if (WorkbenchIcon != null)
                {
                    WorkbenchIcon.enabled = workbench != null;
                    if (workbench != null)
                    {
                        WorkbenchIcon.sprite = workbench.m_icon;
                    }
                }

                var itemData = inventoryGui.m_selectedRecipe.ItemData;
                if (itemData != null)
                {
                    if (OriginalQualityLevel != null)
                    {
                        OriginalQualityLevel.text = itemData.m_quality.ToString();
                    }

                    if (NewQualityLevel != null)
                    {
                        NewQualityLevel.text = (itemData.m_quality + 1).ToString();
                    }
                }
            }
        }
    }
}
