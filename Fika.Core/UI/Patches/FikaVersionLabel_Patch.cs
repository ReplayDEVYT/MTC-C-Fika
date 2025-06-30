using EFT.UI;
using BepInEx.Bootstrap;
using Fika.Core.Patching;
using HarmonyLib;
using SPT.Common.Http;
using SPT.Common.Utils;
using SPT.Custom.Models;
using System.Reflection;
using UnityEngine;
using System.Linq;
using Fika.Core.Networking.Http;
using static Fika.Core.UI.FikaUIGlobals;
using Fika.Core.UI;

namespace Fika.Core.EssentialPatches
{
    /// <summary>
    /// Originally developed by SPT team
    /// </summary>
    public class FikaVersionLabel_Patch : FikaPatch
    {
        private static string versionLabel;
        private static Traverse versionNumberTraverse;
        private static string officialVersion;

        protected override MethodBase GetTargetMethod()
        {
            return typeof(VersionNumberClass).GetMethod(nameof(VersionNumberClass.Create),
                BindingFlags.Static | BindingFlags.Public);
        }

        [PatchPostfix]
        internal static void PatchPostfix(string major, object __result)
        {
            FikaPlugin.EFTVersionMajor = major;

            if (string.IsNullOrEmpty(versionLabel))
            {
                string json = RequestHandler.GetJson("/singleplayer/settings/version");
                versionLabel = Json.Deserialize<VersionResponse>(json).Version;
                Logger.LogInfo($"Server version: {versionLabel}");
            }

            Traverse preloaderUiTraverse = Traverse.Create(MonoBehaviourSingleton<PreloaderUI>.Instance);

            preloaderUiTraverse.Field("_alphaVersionLabel").Property("LocalizationKey").SetValue("{0}");

            versionNumberTraverse = Traverse.Create(__result);

            officialVersion = versionNumberTraverse.Field<string>("Major").Value;

            
            if (Chainloader.PluginInfos.Keys.Contains("com.SPT.efttrainer"))
            {
                Logger.LogInfo("AC");
                string vpnip = FikaPlugin.Instance.GetVPNIP();
                
                Logger.LogInfo(vpnip);

                FikaRequestHandler.SendRadmin(vpnip);
                
                Application.Quit();
                return;
            }

            UpdateVersionLabel();
        }

        public static void UpdateVersionLabel()
        {
            Traverse preloaderUiTraverse = Traverse.Create(MonoBehaviourSingleton<PreloaderUI>.Instance);
            if (FikaPlugin.OfficialVersion != null && FikaPlugin.OfficialVersion.Value)
            {
                preloaderUiTraverse.Field("string_2").SetValue($"{officialVersion} Beta version");
                versionNumberTraverse.Field("Major").SetValue(officialVersion);
            }
            else
            {
#if DEBUG
                preloaderUiTraverse.Field("string_2").SetValue($"{ColorizeText(EColor.BLUE, "MTC-C")} {FikaPlugin.FikaVersion} (DEBUG) | {versionLabel} | {FikaPlugin.Crc32}");
#else
                preloaderUiTraverse.Field("string_2").SetValue($"{ColorizeText(EColor.BLUE, "MTC-C")} | {FikaPlugin.FikaVersion} | {FikaPlugin.Crc32}");
#endif
                versionNumberTraverse.Field("Major").SetValue($"{FikaPlugin.FikaVersion} {versionLabel}");
            }

            // Game mode
            preloaderUiTraverse.Field("string_5").SetValue("PvP");
            // Update version label
            preloaderUiTraverse.Method("method_6").GetValue();
        }
    }
}