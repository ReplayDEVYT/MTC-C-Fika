using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Game.Spawning;
using EFT.HealthSystem;
using EFT.InventoryLogic;
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
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;
using BepInEx.Logging;

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

        private static DateTime LastRespawn = DateTime.Now;

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

            ISpawnPoint spawnpoint = null;

            new RespawnHelper().RepairAll(player.InventoryController);

            try
            {
                spawnpoint = CoopGame.Instance.SpawnSystem.SelectSpawnPoint(ESpawnCategory.Player, profile.Info.Side, null, null, null, null, profile.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error selecting spawn point: {ex}");
            }

            if (!player.IsYourPlayer || player.IsAI) { return false; }

            try
            {

                NotifyRespawn();

                player.Transform.position = spawnpoint.Position;
                player.Transform.rotation = spawnpoint.Rotation;

                PlayerOnDeadFixes(player);

                RespawnHelper.DelayedAction(() =>
                {
                    foreach (EBodyPart BodyPart in Enum.GetValues(typeof(EBodyPart)))
                    {
                        __instance.method_18(BodyPart, (ignore) => true);
                    }
                    __instance.RestoreFullHealth();
                    __instance.DoPainKiller();
                    __instance.DoContusion(2, 10);
                }, 0.05f);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in Player_Respawn_Patch: {ex}");
            }

            return false;
        }

        private static void NotifyRespawn()
        {
            if (DateTime.Now - LastMessage > TimeSpan.FromSeconds(2))
            {
                Respawns++;
                NotificationManagerClass.DisplayMessageNotification($"Respawns: {Respawns}", ENotificationDurationType.Default, ENotificationIconType.Alert, UnityEngine.Color.white);
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.PlayerIsDead);
                LastMessage = DateTime.Now;
            }
        }

        private static void PlayerOnDeadFixes(Player player)
        {
            player.MovementContext.ReleaseDoorIfInteractingWithOne();

            if (player.MovementContext.StationaryWeapon != null)
            {
                player.MovementContext.StationaryWeapon.Show();
                player.ReleaseHand();
            }

            GClass3756.ReleaseBeginSample("Player.OnDead.SoundWork", "OnDead");
            try
            {
                player.Speaker.Play(EPhraseTrigger.OnDeath, player.HealthStatus, demand: true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in Player.OnDead.SoundWork: {ex}");
            }

            player.InventoryController.UnregisterView(player);
            player.PlayDeathSound();
        }
    }
}
