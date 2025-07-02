﻿using EFT.UI.Matchmaker;
using Fika.Core.Coop.Utils;
using Fika.Core.Patching;
using Newtonsoft.Json;
using System;
using System.Reflection;

namespace Fika.Core.UI.Patches.MatchmakerAcceptScreen
{
    public class MatchmakerAcceptScreen_Awake_Patch : FikaPatch
    {
        [Serializable]
        private class ServerStatus
        {
            [JsonProperty("ip")]
            public string Ip { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }
        }

        protected override MethodBase GetTargetMethod() => typeof(MatchMakerAcceptScreen).GetMethod("Awake");

        [PatchPrefix]
        private static bool PatchPrefix(MatchMakerAcceptScreen __instance, PlayersRaidReadyPanel ____playersRaidReadyPanel, MatchMakerGroupPreview ____groupPreview)
        {
            FikaBackendUtils.MatchMakerAcceptScreenInstance = __instance;
            FikaBackendUtils.PlayersRaidReadyPanel = ____playersRaidReadyPanel;
            FikaBackendUtils.MatchMakerGroupPreview = ____groupPreview;
            return true;
        }

    }
}









