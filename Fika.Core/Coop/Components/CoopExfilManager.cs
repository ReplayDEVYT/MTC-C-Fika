﻿using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Interactive.SecretExfiltrations;
using Fika.Core.Coop.GameMode;
using Fika.Core.Coop.Players;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Fika.Core.Networking.GenericSubPackets;
using static Fika.Core.Networking.SubPacket;

namespace Fika.Core.Coop.Components
{
    internal class CoopExfilManager : MonoBehaviour
    {
        private CoopGame game;
        private List<ExtractionPlayerHandler> playerHandlers;
        private List<ExfiltrationPoint> countdownPoints;
        private ExfiltrationPoint[] exfiltrationPoints;
        private SecretExfiltrationPoint[] secretExfiltrationPoints;

        protected void Awake()
        {
            game = gameObject.GetComponent<CoopGame>();
            playerHandlers = [];
            countdownPoints = [];
            exfiltrationPoints = [];
            secretExfiltrationPoints = [];
        }

        protected void Update()
        {
            if (exfiltrationPoints == null || secretExfiltrationPoints == null)
            {
                return;
            }

            for (int i = 0; i < playerHandlers.Count; i++)
            {
                ExtractionPlayerHandler playerHandler = playerHandlers[i];
                if (playerHandler.startTime + playerHandler.point.Settings.ExfiltrationTime - game.PastTime <= 0)
                {
                    playerHandlers.Remove(playerHandler);
                    game.ExitLocation = playerHandler.point.Settings.Name;
                    game.Extract(playerHandler.player, playerHandler.point);
                }
            }

            for (int i = 0; i < countdownPoints.Count; i++)
            {
                ExfiltrationPoint exfiltrationPoint = countdownPoints[i];
                if (game.PastTime - exfiltrationPoint.ExfiltrationStartTime > exfiltrationPoint.Settings.ExfiltrationTime)
                {
                    foreach (Player player in exfiltrationPoint.Entered.ToArray())
                    {
                        if (player == null)
                        {
                            continue;
                        }

                        if (!player.IsYourPlayer)
                        {
                            continue;
                        }

                        if (!player.HealthController.IsAlive)
                        {
                            continue;
                        }

                        if (!exfiltrationPoint.UnmetRequirements(player).Any())
                        {
                            game.ExitLocation = exfiltrationPoint.Settings.Name;
                            game.Extract((CoopPlayer)player, exfiltrationPoint);
                        }
                    }

                    exfiltrationPoint.ExternalSetStatus(EExfiltrationStatus.NotPresent);
                    countdownPoints.Remove(exfiltrationPoint);
                }
            }
        }

        public void Run(ExfiltrationPoint[] exfilPoints, SecretExfiltrationPoint[] secretExfilPoints)
        {
            foreach (ExfiltrationPoint exfiltrationPoint in exfilPoints)
            {
                exfiltrationPoint.OnStartExtraction += ExfiltrationPoint_OnStartExtraction;
                exfiltrationPoint.OnCancelExtraction += ExfiltrationPoint_OnCancelExtraction;
                exfiltrationPoint.OnStatusChanged += ExfiltrationPoint_OnStatusChanged;
                exfiltrationPoint.OnStatusChanged += game.method_9;
                game.UpdateExfiltrationUi(exfiltrationPoint, false, true);
                if (FikaPlugin.Instance.DynamicVExfils && exfiltrationPoint.Settings.PlayersCount > 0 && exfiltrationPoint.Settings.PlayersCount < FikaBackendUtils.HostExpectedNumberOfPlayers)
                {
                    exfiltrationPoint.Settings.PlayersCount = FikaBackendUtils.HostExpectedNumberOfPlayers;
                }
            }

            foreach (SecretExfiltrationPoint secretExfiltrationPoint in secretExfilPoints)
            {
                secretExfiltrationPoint.OnStartExtraction += ExfiltrationPoint_OnStartExtraction;
                secretExfiltrationPoint.OnCancelExtraction += ExfiltrationPoint_OnCancelExtraction;
                secretExfiltrationPoint.OnStatusChanged += ExfiltrationPoint_OnStatusChanged;
                secretExfiltrationPoint.OnStatusChanged += game.method_9;
                secretExfiltrationPoint.OnStatusChanged += game.ShowNewSecretExit;
                game.UpdateExfiltrationUi(secretExfiltrationPoint, false, true);
                secretExfiltrationPoint.OnPointFoundEvent += SecretExfiltrationPoint_OnPointFoundEvent;
            }

            exfiltrationPoints = exfilPoints;
            secretExfiltrationPoints = secretExfilPoints;
        }

        private void SecretExfiltrationPoint_OnPointFoundEvent(string exitName, bool sharedExit)
        {
            CoopPlayer mainPlayer = (CoopPlayer)Singleton<GameWorld>.Instance.MainPlayer;
            GenericPacket packet = new()
            {
                NetId = mainPlayer.NetId,
                Type = EGenericSubPacketType.SecretExfilFound,
                SubPacket = new SecretExfilFound(mainPlayer.GroupId, exitName)
            };

            mainPlayer.PacketSender.SendPacket(ref packet);
        }

        public void Stop()
        {
            playerHandlers.Clear();
            countdownPoints.Clear();

            if (exfiltrationPoints != null)
            {
                foreach (ExfiltrationPoint exfiltrationPoint in exfiltrationPoints)
                {
                    exfiltrationPoint.OnStartExtraction -= ExfiltrationPoint_OnStartExtraction;
                    exfiltrationPoint.OnCancelExtraction -= ExfiltrationPoint_OnCancelExtraction;
                    exfiltrationPoint.OnStatusChanged -= ExfiltrationPoint_OnStatusChanged;
                    exfiltrationPoint.OnStatusChanged -= game.method_9;
                    exfiltrationPoint.Disable();
                }
            }

            if (secretExfiltrationPoints != null)
            {
                foreach (SecretExfiltrationPoint secretExfiltrationPoint in secretExfiltrationPoints)
                {
                    secretExfiltrationPoint.OnStartExtraction -= ExfiltrationPoint_OnStartExtraction;
                    secretExfiltrationPoint.OnCancelExtraction -= ExfiltrationPoint_OnCancelExtraction;
                    secretExfiltrationPoint.OnStatusChanged -= ExfiltrationPoint_OnStatusChanged;
                    secretExfiltrationPoint.OnStatusChanged -= game.method_9;
                    secretExfiltrationPoint.OnStatusChanged -= game.ShowNewSecretExit;
                    secretExfiltrationPoint.OnPointFoundEvent -= SecretExfiltrationPoint_OnPointFoundEvent;
                    secretExfiltrationPoint.Disable();
                }
            }
        }

        public void UpdateExfilPointFromServer(ExfiltrationPoint point, bool enable)
        {
            if (enable)
            {
                point.OnStartExtraction += ExfiltrationPoint_OnStartExtraction;
                point.OnCancelExtraction += ExfiltrationPoint_OnCancelExtraction;
                point.OnStatusChanged += ExfiltrationPoint_OnStatusChanged;
            }
            else
            {
                point.OnStartExtraction -= ExfiltrationPoint_OnStartExtraction;
                point.OnCancelExtraction -= ExfiltrationPoint_OnCancelExtraction;
                point.OnStatusChanged -= ExfiltrationPoint_OnStatusChanged;
            }
        }

        private void ExfiltrationPoint_OnCancelExtraction(ExfiltrationPoint point, Player player)
        {
            if (!player.IsYourPlayer)
            {
                return;
            }

            ExtractionPlayerHandler extractionPlayerHandler = playerHandlers.FirstOrDefault(x => x.player == player);
            if (extractionPlayerHandler != null)
            {
                playerHandlers.Remove(extractionPlayerHandler);
            }
        }

        private void ExfiltrationPoint_OnStartExtraction(ExfiltrationPoint point, Player player)
        {
            if (!player.IsYourPlayer)
            {
                return;
            }

            if (playerHandlers.All(x => x.player != player))
            {
                playerHandlers.Add(new(player, point, game.PastTime));
            }
        }

        private void ExfiltrationPoint_OnStatusChanged(ExfiltrationPoint point, EExfiltrationStatus prevStatus)
        {
            bool isCounting = countdownPoints.Contains(point);
            if (isCounting && point.Status != EExfiltrationStatus.Countdown)
            {
                point.ExfiltrationStartTime = -100;
                countdownPoints.Remove(point);
            }

            if (!isCounting && point.Status == EExfiltrationStatus.Countdown)
            {
                if (point.ExfiltrationStartTime is <= 0 and > -90)
                {
                    point.ExfiltrationStartTime = game.PastTime;

                    CoopPlayer mainPlayer = (CoopPlayer)Singleton<GameWorld>.Instance.MainPlayer;
                    GenericPacket packet = new()
                    {
                        NetId = mainPlayer.NetId,
                        Type = EGenericSubPacketType.ExfilCountdown,
                        SubPacket = new ExfilCountdown(point.Settings.Name, point.ExfiltrationStartTime)
                    };

                    mainPlayer.PacketSender.SendPacket(ref packet);
                }
                countdownPoints.Add(point);
            }
        }

        private class ExtractionPlayerHandler(Player player, ExfiltrationPoint point, float startTime)
        {
            public CoopPlayer player = (CoopPlayer)player;
            public ExfiltrationPoint point = point;
            public float startTime = startTime;
        }
    }
}
