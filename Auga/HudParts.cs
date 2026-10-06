using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // HUD parts AugaStyle.Restyle leaves alone because they are not mapped by a plain sprite swap: GuiBar fills (Restyle
    // keeps every fill's art and colour, mods tint theirs) and the ship HUD, whose Auga look (bundle HUD/ShipHud) is a
    // different set of icons. The vanilla objects, fields and update code (Hud.UpdateShipHud, Hud.cs; GuiBar) stay;
    // only their sprites change, so vanilla still drives rotation, fill, colour lerp and visibility.
    public static class HudParts
    {
        // Vanilla HUD bar fills (hudroot dump, 20-hud): piece health (crosshair/PieceHealthRoot), stagger, action
        // progress, mount health/stamina. The stat cluster's own bars (AugaStatBars) use other sprites.
        private static readonly HashSet<string> BarFills = new HashSet<string> { "bar_monster_hp_5", "bar_gradient_40", "bar_stagger" };
        private static readonly Color RudderColor = new Color(0.92f, 0.66f, 0f); // bundle ShipHud/Controls/RudderIcon

        public static void Setup(Hud hud)
        {
            Bars(hud.m_rootObject.transform);
            Ship(hud);
        }

        // Auga's bar body (bundle HUD crosshair/PieceHealthRoot/PieceHealthBar/bar: AugaProgressBarBody_Small, sliced
        // ends) under the colour vanilla gives the bar; the lit HUD material is the vanilla bar's own glow.
        private static void Bars(Transform root)
        {
            var body = AugaStyle.BundleSprite("AugaProgressBarBody_Small");
            if (!body)
                return;
            foreach (var bar in root.GetComponentsInChildren<GuiBar>(true))
            {
                if (!bar.m_bar || !bar.m_bar.TryGetComponent<Image>(out var fill) || !fill.sprite || !BarFills.Contains(fill.sprite.name))
                    continue;
                fill.sprite = body;
                fill.type = Image.Type.Sliced;
                fill.material = null;
            }
        }

        // Bundle ShipHud: WindIndicator (WindCircleShadow / WindCircle / Ship / WindIndicator), Controls (RudderIndicator
        // ring, rudder wheel in Auga gold) and one speed icon per setting on the ship (ForwardSlow / Forward /
        // ForwardFast / Backward) where vanilla draws one to three rudder_arrow chevrons.
        private static void Ship(Hud hud)
        {
            if (hud.m_shipWindIndicatorRoot)
            {
                Swap(hud.m_shipWindIndicatorRoot.GetComponent<Image>(), "WindCircleShadow", Color.white);
                foreach (var image in hud.m_shipWindIndicatorRoot.GetComponentsInChildren<Image>(true))
                {
                    if (!image.sprite || image == hud.m_shipWindIcon)
                        continue;
                    switch (image.sprite.name)
                    {
                        case "ship_circle_bw": Swap(image, "WindCircle", Color.white); break;
                        case "ship_top": Swap(image, "Ship", Color.white); break;
                        case "ship_wind": image.enabled = false; break; // Auga's wind root carries only its icon
                    }
                }
            }
            // Colour stays vanilla's: UpdateShipHud lerps it by the wind angle factor.
            if (hud.m_shipWindIcon && Swap(hud.m_shipWindIcon, "WindIndicator", null))
                hud.m_shipWindIcon.preserveAspect = true;
            // Radial fill set by UpdateShipHud (fillAmount / fillClockwise) keeps the vanilla fill method.
            if (hud.m_shipRudderIndicator)
                Swap(hud.m_shipRudderIndicator, "RudderIndicator", null);
            if (hud.m_shipRudderIcon)
                Swap(hud.m_shipRudderIcon, "rudder", RudderColor);
            Speed(hud.m_rudderSlow, "ForwardSlow");
            Speed(hud.m_rudderForward, "Forward");
            Speed(hud.m_rudderFastForward, "ForwardFast");
            Speed(hud.m_rudderBackward, "Backward");
        }

        private static void Speed(GameObject group, string sprite)
        {
            if (!group)
                return;
            var first = true;
            foreach (var image in group.GetComponentsInChildren<Image>(true))
            {
                if (!image.sprite || image.sprite.name != "rudder_arrow")
                    continue;
                if (first && Swap(image, sprite, Color.white))
                {
                    // Vanilla turns the one chevron sprite per direction; Auga's icons are drawn pointing their way.
                    image.transform.localRotation = Quaternion.identity;
                    image.preserveAspect = true;
                    first = false;
                }
                else if (!first)
                    image.enabled = false;
            }
        }

        private static bool Swap(Image image, string sprite, Color? color)
        {
            var s = image ? AugaStyle.BundleSprite(sprite) : null;
            if (!s)
                return false;
            image.sprite = s;
            image.material = null;
            if (color.HasValue)
                image.color = color.Value;
            return true;
        }
    }
}
