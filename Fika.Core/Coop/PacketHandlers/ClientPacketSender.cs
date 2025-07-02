// © 2025 Lacyway All Rights Reserved

using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.UI;
using Fika.Core.Bundles;
using Fika.Core.Coop.ClientClasses;
using Fika.Core.Coop.Factories;
using Fika.Core.Coop.FreeCamera;
using Fika.Core.Coop.GameMode;
using Fika.Core.Coop.Players;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace Fika.Core.Coop.PacketHandlers
{
    public class ClientPacketSender : MonoBehaviour, IPacketSender
    {
        public bool Enabled { get; set; }
        public bool SendState { get; set; }
        public FikaServer Server { get; set; }
        public FikaClient Client { get; set; }

        private CoopPlayer player;
        private PlayerStatePacket state;
        private bool IsMoving
        {
            get
            {
                return player.CurrentManagedState.Name is not (EPlayerState.Idle
                    or EPlayerState.IdleWeaponMounting
                    or EPlayerState.ProneIdle);
            }
        }


        private bool CanPing = false;

        private DateTime lastPingTime;
        private float updateRate;
        private float updateCount;
        private float updatesPerTick;

        public static ClientPacketSender Create(CoopPlayer player)
        {
            ClientPacketSender sender = player.gameObject.AddComponent<ClientPacketSender>();
            sender.player = player;
            sender.Client = Singleton<FikaClient>.Instance;
            sender.enabled = false;
            sender.lastPingTime = DateTime.Now;
            sender.updateRate = sender.Client.SendRate;
            sender.updateCount = 0;
            sender.updatesPerTick = 1f / sender.updateRate;
            sender.state = new(player.NetId);
            return sender;
        }

        public void Init()
        {
            enabled = true;
            Enabled = true;
            SendState = true;
            if (player.AbstractQuestControllerClass is CoopClientSharedQuestController sharedQuestController)
            {
                sharedQuestController.LateInit();
            }
        }

        public void SendPacket<T>(ref T packet, bool forced = false) where T : INetSerializable
        {
            if (!Enabled && !forced)
            {
                return;
            }

            Client.SendData(ref packet, DeliveryMethod.ReliableOrdered);
        }

        protected void Update()
        {
            if (!SendState)
            {
                return;
            }

            updateCount += Time.unscaledDeltaTime;
            if (updateCount >= updatesPerTick)
            {
                SendPlayerState();
                updateCount -= updatesPerTick;
            }
        }

        private void SendPlayerState()
        {
            state.UpdateData(player, IsMoving);
            Client.SendData(ref state, DeliveryMethod.Unreliable);
        }

        protected void LateUpdate()
        {
            if (CanPing)
            {
                SendPing();
            }
        }

        private void SendPing()
        {
            Transform originTransform;
            Ray sourceRaycast;
            FreeCameraController freeCamController = Singleton<FreeCameraController>.Instance;
            if (freeCamController != null && freeCamController.IsScriptActive)
            {
                originTransform = freeCamController.CameraMain.gameObject.transform;
                sourceRaycast = new(originTransform.position + originTransform.forward / 2f, originTransform.forward);
            }
            else if (player.HealthController.IsAlive)
            {
                if (player.HandsController is CoopClientFirearmController controller && controller.IsAiming)
                {
                    sourceRaycast = new(controller.FireportPosition, controller.WeaponDirection);
                }
                else
                {
                    originTransform = player.CameraPosition;
                    sourceRaycast = new(originTransform.position + originTransform.forward / 2f, player.LookDirection);
                }
            }
            else
            {
                return;
            }
            int layer = LayerMask.GetMask(["HighPolyCollider", "Interactive", "Deadbody", "Player", "Loot", "Terrain"]);
        }

        public void DestroyThis()
        {
            if (Server != null)
            {
                Server = null;
            }
            if (Client != null)
            {
                Client = null;
            }
            Destroy(this);
        }
    }
}
