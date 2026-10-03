using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Auga
{
    // Server password and connecting dialogs. In 1.0.x each vanilla dialog is a root canvas of its own under
    // _GameMain/LoadingGUI/PixelFix (scene bundle 17245031: Password order 3000, Connecting order 3100), which has no
    // Canvas. The Auga prefabs carry none, so unwrapped they never rendered: joining a password server showed only the
    // black loading screen and the server timed out the handshake (ZNet.RPC_ClientHandshake, ZNet.cs:908-924).
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    public static class ZNet_Awake_Patch
    {
        [UsedImplicitly]
        public static void Postfix(ZNet __instance)
        {
            __instance.m_passwordDialog = Replace(__instance.m_passwordDialog, Auga.Assets.PasswordDialog);
            __instance.m_connectingDialog = Replace(__instance.m_connectingDialog, Auga.Assets.ConnectingDialog);
        }

        private static RectTransform Replace(RectTransform vanilla, GameObject prefab)
        {
            var parent = vanilla.parent;
            var replacement = Object.Instantiate(prefab, parent, false);
            replacement.SetActive(false);
            replacement.transform.SetSiblingIndex(vanilla.GetSiblingIndex());
            SetupHelper.WrapInRootCanvasOf(replacement.transform, vanilla.gameObject);
            Object.Destroy(vanilla.gameObject);
            return replacement.GetComponent<RectTransform>();
        }
    }
}
