using EFT.UI;
using HarmonyLib;
using SPT.Common.Http;
using SPT.Common.Utils;
using SPT.Custom.Models;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Fika.Core.EssentialPatches
{
	/// <summary>
	/// Originally developed by SPT team
	/// </summary>
	public class FikaVersionLabel_Patch : ModulePatch
	{
		private static string versionLabel;
		private static Traverse versionNumberTraverse;
		private static string fikaVersion;
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

			fikaVersion = Assembly.GetAssembly(typeof(FikaVersionLabel_Patch)).GetName().Version.ToString();

			Traverse preloaderUiTraverse = Traverse.Create(MonoBehaviourSingleton<PreloaderUI>.Instance);

			preloaderUiTraverse.Field("_alphaVersionLabel").Property("LocalizationKey").SetValue("{0}");

			versionNumberTraverse = Traverse.Create(__result);

			officialVersion = versionNumberTraverse.Field<string>("Major").Value;

			UpdateVersionLabel();
		}

		public static void UpdateVersionLabel()
		{
			Traverse preloaderUiTraverse = Traverse.Create(MonoBehaviourSingleton<PreloaderUI>.Instance);
#if DEBUG
				preloaderUiTraverse.Field("string_2").SetValue($"MTC-C {fikaVersion} (DEBUG) | {versionLabel}");
#else
				preloaderUiTraverse.Field("string_2").SetValue($"{ColorizeText(EColor.BLUE, "MTC-C")} | {fikaVersion}");
#endif
				versionNumberTraverse.Field("Major").SetValue($"{fikaVersion} {versionLabel}");

			// Game mode
			preloaderUiTraverse.Field("string_4").SetValue("Competitive PvP");
			// Update version label
			// preloaderUiTraverse.Method("method_6").GetValue();
		}
	}
}