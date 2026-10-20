using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Auga.Compat;
using AugaUnity;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using fastJSON;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Bundle assets load on first use (AssetBundle.LoadAsset is synchronous and pulls every texture/font/sprite the
    // prefab depends on), so the plugin Awake only opens the bundle: the in-world prefabs (HUD, Inventory_screen,
    // EnemyHud, ...) load behind the world loading screen, the menu ones when FejdStartup builds the menu.
    // tools/strip_augaassets.py reads the Load<T>("...") names in this file to decide what the bundle keeps and fails
    // the bundle build when one is missing; at runtime a missing one logs an error on first use.
    public class AugaAssets
    {
        private AssetBundle _bundle;
        private readonly Dictionary<string, UnityEngine.Object> _loaded = new Dictionary<string, UnityEngine.Object>();

        internal void Init(AssetBundle bundle) => _bundle = bundle;

        private T Load<T>(string name) where T : UnityEngine.Object
        {
            if (_loaded.TryGetValue(name, out var asset))
                return (T)asset;
            var loaded = _bundle ? _bundle.LoadAsset<T>(name) : null;
            if (!loaded)
                Debug.LogError($"[Auga] asset {typeof(T).Name} '{name}' not found in bundle 'augaassets'");
            _loaded[name] = loaded;
            return loaded;
        }

        public GameObject AugaLogo => Load<GameObject>("AugaLogo");
        public GameObject InventoryScreen => Load<GameObject>("Inventory_screen");
        public Texture2D Cursor => Load<Texture2D>("Cursor2");
        public GameObject MenuPrefab => Load<GameObject>("AugaMenu");
        public GameObject TextViewerPrefab => Load<GameObject>("AugaTextViewer");
        public GameObject Hud => Load<GameObject>("HUD");
        public GameObject MainMenuPrefab => Load<GameObject>("MainMenu");
        public GameObject BuildHudElement => Load<GameObject>("BuildHudElement");
        public GameObject MessageHud => Load<GameObject>("AugaMessageHud");
        public GameObject AugaBarber => Load<GameObject>("AugaBarber");
        public GameObject DamageText => Load<GameObject>("AugaDamageText");
        public GameObject EnemyHud => Load<GameObject>("AugaEnemyHud");
        public GameObject StoreGui => Load<GameObject>("AugaStoreScreen");
        public GameObject WorldListElement => Load<GameObject>("WorldListElement");
        public GameObject ServerListElement => Load<GameObject>("ServerListElement");
        public GameObject PasswordDialog => Load<GameObject>("AugaPassword");
        public GameObject ConnectingDialog => Load<GameObject>("AugaConnecting");
        public GameObject PanelBase => Load<GameObject>("AugaPanelBase");
        public GameObject ButtonSmall => Load<GameObject>("ButtonSmall");
        public GameObject ButtonMedium => Load<GameObject>("ButtonMedium");
        public GameObject ButtonFancy => Load<GameObject>("ButtonFancy");
        public GameObject ButtonToggle => Load<GameObject>("ButtonToggle");
        public GameObject ButtonSettings => Load<GameObject>("ButtonSettings");
        public GameObject DiamondButton => Load<GameObject>("DiamondButton");
        public Font SourceSansProBold => Load<Font>("SourceSansPro-Bold");
        public Font SourceSansProSemiBold => Load<Font>("SourceSansPro-SemiBold");
        public Font SourceSansProRegular => Load<Font>("SourceSansPro-Regular");
        public TMPro.TMP_FontAsset NorseboldTMP => Load<TMPro.TMP_FontAsset>("Norsebold SDF");
        public TMPro.TMP_FontAsset SourceSansProRegularTMP => Load<TMPro.TMP_FontAsset>("SourceSansPro-Regular SDF");
        public Sprite ItemBackgroundSprite => Load<Sprite>("Container_Square_A");
        public GameObject InventoryTooltip => Load<GameObject>("InventoryTooltip");
        public GameObject SimpleTooltip => Load<GameObject>("SimpleTooltip");
        public GameObject DividerSmall => Load<GameObject>("DividerSmall");
        public GameObject DividerMedium => Load<GameObject>("DividerMedium");
        public GameObject DividerLarge => Load<GameObject>("DividerLarge");
        public GameObject ConfirmDialog => Load<GameObject>("ConfirmDialog");
        public Sprite RecyclingPanelIcon => Load<Sprite>("RecyclingPanel");
        public GameObject LeftWristMountUI => Load<GameObject>("LeftWristMountUI");
        public GameObject BuildHud => Load<GameObject>("BuildHud");
    }

    public class AugaColors
    {
        public string BrightestGold = "#FFBF1B";
        public string Topic = "#EAA800";
        public string Emphasis = "#1AACEF";
        public Color Healing = new Color(0.5f, 1.0f, 0.5f, 0.7f);
        public Color PlayerDamage = new Color(1.0f, 0.0f, 0.0f, 1.0f);
        public Color PlayerNoDamage = new Color(0.5f, 0.5f, 0.5f, 1f);
        public Color NormalDamage = new Color(1f, 1f, 1f, 1f);
        public Color ResistDamage = new Color(0.6f, 0.6f, 0.6f, 1f);
        public Color WeakDamage = new Color(1f, 1f, 0.0f, 1f);
        public Color ImmuneDamage = new Color(0.6f, 0.6f, 0.6f, 1f);
        public Color TooHard = new Color(0.8f, 0.7f, 0.7f, 1f);
        public Color Bonus = new Color(1f, 0.63f, 0.24f, 1f);
    }

    // New GUID and assembly name AugaSkin (spec 2026-09-11-auga-native-rework, D0a, Phase 4b). Mods that detect Auga
    // by GUID randyknapp.mods.auga (VNEI Plugin.cs:335, AdventureBackpacks GuiBar.cs:22) or by assembly "Auga"
    // (EpicLoot/EAQS embedded Auga.API stub) take their vanilla path. The config file is new: BepInEx\config\
    // morgott.valheim.augaskin.cfg (no migration from randyknapp.mods.auga.cfg).
    [BepInPlugin(PluginID, "AugaSkin", Version)]
    [BepInDependency("Menthus.bepinex.plugins.BetterTrader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("redseiko.valheim.chatter", BepInDependency.DependencyFlags.SoftDependency)]
    public class Auga : BaseUnityPlugin
    {
        public const string PluginID = "morgott.valheim.augaskin";
        public const string Version = "2.2.5";
        // Embedded resources are named by the project's RootNamespace (Auga), not by the assembly name.
        private const string ResourcePrefix = "Auga.";

        private static ConfigEntry<bool> _loggingEnabled;
        private static ConfigEntry<LogLevel> _logLevel;

        public static readonly AugaAssets Assets = new AugaAssets();
        public static readonly AugaColors Colors = new AugaColors();

        public static bool HasBetterTrader;
        public static bool HasChatter;

        private static Auga _instance;
        private Harmony _harmony;

        public static Auga instance => _instance;

        // Статический конструктор — регистрируем AssemblyResolve до того как CLR
        // попытается разрешить APIManager/fastJSON/Unity.Auga при загрузке типа.
        static Auga()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            return LoadEmbedded(new AssemblyName(args.Name).Name);
        }

        // Returns the already-loaded copy if present, so each embedded assembly loads once.
        private static Assembly LoadEmbedded(string name)
        {
            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loaded.GetName().Name == name) return loaded;
            }

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{ResourcePrefix}{name}.dll");
            if (stream == null) return null;
            using (stream)
            {
                // Stream.Read may return fewer bytes than asked; CopyTo reads to the end.
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                return Assembly.Load(ms.ToArray());
            }
        }

        public void Awake()
        {
            _instance = this;
            if (int.TryParse(Assembly.GetExecutingAssembly().GetName().Version.ToString().Split('.')[3], out var revision))
            {
                if (revision > 0)
                {
                    Debug.LogWarning($"==============================================================================");
                    Debug.LogWarning($"You are using a PTB version of this mod. It will not work in prior versions.");
                    Debug.LogWarning($"Project Auga - Version {Assembly.GetExecutingAssembly().GetName().Version}");
                    Debug.LogWarning($"Valheim - Version {(global::Version.GetVersionString())}");

                    // Version gate removed — PTB check is no longer needed for current Valheim.
                    Debug.LogWarning($"==============================================================================");
                }
            }

            LoadDependencies();
            LoadTranslations();
            LoadConfig();
            LoadAssets();

            ApplyCursor();

            HasBetterTrader = Chainloader.PluginInfos.ContainsKey("Menthus.bepinex.plugins.BetterTrader");
            HasChatter = Chainloader.PluginInfos.TryGetValue("redseiko.valheim.chatter", out var chatterPlugin);

            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginID);
            JotunnGui.Init(_harmony);
            EpicLootEnchanting.Init(_harmony);
            Compat.Almanac.Init(_harmony);
            Imgui.Init(_harmony);

            if (HasChatter)
            {
                Chatter.ChatterType = Assembly.LoadFile(chatterPlugin.Location);
                Chatter.ToggleCell = Chatter.ChatterType.GetType("Chatter.ToggleCell");
                var createChildCellMethod = AccessTools.Method(Chatter.ToggleCell, "CreateChildCell");
                var createChildLabelMethod = AccessTools.Method(Chatter.ToggleCell, "CreateChildLabel");
                var onToggleValueChangedMethod = AccessTools.Method(Chatter.ToggleCell, "OnToggleValueChanged");

                if (Chatter.ToggleCell != null)
                {
                    _harmony.Patch(createChildCellMethod, new HarmonyMethod(typeof(Chatter), nameof(Chatter.CreateChildCell_Patch)));
                    _harmony.Patch(createChildLabelMethod, transpiler: new HarmonyMethod(typeof(Chatter), nameof(Chatter.CreateChildLabel_Transpiler)));
                    _harmony.Patch(onToggleValueChangedMethod, transpiler: new HarmonyMethod(typeof(Chatter), nameof(Chatter.OnToggleValueChanged_Transpiler)));
                }
            }
        }

        public void OnDestroy()
        {
            _instance = null;
        }

        private void LoadDependencies()
        {
            foreach (var assemblyName in new[] { "fastJSON", "Unity.Auga" })
            {
                if (LoadEmbedded(assemblyName) == null)
                    Debug.LogError($"[Auga] Could not load embedded assembly ({assemblyName}.dll)!");
            }
        }

        private static void LoadTranslations()
        {
            var translationsJsonText = LoadJsonText("translations.json");
            if (string.IsNullOrEmpty(translationsJsonText))
            {
                return;
            }

            var translations = (IDictionary<string, object>)JSON.Parse(translationsJsonText);
            foreach (var translation in translations)
            {
                if (!string.IsNullOrEmpty(translation.Key) && !string.IsNullOrEmpty(translation.Value.ToString()))
                {
                    Localization.instance.AddWord(translation.Key, translation.Value.ToString());
                }
            }
        }

        private void LoadConfig()
        {
            _loggingEnabled = Config.Bind("Logging", "LoggingEnabled", false, "Enable logging");
            _logLevel = Config.Bind("Logging", "LogLevel", LogLevel.Info, "Only log messages of the selected level or higher");
            ShowEnergyShield = Config.Bind("Hud", "ShowEnergyShield", true,
                "Show the energy shield (MorgottTweaks, player ZDO mt_es / mt_es_max): a violet outline behind your health bar and a violet strip under other players' health bars.");
            ShowSummonFrames = Config.Bind("Hud", "ShowSummonFrames", true,
                "Show a frame per living summon of yours (icon, health bar, stars) under the hotkey bar.");
        }

        public static ConfigEntry<bool> ShowEnergyShield;
        public static ConfigEntry<bool> ShowSummonFrames;

        private static void LoadAssets()
        {
            var assetBundle = LoadAssetBundle("augaassets");
            if (assetBundle == null)
            {
                Debug.LogError("[Auga] Asset bundle 'augaassets' FAILED to load (embedded resource missing or Unity version incompatible). Auga UI will not work.");
                return;
            }

            Assets.Init(assetBundle);
        }

        private static void ApplyCursor()
        {
            Cursor.SetCursor(Assets.Cursor, new Vector2(6, 5), CursorMode.Auto);
        }

        public static AssetBundle LoadAssetBundle(string filename)
        {
            // Optionally load asset bundle from path, if it exists
            var assetBundlePath = GetAssetPath(filename);
            if (!string.IsNullOrEmpty(assetBundlePath))
            {
                return AssetBundle.LoadFromFile(assetBundlePath);
            }

            var assembly = typeof(Auga).Assembly;
            var assetBundle = AssetBundle.LoadFromStream(assembly.GetManifestResourceStream($"{ResourcePrefix}{filename}"));

            return assetBundle;
        }

        public static string LoadJsonText(string filename)
        {
            var jsonFileName = GetAssetPath(filename);
            return !string.IsNullOrEmpty(jsonFileName) ? File.ReadAllText(jsonFileName) : null;
        }

        public static string GetAssetPath(string assetName)
        {
            // Next to the plugin DLL (plugins\AugaSkin).
            var assetFileName = Path.Combine(Path.GetDirectoryName(typeof(Auga).Assembly.Location) ?? string.Empty, assetName);
            if (File.Exists(assetFileName))
                return assetFileName;
            LogError($"Could not find asset ({assetName})");
            return null;
        }

        public static void Log(string message)
        {
            if (_loggingEnabled.Value && _logLevel.Value <= LogLevel.Info)
            {
                _instance.Logger.LogInfo(message);
            }
        }

        public static void LogWarning(string message)
        {
            if (_loggingEnabled.Value && _logLevel.Value <= LogLevel.Warning)
            {
                _instance.Logger.LogWarning(message);
            }
        }

        public static void LogError(string message)
        {
            if (_loggingEnabled.Value && _logLevel.Value <= LogLevel.Error)
            {
                _instance.Logger.LogError(message);
            }
        }

    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    public static class Terminal_InitTerminal_Patch
    {
        public static void Postfix()
        {
            _ = new Terminal.ConsoleCommand("resetbiomes", "", args =>
            {
                var t = typeof(Player).GetField(nameof(Player.m_knownBiome),
                    BindingFlags.Instance | BindingFlags.NonPublic);
                t.SetValue(Player.m_localPlayer, new HashSet<Heightmap.Biome>());
            });
            AugaAudit.Register();
        }
    }
}

