﻿using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.UI;
using EFT.UI.Matchmaker;
using Fika.Core.Coop.GameMode;
using Fika.Core.Coop.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking.Http;
using Fika.Core.Patching;
using Fika.Core.UI.Custom;
using HarmonyLib;
using JsonType;
using SPT.SinglePlayer.Utils.InRaid;
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Fika.Core.Coop.Patches
{
    /// <summary>
    /// Created by: Paulov
    /// Paulov: Overwrite and use our own CoopGame instance instead
    /// </summary>
    internal class TarkovApplication_LocalGameCreator_Patch : FikaPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TarkovApplication).GetMethod(nameof(TarkovApplication.method_45));
        }

        [PatchPrefix]
        public static bool Prefix(ref Task __result, TarkovApplication __instance, TimeAndWeatherSettings timeAndWeather, MatchmakerTimeHasCome.TimeHasComeScreenClass timeHasComeScreenController,
            RaidSettings ____raidSettings, InputTree ____inputTree, GameDateTime ____localGameDateTime, float ____fixedDeltaTime, string ____backendUrl, MetricsEventsClass metricsEvents,
            MetricsConfigClass metricsConfig, GameWorld gameWorld, MainMenuControllerClass ____menuOperation, CompositeDisposableClass ___compositeDisposableClass, BundleLockClass ___BundleLock)
        {
#if DEBUG
            Logger.LogInfo("TarkovApplication_LocalGameCreator_Patch:Prefix");

#endif
            __result = CreateFikaGame(__instance, timeAndWeather, timeHasComeScreenController, ____raidSettings,
                ____inputTree, ____localGameDateTime, ____fixedDeltaTime, ____backendUrl,
                metricsEvents, metricsConfig, gameWorld, ____menuOperation, ___compositeDisposableClass, ___BundleLock);
            return false;
        }

        public static async Task CreateFikaGame(TarkovApplication instance, TimeAndWeatherSettings timeAndWeather, MatchmakerTimeHasCome.TimeHasComeScreenClass timeHasComeScreenController,
            RaidSettings raidSettings, InputTree inputTree, GameDateTime localGameDateTime, float fixedDeltaTime, string backendUrl, MetricsEventsClass metricsEvents, MetricsConfigClass metricsConfig,
            GameWorld gameWorld, MainMenuControllerClass ___mainMenuController, CompositeDisposableClass compositeDisposableClass, BundleLockClass bundleLock)
        {
            bool isServer = FikaBackendUtils.IsServer;
            bool isTransit = FikaBackendUtils.IsTransit;

            if (isServer && !isTransit)
            {
                FikaBackendUtils.CachedRaidSettings = raidSettings;
            }
            else if (isServer && isTransit && FikaBackendUtils.CachedRaidSettings != null)
            {
                Logger.LogInfo("Applying cached raid settings from previous raid");
                RaidSettings cachedSettings = FikaBackendUtils.CachedRaidSettings;
                raidSettings.WavesSettings = cachedSettings.WavesSettings;
                raidSettings.BotSettings = cachedSettings.BotSettings;
                raidSettings.MetabolismDisabled = cachedSettings.MetabolismDisabled;
                raidSettings.PlayersSpawnPlace = cachedSettings.PlayersSpawnPlace;
            }

            metricsEvents.SetGamePrepared();

            if (Singleton<NotificationManagerClass>.Instantiated)
            {
                Singleton<NotificationManagerClass>.Instance.Deactivate();
            }

            ISession session = instance.Session;

            if (session == null)
            {
                throw new NullReferenceException("Backend session was null when initializing game!");
            }

            Profile profile = session.GetProfileBySide(raidSettings.Side);

            profile.Inventory.Stash = null;
            profile.Inventory.QuestStashItems = null;
            profile.Inventory.DiscardLimits = Singleton<ItemFactoryClass>.Instance.GetDiscardLimits();

#if DEBUG
            Logger.LogInfo("TarkovApplication_LocalGameCreator_Patch:Postfix: Attempt to set Raid Settings");
#endif

            await session.SendRaidSettings(raidSettings);
            LocalRaidSettings localRaidSettings = new()
            {
                location = raidSettings.LocationId,
                timeVariant = raidSettings.SelectedDateTime,
                mode = ELocalMode.PVE_OFFLINE,
                playerSide = raidSettings.Side,
                transitionType = FikaBackendUtils.TransitData.visitedLocations.Length > 0 ? ELocationTransition.Common : ELocationTransition.None
            };
            Traverse applicationTraverse = Traverse.Create(instance);
            applicationTraverse.Field<LocalRaidSettings>("localRaidSettings_0").Value = localRaidSettings;

            LocalSettings localSettings = await instance.Session.LocalRaidStarted(localRaidSettings);
            LocalRaidSettings raidSettingsToUpdate = applicationTraverse.Field<LocalRaidSettings>("localRaidSettings_0").Value;
            int escapeTimeLimit = raidSettings.IsScav ? RaidChangesUtil.NewEscapeTimeMinutes : raidSettings.SelectedLocation.EscapeTimeLimit;
            if (isServer)
            {
                raidSettings.SelectedLocation = localSettings.locationLoot;
                raidSettings.SelectedLocation.EscapeTimeLimit = escapeTimeLimit;
            }
            raidSettingsToUpdate.serverId = localSettings.serverId;
            raidSettingsToUpdate.selectedLocation = localSettings.locationLoot;
            raidSettingsToUpdate.selectedLocation.EscapeTimeLimit = escapeTimeLimit;
            raidSettingsToUpdate.transition = FikaBackendUtils.TransitData;

            GClass1325 profileInsurance = localSettings.profileInsurance;
            if ((profileInsurance?.insuredItems) != null)
            {
                profile.InsuredItems = localSettings.profileInsurance.insuredItems;
            }

            if (!isServer)
            {
                instance.MatchmakerPlayerControllerClass.UpdateMatchingStatus("Joining coop game...");

                RaidSettingsRequest data = new();
                RaidSettingsResponse raidSettingsResponse = await FikaRequestHandler.GetRaidSettings(data);

                raidSettings.MetabolismDisabled = raidSettingsResponse.MetabolismDisabled;
                raidSettings.PlayersSpawnPlace = raidSettingsResponse.PlayersSpawnPlace;
                timeAndWeather.HourOfDay = raidSettingsResponse.HourOfDay;
                timeAndWeather.TimeFlowType = raidSettingsResponse.TimeFlowType;
            }
            else
            {
                instance.MatchmakerPlayerControllerClass.UpdateMatchingStatus("Creating coop game...");
            }

            // This gets incorrectly reset by the server, update it manually here during transit
            if (!FikaBackendUtils.IsHeadless && isTransit && MainMenuUIScript.Exist)
            {
                MainMenuUIScript.Instance.UpdatePresence(UI.FikaUIGlobals.EFikaPlayerPresence.IN_RAID);
            }

            StartHandler startHandler = new(instance, session.Profile, session.ProfileOfPet, raidSettings.SelectedLocation);

            TimeSpan raidLimits = instance.method_46(raidSettings.SelectedLocation.EscapeTimeLimit);

            CoopGame coopGame = CoopGame.Create(inputTree, profile, gameWorld, localGameDateTime, instance.Session.InsuranceCompany,
                MonoBehaviourSingleton<MenuUI>.Instance, MonoBehaviourSingleton<GameUI>.Instance, raidSettings.SelectedLocation,
                timeAndWeather, raidSettings.WavesSettings, raidSettings.SelectedDateTime, startHandler.HandleStop,
                fixedDeltaTime, instance.PlayerUpdateQueue, instance.Session, raidLimits, metricsEvents,
                new GClass2440(metricsConfig, instance), localRaidSettings, raidSettings);

            startHandler.CoopGame = coopGame;

            Singleton<AbstractGame>.Create(coopGame);
            compositeDisposableClass.AddDisposable(coopGame);
            compositeDisposableClass.AddDisposable(startHandler.ReleaseSingleton);
            metricsEvents.SetGameCreated();
            FikaEventDispatcher.DispatchEvent(new AbstractGameCreatedEvent(coopGame));

            ScreenUpdater updater = new(instance.MatchmakerPlayerControllerClass, coopGame);
            if (!isServer)
            {
                coopGame.SetMatchmakerStatus("Coop game joined");
            }
            else
            {
                coopGame.SetMatchmakerStatus("Coop game created");
            }

            await coopGame.InitPlayer(raidSettings.BotSettings, backendUrl);
            GameObject.DestroyImmediate(MonoBehaviourSingleton<MenuUI>.Instance.gameObject);
            ___mainMenuController?.Unsubscribe();
            gameWorld.OnGameStarted();
            updater.Dispose();

            if (FikaBackendUtils.IsSpectator)
            {
                Logger.LogInfo("Starting game as spectator");
                await HandleJoinAsSpectator();
            }
        }

        private class StartHandler(TarkovApplication tarkovApplication, Profile pmcProfile, Profile scavProfile,
            LocationSettingsClass.Location location)
        {
            private readonly TarkovApplication tarkovApplication = tarkovApplication;
            private readonly Profile pmcProfile = pmcProfile;
            private readonly Profile scavProfile = scavProfile;
            private readonly LocationSettingsClass.Location location = location;
            public CoopGame CoopGame;

            public void HandleStop(Result<ExitStatus, TimeSpan, MetricsClass> result)
            {
                tarkovApplication.method_48(pmcProfile.Id, scavProfile, location, result);
            }

            public void ReleaseSingleton()
            {
                Singleton<AbstractGame>.Release(CoopGame);
                CoopGame.Instance = null;
            }
        }

        private static async Task HandleJoinAsSpectator()
        {
            Player MainPlayer = Singleton<GameWorld>.Instance.MainPlayer;

            // Teleport the player underground to avoid it from being looted
            Vector3 currentPosition = MainPlayer.Position;
            MainPlayer.Teleport(new(currentPosition.x, currentPosition.y - 75, currentPosition.z));

            // Small delay to ensure the teleport command is processed first
            await Task.Delay(250);

            DamageInfoStruct damageInfo = new()
            {
                Damage = 1000,
                DamageType = EDamageType.Impact
            };

            // Kill the player to put it in spectator mode
            MainPlayer.ApplyDamageInfo(damageInfo, EBodyPart.Head, EBodyPartColliderType.Eyes, 0);
        }
    }

    internal class ScreenUpdater : IDisposable
    {
        private readonly MatchmakerPlayerControllerClass matchmakerPlayerControllerClass;
        private readonly CoopGame coopGame;

        public ScreenUpdater(MatchmakerPlayerControllerClass controller, CoopGame game)
        {
            matchmakerPlayerControllerClass = controller;
            coopGame = game;
            game.OnMatchingStatusChanged += UpdateStatus;
        }

        private void UpdateStatus(string text, float? progress)
        {
            matchmakerPlayerControllerClass.UpdateMatchingStatus(text, progress);
        }

        public void Dispose()
        {
            coopGame.OnMatchingStatusChanged -= UpdateStatus;
        }
    }
}
