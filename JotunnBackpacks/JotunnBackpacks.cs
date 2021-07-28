/* JotunnBackpacks.cs
 * 
 * CREDIT:
 * Evie/CinnaBunn for their 'eviesbackpacks' assets inside JotunnModExample: https://github.com/Valheim-Modding/JotunnModExample/tree/master/JotunnModExample/AssetsEmbedded
 * Randy Knapp for their Extended Item Framework, without which I wouldn't be able to do this project: https://github.com/RandyKnapp/ValheimMods/tree/main/ExtendedItemDataFramework
 * Aedenthorn for their BackpackRedux mod, which I learned a great deal from: https://github.com/aedenthorn/ValheimMods/blob/master/BackpackRedux/
 * sbtoonz for help with the IsTeleportable_Patch: https://github.com/VMP-Valheim/Back_packs
 * The Jotunn Team for creating Jotunn: The Valheim Library, which makes modding life a lot more convenient: https://valheim-modding.github.io/Jotunn/index.html
 * 
 * Most of this project is the result of the hard work of these people. All I've done is combine their efforts into this mod and smoothed out issues.
 * 
 * COMMENT PHILOSOPHY:
 * I usually comment my code heavily. My comment philosophy is to imagine trying to help previous less informed versions of me learn exactly what's going on in the code.
 * And when I *still* don't understand what's going on in the code even though it works, I'll try to let readers know that so they don't accidentally learn bad practices from me.
 * 
 * GitHub: https://github.com/Emrik-North/JotunnBackpacks
 */

using BepInEx;
using BepInEx.Configuration;
using Jotunn.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using ExtendedItemDataFramework;


/* TODO:
 * Enforce everyone on server must have mod.
 * 
 * TODO SERVER! Backpack inventories do not get saved (neither if the backpack is in a chest or if it's on the player) on a server without the mod, at least one with SSC (haven't tested without).
 * If the server has the mod plugin, inventories get saved, both with backpacks in chests and on player.
 * TODO: Test with SSC.
 * 
 * TODO: Make it so that backpacks also protect against the cold, at least the silver one!
 * 
 * TODO: Backpacks on the ground while logging off will despawn! Why aren't they saved in the world?
 * 
 * TODO: If .dll is on a server, disconnect clients without the mod.
 * 
 * TODO: Sometimes player inventory is loaded with all durability-meters displaying as 0, even though they're at full durability. I can use the sword and it updates its displayed durability to its real value.
 * UPDATE: It's not to do with the items themselves, but the slots they're in.
 * 
 */

namespace JotunnBackpacks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("randyknapp.mods.extendeditemdataframework")]

    // This attribute is set such that both server and clients need to have this mod, and the same version of this mod, otherwise the client cannot connect.
    // This is to prevent cases such as when a client logs on, creates a backpack, puts it in a container, logs off, and then another client without the mod opens the container, and the backpack gets destroyed.
    // Read more about Jotunn's NetworkCompatibilityAttribute here: https://valheim-modding.github.io/Jotunn/tutorials/networkcompatibility.html
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class JotunnBackpacks : BaseUnityPlugin
    {
        public const string PluginGUID = "Emrik-North.JotunnBackpacks";
        public const string PluginName = "JotunnBackpacks";
        public const string PluginVersion = "0.0.2";

        // From Aedenthorn's Backpack Redux
        public static ConfigEntry<string> hotKey;
        public static Vector2 backpackSize;
        public static float backpackWeightMult;
        private static bool opening = false;
        private static Container backpackContainer; // Only need a single Container, I think, because only the contents (Inventory) vary between backpacks, not sizes.

        // Emrik's adventures
        private static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only $item_cape_ironbackpack and $item_cape_silverbackpack from CinnaBunn)
        private static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped
        private static List<ItemDrop.ItemData> backpacksToSave = new List<ItemDrop.ItemData>(); // All the backpack objects modified by player since last time they were saved
        public static string backpackInventoryName = "Backpack";

        // Emrik is new to C#. I took dealing with the assets out of the main file to make it tidier, and put it into its own class outside the file.
        // I'm creating an instance of the class BackpackAssets, otherwise I can't use the stuff in there.
        // I'm making this instance readonly, because I never modify it after creating it, and I assume readonly is more efficient.
        readonly BackpackAssets assets = new BackpackAssets();

        private Harmony harmony;

        // Awake() is run when the game loads up.
        private void Awake()
        {
            assets.LoadAssets();
            assets.AddTranslations();
            assets.AddMockedItems();
            backpackTypes.Add("$item_cape_ironbackpack");
            backpackTypes.Add("$item_cape_silverbackpack");

            hotKey = Config.Bind<string>("General", "HotKey", "i", "Hotkey to open backpack.");

            // Manually setting values for Aedenthorn's BackpackRedux configs.
            backpackSize = new Vector2(6, 3);
            backpackWeightMult = 0.75f; // Items stored in backpacks are 25% lighter. Non-recursive.

            // The "NewExtendedItemData" event is run whenever a newly created item is extended by the ExtendedItemDataFramework.dll, I'm just catching it and appending my own code at the end of it
            ExtendedItemData.NewExtendedItemData += OnNewExtendedItemData;

            harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();

        }

        //  This is the code appended to the NewExtendedItemData event that we're catching, and the argument passed in automatically is the newly generated extended item data.
        public static void OnNewExtendedItemData(ExtendedItemData itemData)
        {
            // TODO: What's the difference between calling itemData.dropPrefab.name and itemData.m_shared.m_name?
            // Apparently having the RRRNpcs.dll in the game makes the former throw a nullreference when a backpack is spawned, but not the latter.


            // I check whether the item created is of a type contained in backpackTypes
            if (backpackTypes.Contains(itemData.m_shared.m_name))
            {
                // Create an instance of an Inventory type
                Inventory inventoryInstance = NewInventoryInstance();

                // Add an empty BackpackComponent to the backpack item
                var component = itemData.AddComponent<BackpackComponent>();

                // Assign the Inventory instance to the backpack item's BackpackComponent
                component.SetInventory(inventoryInstance);
            }

        }

        public static Inventory NewInventoryInstance()
        {
            return new Inventory(
                backpackInventoryName,
                null,
                (int)backpackSize.x,
                (int)backpackSize.y
                );

        }

        private static ItemDrop.ItemData GetEquippedBackpack()
        {
            // Get a list of all equipped items.
            List<ItemDrop.ItemData> equippedItems = Player.m_localPlayer.GetInventory().GetEquipedtems();

            // Go through all the equipped items, match them for any of the names in backpackTypes.
            // If a match is found, return the backpack ItemData object.
            foreach (ItemDrop.ItemData item in equippedItems)
            {
                if (backpackTypes.Contains(item.m_shared.m_name))
                {
                    return item;
                }
            }

            // Return null if no backpacks are found.
            return null;

        }

        // This method is from Aedenthorn's BackpackRedux.
        private void Update()
        {
            if (!Player.m_localPlayer || !ZNetScene.instance)
                return;

            if (!AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown(hotKey.Value) && CanOpenBackpack())
            {
                opening = true;
                OpenBackpack();
            }

        }

        // Every time this function is executed, it sets backpackEquipped to either null, or the name of the then-equipped backpack. 
        private static bool CanOpenBackpack()
        {
            backpackEquipped = GetEquippedBackpack();

            // Return true if GetEquippedBackpack() does not return null.
            if (backpackEquipped != null)
            {
                return true;
            }

            // Return false if GetEquippedBackpack() returns null.
            Jotunn.Logger.LogMessage("No backpack equipped. Can't open any.");
            return false;

        }

        private static void OpenBackpack()
        {
            backpackContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (backpackContainer == null)
                backpackContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();

            AccessTools.FieldRefAccess<Container, Inventory>(backpackContainer, "m_inventory") = backpackEquipped.Extended().GetComponent<BackpackComponent>().GetInventory();
            InventoryGui.instance.Show(backpackContainer);

            // If backpacksToSave list doesn't already have this backpack in it, add it to the list so that the code knows to save the contents of this backpack when it saves the player profile
            if (!backpacksToSave.Contains(backpackEquipped))
            {
                backpacksToSave.Add(backpackEquipped);
            }

        }

        // This method is from Aedenthorn's BackpackRedux.
        [HarmonyPatch(typeof(InventoryGui), "Update")]
        static class InventoryGui_Update_Patch
        {
            static void Postfix(Animator ___m_animator, ref Container ___m_currentContainer)
            {
                if (!AedenthornUtils.CheckKeyDown(hotKey.Value) || !Player.m_localPlayer || !___m_animator.GetBool("visible"))
                    return;

                if (opening)
                {
                    opening = false;
                    return;
                }

                if (___m_currentContainer != null && ___m_currentContainer == backpackContainer)
                {
                    ___m_currentContainer = null;
                }

                else if (CanOpenBackpack())
                {
                    OpenBackpack();
                }
            }

        }

        [HarmonyPatch(typeof(Game), "SavePlayerProfile")]
        static class SavePlayerProfile_Patch
        {
            static void Prefix()
            {
                // Iff there are any items inside the backpacksToSave list, go through a foreach loop to save them all.
                if (backpacksToSave.Any())
                {
                    Jotunn.Logger.LogMessage("Saving backpacks.");
                    foreach (ItemDrop.ItemData backpack in backpacksToSave)
                    {
                        backpack.Extended().Save();
                    }

                    // Since we have saved all the backpacks we needed to save, we can clear the list.
                    backpacksToSave.Clear();
                }
            }

        }

        /* // Just in case I need this method later, I'll leave it here
        private static ItemDrop.ItemData GetBackpackRefFromInventory(Inventory backpackInventory)
        {
            // Get a list of all items in player inventory.
            List<ItemDrop.ItemData> items = Player.m_localPlayer.GetInventory().GetAllItems();

            // Go through all the items, match them for any of the names in backpackTypes.
            foreach (ItemDrop.ItemData item in items)
            {
                if (backpackTypes.Contains(item.m_shared.m_name))
                {
                    // If backpackInventory is the same instance as the item's Inventory instance, then we know that this item is the one we're looking for
                    if (Object.ReferenceEquals(item.Extended().GetComponent<BackpackComponent>().GetInventory(), backpackInventory))
                    {
                        return item;
                    }
                }
            }

            // Return null if no backpacks were found.
            return null;
        */

        private static void EjectBackpack(ItemDrop.ItemData item, Player player, Inventory backpackInventory)
        {
            var playerInventory = player.GetInventory();

            // Move the backpack to the player's Inventory if there's room
            if (playerInventory.HaveEmptySlot())
            {
                playerInventory.MoveItemToThis(backpackInventory, item);
            }

            // Otherwise drop the backpack
            else
            {
                Jotunn.Logger.LogMessage("Clever... But you're still not gonna cause backpackception!");

                // Remove the backpack item from the Inventory instance and then drop the backpack item in front of the player.
                backpackInventory.RemoveItem(item);
                ItemDrop.DropItem(item, 1, player.transform.position + player.transform.forward + player.transform.up, player.transform.rotation);

                // OBS! ItemDrop.DropItem() causes OnNewExtendedItemData() and creates a new backpack with a (presumably) new GUID.
                // But it gets assigned the same Inventory instance, so all the items are preserved.
                // I haven't explored this, because it seems to work, but just writing this here in case I need to debug something related.
            }

        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight))]
        static class GetWeight_Patch
        {

            static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {

                if (backpackTypes.Contains(__instance.m_shared.m_name))
                {
                    if (__instance.IsExtended())
                    {
                        // If the item in GetWeight() is a backpack, and it has been Extended(), call GetTotalWeight() on its Inventory.
                        // Note that GetTotalWeight() just returns a the value of m_totalWeight, and doesn't do any calculation on its own.
                        // If the Inventory has been changed at any point, it calls UpdateTotalWeight(), which should ensure that its m_totalWeight is accurate.
                        var inventoryWeight = __instance.Extended().GetComponent<BackpackComponent>().GetInventory().GetTotalWeight();

                        __result += inventoryWeight * backpackWeightMult; // If the BackpackComponent's weight hasn't been assigned yet, its default value is zero anyway.
                    }
                }
            }

        }

        [HarmonyPatch(typeof(Inventory), "UpdateTotalWeight")]
        static class UpdateTotalWeight_Patch
        {
            static void Prefix(Inventory __instance)
            {
                // If the current Inventory instance belongs to a backpack...
                if (__instance.GetName() == backpackInventoryName)
                {
                    // Get a list of all items in the backpack.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();
                    var player = Player.m_localPlayer;

                    // Go through all the items, match them for any of the names in backpackTypes.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        // If the item is a backpack...
                        if (backpackTypes.Contains(item.m_shared.m_name))
                        {
                            Jotunn.Logger.LogMessage("You can't put a backpack inside a backpack, silly!");
                            EjectBackpack(item, player, __instance);

                            // There is only ever one backpack in the backpack inventory, so we don't need to continue the loop once we've chucked it out
                            // Besides, you'll get a "InvalidOperationExecution: Collection was modified; enumeration operation may not execute" error if you don't break the loop here :p
                            break;
                        }
                    }
                }
            }

        }

       [HarmonyPatch(typeof(Inventory), "IsTeleportable")]
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
                        if (backpackTypes.Contains(item.m_shared.m_name))
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

        [HarmonyPatch(typeof(EnvMan), "IsCold")]
        static class IsCold_Patch
        {
            static void Postfix(bool __result)
            {
                // If you're wearing a backpack, you are not cold.
                if (GetEquippedBackpack() != null) __result = false;
            }

        }

        [HarmonyPatch(typeof(EnvMan), "IsFreezing")]
        static class IsFreezing_Patch
        {
            static void Postfix(bool __result)
            {
                // If you're wearing a backpack, you are not freezing.
                if (GetEquippedBackpack() != null) __result = false;
            }

        }

    }
}