using BepInEx.Bootstrap;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ExtendedItemDataFramework;
using Log = Jotunn.Logger;

namespace JotunnBackpacks
{
    internal class Patches
    {
        // This patch is from Aedenthorn's BackpackRedux.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        static class InventoryGui_Update_Patch
        {
            static void Postfix(Animator ___m_animator, ref Container ___m_currentContainer)
            {
                if (!AedenthornUtils.CheckKeyDown(JotunnBackpacks.hotKey_open.Value) || !Player.m_localPlayer || !___m_animator.GetBool("visible"))
                    return;

                if (JotunnBackpacks.opening)
                {
                    JotunnBackpacks.opening = false;
                    return;
                }

                if (___m_currentContainer != null && ___m_currentContainer == JotunnBackpacks.backpackContainer)
                {
                    ___m_currentContainer = null;
                }

                else if (JotunnBackpacks.CanOpenBackpack())
                {
                    JotunnBackpacks.OpenBackpack();
                }
            }

        }

        // Saving the backpack every time it's changed is marginally more expensive than the alternative, but it's safer and a lot tidier.
        // The alternative would be to patch every method involved in moving the backpack out of the inventory, which includes dropitem, 4 overloaded moveinventorytothis methods, and more.
        // When you drop an item, you remove the original instance and drop a cloned instance. A solution to this is to serialize the Inventory instance into the ItemData m_crafterName before it's moved.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
        static class Inventory_Changed_Patch
        {
            static void Postfix(Inventory __instance)
            {
                // If the inventory changed belongs to a backpack...
                if (__instance.m_name == JotunnBackpacks.backpackInventoryName)
                {
                    // Save the backpack, but only if it's equipped. (This is a workaround to ExtendedItemDataFrameWork_AddItemFromLoad_Patch)
                    var backpack = JotunnBackpacks.GetEquippedBackpack();
                    if (backpack != null) backpack.Extended().Save();
                }
            }

        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight))]
        static class GetWeight_Patch
        {
            static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {
                if (JotunnBackpacks.backpackTypes.Contains(__instance.m_shared.m_name))
                {
                    if (__instance.IsExtended())
                    {
                        // If the item in GetWeight() is a backpack, and it has been Extended(), call GetTotalWeight() on its Inventory.
                        // Note that GetTotalWeight() just returns a the value of m_totalWeight, and doesn't do any calculation on its own.
                        // If the Inventory has been changed at any point, it calls UpdateTotalWeight(), which should ensure that its m_totalWeight is accurate.
                        var inventoryWeight = __instance.Extended().GetComponent<BackpackComponent>().GetInventory().GetTotalWeight();

                        // To the backpack's item weight, add the backpack's inventory weight multiplied by the weightMultiplier in the configs.
                        __result += inventoryWeight * JotunnBackpacks.weightMultiplier.Value;
                    }
                }
            }

        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.UpdateTotalWeight))]
        static class UpdateTotalWeight_Patch
        {
            static void Prefix(Inventory __instance)
            {
                // If the current Inventory instance belongs to a backpack...
                if (__instance.GetName() == JotunnBackpacks.backpackInventoryName)
                {
                    // Get a list of all items in the backpack.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();
                    var player = Player.m_localPlayer;

                    // Go through all the items, match them for any of the names in backpackTypes.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        // If the item is a backpack...
                        if (JotunnBackpacks.backpackTypes.Contains(item.m_shared.m_name))
                        {
                            // Chuck it out!
                            Log.LogMessage("You can't put a backpack inside a backpack, silly!");
                            JotunnBackpacks.EjectBackpack(item, player, __instance);

                            // There is only ever one backpack in the backpack inventory, so we don't need to continue the loop once we've chucked it out.
                            // Besides, you'll get a "InvalidOperationExecution: Collection was modified; enumeration operation may not execute" error if you don't break the loop here :p
                            break;
                        }
                    }
                }
            }

        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsTeleportable))]
        static class IsTeleportable_Patch
        {
            static void Postfix(Inventory __instance, ref bool __result)
            {
                // If the inventory being checked for teleportability is the Player's inventory, see whether it contains any backpacks, and then check the backpack inventories for teleportability too
                if (__instance == Player.m_localPlayer.GetInventory())
                {
                    // Get a list of all items on the player.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();

                    // Go through all the items, match them for any of the names in backpackTypes.
                    // For each match found, check if the Inventory of that backpack is teleportable.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        if (JotunnBackpacks.backpackTypes.Contains(item.m_shared.m_name))
                        {
                            if (!item.Extended().GetComponent<BackpackComponent>().GetInventory().IsTeleportable())
                            {
                                // A backpack's inventory inside player inventory was not teleportable.
                                __result = false;
                                return;
                            }
                        }
                    }
                }

                // We don't need to search for backpacks inside backpacks, because those are immediately chucked out when you try to put them in anyway.
            }

        }

        // This method is straight off from Randy Knapp's Equipment & Quick Slots mod.
        // It fixes a bug where sometimes the durability bar is set to zero length in some container slots if you have weapons/equipments in them.
        [HarmonyPatch(typeof(GuiBar), "Awake")]
        public static class GuiBar_Awake_Patch
        {
            private static bool Prefix(GuiBar __instance)
            {
                // Since EAQS already includes this patch, we only want to include the following code if EAQS isn't installed
                if (!Chainloader.PluginInfos.ContainsKey(JotunnBackpacks.eaqsGUID) && __instance.name == "durability" && __instance.m_bar.sizeDelta.x != 54)
                {
                    // Set the durability bar to normal length, if it isn't already normal length
                    __instance.m_bar.sizeDelta = new Vector2(54, 0);
                }
                return true;
            }

        }

        // TODO: This is dirty... but it's here until I find out how to do it the proper way.
        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.IsCold))]
        static class IsCold_Patch
        {
            static void Postfix(ref bool __result)
            {
                // If you're wearing a backpack, you are not cold.
                if (JotunnBackpacks.GetEquippedBackpack() != null) __result = false;
            }

        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.IsFreezing))]
        static class IsFreezing_Patch
        {
            static void Postfix(ref bool __result)
            {
                // If you're wearing a backpack, you are not freezing.
                if (JotunnBackpacks.GetEquippedBackpack() != null) __result = false;
            }

        }

    }
}