using HarmonyLib;
using UnityEngine.EventSystems;

namespace Auga
{
    // Auga's bundle sets ButtonSfx.m_selectSfxPrefab on ~130 buttons (vanilla: 2 of 59). A mouse click
    // selects on pointer-down (select sfx) and clicks on pointer-up (click sfx) several frames later,
    // past SfxTimer's 2-frame dedupe, so one click sounds like a double click. Keep the select sound
    // for keyboard/gamepad navigation only.
    [HarmonyPatch(typeof(ButtonSfx), nameof(ButtonSfx.OnSelect))]
    public static class ButtonSfx_OnSelect_Patch
    {
        public static bool Prefix(BaseEventData eventData) => !(eventData is PointerEventData);
    }
}
