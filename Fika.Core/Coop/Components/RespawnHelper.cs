using System;
using System.Collections;
using System.Collections.Generic;
using EFT.InventoryLogic;
using UnityEngine;

public class RespawnHelper : MonoBehaviour
{
    public static void DelayedAction(Action action, float delay)
    {
        var go = new GameObject("RespawnHelper");
        var helper = go.AddComponent<RespawnHelper>();
        helper.StartCoroutine(helper.Run(action, delay));
    }

    private IEnumerator Run(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action();
        Destroy(gameObject);
    }

    private static InventoryController inventoryController = null;
    internal static Slot slotContents;

    public void RepairAll(InventoryController controller)
    {
        inventoryController = controller;

        RepairItems(equipmentSlotDictionary, true);
        RepairItems(weaponSlotDictionary, false);
    }

    internal static List<EquipmentSlot> equipmentSlotDictionary = new()
    {
            { EquipmentSlot.ArmorVest},
            { EquipmentSlot.TacticalVest},
            { EquipmentSlot.Eyewear},
            { EquipmentSlot.FaceCover},
            { EquipmentSlot.Headwear},
        };

    internal static List<EquipmentSlot> weaponSlotDictionary = new()
    {
            { EquipmentSlot.FirstPrimaryWeapon },
            { EquipmentSlot.SecondPrimaryWeapon },
            { EquipmentSlot.Holster },
        };


    private static Slot GetEquipSlot(EquipmentSlot slot)
    {
        if (inventoryController != null)
        {
            slotContents = inventoryController.Inventory.Equipment.GetSlot(slot);
            return slotContents.ContainedItem == null ? null : slotContents;
        }
        return null;
    }

    private static void RepairItems(List<EquipmentSlot> slots, bool isArmor)
    {
        float repairRate = isArmor ? 2f : 2f;

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
                    component.Durability = component.MaxDurability;
                }
            }
        }
    }
}