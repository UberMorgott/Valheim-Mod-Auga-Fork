// Copyright (c) 2026 Morgott
// Licensed under CC BY-NC 4.0, see CC-BY-NC-4.0.txt

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Restyle of the EquipmentAndQuickSlots (EAQS 3.1.3) extra-slot panel on EAQS's vanilla path (class EquipmentPanel;
    // AugaSkin has no `Auga` assembly, so EAQS never takes its Auga path). EAQS keeps owning the cells: its
    // InventoryGrid.UpdateGui postfix (UpdateInventorySlots) moves them into EaqsSlotRoot, places them on a staggered
    // 80 px paperdoll and labels them with the slot name; its InventoryGui.Update postfix (UpdateEquipmentBackground)
    // sizes the three backgrounds for 74 px. Running after both, this class only:
    // - re-places the active cells on the vanilla player grid pitch (InventoryGrid.m_elementSpace, the pitch
    //   InventoryGrid.UpdateGui lays the grid out with, InventoryGrid.cs:273) in straight rows and columns;
    // - fits each background around its cells with the vanilla player-grid background padding;
    // - disables the name label on equipment and custom cells like vanilla disables the binding text of non-hotbar
    //   cells (InventoryGrid.cs:293); quick slots keep their hotkey text;
    // - draws a faint silhouette of the expected item type (embedded icon) in empty equipment cells.
    // EAQS is a soft dependency: absent or changed, nothing here runs.
    public static class EaqsSlotPanel
    {
        private const string EaqsGuid = "randyknapp.mods.equipmentandquickslots";

        // EAQS EquipmentPanel constants: top-left corner of its equipment background relative to PanelBase
        // (equipmentBackgroundCenter (132.5,-159) - equipmentBackgroundSize (210,300) / 2), where every background now
        // starts, and the gap between two backgrounds (rowGap 20). Custom slots keep EAQS's 3 per row.
        private static readonly Vector2 BackgroundTopLeft = new Vector2(27.5f, -9f);
        private const float GroupGap = 20f;
        private const int CustomPerRow = 3;
        private const int EquipmentPerColumn = 3;

        private const int Equipment = 0, Quick = 1, Custom = 2;
        private static readonly string[] Backgrounds = { "EaqsEquipmentBkg", "EaqsQuickSlotBkg", "EaqsCustomSlotBkg" };

        // Embedded silhouette icon (Assets/SlotIcons, white on transparent) per EAQS equipment slot ID (Slots.cs:987-994).
        private static readonly Dictionary<string, string> SilhouetteIcons = new Dictionary<string, string>
        {
            { "Helmet", "helmet" },
            { "Chest", "chest" },
            { "Legs", "legs" },
            { "Shoulder", "cape" },
            { "Utility", "utility" },
            { "Utility2", "utility" },
            { "Utility3", "utility" },
            { "Trinket", "trinket" },
        };

        private const string SilhouetteName = "AugaSlotSilhouette";
        // Faint white: reads as an engraving on the dark cell, clearly empty.
        private static readonly Color SilhouetteColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Dictionary<string, Sprite> SilhouetteSprites = new Dictionary<string, Sprite>();

        private static bool? _available;
        private static FieldInfo _slots;
        private static PropertyInfo _inventorySizeVisible, _panelBase, _id, _index, _isActive, _isQuick, _isEquipment, _isCustom;

        private static readonly List<RectTransform>[] GroupCells = { new List<RectTransform>(), new List<RectTransform>(), new List<RectTransform>() };
        private static InventoryGui _measuredFor;
        private static Vector2 _padding;

        private static bool Available => _available ?? (_available = Chainloader.PluginInfos.ContainsKey(EaqsGuid) && Resolve()).Value;

        private static bool Resolve()
        {
            var slotsType = AccessTools.TypeByName("EquipmentAndQuickSlots.Slots");
            var panelType = AccessTools.TypeByName("EquipmentAndQuickSlots.EquipmentPanel");
            _slots = slotsType == null ? null : AccessTools.Field(slotsType, "slots");
            var slotType = _slots?.FieldType.GetElementType();
            if (slotType == null || panelType == null)
                return Missing();

            _inventorySizeVisible = AccessTools.Property(slotsType, "InventorySizeVisible");
            _panelBase = AccessTools.Property(panelType, "PanelBase");
            _id = AccessTools.Property(slotType, "ID");
            _index = AccessTools.Property(slotType, "Index");
            _isActive = AccessTools.Property(slotType, "IsActive");
            _isQuick = AccessTools.Property(slotType, "IsQuickSlot");
            _isEquipment = AccessTools.Property(slotType, "IsEquipmentSlot");
            _isCustom = AccessTools.Property(slotType, "IsCustomSlot");
            foreach (var member in new[] { _inventorySizeVisible, _panelBase, _id, _index, _isActive, _isQuick, _isEquipment, _isCustom })
                if (member == null)
                    return Missing();
            return true;
        }

        private static bool Missing()
        {
            Auga.LogWarning("EAQS found, but its slot layout members changed; EAQS slot panel restyle is off");
            return false;
        }

        // Mirrors the loop of EAQS UpdateInventorySlots: slot i owns m_elements[InventorySizeVisible + i].
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        public static class InventoryGrid_UpdateGui_Patch
        {
            [UsedImplicitly]
            [HarmonyAfter(EaqsGuid)]
            public static void Postfix(InventoryGrid __instance)
            {
                var gui = InventoryGui.instance;
                if (!gui || __instance != gui.m_playerGrid || !Player.m_localPlayer || !Available)
                    return;

                MeasurePadding(gui);
                foreach (var cells in GroupCells)
                    cells.Clear();

                var slots = (Array)_slots.GetValue(null);
                var first = (int)_inventorySizeVisible.GetValue(null);
                var panelBase = (Vector2)_panelBase.GetValue(null);
                var pitch = __instance.m_elementSpace;
                var count = Math.Min(slots.Length, __instance.m_elements.Count - first);

                var customOrder = 0;
                var placed = new List<(InventoryElement Element, int Group, int Column, int Row)>();
                for (var i = 0; i < count; i++)
                {
                    var slot = slots.GetValue(i);
                    var element = __instance.m_elements[first + i];
                    if (!element || !(bool)_isActive.GetValue(slot))
                        continue;

                    var index = (int)_index.GetValue(slot);
                    if ((bool)_isEquipment.GetValue(slot))
                    {
                        // EAQS columns: Head/Chest/Legs, Shoulders/Utility/Trinket, extra Utility 2/3 (Index 8..15).
                        placed.Add((element, Equipment, (index - 8) / EquipmentPerColumn, (index - 8) % EquipmentPerColumn));
                        UpdateSilhouette(element, (string)_id.GetValue(slot));
                    }
                    else if ((bool)_isQuick.GetValue(slot))
                        placed.Add((element, Quick, index, 0));
                    else if ((bool)_isCustom.GetValue(slot))
                    {
                        placed.Add((element, Custom, customOrder % CustomPerRow, customOrder / CustomPerRow));
                        customOrder++;
                    }
                    else
                        continue;

                    if (!(bool)_isQuick.GetValue(slot))
                        HideLabel(element);
                }

                // Groups stack top-down with EAQS's gap between their backgrounds.
                var rows = new int[3];
                foreach (var p in placed)
                    rows[p.Group] = Math.Max(rows[p.Group], p.Row + 1);
                var cellHeight = placed.Count > 0 ? ((RectTransform)placed[0].Element.transform).rect.height : pitch;
                var top = new float[3];
                // Cells use a top-left pivot (the vanilla grid element), so a background edge + padding is the cell edge.
                var left = panelBase.x + BackgroundTopLeft.x + _padding.x;
                var y = panelBase.y + BackgroundTopLeft.y - _padding.y;
                for (var g = 0; g < 3; g++)
                {
                    top[g] = y;
                    if (rows[g] > 0)
                        y -= (rows[g] - 1) * pitch + cellHeight + 2f * _padding.y + GroupGap;
                }

                foreach (var p in placed)
                {
                    var rt = (RectTransform)p.Element.transform;
                    rt.anchoredPosition = new Vector2(left + p.Column * pitch, top[p.Group] - p.Row * pitch);
                    GroupCells[p.Group].Add(rt);
                }
            }
        }

        // EAQS sizes its backgrounds every frame in its InventoryGui.Update postfix; this one runs after it.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        public static class InventoryGui_Update_Patch
        {
            [UsedImplicitly]
            [HarmonyAfter(EaqsGuid)]
            public static void Postfix(InventoryGui __instance)
            {
                if (!Player.m_localPlayer || !InventoryGui.IsVisible() || _measuredFor != __instance || !Available)
                    return;

                var panel = __instance.m_player;
                for (var g = 0; g < 3; g++)
                {
                    var bkg = panel.Find(Backgrounds[g]) as RectTransform;
                    if (!bkg || !bkg.gameObject.activeSelf || !Bounds(panel, GroupCells[g], out var min, out var max))
                        continue;

                    // EAQS CreateBackground: anchor at the panel's top-left corner, pivot centred.
                    var rect = panel.rect;
                    bkg.sizeDelta = max - min + 2f * _padding;
                    bkg.anchoredPosition = (min + max) / 2f - new Vector2(rect.xMin, rect.yMax);
                }
            }
        }

        // Padding of the vanilla player grid background (m_player/Bkg, the rect EAQS clones for its backgrounds)
        // around the first grid cell, both in m_player space.
        private static void MeasurePadding(InventoryGui gui)
        {
            if (_measuredFor == gui)
                return;
            var grid = gui.m_playerGrid;
            var bkg = gui.m_player.Find("Bkg") as RectTransform;
            if (!bkg || grid.m_elements.Count == 0)
                return;

            var cell = new List<RectTransform> { (RectTransform)grid.m_elements[0].transform };
            if (!Bounds(gui.m_player, cell, out var cellMin, out var cellMax) ||
                !Bounds(gui.m_player, new List<RectTransform> { bkg }, out var bkgMin, out var bkgMax))
                return;

            _padding = new Vector2(cellMin.x - bkgMin.x, bkgMax.y - cellMax.y);
            _measuredFor = gui;
            Auga.Log($"EAQS slot panel: pitch {grid.m_elementSpace}, cell {cellMax - cellMin}, background padding {_padding}");
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static bool Bounds(Transform space, List<RectTransform> rects, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            var any = false;
            foreach (var rt in rects)
            {
                if (!rt || !rt.gameObject.activeInHierarchy)
                    continue;
                rt.GetWorldCorners(Corners);
                foreach (var corner in Corners)
                {
                    Vector2 local = space.InverseTransformPoint(corner);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
                any = true;
            }
            return any;
        }

        private static void HideLabel(InventoryElement element)
        {
            var binding = element.transform.Find("binding");
            if (binding && binding.TryGetComponent<TMP_Text>(out var text))
                text.enabled = false;
        }

        // Child of the cell just below its item icon, same rect; shown while vanilla UpdateGui left the cell unused.
        private static void UpdateSilhouette(InventoryElement element, string slotId)
        {
            var silhouette = element.transform.Find(SilhouetteName);
            if (!silhouette)
            {
                var sprite = SilhouetteSprite(slotId);
                if (!sprite)
                    return;

                var icon = element.m_icon.rectTransform;
                var go = new GameObject(SilhouetteName, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(icon.parent, false);
                rt.SetSiblingIndex(icon.GetSiblingIndex());
                rt.anchorMin = icon.anchorMin;
                rt.anchorMax = icon.anchorMax;
                rt.pivot = icon.pivot;
                rt.anchoredPosition = icon.anchoredPosition;
                rt.sizeDelta = icon.sizeDelta;
                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.color = SilhouetteColor;
                image.preserveAspect = element.m_icon.preserveAspect;
                image.raycastTarget = false;
                silhouette = rt;
            }
            silhouette.gameObject.SetActive(!element.m_used);
        }

        private static Sprite SilhouetteSprite(string slotId)
        {
            if (!SilhouetteIcons.TryGetValue(slotId, out var icon))
                return null;
            if (SilhouetteSprites.TryGetValue(icon, out var sprite))
                return sprite;

            sprite = LoadEmbeddedSprite($"Auga.Assets.SlotIcons.{icon}.png");
            if (!sprite)
                Auga.LogWarning($"EAQS slot panel: silhouette icon {icon}.png missing (slot {slotId})");
            SilhouetteSprites[icon] = sprite;
            return sprite;
        }

        private static Sprite LoadEmbeddedSprite(string resourceName)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;
                var data = new byte[stream.Length];
                int read = 0, n;
                while (read < data.Length && (n = stream.Read(data, read, data.Length - read)) > 0)
                    read += n;

                // Same PNG path as AugaCharacterSelect: net472 cannot bind ImageConversion.LoadImage at compile time.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = resourceName };
                AugaUnity.ImageConversionReflection.LoadImage(texture, data);
                if (texture.width <= 2)
                    return null;
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
