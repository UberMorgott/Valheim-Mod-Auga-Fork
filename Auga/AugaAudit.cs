using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Dev-only UI audit (spec 2026-09-11-auga-native-rework, Phase 0). Console: `devcommands`, then `auga_audit`
    // (cheat-gated, Terminal.cs:245). The autotest driver calls Run() directly. Reports only, never changes UI.
    // For each open screen: missing scripts, vanilla fields on destroyed objects, Graphics outside any Canvas,
    // and screen-space overlaps between sibling panels.
    public static class AugaAudit
    {
        // Sibling names that layer over others by design (backdrops, scrims, frames).
        private static readonly HashSet<string> OverlapAllow = new HashSet<string>
        {
            "Blur", "darken", "Darken", "bkg", "Background", "Scrim", "border (1)", "ornament", "GamepadMap",
            "HeaderLine", // vanilla Settings: thin rule across the top of TabButtons/TabContent
            "AugaCorner"  // AugaStyle.Panel corner ornaments, drawn over the panel's corners by design
        };

        private static readonly HashSet<string> VanillaAssemblies = new HashSet<string>
        {
            "assembly_valheim", "assembly_guiutils", "gui_framework"
        };

        public static void Register()
        {
            new Terminal.ConsoleCommand("auga_audit", "[Auga dev] report missing scripts, dead UI refs, canvas-less graphics and panel overlaps",
                args => { args.Context?.AddString($"auga_audit: {Run()} finding(s), see BepInEx log"); }, isCheat: true);
        }

        public static int Run()
        {
            var screens = new List<(string Name, Component Owner, Transform Root)>();
            void Add(string name, Component owner, Transform root, bool open)
            {
                if (owner && root && open && root.gameObject.activeInHierarchy)
                    screens.Add((name, owner, root));
            }

            Add("MainMenu", FejdStartup.instance, FejdStartup.instance ? FejdStartup.instance.transform : null, true);
            Add("Menu", Menu.instance, Menu.instance ? Menu.instance.m_root : null, true);
            Add("Settings", Settings.instance, Settings.instance ? Settings.instance.transform : null, true);
            Add("Hud", Hud.instance, Hud.instance ? Hud.instance.m_rootObject.transform : null, true);
            Add("InventoryGui", InventoryGui.instance, InventoryGui.instance ? InventoryGui.instance.m_player.parent : null, InventoryGui.IsVisible());
            Add("Minimap", Minimap.instance, Minimap.instance ? (Minimap.instance.m_largeRoot.activeInHierarchy ? Minimap.instance.m_largeRoot : Minimap.instance.m_smallRoot).transform : null, true);
            Add("Chat", Chat.instance, Chat.instance ? Chat.instance.transform : null, true);

            var total = 0;
            foreach (var (name, owner, root) in screens)
            {
                var findings = new List<string>();
                MissingScripts(root, findings);
                DeadRefs(owner, root, findings);
                CanvasLess(root, findings);
                Overlaps(root, 2, findings);
                foreach (var f in findings)
                    Debug.LogWarning($"[Auga] audit {name}: {f}");
                total += findings.Count;
            }

            Debug.Log($"[Auga] audit done: {total} finding(s) on {string.Join(", ", screens.Select(s => s.Name))}");
            return total;
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        private static void MissingScripts(Transform root, List<string> findings)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                // A missing MonoBehaviour script comes back as a null entry.
                if (t.GetComponents<Component>().Any(c => c == null))
                    findings.Add($"missing script on {PathOf(t)}");
            }
        }

        // Destroyed UnityEngine.Object: managed reference non-null, Unity-null. Unassigned (null) fields are not reported.
        private static void DeadRefs(Component owner, Transform root, List<string> findings)
        {
            var components = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(mb => mb != null && VanillaAssemblies.Contains(mb.GetType().Assembly.GetName().Name))
                .Cast<Component>().Prepend(owner).Distinct();
            foreach (var c in components)
            {
                var dead = new List<string>();
                foreach (var field in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var value = field.GetValue(c);
                    if (value is Object obj && !obj)
                        dead.Add(field.Name);
                    else if (value is IList list && !(value is string) && list.Cast<object>().Any(e => e is Object o && !o))
                        dead.Add(field.Name + "[]");
                }

                if (dead.Count > 0)
                    findings.Add($"{c.GetType().Name} on {PathOf(c.transform)}: fields point to destroyed objects: {string.Join(", ", dead)}");
            }
        }

        private static void CanvasLess(Transform root, List<string> findings)
        {
            var orphans = root.GetComponentsInChildren<Graphic>().Where(g => g.canvas == null).ToList();
            if (orphans.Count > 0)
                findings.Add($"{orphans.Count} active Graphic(s) without a Canvas ancestor, e.g. {PathOf(orphans[0].transform)}");
        }

        private static Rect ScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // Sibling panels (active, visible content, not a stretched backdrop) whose screen rects overlap by
        // more than a quarter of the smaller one. Checked at `depth` levels below the screen root.
        private static void Overlaps(Transform parent, int depth, List<string> findings)
        {
            if (depth <= 0 || !(parent is RectTransform parentRt))
                return;

            var parentArea = ScreenRect(parentRt).width * ScreenRect(parentRt).height;
            var panels = new List<(RectTransform Rt, Rect Rect)>();
            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeInHierarchy || !(child is RectTransform rt) || OverlapAllow.Contains(child.name))
                    continue;
                if (child.GetComponentInChildren<Graphic>() == null)
                    continue;
                var rect = ScreenRect(rt);
                var area = rect.width * rect.height;
                if (area < 1f || (parentArea > 0f && area >= 0.95f * parentArea))
                    continue;
                panels.Add((rt, rect));
            }

            for (var i = 0; i < panels.Count; i++)
            for (var j = i + 1; j < panels.Count; j++)
            {
                var a = panels[i].Rect;
                var b = panels[j].Rect;
                var w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                var h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                if (w <= 0f || h <= 0f)
                    continue;
                var smaller = Mathf.Min(a.width * a.height, b.width * b.height);
                if (w * h > 0.25f * smaller)
                    findings.Add($"overlap {PathOf(panels[i].Rt)} {a} x {panels[j].Rt.name} {b} ({w * h / smaller:P0} of smaller)");
            }

            foreach (Transform child in parent)
                if (child.gameObject.activeInHierarchy)
                    Overlaps(child, depth - 1, findings);
        }
    }
}
