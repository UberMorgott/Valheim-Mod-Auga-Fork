using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace Auga.Compat
{
    // RuneSkin (morgott.valheim.runeskin, successor skin) owns the screen families it took at startup; AugaSkin must not
    // style those, so each screen has exactly one skin. RuneSkin freezes its set in its own Awake, which runs before
    // ours (soft [BepInDependency] on its GUID in Auga.cs). Read once here, BEFORE Harmony patching, through reflection
    // (no reference to RuneSkin.API.dll): an owned family's patches are not installed at all, or, where a patch also
    // carries a feature (pause-menu Compendium entry), only its restyle is skipped.
    public static class RuneSkinOwnership
    {
        public const string Guid = "morgott.valheim.runeskin";

        public static bool Menu { get; private set; }
        public static bool Tooltips { get; private set; }

        public static void Freeze()
        {
            if (!Chainloader.PluginInfos.ContainsKey(Guid))
                return;
            var api = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RuneSkin.API");
            var isOwned = api?.GetType("RuneSkin.API.Skin")?.GetMethod("IsOwned", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (isOwned == null)
            {
                Debug.LogWarning("[Auga] RuneSkin present but RuneSkin.API.Skin.IsOwned not found; AugaSkin keeps every screen");
                return;
            }
            Menu = (bool)isOwned.Invoke(null, new object[] { "Menu" });
            Tooltips = (bool)isOwned.Invoke(null, new object[] { "Tooltips" });
            Debug.Log($"[Auga] RuneSkin owns: Menu={Menu} Tooltips={Tooltips} (AugaSkin yields those)");
        }

        // Patch classes not installed while RuneSkin owns their family.
        public static bool Skips(Type patchClass) =>
            Tooltips && patchClass == typeof(UITooltip_OnHoverStart_Patch);
    }
}
