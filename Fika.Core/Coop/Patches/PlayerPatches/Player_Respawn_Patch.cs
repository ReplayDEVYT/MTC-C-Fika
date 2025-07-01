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
using System.Linq.Expressions;
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

        private static DateTime LastRespawn = DateTime.Now;

        private static bool HasPainkiller = false;

        private static ISpawnPoint spawnpoint = null;

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

            if (spawnpoint == null)
            {
                try
                {
                    spawnpoint = CoopGame.Instance.SpawnSystem.SelectSpawnPoint(ESpawnCategory.Player, profile.Info.Side, null, null, null, null, profile.Id);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error selecting spawn point: {ex}");
                }
            }

            if (player.IsAI) { return false; }

            if (!player.IsYourPlayer) { return false; }

            try
            {
                if (DateTime.Now - LastMessage > TimeSpan.FromSeconds(2))
                {
                    Respawns++;
                    NotificationManagerClass.DisplayMessageNotification($"Respawns: {Respawns}", ENotificationDurationType.Default, ENotificationIconType.Alert, UnityEngine.Color.white);
                    Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.PlayerIsDead);
                    LastMessage = DateTime.Now;
                }

                if (DateTime.Now - LastRespawn > TimeSpan.FromSeconds(2))
                {
                    player.Transform.position = spawnpoint.Position;
                    player.Transform.rotation = spawnpoint.Rotation;

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

                    Task.Delay(50).ContinueWith(T =>
                    {
                        foreach (EBodyPart BodyPart in Enum.GetValues(typeof(EBodyPart))) // Remove negative effects
                        {
                            __instance.method_18(BodyPart, (ignore) => true);
                        }

                        __instance.RestoreFullHealth();
                        __instance.DoPainKiller();
                        __instance.DoContusion(2, 10);
                    });
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
