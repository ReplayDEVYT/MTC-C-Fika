using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Game.Spawning;
using EFT.HealthSystem;
using EFT.UI;
using Fika.Core.Coop.Components;
using Fika.Core.Coop.GameMode;
using Fika.Core.Networking.Websocket;
using Fika.Core.Patching;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Drawing;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;

namespace Fika.Core.Coop.Patches
{
    /// <summary>
    /// Stops players from dying and respawns them.
    /// </summary>
    public class Player_Respawn_Patch : FikaPatch
    {
        static int Respawns = 0;
        static int MaxRespawns = 0; // not implemented
        static bool ArmorRepairEnabled = false;

        private static DateTime LastMessage = DateTime.Now;

        protected override MethodBase GetTargetMethod()
        {
            //Check for gclass increments
            return typeof(ActiveHealthController).GetMethod(nameof(ActiveHealthController.Kill));
        }

        [PatchPrefix]
        private static bool Prefix(ActiveHealthController __instance)
        {
            Player player = __instance.Player;

            Profile profile = player.Profile;

            if (player.IsAI)
            {
                return true;
            }

            try
            {
                ISpawnPoint spawnpoint = CoopGame.Instance.SpawnSystem.SelectSpawnPoint(ESpawnCategory.Player, profile.Info.Side, null, null, null, null, profile.Id);

                player.Transform.position = spawnpoint.Position;
                player.Transform.rotation = spawnpoint.Rotation;

                Task.Delay(500).ContinueWith(T =>
                {
                    foreach (EBodyPart BodyPart in Enum.GetValues(typeof(EBodyPart))) // Remove negative effects
                    {
                        __instance.method_18(BodyPart, (ignore) => true);
                    }

                    __instance.RestoreFullHealth();
                    __instance.DoPainKiller();
                    __instance.DoContusion(2, 10);
                });

                Respawns++;

                if (DateTime.Now - LastMessage > TimeSpan.FromSeconds(1))
                {
                    NotificationManagerClass.DisplayMessageNotification("Respawns: "+ Respawns, ENotificationDurationType.Default, ENotificationIconType.Alert, UnityEngine.Color.white);
                    Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.PlayerIsDead);
                    LastMessage = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in Player_Respawn_Patch: {ex}");
            }

            return false;
        }
    }
}
