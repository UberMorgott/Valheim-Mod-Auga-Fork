using System;
using System.Collections.Generic;
using System.Reflection;
using AugaUnity;
using HarmonyLib;
using TMPro;
using UnityEngine.UI;

namespace Auga
{
    // Vanilla Settings window (pause menu Menu.cs:411-416 and main menu FejdStartup.cs:2960-2964), restyled in
    // place by the shared AugaStyle map. Tabs, saving and layout stay vanilla (Settings.cs:50-56, 89-99). Awake
    // runs inside Instantiate, so the window is restyled before its first frame.
    [HarmonyPatch(typeof(Settings), nameof(Settings.Awake))]
    public static class Settings_Awake_Patch
    {
        public static void Postfix(Settings __instance) => AugaStyle.Restyle(__instance.transform);
    }

    // AugaBindingDisplay.SetBinding: оригинальный метод обращается к
    // ZInput.instance.m_buttons (private в Valheim 0.221) → FieldAccessException → SetText
    // никогда не вызывается → текст остаётся "W" (дефолт из префаба).
    //
    // Решение: полностью заменяем SetBinding через Prefix (return false всегда).
    // Вся логика повторена с рефлексией для m_buttons + прямым вызовом GetBoundKeyString
    // (публичный метод — обращается к m_buttons изнутри, через свой доступ).
    [HarmonyPatch(typeof(AugaBindingDisplay), nameof(AugaBindingDisplay.SetBinding))]
    public static class AugaBindingDisplay_SetBinding_Patch
    {
        private static readonly FieldInfo s_buttonsField =
            typeof(ZInput).GetField("m_buttons", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool Prefix(AugaBindingDisplay __instance, string keyName)
        {
            try
            {
                if (ZInput.instance == null) { __instance.SetText("?"); return false; }

                // m_buttons приватный — читаем через рефлексию
                var buttons = s_buttonsField?.GetValue(ZInput.instance)
                    as Dictionary<string, ZInput.ButtonDef>;

                if (buttons == null || !buttons.ContainsKey(keyName))
                {
                    __instance.SetText("?");
                    return false;
                }

                // GetBoundKeyString — публичный метод, обращается к m_buttons через
                // собственный доступ класса (не вызывает FieldAccessException).
                var key = Localization.instance.GetBoundKeyString(keyName);

                // Обнаружение кнопок мыши
                var showMouse = -1;
                if (key == "Mouse0" || key == "LMB") showMouse = 0;
                else if (key == "Mouse1" || key == "RMB") showMouse = 1;
                else if (key == "Mouse2" || key == "MMB") showMouse = 2;
                else if (key == "Mouse3") showMouse = 3;
                else if (key == "Mouse4") showMouse = 4;
                else if (key == "Mouse5") showMouse = 5;
                else if (key == "Mouse6") showMouse = 6;

                // Нормализация строк (из оригинального SetBinding)
                switch (key)
                {
                    case "Equals": key = "="; break;
                    case "BackQuote": key = "`"; break;
                }

                if (key.StartsWith("Keypad"))
                {
                    key = key.Replace("Keypad", "Num")
                             .Replace("Divide", "/")
                             .Replace("Minus", "-")
                             .Replace("Multiply", "*")
                             .Replace("Equals", "=")
                             .Replace("Period", ".")
                             .Replace("Plus", "+");
                }
                else if (key.StartsWith("Alpha"))
                {
                    key = key.Replace("Alpha", "");
                }
                else
                {
                    switch (key)
                    {
                        case "LeftArrow": key = "←"; break;
                        case "RightArrow": key = "→"; break;
                        case "UpArrow": key = "↑"; break;
                        case "DownArrow": key = "↓"; break;
                    }
                }

                __instance.SetText(key, showMouse);
            }
            catch (Exception ex)
            {
                Auga.LogWarning($"[AugaBindingDisplay] SetBinding '{keyName}' failed: {ex.Message}");
                try { __instance.SetText("?"); } catch { }
            }
            return false; // всегда пропускаем оригинальный SetBinding
        }
    }
}
