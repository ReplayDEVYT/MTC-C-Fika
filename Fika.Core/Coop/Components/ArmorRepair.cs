using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace Fika.Core.Coop.Components
{
    // THANK YOU DVIZE/WIZARD83 FOR ORIGINAL CODE
    // literally copy pasted from ASS, slight changes. only done so that people cant mess with the code
    internal class ArmorRepair : MonoBehaviour
    {
        internal static ManualLogSource Logger;
        internal static GameWorld gameWorld;
        internal static Player player;
        internal static float timeSinceLastRepair = 0f; // New variable to control repair frequency
        internal static Slot slotContents;
        internal static InventoryController inventoryController;

        internal static List<EquipmentSlot> equipmentSlotDictionary = new List<EquipmentSlot>
        {
            { EquipmentSlot.ArmorVest},
            { EquipmentSlot.TacticalVest},
            { EquipmentSlot.Eyewear},
            { EquipmentSlot.FaceCover},
            { EquipmentSlot.Headwear},
        };

        internal static List<EquipmentSlot> weaponSlotDictionary = new List<EquipmentSlot>
        {
            { EquipmentSlot.FirstPrimaryWeapon },
            { EquipmentSlot.SecondPrimaryWeapon },
            { EquipmentSlot.Holster },
        };

        private void Awake()
        {
            if (Logger == null)
                Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(ArmorRepair));
        }

        private void Start()
        {
            player = gameWorld.MainPlayer;
            inventoryController = (InventoryController)AccessTools.Field(typeof(Player), "_inventoryController").GetValue(player);
            Logger.LogDebug("AssComponent enabled successfully.");
        }

        private void Update()
        {
            timeSinceLastRepair += Time.deltaTime;

            if (timeSinceLastRepair >= 1.0f)
            {
                RepairItems(equipmentSlotDictionary, true);

                RepairItems(weaponSlotDictionary, false);

                timeSinceLastRepair = 0f;
            }
        }

        private static void RepairItems(List<EquipmentSlot> slots, bool isArmor)
        {
            float repairRate = isArmor ? 0.5f : 0.5f;

            foreach (var slot in slots)
            {
                var slotContents = GetEquipSlot(slot);
                if (slotContents?.ContainedItem == null) continue;

                foreach (Item item in slotContents.ContainedItem.GetAllItems())
                {
                    if (isArmor)
                    {
                        // Specifically for face shields, if the item has a FaceShieldComponent.
                        if (item.TryGetItemComponent<FaceShieldComponent>(out var faceShield))
                        {
                            // Check if face shield has been hit and reset hits if so.
                            if (faceShield.Hits > 0)
                            {
                                faceShield.Hits = 0;
                                faceShield.HitsChanged?.Invoke();
                            }
                        }
                    }

                    if (item.TryGetItemComponent<RepairableComponent>(out var component))
                    {
                        float maxCap = isArmor ? 100 : 100;
                        float maxRepairableDurability = (maxCap / 100) * component.MaxDurability;

                        if (component.Durability < maxRepairableDurability)
                        {
#if DEBUG
                            Logger.LogWarning($"Repairing {item.Name.Localized()} in {slot} with {component.Durability} / {component.MaxDurability} durability");
#endif
                            component.Durability = Mathf.Min(component.Durability + repairRate, component.MaxDurability);
                        }
                    }
                }
            }
        }

        private static Slot GetEquipSlot(EquipmentSlot slot)
        {
            if (inventoryController != null)
            {
                slotContents = inventoryController.Inventory.Equipment.GetSlot(slot);
                return slotContents.ContainedItem == null ? null : slotContents;
            }
            return null;
        }

        internal static void Enable()
        {
            if (Singleton<IBotGame>.Instantiated)
            {
                gameWorld = Singleton<GameWorld>.Instance;
                gameWorld.GetOrAddComponent<ArmorRepair>();
            }
        }
    }
}