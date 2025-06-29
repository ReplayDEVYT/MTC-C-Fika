using EFT;
using Fika.Core.Coop.ClientClasses;
using Fika.Core.Coop.Components;
using Fika.Core.Coop.HostClasses;
using Fika.Core.Coop.Utils;
using Fika.Core.Patching;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Fika.Core.Coop.Patches
{
    public class NewGamePatch : FikaPatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
        [PatchPrefix]
        internal static void PatchPrefix()
        {
            ArmorRepair.Enable();
        }
    }
}