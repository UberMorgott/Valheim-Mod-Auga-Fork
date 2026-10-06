using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Auga
{
    // Vanilla panels no screen restyle reaches: each is its own object (or prefab instance) made after the screens
    // AugaSkin restyles (Menu, FejdStartup, Settings, Hud, InventoryGui), or lives outside them. Each gets
    // AugaStyle.RestyleAll once, when vanilla makes it alive; row templates are restyled once too, so later rows are
    // born Auga. Sources (Valheim 1.0.16 decompile):
    // - AchievementUnlockPopup.Start (instance of Achievements' unlockAchievementPopup, Achievements.cs:165)
    // - ConnectPanel.Start (F2; rows from m_playerElement, ConnectPanel.cs:88/166)
    // - Console.Awake (F5 console window, Console.cs:18)
    // - Feedback.Awake (m_feedbackPrefab instance under Menu/FejdStartup, Menu.cs:582, FejdStartup.cs:2978)
    // - EndCredits.Awake (m_creditsPanel, EndCredits.cs:19)
    // - JoinCode.Start (m_root, JoinCode.cs:51)
    // - Valheim.UI.SessionPlayerList.Awake (rows cloned from its own _ownPlayer child, SessionPlayerList.cs:46/241)
    // - ClosedCaptions.Awake (caption template m_captionPrefab, ClosedCaptions.cs:61)
    // - ManageSavesMenu.Awake (rows from saveElement, ManageSavesMenu.cs:87/657; the panel itself is FejdStartup's)
    // - Valheim.UI.RadialBase.Open (radial menu, elements built per open, RadialBase.cs:317)
    [HarmonyPatch]
    public static class VanillaPanels
    {
        private static readonly HashSet<int> Styled = new HashSet<int>();

        [UsedImplicitly]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var (type, method) in new[]
                     {
                         (typeof(AchievementUnlockPopup), "Start"), (typeof(ConnectPanel), "Start"), (typeof(Console), "Awake"),
                         (typeof(Feedback), "Awake"), (typeof(EndCredits), "Awake"), (typeof(JoinCode), "Start"),
                         (typeof(Valheim.UI.SessionPlayerList), "Awake"), (typeof(ClosedCaptions), "Awake"),
                         (typeof(ManageSavesMenu), "Awake"), (typeof(Valheim.UI.RadialBase), "Open"),
                     })
            {
                var m = AccessTools.DeclaredMethod(type, method);
                if (m != null)
                    yield return m;
                else
                    Debug.LogWarning($"[Auga] {type.Name}.{method} not found, that panel keeps the vanilla look");
            }
        }

        [UsedImplicitly]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MonoBehaviour __instance)
        {
            if (!__instance)
                return;
            switch (__instance)
            {
                case ConnectPanel panel:
                    Once(panel.m_playerElement ? panel.m_playerElement.transform : null);
                    break;
                case ClosedCaptions captions:
                    Once(captions.m_captionPrefab ? captions.m_captionPrefab.transform : null);
                    break;
                case ManageSavesMenu saves:
                    Once(saves.saveElement ? saves.saveElement.transform : null);
                    break;
                case EndCredits credits:
                    Once(credits.m_creditsPanel ? credits.m_creditsPanel.transform : null);
                    return;
                case Valheim.UI.RadialBase radial:
                    // Elements are rebuilt per open: fonts and art of the current ones.
                    Restyle(radial.transform);
                    return;
            }
            Once(__instance.transform);
        }

        private static void Once(Transform root)
        {
            if (root && Styled.Add(root.GetInstanceID()))
                Restyle(root);
        }

        private static void Restyle(Transform root)
        {
            try
            {
                AugaStyle.RestyleAll(root);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Auga] {root.name} restyle failed: {e}");
            }
        }
    }
}
