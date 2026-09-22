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
    public class AugaAssets
    {
        public GameObject AugaLogo;
        public GameObject InventoryScreen;
        public GameObject Hud;
        public Texture2D Cursor;
        public GameObject MenuPrefab;
        public GameObject TextViewerPrefab;
        public GameObject MainMenuPrefab;
        public GameObject BuildHudElement;
        public GameObject MessageHud;
        public GameObject AugaBarber;
        public GameObject DamageText;
        public GameObject EnemyHud;
        public GameObject StoreGui;
        public GameObject WorldListElement;
        public GameObject ServerListElement;
        public GameObject PasswordDialog;
        public GameObject ConnectingDialog;
        public GameObject PanelBase;
        public GameObject ButtonSmall;
        public GameObject ButtonMedium;
        public GameObject ButtonFancy;
        public GameObject ButtonToggle;
        public GameObject ButtonSettings;
        public GameObject DiamondButton;
        public Font SourceSansProBold;
        public Font SourceSansProSemiBold;
        public Font SourceSansProRegular;
        public TMPro.TMP_FontAsset NorseboldTMP;
        public TMPro.TMP_FontAsset SourceSansProRegularTMP; public Sprite ItemBackgroundSprite;
        public GameObject InventoryTooltip;
        public GameObject SimpleTooltip;
        public GameObject DividerSmall;
        public GameObject DividerMedium;
        public GameObject DividerLarge;
        public GameObject ConfirmDialog;
        public Sprite RecyclingPanelIcon;
        public GameObject BuildHud;
        public GameObject LeftWristMountUI;
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
        public const string Version = "2.0.3";
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
        }

        private static void LoadAssets()
        {
            var assetBundle = LoadAssetBundle("augaassets");
            if (assetBundle == null)
            {
                Debug.LogError("[Auga] Asset bundle 'augaassets' FAILED to load (embedded resource missing or Unity version incompatible). Auga UI will not work.");
                return;
            }

            var missing = new List<string>();
            var total = 0;
            T Load<T>(string name) where T : UnityEngine.Object
            {
                total++;
                var asset = assetBundle.LoadAsset<T>(name);
                if (asset == null)
                    missing.Add($"{typeof(T).Name} '{name}'");
                return asset;
            }

            Assets.AugaLogo = Load<GameObject>("AugaLogo");
            Assets.InventoryScreen = Load<GameObject>("Inventory_screen");
            Assets.Cursor = Load<Texture2D>("Cursor2");
            Assets.MenuPrefab = Load<GameObject>("AugaMenu");
            Assets.TextViewerPrefab = Load<GameObject>("AugaTextViewer");
            Assets.Hud = Load<GameObject>("HUD");
            Assets.MainMenuPrefab = Load<GameObject>("MainMenu");
            Assets.BuildHudElement = Load<GameObject>("BuildHudElement");
            Assets.MessageHud = Load<GameObject>("AugaMessageHud");
            Assets.AugaBarber = Load<GameObject>("AugaBarber");
            Assets.DamageText = Load<GameObject>("AugaDamageText");
            Assets.EnemyHud = Load<GameObject>("AugaEnemyHud");
            Assets.StoreGui = Load<GameObject>("AugaStoreScreen");
            Assets.WorldListElement = Load<GameObject>("WorldListElement");
            Assets.ServerListElement = Load<GameObject>("ServerListElement");
            Assets.PasswordDialog = Load<GameObject>("AugaPassword");
            Assets.ConnectingDialog = Load<GameObject>("AugaConnecting");
            Assets.PanelBase = Load<GameObject>("AugaPanelBase");
            Assets.ButtonSmall = Load<GameObject>("ButtonSmall");
            Assets.ButtonMedium = Load<GameObject>("ButtonMedium");
            Assets.ButtonFancy = Load<GameObject>("ButtonFancy");
            Assets.ButtonToggle = Load<GameObject>("ButtonToggle");
            Assets.ButtonSettings = Load<GameObject>("ButtonSettings");
            Assets.DiamondButton = Load<GameObject>("DiamondButton");
            Assets.SourceSansProBold = Load<Font>("SourceSansPro-Bold");
            Assets.SourceSansProSemiBold = Load<Font>("SourceSansPro-SemiBold");
            Assets.SourceSansProRegular = Load<Font>("SourceSansPro-Regular");
            Assets.NorseboldTMP = Load<TMPro.TMP_FontAsset>("Norsebold SDF");
            Assets.SourceSansProRegularTMP = Load<TMPro.TMP_FontAsset>("SourceSansPro-Regular SDF");
            Assets.ItemBackgroundSprite = Load<Sprite>("Container_Square_A");
            Assets.InventoryTooltip = Load<GameObject>("InventoryTooltip");
            Assets.SimpleTooltip = Load<GameObject>("SimpleTooltip");
            Assets.DividerSmall = Load<GameObject>("DividerSmall");
            Assets.DividerMedium = Load<GameObject>("DividerMedium");
            Assets.DividerLarge = Load<GameObject>("DividerLarge");
            Assets.ConfirmDialog = Load<GameObject>("ConfirmDialog");
            Assets.RecyclingPanelIcon = Load<Sprite>("RecyclingPanel");
            Assets.LeftWristMountUI = Load<GameObject>("LeftWristMountUI");
            Assets.BuildHud = Load<GameObject>("BuildHud");

            if (missing.Count == 0)
                Debug.Log($"[Auga] Asset bundle 'augaassets' loaded: all {total} assets found");
            else
                Debug.LogError($"[Auga] Asset bundle 'augaassets' loaded, {missing.Count}/{total} assets MISSING: {string.Join(", ", missing)}");
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

