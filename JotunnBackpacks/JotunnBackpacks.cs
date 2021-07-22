using BepInEx;
using BepInEx.Configuration;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ExtendedItemDataFramework;


// TODO:
// * Remove excessive Jotunn.Logger things once you've figured out how stuff works.

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

        private Harmony harmony;

        // Emrik's adventures
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only CapeIronBackpack and CapeSilverBackpack)
        public static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped

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

            // TODO: DEBUGGING: Testing whether I can Serialize() and Deserialize() the backpack's inventory while in-game. Update: Seems to work!
            if (!AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown("y"))
            {
                ItemDrop.ItemData ninjaDebuggerVariable = GetEquippedBackpack();
                string dataStuff = ninjaDebuggerVariable.Extended().GetComponent<BackpackComponent>().Serialize();

                Jotunn.Logger.LogMessage("MANUALLY CALLING Deserialize() ON THE BACKPACK NOW. IS THERE ERROR?");
                ninjaDebuggerVariable.Extended().GetComponent<BackpackComponent>().Deserialize(dataStuff);
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