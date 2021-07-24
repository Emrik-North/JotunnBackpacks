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
 * MOD INCOMPATIBILITY: Seems to be incompatible with RRRNpcs.dll and RRRBetterRaids.dll (seperately). Gotta figure out what's going on there.
 * 
 * TODO SERVER! Backpack inventories do not get saved (neither if the backpack is in a chest or if it's on the player) on a server without the mod, at least one with SSC (haven't tested without).
 * If the server has the mod plugin, inventories get saved, both with backpacks in chests and on player.
 * TODO: Test with SSC.
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
        public const string PluginVersion = "0.0.1";

        // From Aedenthorn's Backpack Redux
        public static ConfigEntry<string> hotKey;
        public static Vector2 backpackSize;
        public static float backpackWeightMult;
        private static bool opening = false;
        private static Container backpackContainer; // Only need a single Container, I think, because only the contents (Inventory) vary between backpacks, not sizes.

        // Emrik's adventures
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only $item_cape_ironbackpack and $item_cape_silverbackpack from CinnaBunn)
        public static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped
        public static List<ItemDrop.ItemData> backpacksToSave = new List<ItemDrop.ItemData>(); // All the backpack objects modified by player since last time they were saved

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
            // TODO: What's the difference between calling itemData.dropPrefab.name and itemData.m_shared.m_name???
            // Apparently having the RRRNpcs.dll in the game makes the former throw a nullreference when a backpack is spawned, but not the latter.


            // I check whether the item created is of a type contained in backpackTypes
            if (backpackTypes.Contains(itemData.m_shared.m_name))
            {
                Jotunn.Logger.LogMessage($"OnNewExtendedItemData! itemData.m_shared.m_name: {itemData.m_shared.m_name}");

                // Create an instance of an Inventory type
                Inventory inventoryInstance = NewInventoryInstance();

                // Add an empty BackpackComponent to the backpack item
                itemData.AddComponent<BackpackComponent>();

                Jotunn.Logger.LogMessage("New backpack created! Adding an Inventory component to it.");

                // Assign the Inventory instance to the backpack item's BackpackComponent
                itemData.GetComponent<BackpackComponent>().SetBackpackInventory(inventoryInstance);
            }
        }

        public static Inventory NewInventoryInstance()
        {
            return new Inventory(
                "Backpack", // IMPORTANT! If you change this field from "Backpack", you need to also change it in GetTotalWeight_Patch and IsTeleportable_Patch down below.
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

        // This method is from Aedenthorn's BackpackRedux. I can't see this method being called anywhere, but I tested and it's essential for mod functionality. Where is it being called?
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

            AccessTools.FieldRefAccess<Container, Inventory>(backpackContainer, "m_inventory") = backpackEquipped.Extended().GetComponent<BackpackComponent>().backpackInventory; // GetBackpackInventory(backpackEquipped);
            InventoryGui.instance.Show(backpackContainer);

            // If backpacksToSave list doesn't already have this backpack in it, add it to the list so that the code knows to save the contents of this backpack when it saves the player profile
            if (!backpacksToSave.Contains(backpackEquipped))
            {
                backpacksToSave.Add(backpackEquipped);
            }
        }

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
                    // TODO: Is this necessary? Isn't the Inventory saved on updates automatically? Test without it.
                    // GetEquippedBackpack().Extended().GetComponent<BackpackComponent>().backpackInventory = backpackContainer.GetInventory();
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
            static void Prefix() // TODO: Should this be Prefix or Postfix? Do I want to save the backpacks before or after the player inventory?
            {
                // Iff there are any items inside the backpacksToSave list, go through a foreach loop to save them all.
                if (backpacksToSave.Any())
                {
                    foreach (ItemDrop.ItemData backpack in backpacksToSave)
                    {
                        backpack.Extended().Save();
                    }

                    // Since we have saved all the backpacks we needed to save, we can clear the list.
                    backpacksToSave.Clear();
                }
            }
        }

        private static void EjectBackpacks(Inventory inventory)
        {
            // Get a list of all items in the Inventory object.
            List<ItemDrop.ItemData> items = inventory.GetAllItems();

            // Go through all the items, match them for any of the names in backpackTypes.
            foreach (ItemDrop.ItemData item in items)
            {
                if (backpackTypes.Contains(item.m_shared.m_name))
                {
                    item.m_dropPrefab.GetInstanceID();
                    ItemDrop.DropItem(item, 1, Player.m_localPlayer.transform.position, Quaternion.identity);
                }
            }
        }

        // TODO:
        // PREFIX check the inventory for backpacks, and update each backpack ItemData.Extended().m_shared.m_weight with the GetTotalWeight of their unique Inventories, multiplied by their backpackWeightMult.
        // THEN GetTotalWeight of the player Inventory.
        // POSTFIX multiply the player inventory 
        // 
        // ALSO in the Prefix, before updating the weight of backpack ItemDatas, check the backpack Inventory for backpack items, and eject them if any are found.

        // TODO: Make Inventories update the _backpack item's_ weight, rather than applying a += on the __result. Skips over all the recursion nonsense, and also looks more sensicle in game.
        [HarmonyPatch(typeof(Inventory), "GetTotalWeight")]
        [HarmonyPriority(Priority.Last)]
        static class GetTotalWeight_Patch
        {
            /*
            static void Prefix(Inventory __instance)
            {

                // If the current Inventory instance is a backpack, eject all backpacks inside
                if (__instance.GetName() == "Backpack")
                {
                    EjectBackpacks(__instance);

                }

                if (__instance.GetName() == "Backpack")
                {
                    // Get a list of all items on the player.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();

                    // Go through all the items, match them for any of the names in backpackTypes.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        if (backpackTypes.Contains(item.m_shared.m_name))
                        {
                            // ReferenceEquals checks if __instance is the same object instance as the current backpack Inventory instance in the loop.
                            // This is so that we can get to the ItemData instance by knowing the Inventory instance.
                            if (ReferenceEquals(item.Extended().GetComponent<BackpackComponent>().backpackInventory, __instance))
                            {
                                // Just testing stuff here
                                Jotunn.Logger.LogMessage("__INSTANCE IS BACKPACK INVENTORY. SETTING m_weight TO 131.");
                                item.Extended().m_shared.m_weight = 131;
                            }

                        }
                    }
                }
            }
            */

            static void Postfix(Inventory __instance, ref float __result)
            {
                // If calculating the weight of the player's inventory, run this:
                if (__instance == Player.m_localPlayer.GetInventory())
                {
                    // Get a list of all items on the player.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();

                    // Go through all the items, match them for any of the names in backpackTypes.
                    // For all matches found, add the backpack inventory weight to the total weight.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        if (backpackTypes.Contains(item.m_shared.m_name))
                        {
                            __result += item.Extended().GetComponent<BackpackComponent>().backpackInventory.GetTotalWeight();
                            
                        }
                    }
                }

                // If calculating the weight of a backpack's inventory, run this:
                else if (__instance.GetName() == "Backpack") // It's shoddy, but the easiest way I could think of to check whether we are weighing backpack inventories in particular
                {
                    // We start by assuming the backpack inventory contains no other backpacks.
                    bool containsBackpack = false;

                    // Get a list of all items in the backpack.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();

                    // Go through all the items, match them for any of the names in backpackTypes.
                    // For all matches found, add the backpack inventory weight to the total weight.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        if (backpackTypes.Contains(item.m_shared.m_name))
                        {
                            // The backpack inventory currently being calculated contains a backpack!
                            containsBackpack = true;
                            __result += item.Extended().GetComponent<BackpackComponent>().backpackInventory.GetTotalWeight();
                        }
                    }

                    // If the backpack Inventory currently being calculated does not contain a backpack, multiply its weight by backpackWeightMult.
                    // So if you have several iterations of backpacks inside backpacks, only the innermost backpacks get a backpackWeightMult modifier.
                    // This means that you can't exploit the backpackWeightMult by recursively adding backpacks into backpacks!
                    if (!containsBackpack)
                    {
                        __result *= backpackWeightMult;
                    }
                    
                }
            }
        }

       [HarmonyPatch(typeof(Inventory), "IsTeleportable")]
        static class IsTeleportable_Patch
        {
            static void Postfix(Inventory __instance, ref bool __result)
            {
                bool canTeleport;

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
                            canTeleport = item.Extended().GetComponent<BackpackComponent>().backpackInventory.IsTeleportable();
                            if (!canTeleport)
                            {
                                // A backpack's inventory inside player inventory was not teleportable.
                                __result = false;
                                return;
                            }
                        }
                    }
                }

                // Any time the game checks a backpack inventory for teleportability, it will check its inventory for backpacks and call IsTeleportable() for those too. This works recursively, so good luck trying to exploit it!
                else if (__instance.GetName() == "Backpack") // OBS! All backpack inventories must share this inventory name for this to work.
                {
                    // Get a list of all items in the backpack.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();

                    foreach (ItemDrop.ItemData item in items)
                    {
                        if (backpackTypes.Contains(item.m_shared.m_name))
                        {
                            canTeleport = item.Extended().GetComponent<BackpackComponent>().backpackInventory.IsTeleportable();
                            if (!canTeleport)
                            {
                                Jotunn.Logger.LogMessage("Backpack contains a backpack that cannot be teleported!");
                                __result = false;
                                return;
                            }
                        }
                    }

                }

            }


        }


    }
}