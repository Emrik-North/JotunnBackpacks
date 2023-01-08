using System;
using BepInEx.Bootstrap;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using JotunnBackpacks.Data;
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

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
        static class Inventory_Changed_Patch
        {
            // Saving the backpack every time it's changed is marginally more expensive than the alternative, but it's safer and a lot tidier.
            // The alternative would be to patch every method involved in moving the backpack out of the inventory, which includes dropitem, 4 overloaded moveinventorytothis methods, and more.
            // When you drop an item, you remove the original instance and drop a cloned instance. A solution to this is to serialize the Inventory instance into the ItemData m_crafterName before it's moved.
            static void Postfix(Inventory __instance)
            {
                // If the inventory changed belongs to a backpack...
                if (__instance.m_name == JotunnBackpacks.backpackInventoryName)
                {
                    // Save the backpack, but only if it's equipped. (This is a workaround to ExtendedItemDataFrameWork_AddItemFromLoad_Patch)
                    var backpack = JotunnBackpacks.GetEquippedBackpack();
                    if (backpack != null) backpack.Data().GetOrCreate<BackpackComponent>().Save();
                }
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight))]
        static class GetWeight_Patch
        {
            static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {
                try
                {
                    if (__instance == null || string.IsNullOrEmpty(__instance.m_shared.m_name))
                        return;

                    if (JotunnBackpacks.backpackTypes.Contains(__instance.m_shared.m_name))
                    {
                        // If the item in GetWeight() is a backpack, and it has been Extended(), call GetTotalWeight() on its Inventory.
                        // Note that GetTotalWeight() just returns a the value of m_totalWeight, and doesn't do any calculation on its own.
                        // If the Inventory has been changed at any point, it calls UpdateTotalWeight(), which should ensure that its m_totalWeight is accurate.
                        var inventoryWeight = __instance.Data().GetOrCreate<BackpackComponent>().GetInventory()?.GetTotalWeight() ?? 0;

                        // To the backpack's item weight, add the backpack's inventory weight multiplied by the weightMultiplier in the configs.
                        __result += inventoryWeight * JotunnBackpacks.weightMultiplier.Value;
                    }
                }
                catch (Exception e)
                {
                    Log.LogDebug($"[ItemDrop.ItemData.GetWeight] An Error occurred - {e.Message}");
                }
            }
        }
        
        // If the player drops the backpack while the backpack inventory is open, the backpack inventory closes.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        static class Humanoid_UnequipItem_Patch
        {
            // The "__instance" here is a Humanoid type, but we want the ItemData argument, so we use "__0" instead.
            // "__0" fetches the argument passed into the first parameter of the original method, which in this case is an ItemData object.
            static void Prefix(ItemDrop.ItemData __0) 
            {
                if (__0 is null) return;
                var player = Player.m_localPlayer;
                if (!player) return;

                var item = __0;

                // Check if the item being unequipped is a backpack, and see if it is the same backpack the player is wearing
                if (JotunnBackpacks.backpackTypes.Contains(item.m_shared.m_name)
                    && player.m_shoulderItem == item)
                {
                    var backpackInventory = JotunnBackpacks.backpackContainer?.m_inventory;
                    if (backpackInventory is null) return;

                    //Save Backpack
                    var backpackComponent = item.Data().GetOrCreate<BackpackComponent>();
                    backpackComponent.Save(backpackInventory);

                    var inventoryGui = InventoryGui.instance;

                    // Close the backpack inventory if it's currently open
                    if (inventoryGui.IsContainerOpen())
                    {
                        inventoryGui.CloseContainer();
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

            static void Postfix(Inventory __instance)
            {
                var player = Player.m_localPlayer;
                
                if (__instance.GetName() == JotunnBackpacks.backpackInventoryName)
                {
                    // When the equipped backpack inventory total weight is updated, the player inventory total weight should also be updated.
                    if (player)
                    {
                        player.GetInventory().UpdateTotalWeight();
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
                            if (!item.Data().GetOrCreate<BackpackComponent>().GetInventory().IsTeleportable())
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

        [HarmonyPatch(typeof(ArmorStand), nameof(ArmorStand.DropItem))]
        public static class ArmorStand_DropItem_Patch
        {
            public static bool Prefix(ArmorStand __instance, int index)
            {
                if (!__instance.HaveAttachment(index))
                    return false;

                Log.LogDebug($"[ArmorStand.DropItem] Starting for name {__instance.m_name}");

                if (JotunnBackpacks.backpackTypes.Contains(__instance.m_name))
                {
                    GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(__instance.m_nview.GetZDO().GetString(index.ToString() + "_item"));

                    if (itemPrefab != null)
                    {
                        GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(itemPrefab, __instance.m_dropSpawnPoint.position, __instance.m_dropSpawnPoint.rotation);
                        ItemDrop itemDrop = gameObject.GetComponent<ItemDrop>();
                        ItemDrop.LoadFromZDO(index, itemDrop.m_itemData, __instance.m_nview.GetZDO());
                        var itemData = itemDrop.m_itemData;

                        var instanceData = itemData.Data().Get<BackpackComponent>();

                        if (instanceData != null)
                        {
                            Log.LogDebug($"[ArmorStand.DropItem] instanceData not null");
                            instanceData.Save();
                            itemDrop.m_itemData = instanceData.Item;
                        }


                        itemDrop.Save();

                        gameObject.GetComponent<Rigidbody>().velocity = Vector3.up * 4f;
                        __instance.m_destroyEffects.Create(__instance.m_dropSpawnPoint.position, Quaternion.identity);

                    }

                    __instance.m_nview.GetZDO().Set(index.ToString() + "_item", "");
                    __instance.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetVisualItem", index, "", 0);
                    __instance.UpdateSupports();
                    __instance.m_cloths = __instance.GetComponentsInChildren<Cloth>();

                    return false;
                }

                return true;
            }
        }
    }
}