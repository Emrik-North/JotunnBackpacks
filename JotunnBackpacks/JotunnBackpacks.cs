// JotunnBackpacks
// A Valheim mod using Jötunn
// Used to demonstrate the libraries capabilities
// 
// File:    JotunnBackpacks.cs
// Project: JotunnBackpacks

// TODO: 
// Check backpack for unteleportable items before going through portal.
// Use ExtendedItemDataFramework to attach a separate inventory to each backpack.

// From Jotunn
using BepInEx;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;

// Emrik's import
using ExtendedItemDataFramework; // Maybe we can make it work without this dependency

// From Aedenthorn's Backpack Redux
using HarmonyLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;


// TODO:
// * Remove excessive Jotunn.Logger things once you've figured out that stuff works.
// * Consider storing backpack inventories in m_craftername rather than as files in modfolder. Would that be more efficient?

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
        // private static Inventory backpackInventory; // = new Inventory("Backpack", null, (int)backpackSize.x, (int)backpackSize.y);

        private Harmony harmony;

        // Emrik's adventures
        public static List<string> backpackList = new List<string>(); // This is to store all unique IDs for each backpack
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only CapeIronBackpack and CapeSilverBackpack)
        public static Dictionary<string, Inventory> backpackDict = new Dictionary<string, Inventory>(); // This needs to be loaded up at start and saved on quit.
        public static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped

        // The advantage to using List instead of Array is that I don't need to assign a size to the List while declaring it.
        // On the other hand, Arrays are faster _because_ all the memory locations used are reserved at the beginning.

        // Emrik is new to C#. I took dealing with the assets out of the main file to make it tidier, and put it into its own class outside the file.
        // I'm creating an instance of the class BackpackAssets, otherwise I can't use the stuff in there.
        // I'm making this instance readonly, because I never modify it after creating it, so I assuming readonly is more efficient.
        readonly BackpackAssets assets = new BackpackAssets();

        private void Awake()
        {
            assets.LoadAssets();
            assets.AddTranslations();
            assets.AddMockedItems();

            hotKey = Config.Bind<string>("General", "HotKey", "i", "Hotkey to open backpack.");

            // Manually setting values for Aedenthorn's BackpackRedux configs.
            backpackSize = new Vector2(6, 3);
            backpackWeightMult = 0.8f; // Items stored in backpacks are 20% lighter.

            // TODO: Maybe this doesn't need to be inside Awake(), test moving it
            backpackTypes.Add("CapeIronBackpack");
            backpackTypes.Add("CapeSilverBackpack");

            // The "NewExtendedItemData" event is run whenever a newly created item is "extended" by the ExtendedItemDataFramework.dll, I'm just catching it and appending my own code at the end of it
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
                Inventory inventoryInstance = new Inventory("Backpack", null, (int)backpackSize.x, (int)backpackSize.y);

                // Add an empty BackpackComponent to the backpack item
                itemData.AddComponent<BackpackComponent>();

                // Assign the Inventory instance to the backpack item's BackpackComponent
                itemData.GetComponent<BackpackComponent>().SetBackpackInventory(inventoryInstance);


                // Assign that Inventory instance to the BackpackComponent of the current item object
                // itemData.ReplaceComponent<BackpackComponent>().BackpackInventory = inventoryInstance;
                //itemData.ReplaceComponent<BackpackComponent>().SetBackpackInventory(inventoryInstance);

                // TODO: Can I just store an Inventory object to the backpack ItemData object, or do I need to Serialize it into a string, like with ZPackages?

                Jotunn.Logger.LogMessage($"New backpack created! Adding an Inventory component to it.\nType: {itemData.m_dropPrefab.name}\nGUID: {itemData.Extended().GetUniqueId()}\n\n");
            }

        }

        private static ItemDrop.ItemData GetEquippedBackpack()
        {
            // Get a list of all equipped items.
            List<ItemDrop.ItemData> equippedItems = Player.m_localPlayer.GetInventory().GetEquipedtems();
            ItemDrop.ItemData backpack;

            // Go through all the equipped items, match them for any of the names in backpackTypes.
            // If a match is found, return the backpack ItemData object.
            for (int i = 0; i < equippedItems.Count; i++)
            {
                backpack = equippedItems[i];
                if (backpackTypes.Contains(backpack.m_dropPrefab.name))
                {
                    return backpack;
                }
            }

            // Return null if no backpacks are found.
            return null;
        }

        // Emrik: This method is from Aedenthorn's BackpackRedux, and I'm not entirely sure what it does yet.
        // Emrik: I can't see this method being called anywhere, but I tested and it's essential for mod functionality. Where is it being called?
        private void Update()
        {
            if (!Player.m_localPlayer || !ZNetScene.instance)
                return;

            if (!AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown(hotKey.Value) && CanOpenBackpack())
            {
                opening = true;
                OpenBackpack();
            }

            // TODO: Emrik testing things
            if (!AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown("y"))
            {
                ItemDrop.ItemData bogoBAM = GetEquippedBackpack();
                string dataStuff = bogoBAM.Extended().GetComponent<BackpackComponent>().Serialize();

                Jotunn.Logger.LogMessage("MANUALLY CALLING Deserialize() ON THE BACKPACK. IS THERE ERROR?");
                bogoBAM.Extended().GetComponent<BackpackComponent>().Deserialize(dataStuff);



            }
        }

        private static bool CanOpenBackpack() // Every time this function is executed, it sets backpackEquipped to either null, or the name of the then-equipped backpack. 
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
            // TODO: When you try to open a backpack, it should query for if this backpack has a unique ID (or backpackfile) associated with it. And if that ID is null, it should create a unique ID for it.

            backpackContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (backpackContainer == null)
                backpackContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();

            // CanOpenBackpack() is always executed before this code, so backpackEquipped has a value, otherwise OpenBackback wouldn't get executed in the first place
            // backpackContainer.m_name = backpackEquipped; // We don't need to name this, probably

            // Checks to see if backpackEquipped already has an entry in the backpackDict, and loads it into backpackInventory if so, otherwise it creates a new Inventory instance for this backpack and stores that to backpackInventory
            // LoadBackpackInventory();

            // TODO: Remove this testing stuff
            Jotunn.Logger.LogMessage($"Is the equippedBackpack itemdata extended?: {backpackEquipped.IsExtended()}");
            Jotunn.Logger.LogMessage($"List of components in equippedBackpack: {backpackEquipped.Extended().Components.ToString()}");


            AccessTools.FieldRefAccess<Container, Inventory>(backpackContainer, "m_inventory") = backpackEquipped.Extended().GetComponent<BackpackComponent>().backpackInventory; // GetBackpackInventory(backpackEquipped);
            InventoryGui.instance.Show(backpackContainer);
        }

        [HarmonyPatch(typeof(FejdStartup), "LoadMainScene")]
        static class LoadMainScene_Patch
        {
            // TODO: Save backpackfiles with their unique IDs. Remove player name from backpackfilenames, because we want other players to be able to pick up the backpack and see its contents too.
            // TODO: I think this section can be removed, and we move the loading of the backpack files to happen _when we equip the backpack_.

            // if (Enumerate(Player.m_localPlayer.GetInventory().GetEquipedtems().m_dropPrefab?.name, "*backpack*").ToString() != null)
            //     {
            //        load the backpack
            //     }


            static void Prefix(FejdStartup __instance, List<PlayerProfile> ___m_profiles)
            {
                // var profile = ___m_profiles.Find(p => p.GetFilename() == (string)typeof(Game).GetField("m_profileFilename", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
                // backpackFileName = "backpack_" + Path.GetFileNameWithoutExtension(profile.GetFilename()) + "_" + backpackName;

                // Get all the backpack infos stored in the JotunnBackpacks folder, if there are any there.
                // TODO: NOTE that this will even load the backpack infos that you generated in other worlds/servers, but you won't be able to access them because you can't access their unique IDs.
                // It's inefficient, though, since you'll not be using those infos in the world you're playing in. Will this be a problem?
                // Jotunn.Logger.LogMessage("WE ARE NOW IN LoadMainScene");
                // LoadBackpackDictFromFile();

                // Q TODO: Do we load them at Awake() or during LoadMainScene?
            }
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

        // TODO: I haven't touched this yet at all. I should do that.
        // TODO: [HarmonyPatch(typeof(Inventory), "GetTotalWeight")]


        // This particular patch I got from sbtoonz on GitHub: https://github.com/VMP-Valheim/Back_packs/blob/master/JotunnModStub/BackPacks.cs
        // TODO: [HarmonyPatch(typeof(Inventory), "IsTeleportable")]


    }
}