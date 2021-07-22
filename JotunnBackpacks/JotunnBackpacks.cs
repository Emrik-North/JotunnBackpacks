using BepInEx;
using BepInEx.Configuration;
using Jotunn.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using HarmonyLib;
using ExtendedItemDataFramework;


// TODO:
// * Remove excessive Jotunn.Logger things once you've figured out how stuff works.
// * Find out how to hook into the event _when the backpack container closes_, and save the backpack inventory then rather than with a SavePlayerProfile Harmony patch.

namespace JotunnBackpacks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("randyknapp.mods.extendeditemdataframework")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class JotunnBackpacks : BaseUnityPlugin
    {
        public const string PluginGUID = "Emrik-North.JotunnBackpacks";
        public const string PluginName = "JotunnBackpacks";
        public const string PluginVersion = "0.0.1";

        // Configuration values from Aedenthorn's BackpackRedux. Setting some of these manually.
        public static ConfigEntry<string> hotKey;
        public static Vector2 backpackSize;
        public static float backpackWeightMult;

        // From Aedenthorn's Backpack Redux
        private static bool opening = false;
        private static Container backpackContainer; // Only need a single Container, I think, because only the contents (Inventory) vary between backpacks, not sizes.

        // Emrik's adventures
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only CapeIronBackpack and CapeSilverBackpack)
        public static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped
        public static List<ItemDrop.ItemData> backpacksToSave = new List<ItemDrop.ItemData>(); // All the backpack objects modified by player since last time they were saved

        // Emrik is new to C#. I took dealing with the assets out of the main file to make it tidier, and put it into its own class outside the file.
        // I'm creating an instance of the class BackpackAssets, otherwise I can't use the stuff in there.
        // I'm making this instance readonly, because I never modify it after creating it, so I assuming readonly is more efficient.
        readonly BackpackAssets assets = new BackpackAssets();

        private Harmony harmony;

        private void Awake()
        {
            assets.LoadAssets();
            assets.AddTranslations();
            assets.AddMockedItems();
            backpackTypes.Add("CapeIronBackpack");
            backpackTypes.Add("CapeSilverBackpack");

            hotKey = Config.Bind<string>("General", "HotKey", "i", "Hotkey to open backpack.");

            // Manually setting values for Aedenthorn's BackpackRedux configs.
            backpackSize = new Vector2(6, 3);
            backpackWeightMult = 0.75f; // Items stored in backpacks are 25% lighter.

            // The "NewExtendedItemData" event is run whenever a newly created item is extended by the ExtendedItemDataFramework.dll, I'm just catching it and appending my own code at the end of it
            ExtendedItemData.NewExtendedItemData += OnNewExtendedItemData;

            harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();
        }

        //  This is the code appended to the NewExtendedItemData event that we're catching, and the argument passed in automatically is the newly generated extended item data.
        public static void OnNewExtendedItemData(ExtendedItemData itemData)
        {
            // I check whether the item created is of a type contained in backpackTypes
            if (backpackTypes.Contains(itemData.m_dropPrefab.name))
            {

                // Create an instance of an Inventory type
                // OBS! The 'name' field of the Inventory object must be "Backpack", otherwise it messes up the GetTotalWeight_Patch down below.
                Inventory inventoryInstance = new Inventory("Backpack", null, (int)backpackSize.x, (int)backpackSize.y);

                // Add an empty BackpackComponent to the backpack item
                itemData.AddComponent<BackpackComponent>();

                // Assign the Inventory instance to the backpack item's BackpackComponent
                itemData.GetComponent<BackpackComponent>().SetBackpackInventory(inventoryInstance);

                // TODO: Can I just store an Inventory object to the backpack ItemData object, or do I need to Serialize it into a string, like with ZPackages?

                Jotunn.Logger.LogMessage($"New backpack created! Adding an Inventory component to it.\nType: {itemData.m_dropPrefab.name}\nGUID: {itemData.Extended().GetUniqueId()}\n\n");
            }

        }

        private static ItemDrop.ItemData GetEquippedBackpack()
        {
            // Get a list of all equipped items.
            List<ItemDrop.ItemData> equippedItems = Player.m_localPlayer.GetInventory().GetEquipedtems();

            // Go through all the equipped items, match them for any of the names in backpackTypes.
            // If a match is found, return the backpack ItemData object.
            foreach (ItemDrop.ItemData item in equippedItems)
            {
                if (backpackTypes.Contains(item.m_dropPrefab.name))
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

            // Return false GetEquippedBackpack() returns null.
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

            // Add backpack to the backpacksToSave list, so that the code knows to save the contents of this backpack when it saves the player profile
            backpacksToSave.Add(backpackEquipped);
        }

        // TODO: Figure out precisely what's going on here
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
            static void Prefix()
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

        //[HarmonyPatch(typeof(Inventory), "GetTotalWeight")]
        //[HarmonyPriority(Priority.Last)]
        static class GetTotalWeight_Patch
        {
            static void Postfix(Inventory __instance, ref float __result)
            {
                // TODO: You can put a backpack inside a backpack to accumulate the weightmultiplier


                // If calculating the weight of the player's inventory, run this:
                if (__instance == Player.m_localPlayer.GetInventory())
                {
                    // TODO: Figure out what this one does. It's from Aedenthorn. It prevents an eternally persistent loop?
                    if (new StackFrame(2).ToString().IndexOf("OverrideGetTotalWeight") > -1)
                    {
                        return;
                    }

                    // Get a list of all items on the player.
                    List<ItemDrop.ItemData> items = __instance.GetAllItems();

                    // Go through all the items, match them for any of the names in backpackTypes.
                    // For all matches found, add the backpack inventory weight to the total weight.
                    foreach (ItemDrop.ItemData item in items)
                    {
                        if (backpackTypes.Contains(item.m_dropPrefab.name))
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
                        if (backpackTypes.Contains(item.m_dropPrefab.name))
                        {
                            // The backpack inventory currently being calculated contains a backpack!
                            containsBackpack = true;
                            Jotunn.Logger.LogMessage("Backpack contains a backpack!");
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

        // TODO: Test this.
       [HarmonyPatch(typeof(Inventory), "IsTeleportable")]
        static class IsTeleportable_Patch
        {
            static bool Prefix(Inventory __instance, ref bool __result)
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
                        if (backpackTypes.Contains(item.m_dropPrefab.name))
                        {
                            canTeleport = item.Extended().GetComponent<BackpackComponent>().backpackInventory.IsTeleportable();
                            if (!canTeleport)
                            {
                                // Any Harmony Prefix that returns false causes the game to skip over the original method. (But it won't skip over Postfixes, if there are any.)
                                return false;
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
                        if (backpackTypes.Contains(item.m_dropPrefab.name))
                        {
                            canTeleport = item.Extended().GetComponent<BackpackComponent>().backpackInventory.IsTeleportable();
                            if (!canTeleport)
                            {
                                Jotunn.Logger.LogMessage("Backpack contains a backpack that cannot be teleported!");
                                return false;
                            }
                        }
                    }

                    // If no backpacks have been found in the backpack inventory, just check whether its current inventory is teleportable.
                    return __instance.IsTeleportable();
                }

                // If no backpacks have been found in the Inventory currently being calculated, just do a normal IsTeleportable() check.
                return __instance.IsTeleportable();
            }
        }
    }
}