﻿using Audio.Vehicles.BTR;
using Comfort.Common;
using EFT;
using EFT.Vehicle;
using Fika.Core.Coop.Players;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using Fika.Core.Patching;
using HarmonyLib;
using LiteNetLib;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Fika.Core.Coop.Patches
{
    public class BTRView_GoOut_Patch : FikaPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BTRView).GetMethod(nameof(BTRView.GoOut));
        }

        [PatchPrefix]
        public static bool Prefix(BTRView __instance, Player player, BTRSide side, bool fast, byte placeId, ref Task __result)
        {
            if (player is ObservedCoopPlayer observedPlayer)
            {
                __result = ObservedGoOut(__instance, observedPlayer, side, fast);
                Singleton<IFikaNetworkManager>.Instance.ObservedCoopPlayers.Add(observedPlayer);
                return false;
            }

            if (player.IsYourPlayer)
            {
                CoopPlayer myPlayer = (CoopPlayer)player;
                myPlayer.PacketSender.SendState = true;
                if (FikaBackendUtils.IsServer)
                {
                    BTRInteractionPacket packet = new(myPlayer.NetId)
                    {
                        Data = new()
                        {
                            HasInteraction = true,
                            InteractionType = EInteractionType.GoOut,
                            SideId = __instance.GetSideId(side),
                            SlotId = placeId,
                            Fast = fast
                        }
                    };

                    Singleton<FikaServer>.Instance.SendDataToAll(ref packet, DeliveryMethod.ReliableOrdered);
                }
            }

            return true;
        }

        private static async Task ObservedGoOut(BTRView view, ObservedCoopPlayer observedPlayer, BTRSide side, bool fast)
        {
            try
            {
                CancellationToken cancellationToken = view.method_12(observedPlayer);
                observedPlayer.BtrState = EPlayerBtrState.GoOut;
                BtrSoundController soundController = Traverse.Create(view).Field<BtrSoundController>("_soundController").Value;
                if (soundController != null)
                {
                    soundController.UpdateBtrAudioRoom(EnvironmentType.Outdoor, observedPlayer);
                }
                await view.method_16(observedPlayer.MovementContext.PlayerAnimator, fast, true, cancellationToken);
                ValueTuple<Vector3, Vector3> valueTuple = side.GoOutPoints();
                side.ApplyPlayerRotation(observedPlayer.MovementContext, valueTuple.Item1, valueTuple.Item2 + Vector3.up * 1.9f);
                observedPlayer.BtrState = EPlayerBtrState.Outside;
                observedPlayer.CharacterController.isEnabled = true;
                side.RemovePassenger(observedPlayer);
                observedPlayer.MovementContext.IsAxesIgnored = false;
                observedPlayer.IsInBufferZone = false;
            }
            catch (Exception ex)
            {
                FikaPlugin.Instance.FikaLogger.LogError("BTRView_GoOut_Patch: " + ex.Message);
            }
        }
    }
}
