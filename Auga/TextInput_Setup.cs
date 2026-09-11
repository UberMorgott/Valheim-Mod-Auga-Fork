using HarmonyLib;
using JetBrains.Annotations;

namespace Auga
{
    // Sign/portal text dialog (spec 2026-09-11-auga-native-rework, Phase 4, option A): the vanilla TextInput keeps its
    // panel, GuiInputField and fields (TextInput.cs:9-13, Show :90-97). The old AugaTextInput bundle replacement
    // predates the 1.0.x GUIFramework.GuiInputField, so TextInput.Show threw on m_inputField and no text could be entered.
    [HarmonyPatch(typeof(TextInput), nameof(TextInput.Awake))]
    public static class TextInput_Setup
    {
        [UsedImplicitly]
        public static void Postfix(TextInput __instance) => AugaStyle.Restyle(__instance.transform);
    }
}
