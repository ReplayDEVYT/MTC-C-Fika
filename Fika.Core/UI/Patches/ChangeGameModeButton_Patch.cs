using EFT.UI;
using SPT.Reflection.Patching;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Fika.Core.UI.FikaUIGlobals;

namespace Fika.Core.UI.Patches
{
	public class ChangeGameModeButton_Patch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(ChangeGameModeButton).GetMethod(nameof(ChangeGameModeButton.Show));
		}

		[PatchPrefix]
		private static bool PrefixChange(TextMeshProUGUI ____buttonLabel, TextMeshProUGUI ____buttonDescription, Image ____buttonDescriptionIcon,
			GameObject ____availableState)
		{
			____buttonLabel.text = "Competitive PvP";
			____buttonDescription.text = $"MTC-C is {ColorizeText(EColor.BLUE, "PvP.")} To change gamemodes, switch servers.";
			____buttonDescriptionIcon.gameObject.SetActive(false);
			____availableState.SetActive(true);
			return false;
		}
	}
}