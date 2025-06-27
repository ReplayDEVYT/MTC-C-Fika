using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Diz.Utils;
using EFT.UI;
using Fika.Core.Bundles;
using Fika.Core.Console;
using Fika.Core.Coop.Custom;
using Fika.Core.Coop.Utils;
using Fika.Core.EssentialPatches;
using Fika.Core.Models;
using Fika.Core.Networking.Http;
using Fika.Core.Networking.Websocket;
using Fika.Core.Patching;
using Fika.Core.UI;
using Fika.Core.UI.Models;
using Fika.Core.UI.Patches;
using Fika.Core.Utils;
using SPT.Common.Http;
using SPT.Custom.Patches;
using SPT.Custom.Utils;
using SPT.SinglePlayer.Patches.MainMenu;
using SPT.SinglePlayer.Patches.RaidFix;
using SPT.SinglePlayer.Patches.ScavMode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Fika.Core
{
    /// <summary>
    /// Fika.Core Plugin. <br/> <br/>
    /// Originally by: Paulov <br/>
    /// Re-written by: Lacyway & the Fika team
    /// </summary>
    [BepInPlugin("com.fika.core", "Fika.Core", FikaVersion)]
    [BepInProcess("EscapeFromTarkov.exe")]
    [BepInDependency("com.SPT.custom", BepInDependency.DependencyFlags.HardDependency)] // This is used so that we guarantee to load after spt-custom, that way we can disable its patches
    [BepInDependency("com.SPT.singleplayer", BepInDependency.DependencyFlags.HardDependency)] // This is used so that we guarantee to load after spt-singleplayer, that way we can disable its patches
    [BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.HardDependency)] // This is used so that we guarantee to load after spt-core, that way we can disable its patches
    [BepInDependency("com.SPT.debugging", BepInDependency.DependencyFlags.HardDependency)] // This is used so that we guarantee to load after spt-debugging, that way we can disable its patches
    public class FikaPlugin : BaseUnityPlugin
    {
        public const string FikaVersion = "1.2.8";
        public static FikaPlugin Instance;
        public static string EFTVersionMajor { get; internal set; }
        public static string ServerModVersion { get; private set; }
        public ManualLogSource FikaLogger
        {
            get
            {
                return Logger;
            }
        }
        public bool LocalesLoaded { get; internal set; }
        public BotDifficulties BotDifficulties;
        public FikaModHandler ModHandler = new();
        public string[] LocalIPs;
        public IPAddress WanIP;

        internal static uint Crc32 { get; set; }
        internal InternalBundleLoader BundleLoaderPlugin { get; private set; }
        internal FikaNotificationManager NotificationManager { get; set; }

        private static readonly Version RequiredServerVersion = new("2.4.7");
        private PatchManager _patchManager;

        public static HeadlessRequesterWebSocket HeadlessRequesterWebSocket { get; set; }

        public static Dictionary<string, string> RespectedPlayersList = new()
        {
            { "samswat",      "godfather of modern SPT modding ~ SSH"                                                       },
            { "katto",        "kmc leader & founder. OG revolutionary of custom assets ~ SSH"                               },
            { "polivilas",    "who started it all -- #emutarkov2019 ~ Senko-san"                                            },
            { "balist0n",     "author of the first singleplayer-focussed mechanics and good friend ~ Senko-san"             },
            { "ghostfenixx",  "keeps asking me to fix bugs ~ TheSparta"                                                     },
            { "thurman",      "aka TwistedGA, helped a lot of new modders, including me when I first started ~ TheSparta"   },
            { "chomp",        "literally unstoppable, carrying SPT development every single day ~ TheSparta"                },
            { "nimbul",       "Sat with Lacy many night and is loved by both Lacy & me. We miss you <3 ~ SSH"               },
            { "vox",          "My favourite american. ~ Lacyway"                                                            },
            { "rairai",       "Very nice and caring person, someone I've appreciated getting to know. ~ Lacyway"            },
            { "cwx",          "Active and dedicated tester who has contributed a lot of good ideas to Fika. ~ Lacyway"      }
        };

        public static Dictionary<string, string> DevelopersList = new()
        {
            { "lacyway",      "no one unified the community as much as you ~ Senko-san"                  },
            { "ssh_",         "my little favorite gremlin. ~ Lacyway"                                    },
            { "nexus4880",    "the one who taught me everything I know now. ~ SSH"                       },
            { "thesparta",    "I keep asking him to fix these darn bugs ~ GhostFenixx"                   },
            { "senko-san",    "creator of SPT, extremely talented dev, a blast to work with ~ TheSparta" },
            { "leaves",       "Super talented person who comes up with the coolest ideas ~ Lacyway"      },
            { "Archangel",    "The 'tbh' guy :pepeChad: ~ Lacyway"                                       },
            { "trippy",       "One of the chads that made the headless client a reality ~ Archangel"     }
        };

        #region config values

        // Hidden
        public static ConfigEntry<string> LastVersion { get; set; }

        //Advanced
        public static ConfigEntry<bool> OfficialVersion { get; set; }

        // Coop
        public static ConfigEntry<bool> UseHeadlessIfAvailable { get; set; }
        public static ConfigEntry<bool> ShowNotifications { get; set; }
        public static ConfigEntry<bool> AutoExtract { get; set; }
        public static ConfigEntry<bool> ShowExtractMessage { get; set; }
        public static ConfigEntry<KeyboardShortcut> ExtractKey { get; set; }
        public static ConfigEntry<bool> EnableChat { get; set; }
        public static ConfigEntry<KeyboardShortcut> ChatKey { get; set; }
        public static ConfigEntry<bool> EnableOnlinePlayers { get; set; }
        public static ConfigEntry<float> OnlinePlayersScale { get; set; }

        // Coop | Quest Sharing
        public static ConfigEntry<EQuestSharingTypes> QuestTypesToShareAndReceive { get; set; }
        public static ConfigEntry<bool> QuestSharingNotifications { get; set; }
        public static ConfigEntry<bool> EasyKillConditions { get; set; }
        public static ConfigEntry<bool> SharedKillExperience { get; set; }
        public static ConfigEntry<bool> SharedBossExperience { get; set; }

        // Performance
        public static ConfigEntry<bool> DynamicAI { get; set; }
        public static ConfigEntry<float> DynamicAIRange { get; set; }
        public static ConfigEntry<EDynamicAIRates> DynamicAIRate { get; set; }
        public static ConfigEntry<bool> DynamicAIIgnoreSnipers { get; set; }
        public static ConfigEntry<bool> UseFikaGC { get; set; }

        // Network
        public static ConfigEntry<bool> NativeSockets { get; set; }
        public static ConfigEntry<string> ForceIP { get; set; }
        public static ConfigEntry<string> ForceBindIP { get; set; }
        public static ConfigEntry<int> UDPPort { get; set; }
        public static ConfigEntry<bool> UseUPnP { get; set; }
        public static ConfigEntry<bool> UseNatPunching { get; set; }
        public static ConfigEntry<int> ConnectionTimeout { get; set; }
        public static ConfigEntry<ESendRate> SendRate { get; set; }
        public static ConfigEntry<bool> AllowVOIP { get; set; }

        // Gameplay
        public static ConfigEntry<bool> DisableBotMetabolism { get; set; }
        #endregion

        #region client config
        public bool UseBTR;
        public bool FriendlyFire;
        public bool DynamicVExfils;
        public bool AllowFreeCam;
        public bool AllowSpectateFreeCam;
        public bool AllowItemSending;
        public string[] BlacklistedItems;
        public bool ForceSaveOnDeath;
        public bool UseInertia;
        public bool SharedQuestProgression;
        public bool CanEditRaidSettings;
        public bool EnableTransits;
        public bool AnyoneCanStartRaid;
        #endregion

        #region natpunch config
        public bool NatPunchServerEnable;
        public string NatPunchServerIP;
        public int NatPunchServerPort;
        public int NatPunchServerNatIntroduceAmount;
        #endregion

        protected void Awake()
        {
            Instance = this;
            _patchManager = new(this, true);

            GetNatPunchServerConfig();
            EnableFikaPatches();
            DisableSPTPatches();
            FixSPTBugPatches();

            GetClientConfig();

            string fikaVersion = Assembly.GetAssembly(typeof(FikaPlugin)).GetName().Version.ToString();

            Logger.LogInfo($"Fika is loaded! Running version: " + fikaVersion);

            BundleLoaderPlugin = new();

            BotSettingsRepoClass.Init();

            BotDifficulties = FikaRequestHandler.GetBotDifficulties();
            ConsoleScreen.Processor.RegisterCommandGroup<FikaCommands>();

            if (AllowItemSending)
            {
                _patchManager.EnablePatch(new ItemContext_Patch());
            }

            _ = Task.Run(RunChecks);
        }

        private void SetupConfigEventHandlers()
        {
            OfficialVersion.SettingChanged += OfficialVersion_SettingChanged;
        }

        private void EnableFikaPatches()
        {
            _patchManager.EnablePatches();
        }

        private void VerifyServerVersion()
        {
            string version = FikaRequestHandler.CheckServerVersion().Version;
            bool failed = true;
            if (Version.TryParse(version, out Version serverVersion))
            {
                if (serverVersion >= RequiredServerVersion)
                {
                    failed = false;
                }
            }

            if (failed)
            {
                FikaLogger.LogError($"Server version check failed. Expected: >{RequiredServerVersion}, received: {serverVersion}");
                AsyncWorker.RunInMainTread(ShowServerCheckFailMessage);
            }
            else
            {
                FikaLogger.LogInfo($"Server version check passed. Expected: >{RequiredServerVersion}, received: {serverVersion}");
            }
        }

        private void ShowServerCheckFailMessage()
        {
            MessageBoxHelper.Show($"Failed to verify server mod version.\nMake sure that the server mod is installed and up-to-date!\nRequired Server Version: {RequiredServerVersion}",
                    "FIKA ERROR", MessageBoxHelper.MessageBoxType.OK);
            Application.Quit();
        }

        /// <summary>
        /// Coroutine to ensure all mods are loaded by waiting 5 seconds
        /// </summary>
        /// <returns></returns>
        private async Task RunChecks()
        {
            try
            {
                WanIP = await FikaRequestHandler.GetPublicIP();
            }
            catch (Exception ex)
            {
                Logger.LogError($"RunChecks: {ex.Message}");
            }

            await Task.Delay(5000);
#if !DEBUG
            VerifyServerVersion();
#endif
            ModHandler.VerifyMods(_patchManager);

            if (Crc32 == 0)
            {
                Logger.LogError($"RunChecks: {LocaleUtils.UI_MOD_VERIFY_FAIL.Localized()}");
            }

            _patchManager = null;
        }

        private void GetClientConfig()
        {
            ClientConfigModel clientConfig = FikaRequestHandler.GetClientConfig();

            UseBTR = clientConfig.UseBTR;
            FriendlyFire = clientConfig.FriendlyFire;
            DynamicVExfils = clientConfig.DynamicVExfils;
            AllowFreeCam = clientConfig.AllowFreeCam;
            AllowSpectateFreeCam = clientConfig.AllowSpectateFreeCam;
            AllowItemSending = clientConfig.AllowItemSending;
            BlacklistedItems = clientConfig.BlacklistedItems;
            ForceSaveOnDeath = clientConfig.ForceSaveOnDeath;
            UseInertia = clientConfig.UseInertia;
            SharedQuestProgression = clientConfig.SharedQuestProgression;
            CanEditRaidSettings = clientConfig.CanEditRaidSettings;
            EnableTransits = clientConfig.EnableTransits;
            AnyoneCanStartRaid = clientConfig.AnyoneCanStartRaid;

            clientConfig.LogValues();
        }

        private void GetNatPunchServerConfig()
        {
            NatPunchServerConfigModel natPunchServerConfig = FikaRequestHandler.GetNatPunchServerConfig();

            NatPunchServerEnable = natPunchServerConfig.Enable;
            NatPunchServerIP = RequestHandler.Host.Replace("https://", "").Split(':')[0];
            NatPunchServerPort = natPunchServerConfig.Port;
            NatPunchServerNatIntroduceAmount = natPunchServerConfig.NatIntroduceAmount;

            natPunchServerConfig.LogValues();
        }

        /// <summary>
        /// This is required for the locales to be properly loaded, for some reason they are still unavailable for a few seconds after getting populated
        /// </summary>
        /// <param name="__result">The <see cref="Task"/> that populates the locales</param>
        /// <returns></returns>
        public IEnumerator WaitForLocales(Task __result)
        {
            Logger.LogInfo("Waiting for locales to be ready...");
            while (!__result.IsCompleted)
            {
                yield return null;
            }
            while (LocaleUtils.BEPINEX_H_ADVANCED.Localized() == "F_BepInEx_H_Advanced")
            {
                yield return new WaitForSeconds(1);
            }
            LocalesLoaded = true;
            Logger.LogInfo("Locales are ready!");
            SetupConfig();
            FikaVersionLabel_Patch.UpdateVersionLabel();
        }

        private string CleanConfigString(string header)
        {
            string original = string.Copy(header);
            bool foundForbidden = false;
            char[] forbiddenChars = ['\n', '\t', '\\', '\"', '\'', '[', ']'];
            foreach (char character in forbiddenChars)
            {
                if (header.Contains(character))
                {
                    FikaLogger.LogWarning($"Header '{original}' contains an illegal character: {character}\nReport this to the developers!");
                    header = header.Replace(character, char.MinValue);
                    foundForbidden = true;
                }
            }

            if (foundForbidden)
            {
                FikaLogger.LogWarning($"Header '{original}' was changed to '{header}'");
            }
            return header;
        }

        private ConfigEntry<T> SetupSetting<T>(string section, string key, T defValue, ConfigDescription configDescription, string fallback, ref bool failed, List<string> error)
        {
            try
            {
                return Config.Bind(section, key, defValue, configDescription);
            }
            catch (Exception ex)
            {
                FikaLogger.LogError($"Could not set up section {fallback}! Exception:\n{ex.Message}");
                failed = true;
                error.Add(fallback);

                return Config.Bind(section, fallback, defValue, configDescription);
            }
        }

        private void SetupConfig()
        {
            bool failed = false;
            List<string> headers = [];

            // Hidden

            LastVersion = Config.Bind("Hidden", "Last Version", "0",
                new ConfigDescription("Last loaded version of Fika", tags: new ConfigurationManagerAttributes() { Browsable = false }));

#if GOLDMASTER
            if (LastVersion.Value != FikaVersion)
            {
                Singleton<PreloaderUI>.Instance.ShowFikaMessage("FIKA", LocaleUtils.UI_TOS_LONG.Localized(), ErrorScreen.EButtonType.QuitButton, 15f,
                    null, () =>
                    {
                        LastVersion.Value = FikaVersion;
                    });
            }
#endif

            // Advanced

            string advancedHeader = LocaleUtils.BEPINEX_H_ADVANCED.Localized();
            string advancedDefaultHeader = "Advanced";

            OfficialVersion = SetupSetting(advancedDefaultHeader, "Show Official Version", false,
                    new ConfigDescription(LocaleUtils.BEPINEX_OFFICIAL_VERSION_D.Localized(), tags: new ConfigurationManagerAttributes()
                    {
                        IsAdvanced = true,
                        Category = advancedHeader,
                        DispName = LocaleUtils.BEPINEX_OFFICIAL_VERSION_T.Localized()
                    }),
                    "Official Version", ref failed, headers);

            // Coop

            string coopHeader = CleanConfigString(LocaleUtils.BEPINEX_H_COOP.Localized());
            string coopDefaultHeader = "Coop";

            UseHeadlessIfAvailable = SetupSetting(coopDefaultHeader, "Auto Use Headless", false,
                new ConfigDescription(LocaleUtils.BEPINEX_USE_HEADLESS_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_USE_HEADLESS_T.Localized(),
                    Order = 8
                }), "Auto Use Headless", ref failed, headers);

            ShowNotifications = SetupSetting(coopDefaultHeader, "Show Feed", true,
                new ConfigDescription(LocaleUtils.BEPINEX_SHOW_FEED_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_SHOW_FEED_T.Localized(),
                    Order = 7
                }),
                "Show Feed", ref failed, headers);

            AutoExtract = SetupSetting(coopDefaultHeader, "Auto Extract", false,
                new ConfigDescription(LocaleUtils.BEPINEX_AUTO_EXTRACT_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_AUTO_EXTRACT_T.Localized(),
                    Order = 6
                }),
                "Auto Extract", ref failed, headers);

            ShowExtractMessage = SetupSetting(coopDefaultHeader, "Show Extract Message", true,
                new ConfigDescription(LocaleUtils.BEPINEX_SHOW_EXTRACT_MESSAGE_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_SHOW_EXTRACT_MESSAGE_T.Localized(),
                    Order = 5
                }),
                "Show Extract Message", ref failed, headers);

            ExtractKey = SetupSetting(coopDefaultHeader, "Extract Key", new KeyboardShortcut(KeyCode.F8),
                new ConfigDescription(LocaleUtils.BEPINEX_EXTRACT_KEY_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_EXTRACT_KEY_T.Localized(),
                    Order = 4
                }),
                "Extract Key", ref failed, headers);

            EnableChat = SetupSetting(coopDefaultHeader, "Enable Chat", false,
                new ConfigDescription(LocaleUtils.BEPINEX_ENABLE_CHAT_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_ENABLE_CHAT_T.Localized(),
                    Order = 3
                }),
                "Enable Chat", ref failed, headers);

            ChatKey = SetupSetting(coopDefaultHeader, "Chat Key", new KeyboardShortcut(KeyCode.RightControl),
                new ConfigDescription(LocaleUtils.BEPINEX_CHAT_KEY_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_CHAT_KEY_T.Localized(),
                    Order = 2
                }),
                "Chat Key", ref failed, headers);

            EnableOnlinePlayers = SetupSetting(coopDefaultHeader, "Enable Online Players", true,
                new ConfigDescription(LocaleUtils.BEPINEX_ENABLE_ONLINE_PLAYER_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_ENABLE_ONLINE_PLAYER_T.Localized(),
                    Order = 1
                }),
                "Enable Online Players", ref failed, headers);

            OnlinePlayersScale = SetupSetting(coopDefaultHeader, "Online Players Scale", 1f,
                new ConfigDescription(LocaleUtils.BEPINEX_ONLINE_PLAYERS_SCALE_D.Localized(),
                new AcceptableValueRange<float>(0.5f, 1.5f), new ConfigurationManagerAttributes()
                {
                    Category = coopHeader,
                    DispName = LocaleUtils.BEPINEX_ONLINE_PLAYERS_SCALE_T.Localized(),
                    Order = 0
                }),
                "Online Players Scale", ref failed, headers);

            // Performance

            string performanceHeader = CleanConfigString(LocaleUtils.BEPINEX_H_PERFORMANCE.Localized());
            string performanceDefaultHeader = "Performance";

            DynamicAI = SetupSetting(performanceDefaultHeader, "Dynamic AI", false,
                new ConfigDescription(LocaleUtils.BEPINEX_DYNAMIC_AI_T.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = performanceHeader,
                    DispName = LocaleUtils.BEPINEX_DYNAMIC_AI_T.Localized(),
                    Order = 4
                }),
                "Dynamic AI", ref failed, headers);

            DynamicAIRange = SetupSetting(performanceDefaultHeader, "Dynamic AI Range", 100f,
                new ConfigDescription(LocaleUtils.BEPINEX_DYNAMIC_AI_RANGE_D.Localized(),
                new AcceptableValueRange<float>(150f, 1000f), new ConfigurationManagerAttributes()
                {
                    Category = performanceHeader,
                    DispName = LocaleUtils.BEPINEX_DYNAMIC_AI_RANGE_T.Localized(),
                    Order = 3
                }),
                "Dynamic AI Range", ref failed, headers);

            DynamicAIRate = SetupSetting(performanceDefaultHeader, "Dynamic AI Rate", EDynamicAIRates.Medium,
                new ConfigDescription(LocaleUtils.BEPINEX_DYNAMIC_AI_RATE_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = performanceHeader,
                    DispName = LocaleUtils.BEPINEX_DYNAMIC_AI_RATE_T.Localized(),
                    Order = 2
                }),
                "Dynamic AI Rate", ref failed, headers);

            DynamicAIIgnoreSnipers = SetupSetting(performanceDefaultHeader, "Ignore Snipers", true,
                new ConfigDescription(LocaleUtils.BEPINEX_DYNAMIC_AI_NO_SNIPERS_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = performanceHeader,
                    DispName = LocaleUtils.BEPINEX_DYNAMIC_AI_NO_SNIPERS_T.Localized(),
                    Order = 1
                }),
                "Ignore Snipers", ref failed, headers);

            UseFikaGC = SetupSetting(performanceDefaultHeader, "Use Fika GC", false,
                new ConfigDescription(LocaleUtils.BEPINEX_FIKA_GC_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = performanceHeader,
                    DispName = LocaleUtils.BEPINEX_FIKA_GC_T.Localized(),
                    Order = 0
                }),
                "Use Fika GC", ref failed, headers);

            // Network

            string networkHeader = CleanConfigString(LocaleUtils.BEPINEX_H_NETWORK.Localized());
            string networkDefaultHeader = "Network";

            NativeSockets = SetupSetting(networkDefaultHeader, "Native Sockets", true,
                new ConfigDescription(LocaleUtils.BEPINEX_NATIVE_SOCKETS_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_NATIVE_SOCKETS_T.Localized(),
                    Order = 9
                }),
                "Native Sockets", ref failed, headers);

            ForceIP = SetupSetting(networkDefaultHeader, "Force IP", "",
                new ConfigDescription(LocaleUtils.BEPINEX_FORCE_IP_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_FORCE_IP_T.Localized(),
                    Order = 8
                }),
                "Force IP", ref failed, headers);

            ForceBindIP = SetupSetting(networkDefaultHeader, "Force Bind IP", "0.0.0.0",
                new ConfigDescription(LocaleUtils.BEPINEX_FORCE_BIND_IP_D.Localized(),
                new AcceptableValueList<string>(GetLocalAddresses()), new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_FORCE_BIND_IP_T.Localized(),
                    Order = 7
                }),
                "Force Bind IP", ref failed, headers);

            UDPPort = SetupSetting(networkDefaultHeader, "UDP Port", 25565,
                new ConfigDescription(LocaleUtils.BEPINEX_UDP_PORT_D.Localized(), new AcceptableValueRange<int>(0, 65535),
                tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_UDP_PORT_T.Localized(),
                    Order = 5
                }),
                "UDP Port", ref failed, headers);

            UseUPnP = SetupSetting(networkDefaultHeader, "Use UPnP", false,
                new ConfigDescription(LocaleUtils.BEPINEX_USE_UPNP_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_USE_UPNP_T.Localized(),
                    Order = 4
                }),
                "Use UPnP", ref failed, headers);

            UseNatPunching = SetupSetting(networkDefaultHeader, "Use NAT Punching", false,
                new ConfigDescription(LocaleUtils.BEPINEX_USE_NAT_PUNCH_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_USE_NAT_PUNCH_T.Localized(),
                    Order = 3
                }),
                "Use NAT Punching", ref failed, headers);

            ConnectionTimeout = SetupSetting(networkDefaultHeader, "Connection Timeout", 30,
                new ConfigDescription(LocaleUtils.BEPINEX_CONNECTION_TIMEOUT_D.Localized(),
                new AcceptableValueRange<int>(5, 60), new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_CONNECTION_TIMEOUT_T.Localized(),
                    Order = 2
                }),
                "Connection Timeout", ref failed, headers);

            SendRate = SetupSetting(networkDefaultHeader, "Send Rate", ESendRate.Medium,
                new ConfigDescription(LocaleUtils.BEPINEX_SEND_RATE_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_SEND_RATE_T.Localized(),
                    Order = 1
                }),
                "Send Rate", ref failed, headers);

            AllowVOIP = SetupSetting(networkDefaultHeader, "Allow VOIP", false,
                new ConfigDescription(LocaleUtils.BEPINEX_NET_VOIP_D.Localized(), tags: new ConfigurationManagerAttributes()
                {
                    Category = networkHeader,
                    DispName = LocaleUtils.BEPINEX_NET_VOIP_T.Localized(),
                    Order = 0
                }),
                "Allow VOIP", ref failed, headers);

            // Gameplay

            DisableBotMetabolism = SetupSetting("Gameplay", "Disable Bot Metabolism",
                false, new ConfigDescription(LocaleUtils.BEPINEX_DISABLE_BOT_METABOLISM_D.Localized(),
                tags: new ConfigurationManagerAttributes()
                {
                    Category = LocaleUtils.BEPINEX_H_GAMEPLAY.Localized(),
                    DispName = LocaleUtils.BEPINEX_DISABLE_BOT_METABOLISM_T.Localized(),
                    Order = 1
                }),
                "Disable Bot Metabolism", ref failed, headers);

            if (failed)
            {
                string headerString = string.Join(", ", headers);
                Singleton<PreloaderUI>.Instance.ShowErrorScreen(LocaleUtils.UI_LOCALE_ERROR_HEADER.Localized(),
                    string.Format(LocaleUtils.UI_LOCALE_ERROR_DESCRIPTION.Localized(), headerString));
                FikaLogger.LogError("SetupConfig: Headers failed: " + headerString);
            }

            SetupConfigEventHandlers();

            if (ForceBindIP.Value == "Disabled" && FikaBackendUtils.VPNIP != null)
            {
                ForceBindIP.Value = FikaBackendUtils.VPNIP.ToString();
                Logger.LogInfo($"Auto-detected VPN IP: {FikaBackendUtils.VPNIP}, setting as ForceBindIP");
            }
        }

        private void OfficialVersion_SettingChanged(object sender, EventArgs e)
        {
            FikaVersionLabel_Patch.UpdateVersionLabel();
        }

        private string[] GetLocalAddresses()
        {
            List<string> ips = [];
            ips.Add("Disabled");
            ips.Add("0.0.0.0");

            try
            {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (networkInterface.Description.Contains("Radmin VPN") || networkInterface.Description.Contains("ZeroTier"))
                        {
                            FikaBackendUtils.VPNIP = ip.Address;
                        }

                        if (!ip.IsDnsEligible)
                        {
                            continue;
                        }

                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string stringIp = ip.Address.ToString();
                            if (stringIp != "127.0.0.1")
                            {
                                ips.Add(stringIp);
                            }
                        }
                    }
                }

                LocalIPs = ips.Skip(1).ToArray();
                string allIps = string.Join(", ", LocalIPs);
                Logger.LogInfo($"Cached local IPs: {allIps}");
                return [.. ips];
            }
            catch (Exception ex)
            {
                Instance.FikaLogger.LogError("GetLocalAddresses: " + ex.Message);
                return [.. ips];
            }
        }

        private void DisableSPTPatches()
        {
            // Disable these as they interfere with Fika
            new VersionLabelPatch().Disable();
            new AmmoUsedCounterPatch().Disable();
            new ArmorDamageCounterPatch().Disable();
            new ScavRepAdjustmentPatch().Disable();
            new GetProfileAtEndOfRaidPatch().Disable();
            new ScavExfilPatch().Disable();
            new SendPlayerScavProfileToServerAfterRaidPatch().Disable();
            new MatchStartServerLocationPatch().Disable();
            new QuestAchievementRewardInRaidPatch().Disable();
        }

        public void FixSPTBugPatches()
        {
            if (ModHandler.SPTCoreVersion.ToString() == "3.11.0")
            {
                // Empty, for now ;)
            }
        }

        public enum EDynamicAIRates
        {
            Low,
            Medium,
            High
        }

        public enum EPingSound
        {
            SubQuestComplete,
            InsuranceInsured,
            ButtonClick,
            ButtonHover,
            InsuranceItemInsured,
            MenuButtonBottom,
            ErrorMessage,
            InspectWindow,
            InspectWindowClose,
            MenuEscape,
        }

        /// <summary>
        /// The SendRate of the <see cref="Networking.IFikaNetworkManager"/>
        /// </summary>
        public enum ESendRate
        {
            Low,
            Medium,
            High
        }

        [Flags]
        public enum EQuestSharingTypes
        {
            Kills = 1,
            Item = 2,
            Location = 4,
            PlaceBeacon = 8,

            All = Kills | Item | Location | PlaceBeacon
        }
    }
}
