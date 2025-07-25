﻿using Comfort.Common;
using EFT;
using EFT.Console.Core;
using EFT.InventoryLogic;
using EFT.UI;
using Fika.Core.Coop.Components;
using Fika.Core.Coop.GameMode;
using Fika.Core.Coop.HostClasses;
using Fika.Core.Coop.Players;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fika.Core.Console
{
    public class FikaCommands
    {
#if DEBUG
        [ConsoleCommand("bring", "", null, "Teleports all AI to yourself as the host", [])]
        public static void Bring()
        {
            if (!CheckForGame())
            {
                return;
            }

            if (CoopHandler.TryGetCoopHandler(out CoopHandler coopHandler))
            {
                if (FikaBackendUtils.IsServer)
                {
                    int count = 0;
                    foreach (CoopPlayer player in coopHandler.Players.Values)
                    {
                        BifacialTransform myPosition = coopHandler.MyPlayer.Transform;
                        if (player.IsAI && player.HealthController.IsAlive)
                        {
                            count++;
                            player.Teleport(myPosition.Original.position + myPosition.Original.forward * 2);
                        }
                    }
                    ConsoleScreen.Log($"Teleported {count} AI to host.");
                }
                else
                {
                    ConsoleScreen.LogWarning("You are not the host");
                }
            }
            else
            {
                ConsoleScreen.LogWarning("Could not find CoopHandler.");
            }
        }

        [ConsoleCommand("god", "", null, "Set god mode on/off", [])]
        public static void God([ConsoleArgument(false, "true or false to toggle god mode")] bool state)
        {
            if (!CheckForGame())
            {
                return;
            }

            if (CoopHandler.TryGetCoopHandler(out CoopHandler coopHandler))
            {
                int value = state ? 0 : 1;
                coopHandler.MyPlayer.ActiveHealthController.SetDamageCoeff(value);
                if (value == 0)
                {
                    ConsoleScreen.Log("God mode on");
                }
                else
                {
                    ConsoleScreen.Log("God mode off");
                }
            }
            else
            {
                ConsoleScreen.LogWarning("Could not find CoopHandler.");
            }
        }

        [ConsoleCommand("extract", "", null, "Extract from raid", [])]
        public static void Extract()
        {
            CoopGame coopGame = CoopGame.Instance;

            if (coopGame == null)
            {
                ConsoleScreen.LogWarning("You are not in a game.");
                return;
            }

            if (coopGame.Status != GameStatus.Started)
            {
                ConsoleScreen.LogWarning("Game is not running.");
                return;
            }

            CoopPlayer localPlayer = (CoopPlayer)Singleton<GameWorld>.Instance.MainPlayer;

            coopGame.Extract(localPlayer, null);
        }

        [ConsoleCommand("despawnallai", "", null, "Despawns all AI bots", [])]
        public static void DespawnAllAI()
        {
            CoopGame coopGame = CoopGame.Instance;
            if (coopGame != null)
            {
                if (!FikaBackendUtils.IsServer)
                {
                    ConsoleScreen.LogWarning("You are not the host.");
                    return;
                }

                if (CoopHandler.TryGetCoopHandler(out CoopHandler coopHandler))
                {
                    foreach (Player bot in coopGame.Bots.Values)
                    {
                        if (bot.AIData.BotOwner == null)
                        {
                            continue;
                        }

                        ConsoleScreen.Log($"Despawning: {bot.Profile.Nickname}");

                        coopGame.DespawnBot(coopHandler, bot);
                    }
                    return;
                }

                ConsoleScreen.LogError("Could not find CoopHandler!");
            }
        }

        [ConsoleCommand("stoptimer", "", null, "Stops the game timer", [])]
        public static void StopTimer()
        {
            CoopGame coopGame = CoopGame.Instance;
            if (coopGame != null)
            {
                if (coopGame.GameTimer.Status == GameTimerClass.EGameTimerStatus.Stopped)
                {
                    ConsoleScreen.LogError("GameTimer is already stopped at: " + coopGame.GameTimer.PastTime.ToString());
                    return;
                }
                coopGame.GameTimer.TryStop();
                if (coopGame.GameTimer.Status == GameTimerClass.EGameTimerStatus.Stopped)
                {
                    ConsoleScreen.Log("GameTimer stopped at: " + coopGame.GameTimer.PastTime.ToString());
                }
            }
        }

        [ConsoleCommand("goToBTR", "", null, "Teleports you to the BTR if active", [])]
        public static void GoToBTR()
        {
            CoopGame coopGame = CoopGame.Instance;
            if (coopGame != null)
            {
                GameWorld gameWorld = coopGame.GameWorld_0;
                if (gameWorld != null)
                {
                    if (gameWorld.BtrController != null)
                    {
                        Transform btrTransform = Traverse.Create(gameWorld.BtrController.BtrView).Field<Transform>("_cachedTransform").Value;
                        if (btrTransform != null)
                        {
                            Player myPlayer = gameWorld.MainPlayer;
                            if (myPlayer != null)
                            {
                                myPlayer.Teleport(btrTransform.position + (Vector3.forward * 3));
                            }
                        }
                    }
                    else
                    {
                        ConsoleScreen.LogWarning("There is no BTRController active!");
                    }
                }
            }
        }

        [ConsoleCommand("spawnItem", "", null, "Spawns an item from a templateId")]
        public static void SpawnItem([ConsoleArgument("", "The templateId to spawn an item from")] string templateId,
            [ConsoleArgument(1, "The amount to spawn if the item can stack")] int amount = 1)
        {
            if (!CheckForGame())
            {
                return;
            }

            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            CoopPlayer player = (CoopPlayer)gameWorld.MainPlayer;
            if (!player.HealthController.IsAlive)
            {
                ConsoleScreen.LogError("You cannot spawn an item while dead!");
                return;
            }

            ItemFactoryClass itemFactory = Singleton<ItemFactoryClass>.Instance;
            if (itemFactory == null)
            {
                ConsoleScreen.LogError("ItemFactory was null!");
                return;
            }

            Item item = itemFactory.GetPresetItem(templateId);
            if (amount > 1 && item.StackMaxSize > 1)
            {
                item.StackObjectsCount = Mathf.Clamp(amount, 1, item.StackMaxSize);
            }
            else
            {
                item.StackObjectsCount = 1;
            }
            FikaGlobals.SpawnItemInWorld(item, player);

            SpawnItemPacket packet = new()
            {
                NetId = player.NetId,
                Item = item
            };

            if (FikaBackendUtils.IsServer)
            {
                Singleton<FikaServer>.Instance.SendDataToAll(ref packet, LiteNetLib.DeliveryMethod.ReliableOrdered);
                return;
            }
            Singleton<FikaClient>.Instance.SendData(ref packet, LiteNetLib.DeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Based on SSH's TarkyMenu command
        /// </summary>
        /// <param name="wildSpawnType"></param>
        /// <param name="number"></param>
        [ConsoleCommand("spawnNPC", "", null, "Spawn NPC with specified WildSpawnType")]
        public static void SpawnNPC([ConsoleArgument("pmcBot", "The WildSpawnType to spawn (use help for a list)")] string wildSpawnType, [ConsoleArgument(1, "The amount of AI to spawn")] int amount)
        {
            if (!CheckForGame())
            {
                return;
            }

            if (!FikaBackendUtils.IsServer)
            {
                ConsoleScreen.LogWarning("You cannot spawn AI as a client!");
                return;
            }

            if (string.IsNullOrEmpty(wildSpawnType) || wildSpawnType.ToLower() == "help")
            {
                foreach (object availableSpawnType in Enum.GetValues(typeof(WildSpawnType)))
                {
                    ConsoleScreen.Log(availableSpawnType.ToString());
                }
                ConsoleScreen.Log("Available WildSpawnType options below");
                return;
            }

            if (!Enum.TryParse(wildSpawnType, true, out WildSpawnType selectedSpawnType))
            {
                ConsoleScreen.Log($"Invalid WildSpawnType: {wildSpawnType}");
                return;
            }

            if (amount <= 0)
            {
                ConsoleScreen.Log($"Invalid number: {amount}. Please enter a valid positive integer.");
                return;
            }

            BotWaveDataClass newBotData = new()
            {
                BotsCount = amount,
                Side = EPlayerSide.Savage,
                SpawnAreaName = "",
                Time = 0f,
                WildSpawnType = selectedSpawnType,
                IsPlayers = false,
                Difficulty = BotDifficulty.easy,
                ChanceGroup = 100f,
                WithCheckMinMax = false
            };

            IBotGame botController = (IBotGame)Singleton<AbstractGame>.Instance;
            botController.BotsController.BotSpawner.ActivateBotsByWave(newBotData);
            ConsoleScreen.Log($"SpawnNPC completed. {amount} bots spawned.");
        }

        [ConsoleCommand("spawnAirdrop", "", null, "Spawns an airdrop")]
        public static void SpawnAirdrop()
        {
            if (!CheckForGame())
            {
                return;
            }

            if (!FikaBackendUtils.IsServer)
            {
                ConsoleScreen.LogWarning("You cannot spawn an airdrop as a client!");
                return;
            }

            CoopHostGameWorld gameWorld = (CoopHostGameWorld)Singleton<GameWorld>.Instance;
            if (gameWorld == null)
            {
                ConsoleScreen.LogError("GameWorld does not exist or you are a client!");
                return;
            }

            GClass2459 serverAirdropManager = gameWorld.ClientSynchronizableObjectLogicProcessor.ServerAirdropManager;
            if (serverAirdropManager == null)
            {
                ConsoleScreen.LogError("ServerAirdropManager was null!");
                return;
            }

            if (!serverAirdropManager.Boolean_0)
            {
                ConsoleScreen.LogError("Airdrops are disabled!");
                return;
            }

            List<Vector3> dropPoints = serverAirdropManager.list_2;
            if (dropPoints != null && dropPoints.Count > 0)
            {
                string templateId = serverAirdropManager.string_0;
                serverAirdropManager.method_5(serverAirdropManager.Single_0);
                gameWorld.InitAirdrop(templateId, true, serverAirdropManager.method_6());
                serverAirdropManager.string_0 = null;
                dropPoints.Clear();
                ConsoleScreen.Log("Started airdrop");
                return;
            }

            serverAirdropManager.method_5(serverAirdropManager.Single_0);
            gameWorld.InitAirdrop();
            ConsoleScreen.Log("Started airdrop");
        }
#endif

        [ConsoleCommand("debug", "", null, "Toggle debug window", [])]
        public static void Debug(bool state)
        {
            CoopGame coopGame = CoopGame.Instance;
            if (coopGame == null)
            {
                ConsoleScreen.LogWarning("You are not in a game.");
                return;
            }

            if (coopGame.Status != GameStatus.Started)
            {
                ConsoleScreen.LogWarning("Game is not running.");
                return;
            }
        }

        [ConsoleCommand("clear", "", null, "Clears the console output", [])]
        public static void Clear()
        {
            Singleton<PreloaderUI>.Instance.Console.Clear();
        }

        private static bool CheckForGame()
        {
            CoopGame coopGame = CoopGame.Instance;
            if (coopGame == null)
            {
                ConsoleScreen.LogWarning("You are not in a game.");
                return false;
            }

            if (coopGame.Status != GameStatus.Started)
            {
                ConsoleScreen.LogWarning("Game is not running.");
                return false;
            }

            return true;
        }
    }
}
