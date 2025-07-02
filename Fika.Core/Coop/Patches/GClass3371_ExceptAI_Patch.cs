﻿using Comfort.Common;
using EFT;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using Fika.Core.Patching;
using System.Collections.Generic;
using System.Reflection;

namespace Fika.Core.Coop.Patches
{
    public class GClass3371_ExceptAI_Patch : FikaPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass3371).GetMethod(nameof(GClass3371.ExceptAI));
        }

        [PatchPrefix]
        public static bool Prefix(IEnumerable<IPlayer> persons, ref IEnumerable<IPlayer> __result)
        {
            if (persons != null)
            {
                if (FikaBackendUtils.IsHeadless)
                {
                    List<IPlayer> humanPlayers = [.. Singleton<IFikaNetworkManager>.Instance.CoopHandler.HumanPlayers];
                    humanPlayers.Remove(Singleton<GameWorld>.Instance.MainPlayer);
                    __result = humanPlayers;
                    return false;
                }

                __result = Singleton<IFikaNetworkManager>.Instance.CoopHandler.HumanPlayers;
                return false;
            }

            return true;
        }
    }
}
