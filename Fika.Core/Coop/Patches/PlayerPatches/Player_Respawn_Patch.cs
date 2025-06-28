using EFT;
using EFT.Game.Spawning;
using EFT.HealthSystem;
using Fika.Core.Coop.GameMode;
using Fika.Core.Patching;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Fika.Core.Coop.Patches
{
    /// <summary>
    /// Stops players from dying and respawns them.
    /// </summary>
    public class Player_Respawn_Patch : FikaPatch
    {
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
            });

            return false;
        }
    }
}
