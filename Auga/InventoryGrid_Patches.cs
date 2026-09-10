using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga;

public static class InventoryGrid_Patches
{
  // Auga's slot prefab predates the 1.0.7 InventoryElement component. Prepare it once so vanilla
  // InventoryGrid.UpdateGui (centering, slot numbers, selection, drag, drop focus) runs unchanged.
  [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
  public static class UpdateGuiPatch
  {
    public static void Prefix(InventoryGrid __instance)
    {
      var prefab = __instance.m_elementPrefab;
      if (prefab == null || prefab.GetComponent<InventoryElement>() != null)
        return;

      var t = prefab.transform;
      // Vanilla dereferences UIDragHandler unchecked (InventoryGrid.cs:283).
      if (prefab.GetComponentInChildren<UIDragHandler>(true) == null)
        prefab.AddComponent<UIDragHandler>();

      var button = prefab.GetComponentInChildren<Button>(true);
      if (button == null)
      {
        button = prefab.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
      }

      // Dedicated drop-focus overlay (touch only); must not be the slot background.
      var dropFocusGo = new GameObject("dropFocus", typeof(RectTransform), typeof(Image));
      var dropFocusRect = (RectTransform)dropFocusGo.transform;
      dropFocusRect.SetParent(t, false);
      dropFocusRect.anchorMin = Vector2.zero;
      dropFocusRect.anchorMax = Vector2.one;
      dropFocusRect.offsetMin = dropFocusRect.offsetMax = Vector2.zero;
      var dropFocus = dropFocusGo.GetComponent<Image>();
      dropFocus.raycastTarget = false;
      dropFocus.color = new Color(1f, 1f, 1f, 0.25f);

      var el = prefab.AddComponent<InventoryElement>();
      el.m_button = button;
      el.m_touchRect = t as RectTransform;
      el.m_dropFocus = dropFocus;
      el.m_touchHighlightColor = button.colors.highlightedColor;
      el.m_icon = t.Find("icon").GetComponent<Image>();
      el.m_amount = t.Find("amount").GetComponent<TMP_Text>();
      el.m_quality = t.Find("quality").GetComponent<TMP_Text>();
      el.m_equiped = t.Find("equiped").GetComponent<Image>();
      el.m_queued = t.Find("queued").GetComponent<Image>();
      el.m_noteleport = t.Find("noteleport").GetComponent<Image>();
      el.m_food = t.Find("foodicon").GetComponent<Image>();
      el.m_selected = t.Find("selected").gameObject;
      el.m_tooltip = prefab.GetComponent<UITooltip>();
      el.m_durability = t.Find("durability").GetComponent<GuiBar>();
    }
  }
}
