﻿// © 2024 Lacyway All Rights Reserved

using Comfort.Common;
using Diz.Binding;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Vaulting;
using Fika.Core.Coop.Custom;
using Fika.Core.Coop.Factories;
using Fika.Core.Coop.GameMode;
using Fika.Core.Coop.ObservedClasses;
using Fika.Core.Coop.ObservedClasses.Snapshotting;
using Fika.Core.Coop.PacketHandlers;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using Fika.Core.Utils;
using HarmonyLib;
using JsonType;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using static Fika.Core.Networking.CommonSubPackets;
using static Fika.Core.Networking.Packets.SubPacket;
using static Fika.Core.Networking.Packets.SubPackets;
using static Fika.Core.UI.FikaUIGlobals;

namespace Fika.Core.Coop.Players
{
	/// <summary>
	/// Observed players are any other players in the world for a client, including bots.
	/// They are all being handled by the server that moves them through packets.
	/// As a host this is only other clients.
	/// </summary>
	public class ObservedCoopPlayer : CoopPlayer
	{
		#region Fields and Properties
		public FikaHealthBar HealthBar
		{
			get
			{
				return healthBar;
			}
		}
		private FikaHealthBar healthBar = null;
		private Coroutine waitForStartRoutine;
		private bool isServer;
		public ObservedHealthController NetworkHealthController
		{
			get
			{
				return HealthController as ObservedHealthController;
			}
		}
		private readonly ObservedVaultingParametersClass ObservedVaultingParameters = new();
		public override bool CanBeSnapped
		{
			get
			{
				return false;
			}
		}
		public override EPointOfView PointOfView
		{
			get
			{
				return EPointOfView.ThirdPerson;
			}
			set
			{
				if (_playerBody.PointOfView.Value == value)
				{
					return;
				}
				_playerBody.PointOfView.Value = value;
				CalculateScaleValueByFov((float)Singleton<SharedGameSettingsClass>.Instance.Game.Settings.FieldOfView);
				SetCompensationScale(false);
				PlayerBones.Ribcage.Original.localScale = new Vector3(1f, 1f, 1f);
				MovementContext.PlayerAnimatorPointOfView(value);
				BindableEvent pointOfViewChanged = PointOfViewChanged;
				pointOfViewChanged?.Invoke();
				_playerBody.UpdatePlayerRenders(_playerBody.PointOfView.Value, Side);
				ProceduralWeaponAnimation.PointOfView = value;
			}
		}
		public override AbstractHandsController HandsController
		{
			get
			{
				return base.HandsController;
			}

			set
			{
				base.HandsController = value;
				PlayerAnimator.EWeaponAnimationType weaponAnimationType = GetWeaponAnimationType(_handsController);
				MovementContext.PlayerAnimatorSetWeaponId(weaponAnimationType);
			}
		}
		public override Ray InteractionRay
		{
			get
			{
				Vector3 vector = HandsRotation * Vector3.forward;
				return new(_playerLookRaycastTransform.position, vector);
			}
		}
		public override float ProtagonistHearing
		{
			get
			{
				return Mathf.Max(1f, Singleton<BetterAudio>.Instance.ProtagonistHearing + 1f);
			}
		}
		private GClass869 cullingHandler;
		#endregion

		public static async Task<ObservedCoopPlayer> CreateObservedPlayer(GameWorld gameWorld, int playerId, Vector3 position, Quaternion rotation,
			string layerName, string prefix, EPointOfView pointOfView, Profile profile, byte[] healthBytes,
			bool aiControl, EUpdateQueue updateQueue, EUpdateMode armsUpdateMode,
			EUpdateMode bodyUpdateMode, CharacterControllerSpawner.Mode characterControllerMode,
			Func<float> getSensitivity, Func<float> getAimingSensitivity, IViewFilter filter, MongoID firstId, ushort firstOperationId)
		{
			ObservedCoopPlayer player = Create<ObservedCoopPlayer>(gameWorld, ResourceKeyManagerAbstractClass.PLAYER_BUNDLE_NAME, playerId, position, updateQueue,
				armsUpdateMode, bodyUpdateMode, characterControllerMode, getSensitivity, getAimingSensitivity, prefix, aiControl);

			player.IsYourPlayer = false;

			ObservedInventoryController inventoryController = new(player, profile, true, firstId, firstOperationId, aiControl);
			ObservedHealthController healthController = new(healthBytes, player, inventoryController, profile.Skills);

			ObservedStatisticsManager statisticsManager = new();
			ObservedQuestController observedQuestController = null;
			if (!aiControl)
			{
				observedQuestController = new(profile, inventoryController, null, false);
				observedQuestController.Init();
				observedQuestController.Run();
			}

			await player.Init(rotation, layerName, pointOfView, profile, inventoryController, healthController,
				statisticsManager, observedQuestController, null, filter, EVoipState.NotAvailable, aiControl, false);

			player._handsController = EmptyHandsController.smethod_6<EmptyHandsController>(player);
			player._handsController.Spawn(1f, delegate { });

			player.AIData = new GClass540(null, player);

			Traverse observedTraverse = Traverse.Create(player);
			observedTraverse.Field<GClass869>("gclass869_0").Value = new();
			player.cullingHandler = observedTraverse.Field<GClass869>("gclass869_0").Value;
			player.cullingHandler.Initialize(player, player.PlayerBones);

			if (!aiControl)
			{
				HashSet<ETraderServiceType> services = Traverse.Create(player).Field<HashSet<ETraderServiceType>>("hashSet_0").Value;
				foreach (ETraderServiceType etraderServiceType in Singleton<BackendConfigSettingsClass>.Instance.ServicesData.Keys)
				{
					services.Add(etraderServiceType);
				}
			}

			player.AggressorFound = false;
			player._animators[0].enabled = true;
			player.isServer = FikaBackendUtils.IsServer;
			player.Snapshotter = FikaSnapshotter.Create(player);

			if (FikaBackendUtils.IsDedicated)
			{
				player.cullingHandler.Disable();
			}

			return player;
		}

		public override BasePhysicalClass CreatePhysical()
		{
			return new BasePhysicalClass();
		}

		public override bool CheckSurface()
		{
			float spreadRange = 42f * ProtagonistHearing;
			return !(Distance - spreadRange > 0);
		}

		public override void Say(EPhraseTrigger phrase, bool demand = false, float delay = 0, ETagStatus mask = 0, int probability = 100, bool aggressive = false)
		{
			if (gameObject.activeSelf)
			{
				base.Say(phrase, demand, delay, mask, probability, aggressive);
			}
		}

		public override void PlayGroundedSound(float fallHeight, float jumpHeight)
		{
			(bool hit, BaseBallistic.ESurfaceSound surfaceSound) = method_62();
			method_63(hit, surfaceSound);
			base.PlayGroundedSound(fallHeight, jumpHeight);
		}

		public override void OnSkillLevelChanged(AbstractSkillClass skill)
		{
			// Do nothing
		}

		public override void OnWeaponMastered(MasterSkillClass masterSkill)
		{
			// Do nothing
		}

		public override void StartInflictSelfDamageCoroutine()
		{
			// Do nothing
		}

		public override void AddStateSpeedLimit(float speedDelta, ESpeedLimit cause)
		{
			// Do nothing
		}

		public override void UpdateSpeedLimit(float speedDelta, ESpeedLimit cause)
		{
			// Do nothing
		}

		public override void UpdateSpeedLimitByHealth()
		{
			// Do nothing
		}

		public override void UpdatePhones()
		{
			// Do nothing
		}

		public override void FaceshieldMarkOperation(FaceShieldComponent armor, bool hasServerOrigin)
		{
			// Do nothing
		}

		public override void ManageAggressor(DamageInfo damageInfo, EBodyPart bodyPart, EBodyPartColliderType colliderType)
		{
			if (_isDeadAlready)
			{
				return;
			}
			if (!HealthController.IsAlive)
			{
				_isDeadAlready = true;
			}
			Player player = (damageInfo.Player == null) ? null : Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(damageInfo.Player.iPlayer.ProfileId);
			if (player == this)
			{
				return;
			}
			if (player == null)
			{
				return;
			}
			if (damageInfo.Weapon != null)
			{
				player.ExecuteShotSkill(damageInfo.Weapon);
			}

			if (player.IsYourPlayer)
			{
				bool flag = damageInfo.DidBodyDamage / HealthController.GetBodyPartHealth(bodyPart, false).Maximum >= 0.6f && HealthController.FindExistingEffect<GInterface290>(bodyPart) != null;
				player.StatisticsManager.OnEnemyDamage(damageInfo, bodyPart, ProfileId, Side, Profile.Info.Settings.Role,
					GroupId, HealthController.GetBodyPartHealth(EBodyPart.Common, false).Maximum, flag,
					Vector3.Distance(player.Transform.position, Transform.position), CurrentHour,
					Inventory.EquippedInSlotsTemplateIds, HealthController.BodyPartEffects, TriggerZones);
				return;
			}
		}

		public override void UpdateArmsCondition()
		{
			// Do nothing
		}

		public override bool ShouldVocalizeDeath(EBodyPart bodyPart)
		{
			return bodyPart > EBodyPart.Head;
		}

		public override void SendHeadlightsPacket(bool isSilent)
		{
			// Do nothing
		}

		public override void ApplyHitDebuff(float damage, float staminaBurnRate, EBodyPart bodyPartType, EDamageType damageType)
		{
			if (damageType.IsEnemyDamage())
			{
				IncreaseAwareness(20f);
			}
			if (HealthController.IsAlive && (!MovementContext.PhysicalConditionIs(EPhysicalCondition.OnPainkillers) || damage > 4f) && !IsAI)
			{
				if (gameObject.activeSelf && Speaker != null)
				{
					Speaker.Play(EPhraseTrigger.OnBeingHurt, HealthStatus, true, null);
				}
			}
		}

		public override void UpdateBreathStatus()
		{
			if (HealthController == null || Speaker == null)
			{
				return;
			}

			ETagStatus healthStatus = HealthStatus;
			bool isDying;
			if (healthStatus != ETagStatus.BadlyInjured && healthStatus != ETagStatus.Dying)
			{
				isDying = false;				
			}
			else
			{
				// Check for GClass increments
				isDying = HealthController != null && HealthController.FindActiveEffect<GInterface309>(EBodyPart.Common) == null;
			}

			bool isAudible = Physical.BreathIsAudible;
			bool shouldHeavyBreathe = isAudible || Muffled;
			if (gameObject.activeSelf && !HeavyBreath && shouldHeavyBreathe)
			{
				ETagStatus status1 = isDying ? healthStatus : ETagStatus.Healthy;
				ETagStatus status2 = isAudible ? ETagStatus.Aware : ETagStatus.Unaware;
				if (Speaker != null)
				{
					if (status1 == ETagStatus.Healthy && status2 == ETagStatus.Unaware)
					{
						Speaker.Play(EPhraseTrigger.OnBreath, status1 | status2, true, new int?(-1));
					}
					else
					{
						Speaker.Play(EPhraseTrigger.OnBreath, status1 | status2, true, null);
					} 
				}
			}
			HeavyBreath = shouldHeavyBreathe;
		}

		public void HandleExplosive(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType)
		{
			if (HealthController.DamageCoeff == 0)
			{
				return;
			}

			LastDamagedBodyPart = bodyPartType;
			LastBodyPart = bodyPartType;
			LastDamageInfo = damageInfo;
			LastDamageType = damageInfo.DamageType;

			PacketSender.DamagePackets.Enqueue(new()
			{
				Damage = damageInfo.Damage,
				DamageType = damageInfo.DamageType,
				BodyPartType = bodyPartType,
				ColliderType = colliderType,
				Direction = damageInfo.Direction,
				Point = damageInfo.HitPoint,
				HitNormal = damageInfo.HitNormal,
				PenetrationPower = damageInfo.PenetrationPower,
				BlockedBy = damageInfo.BlockedBy,
				DeflectedBy = damageInfo.DeflectedBy,
				SourceId = damageInfo.SourceId,
				ArmorDamage = damageInfo.ArmorDamage,
				WeaponId = damageInfo.Weapon.Id
			});
		}

		public override void ApplyDamageInfo(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, float absorbed)
		{
			LastAggressor = damageInfo.Player.iPlayer;
			LastDamagedBodyPart = bodyPartType;
			LastBodyPart = bodyPartType;
			LastDamageInfo = damageInfo;
			LastDamageType = damageInfo.DamageType;
		}

		public ShotInfoClass HandleSniperShot(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, EArmorPlateCollider armorPlateCollider, GStruct400 shotId)
		{
			if (HealthController.DamageCoeff == 0)
			{
				return null;
			}

			LastDamagedBodyPart = bodyPartType;
			LastBodyPart = bodyPartType;
			LastDamageInfo = damageInfo;
			LastDamageType = damageInfo.DamageType;

			PacketSender.DamagePackets.Enqueue(new()
			{
				Damage = damageInfo.Damage,
				DamageType = damageInfo.DamageType,
				BodyPartType = bodyPartType,
				ColliderType = colliderType,
				ArmorPlateCollider = armorPlateCollider,
				Direction = damageInfo.Direction,
				Point = damageInfo.HitPoint,
				HitNormal = damageInfo.HitNormal,
				PenetrationPower = damageInfo.PenetrationPower,
				BlockedBy = damageInfo.BlockedBy,
				DeflectedBy = damageInfo.DeflectedBy,
				SourceId = damageInfo.SourceId,
				ArmorDamage = damageInfo.ArmorDamage,
				WeaponId = damageInfo.Weapon.Id
			});

			return new()
			{
				PoV = EPointOfView.ThirdPerson,
				Penetrated = damageInfo.Penetrated,
				Material = MaterialType.Body
			};
		}

		public override ShotInfoClass ApplyShot(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, EArmorPlateCollider armorPlateCollider, GStruct400 shotId)
		{
			if (HealthController != null && !HealthController.IsAlive)
			{
				return null;
			}

			ShotReactions(damageInfo, bodyPartType);
			bool flag = !string.IsNullOrEmpty(damageInfo.DeflectedBy);
			float damage = damageInfo.Damage;
			List<ArmorComponent> list = ProceedDamageThroughArmor(ref damageInfo, colliderType, armorPlateCollider, true);
			MaterialType materialType = flag ? MaterialType.HelmetRicochet : ((list == null || list.Count < 1) ? MaterialType.Body : list[0].Material);
			ShotInfoClass hitInfo = new()
			{
				PoV = PointOfView,
				Penetrated = string.IsNullOrEmpty(damageInfo.BlockedBy) || string.IsNullOrEmpty(damageInfo.DeflectedBy),
				Material = materialType
			};
			float num = damage - damageInfo.Damage;
			if (num > 0)
			{
				damageInfo.DidArmorDamage = num;
			}
			damageInfo.DidBodyDamage = damageInfo.Damage;
			ReceiveDamage(damageInfo.Damage, bodyPartType, damageInfo.DamageType, num, hitInfo.Material);

			PacketSender.DamagePackets.Enqueue(new()
			{
				Damage = damageInfo.Damage,
				DamageType = damageInfo.DamageType,
				BodyPartType = bodyPartType,
				ColliderType = colliderType,
				ArmorPlateCollider = armorPlateCollider,
				Direction = damageInfo.Direction,
				Point = damageInfo.HitPoint,
				HitNormal = damageInfo.HitNormal,
				PenetrationPower = damageInfo.PenetrationPower,
				BlockedBy = damageInfo.BlockedBy,
				DeflectedBy = damageInfo.DeflectedBy,
				SourceId = damageInfo.SourceId,
				ArmorDamage = damageInfo.ArmorDamage,
				ProfileId = damageInfo.Player.iPlayer.ProfileId,
				Material = materialType,
				WeaponId = damageInfo.Weapon.Id
			});

			if (list != null)
			{
				QueueArmorDamagePackets([.. list]);
			}

			// Run this to get weapon skill
			ManageAggressor(damageInfo, bodyPartType, colliderType);

			return hitInfo;
		}

		public override void ApplyExplosionDamageToArmor(Dictionary<GStruct209, float> armorDamage, DamageInfo damageInfo)
		{
			if (isServer)
			{
				_preAllocatedArmorComponents.Clear();
				Inventory.GetPutOnArmorsNonAlloc(_preAllocatedArmorComponents);
				List<ArmorComponent> armorComponents = [];
				foreach (ArmorComponent armorComponent in _preAllocatedArmorComponents)
				{
					float num = 0f;
					foreach (KeyValuePair<GStruct209, float> keyValuePair in armorDamage)
					{
						if (armorComponent.ShotMatches(keyValuePair.Key.BodyPartColliderType, keyValuePair.Key.ArmorPlateCollider))
						{
							num += keyValuePair.Value;
							armorComponents.Add(armorComponent);
						}
					}
					if (num > 0f)
					{
						num = armorComponent.ApplyExplosionDurabilityDamage(num, damageInfo, _preAllocatedArmorComponents);
						method_95(num, armorComponent);
					}
				}

				if (armorComponents.Count > 0)
				{
					QueueArmorDamagePackets([.. armorComponents]);
				}
			}
		}

		public ShotInfoClass ApplyClientShot(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, EArmorPlateCollider armorPlateCollider, GStruct400 shotId)
		{
			ShotReactions(damageInfo, bodyPartType);
			LastAggressor = damageInfo.Player.iPlayer;
			LastDamagedBodyPart = bodyPartType;
			LastBodyPart = bodyPartType;
			LastDamageInfo = damageInfo;
			LastDamageType = damageInfo.DamageType;

			if (HealthController != null && !HealthController.IsAlive)
			{
				return null;
			}

			bool flag = !string.IsNullOrEmpty(damageInfo.DeflectedBy);
			float damage = damageInfo.Damage;
			List<ArmorComponent> list = ProceedDamageThroughArmor(ref damageInfo, colliderType, armorPlateCollider, true);
			MaterialType materialType = flag ? MaterialType.HelmetRicochet : ((list == null || list.Count < 1) ? MaterialType.Body : list[0].Material);
			ShotInfoClass hitInfo = new()
			{
				PoV = PointOfView,
				Penetrated = string.IsNullOrEmpty(damageInfo.BlockedBy) || string.IsNullOrEmpty(damageInfo.DeflectedBy),
				Material = materialType
			};
			float num = damage - damageInfo.Damage;
			if (num > 0)
			{
				damageInfo.DidArmorDamage = num;
			}
			damageInfo.DidBodyDamage = damageInfo.Damage;
			ReceiveDamage(damageInfo.Damage, bodyPartType, damageInfo.DamageType, num, hitInfo.Material);

			PacketSender.DamagePackets.Enqueue(new()
			{
				Damage = damageInfo.Damage,
				DamageType = damageInfo.DamageType,
				BodyPartType = bodyPartType,
				ColliderType = colliderType,
				ArmorPlateCollider = armorPlateCollider,
				Direction = damageInfo.Direction,
				Point = damageInfo.HitPoint,
				HitNormal = damageInfo.HitNormal,
				PenetrationPower = damageInfo.PenetrationPower,
				BlockedBy = damageInfo.BlockedBy,
				DeflectedBy = damageInfo.DeflectedBy,
				SourceId = damageInfo.SourceId,
				ArmorDamage = damageInfo.ArmorDamage,
				ProfileId = damageInfo.Player.iPlayer.ProfileId,
				Material = materialType,
				WeaponId = damageInfo.Weapon.Id
			});

			if (list != null)
			{
				QueueArmorDamagePackets([.. list]);
			}

			// Run this to get weapon skill
			ManageAggressor(damageInfo, bodyPartType, colliderType);

			return hitInfo;
		}

		public override void OnMounting(GStruct179.EMountingCommand command)
		{
			// Do nothing
		}

		public override void ApplyCorpseImpulse()
		{
			if (CorpseSyncPacket.BodyPartColliderType != EBodyPartColliderType.None
				&& PlayerBones.BodyPartCollidersDictionary.TryGetValue(CorpseSyncPacket.BodyPartColliderType, out BodyPartCollider bodyPartCollider))
			{
				Corpse.Ragdoll.ApplyImpulse(bodyPartCollider.Collider, CorpseSyncPacket.Direction, CorpseSyncPacket.Point, CorpseSyncPacket.Force);
			}
		}

		public override void CreateMovementContext()
		{
			LayerMask movement_MASK = EFTHardSettings.Instance.MOVEMENT_MASK;
			MovementContext = ObservedMovementContext.Create(this, GetBodyAnimatorCommon, GetCharacterControllerCommon, movement_MASK);
		}

		public override void OnHealthEffectAdded(IEffect effect)
		{
			// Remember for GClass increments
			if (gameObject.activeSelf && effect is GInterface294 && FractureSound != null && Singleton<BetterAudio>.Instantiated)
			{
				Singleton<BetterAudio>.Instance.PlayAtPoint(Position, FractureSound, CameraClass.Instance.Distance(Position),
					BetterAudio.AudioSourceGroupType.Impacts, 15, 0.7f, EOcclusionTest.Fast, null, false);
			}
		}

		public override void OnHealthEffectRemoved(IEffect effect)
		{
			// Do nothing
		}

		public override void ConnectSkillManager()
		{
			// Do nothing
		}

		#region proceed
		public override void Proceed(KnifeComponent knife, Callback<IKnifeController> callback, bool scheduled = true)
		{
			HandsControllerFactory factory = new(this, knifeComponent: knife);
			Func<KnifeController> func = new(factory.CreateObservedKnifeController);
			new Process<KnifeController, IKnifeController>(this, func, factory.knifeComponent.Item)
				.method_0(null, callback, scheduled);
		}

		public override void Proceed(GrenadeClass throwWeap, Callback<IHandsThrowController> callback, bool scheduled = true)
		{
			HandsControllerFactory factory = new(this, throwWeap);
			Func<GrenadeHandsController> func = new(factory.CreateObservedGrenadeController);
			new Process<GrenadeHandsController, IHandsThrowController>(this, func, throwWeap, false)
				.method_0(null, callback, scheduled);
		}

		public override void Proceed(GrenadeClass throwWeap, Callback<GInterface160> callback, bool scheduled = true)
		{
			HandsControllerFactory factory = new(this, throwWeap);
			Func<QuickGrenadeThrowHandsController> func = new(factory.CreateObservedQuickGrenadeController);
			new Process<QuickGrenadeThrowHandsController, GInterface160>(this, func, throwWeap, false)
				.method_0(null, callback, scheduled);
		}

		public override void Proceed(Weapon weapon, Callback<IFirearmHandsController> callback, bool scheduled = true)
		{
			HandsControllerFactory factory = new(this, weapon);
			Func<FirearmController> func = new(factory.CreateObservedFirearmController);
			new Process<FirearmController, IFirearmHandsController>(this, func, factory.item, true)
				.method_0(null, callback, scheduled);
		}

		public override void Proceed(MedsClass meds, EBodyPart bodyPart, Callback<GInterface157> callback, int animationVariant, bool scheduled = true)
		{
			HandsControllerFactory factory = new(this)
			{
				meds = meds,
				bodyPart = bodyPart,
				animationVariant = animationVariant
			};
			Func<MedsController> func = new(factory.CreateObservedMedsController);
			new Process<MedsController, GInterface157>(this, func, meds, false)
				.method_0(null, callback, scheduled);
		}

		public override void Proceed(FoodClass foodDrink, float amount, Callback<GInterface157> callback, int animationVariant, bool scheduled = true)
		{
			HandsControllerFactory factory = new(this)
			{
				food = foodDrink,
				amount = amount,
				animationVariant = animationVariant
			};
			Func<MedsController> func = new(factory.CreateObservedMedsController);
			new Process<MedsController, GInterface157>(this, func, foodDrink, false)
				.method_0(null, callback, scheduled);
		}
		#endregion

		public override void OnFovUpdatedEvent(int fov)
		{
			// Do nothing
		}

		public override void ShowHelloNotification(string sender)
		{
			// Do nothing
		}

		public override void HealthControllerUpdate(float deltaTime)
		{
			// Do nothing
		}

		public override void DropCurrentController(Action callback, bool fastDrop, Item nextControllerItem = null)
		{
			HandsController.Drop(1f, callback, fastDrop, nextControllerItem);
		}

		public override void OnPhraseTold(EPhraseTrigger @event, TaggedClip clip, TagBank bank, PhraseSpeakerClass speaker)
		{
			method_32(clip);
		}

		public override void MouseLook(bool forceApplyToOriginalRibcage = false)
		{
			if (HandsController != null)
			{
				MovementContext.RotationAction?.Invoke(this);
			}
		}

		public void Interpolate(ref PlayerStatePacket to, ref PlayerStatePacket from, double ratio)
		{
			float interpolateRatio = (float)ratio;
			bool isJumpSet = MovementContext.PlayerAnimatorIsJumpSetted();

			method_63(to.HasGround, to.SurfaceSound);

			Rotation = new Vector2(Mathf.LerpAngle(from.Rotation.x, to.Rotation.x, interpolateRatio),
				Mathf.LerpUnclamped(from.Rotation.y, to.Rotation.y, interpolateRatio));

			if (to.HeadRotation != default)
			{
				Vector3 newRotation = Vector3.LerpUnclamped(HeadRotation, to.HeadRotation, interpolateRatio);
				HeadRotation = newRotation;
				ProceduralWeaponAnimation.SetHeadRotation(newRotation);
			}

			bool isGrounded = to.IsGrounded;
			MovementContext.IsGrounded = isGrounded;

			EPlayerState newState = to.State;

			if (newState == EPlayerState.Jump)
			{
				MovementContext.PlayerAnimatorEnableJump(true);
				if (isServer)
				{
					MovementContext.method_2(1f);
				}
			}

			if (isJumpSet && isGrounded)
			{
				MovementContext.PlayerAnimatorEnableJump(false);
				MovementContext.PlayerAnimatorEnableLanding(true);
			}
			if (CurrentStateName == EPlayerState.Sprint && newState == EPlayerState.Transition)
			{
				MovementContext.UpdateSprintInertia();
				MovementContext.PlayerAnimatorEnableInert(false);
			}

			Physical.SerializationStruct = to.Stamina;

			if (!Mathf.Approximately(MovementContext.Step, to.Step))
			{
				CurrentManagedState.SetStep(to.Step);
			}

			if (MovementContext.IsSprintEnabled != to.IsSprinting)
			{
				CurrentManagedState.EnableSprint(to.IsSprinting);
			}

			if (MovementContext.IsInPronePose != to.IsProne)
			{
				MovementContext.IsInPronePose = to.IsProne;
			}

			if (!Mathf.Approximately(PoseLevel, to.PoseLevel))
			{
				MovementContext.SetPoseLevel(from.PoseLevel + (to.PoseLevel - from.PoseLevel));
			}

			MovementContext.SetCurrentClientAnimatorStateIndex(to.AnimatorStateIndex);
			MovementContext.SetCharacterMovementSpeed(to.CharacterMovementSpeed, true);

			if (MovementContext.BlindFire != to.Blindfire)
			{
				MovementContext.SetBlindFire(to.Blindfire);
			}

			if (!IsInventoryOpened && isGrounded)
			{
				Move(to.MovementDirection);
				if (isServer)
				{
					MovementContext.method_1(to.MovementDirection);
				}
			}

			Transform.position = Vector3.LerpUnclamped(from.Position, to.Position, interpolateRatio);

			float currentTilt = MovementContext.Tilt;
			if (!Mathf.Approximately(currentTilt, to.Tilt))
			{
				float newTilt = Mathf.LerpUnclamped(currentTilt, to.Tilt, interpolateRatio);
				MovementContext.SetTilt(newTilt, true);
			}

			ObservedOverlap = to.WeaponOverlap;
			LeftStanceDisabled = to.LeftStanceDisabled;
		}

		public override void InteractionRaycast()
		{
			if (_playerLookRaycastTransform == null || !HealthController.IsAlive)
			{
				return;
			}

			InteractableObjectIsProxy = false;
			Ray interactionRay = InteractionRay;
			Boolean_0 = false;
			GameObject gameObject = GameWorld.FindInteractable(interactionRay, out _);
			if (gameObject != null)
			{
				Player player = gameObject.GetComponent<Player>();
				if (player != null && player != InteractablePlayer)
				{
					InteractablePlayer = (player != this) ? player : null;
				}
				return;
			}

			InteractablePlayer = null;
		}

		public override Corpse CreateCorpse()
		{
			if (CorpseSyncPacket.Equipment != null)
			{
				SetInventory(CorpseSyncPacket.Equipment);
			}
			return CreateCorpse<Corpse>(CorpseSyncPacket.OverallVelocity);
		}

		public override void OnDead(EDamageType damageType)
		{
			StartCoroutine(DestroyNetworkedComponents());

			if (HealthBar != null)
			{
				Destroy(HealthBar);
			}

			if (FikaPlugin.ShowNotifications.Value)
			{
				if (!IsObservedAI)
				{
					string nickname = !string.IsNullOrEmpty(Profile.Info.MainProfileNickname) ? Profile.Info.MainProfileNickname : Profile.Nickname;
					if (damageType != EDamageType.Undefined)
					{
						NotificationManagerClass.DisplayWarningNotification(string.Format(LocaleUtils.GROUP_MEMBER_DIED_FROM.Localized(),
							[ColorizeText(EColor.GREEN, nickname), ColorizeText(EColor.RED, ("DamageType_" + damageType.ToString()).Localized())]));
					}
					else
					{
						NotificationManagerClass.DisplayWarningNotification(string.Format(LocaleUtils.GROUP_MEMBER_DIED.Localized(),
							ColorizeText(EColor.GREEN, nickname)));
					}
				}
				if (LocaleUtils.IsBoss(Profile.Info.Settings.Role, out string name) && IsObservedAI && LastAggressor != null)
				{
					if (LastAggressor is CoopPlayer aggressor)
					{
						string aggressorNickname = !string.IsNullOrEmpty(LastAggressor.Profile.Info.MainProfileNickname) ? LastAggressor.Profile.Info.MainProfileNickname : LastAggressor.Profile.Nickname;
						if (aggressor.gameObject.name.StartsWith("Player_") || aggressor.IsYourPlayer)
						{
							NotificationManagerClass.DisplayMessageNotification(string.Format(LocaleUtils.KILLED_BOSS.Localized(),
							[ColorizeText(EColor.GREEN, LastAggressor.Profile.Info.MainProfileNickname), ColorizeText(EColor.BROWN, name)]),
							iconType: EFT.Communications.ENotificationIconType.Friend);
						}
					}
				}
			}
			Singleton<BetterAudio>.Instance.ProtagonistHearingChanged -= UpdateStepSoundRolloff;
			base.OnDead(damageType);
			if (cullingHandler != null)
			{
				cullingHandler.DisableCullingOnDead();
			}
			if (CorpseSyncPacket.ItemInHands != null)
			{
				Corpse.SetItemInHandsLootedCallback(null);
				Corpse.ItemInHands.Value = CorpseSyncPacket.ItemInHands;
				Corpse.SetItemInHandsLootedCallback(ReleaseHand);
			}
			CorpseSyncPacket = default;
		}

		public override void vmethod_3(GClass1615 controller, int transitPointId, string keyId, EDateTime time)
		{
			// Do nothing
		}

		public override void HandleDamagePacket(ref DamagePacket packet)
		{
			DamageInfo damageInfo = new()
			{
				Damage = packet.Damage,
				DamageType = packet.DamageType,
				BodyPartColliderType = packet.ColliderType,
				HitPoint = packet.Point,
				HitNormal = packet.HitNormal,
				Direction = packet.Direction,
				PenetrationPower = packet.PenetrationPower,
				BlockedBy = packet.BlockedBy,
				DeflectedBy = packet.DeflectedBy,
				SourceId = packet.SourceId,
				ArmorDamage = packet.ArmorDamage
			};

			if (!string.IsNullOrEmpty(packet.ProfileId))
			{
				IPlayerOwner player = Singleton<GameWorld>.Instance.GetAlivePlayerBridgeByProfileID(packet.ProfileId);

				if (player != null)
				{
					damageInfo.Player = player;
					LastAggressor = player.iPlayer;
					if (IsYourPlayer)
					{
						if (!FikaPlugin.Instance.FriendlyFire && damageInfo.Player.iPlayer.GroupId == GroupId)
						{
							return;
						}
					}
				}

				/*// TODO: Fix this and consistently get the correct data...
				if (Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(packet.ProfileId).HandsController.Item is Weapon weapon)
				{
					damageInfo.Weapon = weapon;
				}*/
				lastWeaponId = packet.WeaponId;
			}

			ShotReactions(damageInfo, packet.BodyPartType);
			ReceiveDamage(damageInfo.Damage, packet.BodyPartType, damageInfo.DamageType, packet.Absorbed, packet.Material);

			LastDamageInfo = damageInfo;
			LastBodyPart = packet.BodyPartType;
			LastDamagedBodyPart = packet.BodyPartType;
		}

		public override void OnBeenKilledByAggressor(IPlayer aggressor, DamageInfo damageInfo, EBodyPart bodyPart, EDamageType lethalDamageType)
		{
			// Only handle if it was ourselves as otherwise it's irrelevant
			if (LastAggressor.IsYourPlayer)
			{
				base.OnBeenKilledByAggressor(aggressor, damageInfo, bodyPart, lethalDamageType);
				return;
			}

			if (FikaPlugin.Instance.SharedQuestProgression && FikaPlugin.EasyKillConditions.Value)
			{
				if (aggressor.GroupId == "Fika" && !aggressor.IsYourPlayer)
				{
#if DEBUG
					FikaPlugin.Instance.FikaLogger.LogInfo("Handling teammate kill from teammate: " + aggressor.Profile.Nickname);
#endif
					CoopPlayer mainPlayer = (CoopPlayer)Singleton<GameWorld>.Instance.MainPlayer;
					if (mainPlayer != null)
					{
						float distance = Vector3.Distance(aggressor.Position, Position);
						mainPlayer.HandleTeammateKill(ref damageInfo, bodyPart, Side, Profile.Info.Settings.Role, ProfileId,
							distance, Inventory.EquippedInSlotsTemplateIds, HealthController.BodyPartEffects, TriggerZones,
							(CoopPlayer)aggressor, Profile.Info.Settings.Experience);
					}
				}
			}
		}

		public override void ExternalInteraction()
		{
			// Do nothing
		}

		// TODO: This code needs refactoring and hopefully removing
		// The reason it was added was due to a lot of bots inventories desyncing because of their unnatural inventory operations
		public void SetInventory(InventoryEquipment equipmentClass)
		{
			if (HandsController != null)
			{
				HandsController.FastForwardCurrentState();
			}

			Inventory inventory = new()
			{
				Equipment = equipmentClass
			};

			InventoryController.ReplaceInventory(inventory);
			if (CorpseSyncPacket.ItemSlot <= EquipmentSlot.Scabbard)
			{
				Item heldItem = Equipment.GetSlot(CorpseSyncPacket.ItemSlot).ContainedItem;
				if (heldItem != null)
				{
					CorpseSyncPacket.ItemInHands = heldItem;
				}
			}

			foreach (EquipmentSlot equipmentSlot in PlayerBody.SlotNames)
			{
				Transform slotBone = PlayerBody.GetSlotBone(equipmentSlot);
				Transform alternativeHolsterBone = PlayerBody.GetAlternativeHolsterBone(equipmentSlot);
				PlayerBody.GClass2008 slowView = new(PlayerBody, Inventory.Equipment.GetSlot(equipmentSlot), slotBone, equipmentSlot,
					Inventory.Equipment.GetSlot(EquipmentSlot.Backpack), alternativeHolsterBone);
				PlayerBody.GClass2008 oldSlotView = PlayerBody.SlotViews.AddOrReplace(equipmentSlot, slowView);
				if (oldSlotView != null)
				{
					oldSlotView.Dispose();
				}
				PlayerBody.ValidateHoodedDress(equipmentSlot);
			}

			if (PlayerBody.HaveHolster && PlayerBody.SlotViews.ContainsKey(EquipmentSlot.Holster))
			{
				Transform slotBone = PlayerBody.GetSlotBone(EquipmentSlot.Holster);
				Transform alternativeHolsterBone = PlayerBody.GetAlternativeHolsterBone(EquipmentSlot.Holster);
				PlayerBody.GClass2008 slotView = new(PlayerBody, Inventory.Equipment.GetSlot(EquipmentSlot.Holster), slotBone, EquipmentSlot.Holster,
					Inventory.Equipment.GetSlot(EquipmentSlot.Backpack), alternativeHolsterBone);
				PlayerBody.GClass2008 oldSlotView = PlayerBody.SlotViews.AddOrReplace(EquipmentSlot.Holster, slotView);
				if (oldSlotView != null)
				{
					oldSlotView.Dispose();
				}
			}
		}

		public override void DoObservedVault(ref VaultPacket packet)
		{
			if (packet.VaultingStrategy != EVaultingStrategy.Vault)
			{
				if (packet.VaultingStrategy != EVaultingStrategy.Climb)
				{
					return;
				}
				MovementContext.PlayerAnimator.SetDoClimb(true);
			}
			else
			{
				MovementContext.PlayerAnimator.SetDoVault(true);
			}

			ObservedVaultingParameters.MaxWeightPointPosition = packet.VaultingPoint;
			ObservedVaultingParameters.VaultingHeight = packet.VaultingHeight;
			ObservedVaultingParameters.VaultingLength = packet.VaultingLength;
			ObservedVaultingParameters.VaultingSpeed = packet.VaultingSpeed;
			ObservedVaultingParameters.AbsoluteForwardVelocity = packet.AbsoluteForwardVelocity;
			ObservedVaultingParameters.BehindObstacleRatio = packet.BehindObstacleHeight;

			MovementContext.PlayerAnimator.SetVaultingSpeed(packet.VaultingSpeed);
			MovementContext.PlayerAnimator.SetVaultingHeight(packet.VaultingHeight);
			MovementContext.PlayerAnimator.SetVaultingLength(packet.VaultingLength);
			MovementContext.PlayerAnimator.SetBehindObstacleRatio(packet.BehindObstacleHeight);
			MovementContext.PlayerAnimator.SetAbsoluteForwardVelocity(packet.AbsoluteForwardVelocity);

			MovementContext.PlayerAnimator.SetIsGrounded(true);
		}

		private IEnumerator DestroyNetworkedComponents()
		{
			yield return new WaitForSeconds(2);

			if (Speaker != null)
			{
				Speaker.Shut();
				Speaker.OnPhraseTold -= OnPhraseTold;
				Speaker.OnDestroy();
			}

			// Try to mitigate infinite firing loop further
			if (HandsController is CoopObservedFirearmController firearmController)
			{
				if (firearmController.WeaponSoundPlayer != null && firearmController.WeaponSoundPlayer.enabled)
				{
					firearmController.WeaponSoundPlayer.enabled = false;
				}
			}
		}

		public void InitObservedPlayer(bool isDedicatedHost)
		{
			if (gameObject.name.StartsWith("Bot_"))
			{
				IsObservedAI = true;
			}

			PacketSender = gameObject.AddComponent<ObservedPacketSender>();
			Traverse playerTraverse = Traverse.Create(this);

			if (IsObservedAI)
			{
				BotStatePacket packet = new()
				{
					NetId = NetId,
					Type = BotStatePacket.EStateType.LoadBot
				};

				PacketSender.Client.SendData(ref packet, LiteNetLib.DeliveryMethod.ReliableOrdered);

				IVaultingComponent vaultingComponent = playerTraverse.Field<IVaultingComponent>("_vaultingComponent").Value;
				if (vaultingComponent != null)
				{
					UpdateEvent -= vaultingComponent.DoVaultingTick;
				}

				playerTraverse.Field("_vaultingComponent").SetValue(null);
				playerTraverse.Field("_vaultingComponentDebug").SetValue(null);
				playerTraverse.Field("_vaultingParameters").SetValue(null);
				playerTraverse.Field("_vaultingGameplayRestrictions").SetValue(null);
				playerTraverse.Field("_vaultAudioController").SetValue(null);
				playerTraverse.Field("_sprintVaultAudioController").SetValue(null);
				playerTraverse.Field("_climbAudioController").SetValue(null);
			}

			PacketReceiver = gameObject.AddComponent<PacketReceiver>();

			if (!IsObservedAI)
			{
				if (!isDedicatedHost)
				{
					Profile.Info.GroupId = "Fika";
					waitForStartRoutine = StartCoroutine(CreateHealthBar());
				}

				IVaultingComponent vaultingComponent = playerTraverse.Field<IVaultingComponent>("_vaultingComponent").Value;
				if (vaultingComponent != null)
				{
					UpdateEvent -= vaultingComponent.DoVaultingTick;
				}
				playerTraverse.Field("_vaultingComponent").SetValue(null);
				playerTraverse.Field("_vaultingComponentDebug").SetValue(null);
				playerTraverse.Field("_vaultingParameters").SetValue(null);
				playerTraverse.Field("_vaultingGameplayRestrictions").SetValue(null);

				InitVaultingAudioControllers(ObservedVaultingParameters);

				if (FikaPlugin.ShowNotifications.Value && !isDedicatedHost)
				{
					NotificationManagerClass.DisplayMessageNotification(string.Format(LocaleUtils.GROUP_MEMBER_SPAWNED.Localized(),
						ColorizeText(EColor.GREEN, Profile.Info.MainProfileNickname)),
					EFT.Communications.ENotificationDurationType.Default, EFT.Communications.ENotificationIconType.Friend);
				}

				Singleton<GameWorld>.Instance.MainPlayer.StatisticsManager.OnGroupMemberConnected(Inventory);
			}
		}

		private IEnumerator CreateHealthBar()
		{
			CoopGame coopGame = (CoopGame)Singleton<IFikaGame>.Instance;

			if (coopGame == null)
			{
				yield break;
			}

			while (coopGame.Status != GameStatus.Started)
			{
				yield return null;
			}

			healthBar = FikaHealthBar.Create(this);

			yield break;
		}

		public override void LateUpdate()
		{
			DistanceDirty = true;
			OcclusionDirty = true;
			if (UpdateQueue == EUpdateQueue.FixedUpdate)
			{
				return;
			}
			if (HealthController == null || !HealthController.IsAlive)
			{
				return;
			}
			Physical.LateUpdate();
			VisualPass();
			PropUpdate();
			_armsupdated = false;
			_bodyupdated = false;
			if (_nFixedFrames > 0)
			{
				_nFixedFrames = 0;
				_fixedTime = 0f;
			}
		}

		public override void UpdateOcclusion()
		{
			if (OcclusionDirty && MonoBehaviourSingleton<BetterAudio>.Instantiated)
			{
				OcclusionDirty = false;
				BetterAudio instance = MonoBehaviourSingleton<BetterAudio>.Instance;
				AudioMixerGroup audioMixerGroup = Muffled ? instance.SimpleOccludedMixerGroup : instance.ObservedPlayerSpeechMixer;
				if (SpeechSource != null)
				{
					SpeechSource.SetMixerGroup(audioMixerGroup);
				}
				return;
			}
		}

		public override void LandingAdjustments(float d)
		{
			// Do nothing
		}

		public new void CreateCompass()
		{
			bool compassInstantiated = Traverse.Create(this).Field<bool>("_compassInstantiated").Value;
			if (!compassInstantiated)
			{
				Transform transform = Singleton<PoolManager>.Instance.CreateFromPool<Transform>(new ResourceKey
				{
					path = "assets/content/weapons/additional_hands/item_compass.bundle"
				});
				transform.SetParent(PlayerBones.Ribcage.Original, false);
				transform.localRotation = Quaternion.identity;
				transform.localPosition = Vector3.zero;
				method_27(transform.gameObject);
				Traverse.Create(this).Field("_compassInstantiated").SetValue(true);
				return;
			}
		}

		public override void OnAnimatedInteraction(EInteraction interaction)
		{
			if (interaction == EInteraction.FriendlyGesture)
			{
				InteractionRaycast();
				if (InteractablePlayer != null)
				{
					InteractablePlayer.ShowHelloNotification(Profile.Nickname);
				}
			}
		}

		public override void PauseAllEffectsOnPlayer()
		{
			NetworkHealthController.PauseAllEffects();
		}

		public override void UnpauseAllEffectsOnPlayer()
		{
			NetworkHealthController.UnpauseAllEffects();
		}

		public override void OnVaulting()
		{
			// Do nothing
		}

		public override void ManualUpdate(float deltaTime, float? platformDeltaTime = null, int loop = 1)
		{
			_bodyupdated = true;
			_bodyTime = deltaTime;

			method_13(deltaTime);

			UpdateTriggerColliderSearcher(deltaTime, true);
			if (cullingHandler != null)
			{
				cullingHandler.ManualUpdate(deltaTime);
			}
		}

		public override void InitAudioController()
		{
			base.InitAudioController();
			Singleton<BetterAudio>.Instance.ProtagonistHearingChanged += UpdateStepSoundRolloff;
		}

		public override bool UpdateGrenadeAnimatorDuePoV()
		{
			return true;
		}

		public override void FixedUpdateTick()
		{
			// Do nothing
		}

		public override void OnDestroy()
		{
			if (HandsController != null)
			{
				AbstractHandsController handsController = HandsController;
				if (handsController != null && handsController.ControllerGameObject != null)
				{
					HandsController.OnGameSessionEnd();
					HandsController.Destroy();
				}
			}
			if (HealthBar != null)
			{
				Destroy(HealthBar);
			}
			if (Singleton<BetterAudio>.Instantiated)
			{
				Singleton<BetterAudio>.Instance.ProtagonistHearingChanged -= UpdateStepSoundRolloff;
			}
			base.OnDestroy();
		}

		public override void SendHandsInteractionStateChanged(bool value, int animationId)
		{
			if (value)
			{
				MovementContext.SetBlindFire(0);
			}
		}

		public void HandleProceedPacket(ref ProceedPacket packet)
		{
			switch (packet.ProceedType)
			{
				case EProceedType.EmptyHands:
					{
						CreateEmptyHandsController();
						break;
					}
				case EProceedType.FoodClass:
				case EProceedType.MedsClass:
					{
						CreateMedsController(packet.ItemId, packet.BodyPart, packet.Amount, packet.AnimationVariant);
						break;
					}
				case EProceedType.GrenadeClass:
					{
						CreateGrenadeController(packet.ItemId);
						break;
					}
				case EProceedType.QuickGrenadeThrow:
					{
						CreateQuickGrenadeController(packet.ItemId);
						break;
					}
				case EProceedType.QuickKnifeKick:
					{
						CreateQuickKnifeController(packet.ItemId);
						break;
					}
				case EProceedType.QuickUse:
					{
						CreateQuickUseItemController(packet.ItemId);
						break;
					}
				case EProceedType.Weapon:
					{
						CreateFirearmController(packet.ItemId);
						break;
					}
				case EProceedType.Knife:
					{
						CreateKnifeController(packet.ItemId);
						break;
					}
				case EProceedType.UsableItem:
					{
						CreateUsableItemController(packet.ItemId);
						break;
					}
				case EProceedType.Stationary:
					{
						CreateFirearmController(packet.ItemId, true);
						break;
					}
			}
		}

		#region handControllers
		private void CreateHandsController(Func<AbstractHandsController> controllerFactory, Item item)
		{
			CreateHandsControllerHandler handler = new((item != null) ? method_80(item) : null);

			handler.setInHandsOperation?.Confirm(true);

			if (HandsController != null)
			{
				AbstractHandsController handsController = HandsController;
				HandsController.FastForwardCurrentState();
				if (HandsController != handsController && HandsController != null)
				{
					HandsController.FastForwardCurrentState();
				}
				HandsController.Destroy();
				if (HandsController != null)
				{
					Destroy(HandsController);
				}
				HandsController = null;
			}

			base.SpawnController(controllerFactory(), handler.DisposeHandler);
		}

		public void SpawnHandsController(EHandsControllerType controllerType, string itemId, bool isStationary)
		{
			switch (controllerType)
			{
				case EHandsControllerType.Empty:
					CreateEmptyHandsController();
					break;
				case EHandsControllerType.Firearm:
					CreateFirearmController(itemId, isStationary, true);
					break;
				case EHandsControllerType.Meds:
					CreateMedsController(itemId, EBodyPart.Head, 1f, 1);
					break;
				case EHandsControllerType.Grenade:
					CreateGrenadeController(itemId);
					break;
				case EHandsControllerType.Knife:
					CreateKnifeController(itemId);
					break;
				case EHandsControllerType.QuickGrenade:
					CreateQuickGrenadeController(itemId);
					break;
				case EHandsControllerType.QuickKnife:
					CreateQuickKnifeController(itemId);
					break;
				case EHandsControllerType.QuickUseItem:
					CreateQuickUseItemController(itemId);
					break;
				case EHandsControllerType.UsableItem:
					CreateUsableItemController(itemId);
					break;
				default:
					FikaPlugin.Instance.FikaLogger.LogWarning($"ObservedCoopPlayer::SpawnHandsController: Unhandled ControllerType, was {controllerType}");
					break;
			}
		}

		private void CreateEmptyHandsController()
		{
			CreateHandsController(ReturnEmptyHandsController, null);
		}

		private AbstractHandsController ReturnEmptyHandsController()
		{
			return CoopObservedEmptyHandsController.Create(this);
		}

		private void CreateFirearmController(string itemId, bool isStationary = false, bool initial = false)
		{
			CreateFirearmControllerHandler handler = new(this);

			if (isStationary)
			{
				if (initial)
				{
					handler.item = Singleton<GameWorld>.Instance.FindStationaryWeaponByItemId(itemId).Item;
					CreateHandsController(handler.ReturnController, handler.item);
					FastForwardToStationaryWeapon(handler.item, MovementContext.Rotation, Transform.rotation, Transform.rotation);
					return;
				}
				handler.item = Singleton<GameWorld>.Instance.FindStationaryWeaponByItemId(itemId).Item;
				CreateHandsController(handler.ReturnController, handler.item);
				return;
			}
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			handler.item = result.Value;
			CreateHandsController(handler.ReturnController, handler.item);
		}

		private void CreateGrenadeController(string itemId)
		{
			CreateGrenadeControllerHandler handler = new(this);

			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			handler.item = result.Value;
			if (handler.item is GrenadeClass)
			{
				CreateHandsController(handler.ReturnController, handler.item);
			}
			else
			{
				FikaPlugin.Instance.FikaLogger.LogError($"CreateGrenadeController: Item was not of type GrenadeClass, was {handler.item.GetType()}!");
			}
		}

		private void CreateMedsController(string itemId, EBodyPart bodyPart, float amount, int animationVariant)
		{
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			CreateMedsControllerHandler handler = new(this, result.Value, bodyPart, amount, animationVariant);
			CreateHandsController(handler.ReturnController, handler.item);
		}

		private void CreateKnifeController(string itemId)
		{
			CreateKnifeControllerHandler handler = new(this);
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			handler.knife = result.Value.GetItemComponent<KnifeComponent>();
			if (handler.knife != null)
			{
				CreateHandsController(handler.ReturnController, handler.knife.Item);
			}
			else
			{
				FikaPlugin.Instance.FikaLogger.LogError($"CreateKnifeController: Item did not contain a KnifeComponent, was of type {handler.knife.GetType()}!");
			}
		}

		private void CreateQuickGrenadeController(string itemId)
		{
			CreateQuickGrenadeControllerHandler handler = new(this);
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			handler.item = result.Value;
			if (handler.item is GrenadeClass)
			{
				CreateHandsController(handler.ReturnController, handler.item);
			}
			else
			{
				FikaPlugin.Instance.FikaLogger.LogError($"CreateQuickGrenadeController: Item was not of type GrenadeClass, was {handler.item.GetType()}!");
			}
		}

		private void CreateQuickKnifeController(string itemId)
		{
			CreateQuickKnifeControllerHandler handler = new(this);
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			handler.knife = result.Value.GetItemComponent<KnifeComponent>();
			if (handler.knife != null)
			{
				CreateHandsController(handler.ReturnController, handler.knife.Item);
			}
			else
			{
				FikaPlugin.Instance.FikaLogger.LogError($"CreateQuickKnifeController: Item did not contain a KnifeComponent, was of type {handler.knife.GetType()}!");
			}
		}

		private void CreateUsableItemController(string itemId)
		{
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			CreateUsableItemControllerHandler handler = new(this, result.Value);
			CreateHandsController(handler.ReturnController, handler.item);
		}

		private void CreateQuickUseItemController(string itemId)
		{
			GStruct428<Item> result = FindItemById(itemId, false, false);
			if (!result.Succeeded)
			{
				FikaPlugin.Instance.FikaLogger.LogError(result.Error);
				return;
			}
			CreateQuickUseItemControllerHandler handler = new(this, result.Value);
			CreateHandsController(handler.ReturnController, handler.item);
		}

		public void SetAggressorData(string killerId, EBodyPart bodyPart, string weaponId)
		{
			Player killer = Singleton<GameWorld>.Instance.GetEverExistedPlayerByID(killerId);
			if (killer != null)
			{
				LastAggressor = killer;
			}
			LastBodyPart = bodyPart;
			lastWeaponId = weaponId;
		}

		private class RemoveHandsControllerHandler(ObservedCoopPlayer coopPlayer, Callback callback)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			private readonly Callback callback = callback;

			public void Handle(Result<GInterface152> result)
			{
				if (coopPlayer._removeFromHandsCallback == callback)
				{
					coopPlayer._removeFromHandsCallback = null;
				}
				callback.Invoke(result);
			}
		}

		private class CreateHandsControllerHandler(Class1149 setInHandsOperation)
		{
			public readonly Class1149 setInHandsOperation = setInHandsOperation;

			internal void DisposeHandler()
			{
				Class1149 handler = setInHandsOperation;
				if (handler == null)
				{
					return;
				}
				handler.Dispose();
			}
		}

		private class CreateFirearmControllerHandler(ObservedCoopPlayer coopPlayer)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public Item item;

			internal AbstractHandsController ReturnController()
			{
				return CoopObservedFirearmController.Create(coopPlayer, (Weapon)item);
			}
		}

		private class CreateGrenadeControllerHandler(ObservedCoopPlayer coopPlayer)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public Item item;

			internal AbstractHandsController ReturnController()
			{
				return CoopObservedGrenadeController.Create(coopPlayer, (GrenadeClass)item);
			}
		}

		private class CreateMedsControllerHandler(ObservedCoopPlayer coopPlayer, Item item, EBodyPart bodyPart, float amount, int animationVariant)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public readonly Item item = item;
			private readonly EBodyPart bodyPart = bodyPart;
			private readonly float amount = amount;
			private readonly int animationVariant = animationVariant;

			internal AbstractHandsController ReturnController()
			{
				return CoopObservedMedsController.Create(coopPlayer, item, bodyPart, amount, animationVariant);
			}
		}

		private class CreateKnifeControllerHandler(ObservedCoopPlayer coopPlayer)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public KnifeComponent knife;

			internal AbstractHandsController ReturnController()
			{
				return CoopObservedKnifeController.Create(coopPlayer, knife);
			}
		}

		private class CreateQuickGrenadeControllerHandler(ObservedCoopPlayer coopPlayer)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public Item item;

			internal AbstractHandsController ReturnController()
			{
				return CoopObservedQuickGrenadeController.Create(coopPlayer, (GrenadeClass)item);
			}
		}

		private class CreateQuickKnifeControllerHandler(ObservedCoopPlayer coopPlayer)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public KnifeComponent knife;

			internal AbstractHandsController ReturnController()
			{
				return QuickKnifeKickController.smethod_9<QuickKnifeKickController>(coopPlayer, knife);
			}
		}

		private class CreateUsableItemControllerHandler(ObservedCoopPlayer coopPlayer, Item item)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public readonly Item item = item;

			internal AbstractHandsController ReturnController()
			{
				return UsableItemController.smethod_6<UsableItemController>(coopPlayer, item);
			}
		}

		private class CreateQuickUseItemControllerHandler(ObservedCoopPlayer coopPlayer, Item item)
		{
			private readonly ObservedCoopPlayer coopPlayer = coopPlayer;
			public readonly Item item = item;

			internal AbstractHandsController ReturnController()
			{
				return QuickUseItemController.smethod_6<QuickUseItemController>(coopPlayer, item);
			}
		}
	}
}

#endregion