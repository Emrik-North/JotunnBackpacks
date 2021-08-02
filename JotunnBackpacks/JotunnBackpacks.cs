/* JotunnBackpacks.cs
 * 
 * CREDIT:
 * Evie/CinnaBunn for their 'eviesbackpacks' assets inside JotunnModExample: https://github.com/Valheim-Modding/JotunnModExample/tree/master/JotunnModExample/AssetsEmbedded
 * Aedenthorn for their BackpackRedux mod, which I derived and learned a lot from: https://github.com/aedenthorn/ValheimMods/blob/master/BackpackRedux/
 * Randy Knapp for their Extended Item Framework, without which this project would have been much harder: https://github.com/RandyKnapp/ValheimMods/tree/main/ExtendedItemDataFramework
 * sbtoonz/Zarboz for guidance and help with various things like setting ZNetView().m_persistent=true: https://github.com/VMP-Valheim/Back_packs
 * The Jotunn Team for creating Jotunn: The Valheim Library, which is the framework this mod uses: https://valheim-modding.github.io/Jotunn/index.html
 * 
 * Most of this project is the result of the hard work of these awesome people!
 * 
 * *
 * 
 * I usually comment my code heavily. My comment philosophy is to imagine trying to help previous less informed versions of myself learn exactly what's going on in the code.
 * And when I *still* don't understand what's going on in the code even though it works, I'll try to let readers know that so they don't accidentally learn bad practices from me.
 * 
 * GitHub: https://github.com/Emrik-North/JotunnBackpacks
 */

using BepInEx;
using BepInEx.Configuration;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ExtendedItemDataFramework;
using Log = Jotunn.Logger;

/* TODOS
 * • Hotkey to drop backpack to be able to run faster out of danger, like in Outward!
 * • Also make backpacks never despawn when dropped.
 * 
 */

namespace JotunnBackpacks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(eidfGUID)]
    [BepInDependency(eaqsGUID, BepInDependency.DependencyFlags.SoftDependency)] // This soft dependency is just here to check if it's installed on the client, see the GuiBar_Awake_Patch.

    // This attribute is set such that both server and clients need to have this mod, and the same version of this mod, otherwise the client cannot connect.
    // This is to prevent cases such as when a client logs on, creates a backpack, puts it in a container, logs off, and then another client without the mod opens the container, and the backpack gets destroyed.
    // Read more about Jotunn's NetworkCompatibilityAttribute here: https://valheim-modding.github.io/Jotunn/tutorials/networkcompatibility.html
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class JotunnBackpacks : BaseUnityPlugin
    {
        public const string PluginGUID = "JotunnBackpacks";
        public const string PluginName = "JotunnBackpacks";
        public const string PluginVersion = "0.3.0";
        public const string eidfGUID = "randyknapp.mods.extendeditemdataframework";
        public const string eaqsGUID = "randyknapp.mods.equipmentandquickslots";

        // Config entries
        public static ConfigEntry<KeyCode> hotKey;
        public static ConfigEntry<Vector2> backpackSize;
        public static ConfigEntry<float> weightMultiplier;
        public static ConfigEntry<int> carryBonusRugged;
        public static ConfigEntry<int> carryBonusArctic;
        public static ConfigEntry<float> speedModRugged;
        public static ConfigEntry<float> speedModArctic;

        // Initialise variables
        public static Container backpackContainer; // Only need a single Container because only the contents (Inventory) vary between backpacks, not sizes.
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only $item_cape_ironbackpack and $item_cape_silverbackpack from CinnaBunn)
        private static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped
        public static string backpackInventoryName = "$ui_backpack_inventoryname";
        public static bool opening = false;

        // Awake() is run when the game loads up.
        private void Awake()
        {
            CreateConfigValues();
            BackpackAssets.LoadAssets();
            BackpackAssets.AddStatusEffects();
            BackpackAssets.AddMockedItems();
            backpackTypes.Add("$item_cape_ironbackpack");
            backpackTypes.Add("$item_cape_silverbackpack");

            // The "NewExtendedItemData" event is run whenever a newly created item is extended by the ExtendedItemDataFramework.dll, I'm just catching it and appending my own code at the end of it
            ExtendedItemData.NewExtendedItemData += OnNewExtendedItemData;

            Harmony harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();

        }

        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;

            // These configs can be edited by local users.
            hotKey = Config.Bind(
                        "Local config", "HotKey", KeyCode.I,
                        new ConfigDescription("Hotkey to open backpack."));

            // These configs are enforced by the server, but can be edited locally if in single-player.
            backpackSize = Config.Bind(
                        "Server-enforceable config", "Backpack Size", new Vector2(6, 3),
                        new ConfigDescription("Backpack size (width, height).\nMax width is 8 unless you want to break things.",
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 6 }));

            weightMultiplier = Config.Bind(
                        "Server-enforceable config", "Weight Multiplier", 0.5f,
                        new ConfigDescription("The weight of items stored in the backpack gets multiplied by this value.",
                        new AcceptableValueRange<float>(0f, 1f), // range between 0f and 1f will make it display as a percentage slider
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 5 }));

            carryBonusRugged = Config.Bind(
                        "Server-enforceable config", "Rugged Backpack: Carry Bonus", 50,
                        new ConfigDescription("Increases your carry capacity by this much while wearing the backpack.",
                        new AcceptableValueRange<int>(0, 300),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 4 }));

            speedModRugged = Config.Bind(
                        "Server-enforceable config", "Rugged Backpack: Speed Modifier", -0.15f,
                        new ConfigDescription("Wearing the backpack slows you down by this much.",
                        new AcceptableValueRange<float>(-1f, -0f),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 3 }));

            carryBonusArctic = Config.Bind(
                        "Server-enforceable config", "Arctic Backpack: Carry Bonus", 0,
                        new ConfigDescription("Increases your carry capacity by this much while wearing the backpack.",
                        new AcceptableValueRange<int>(0, 300),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 2 }));

            speedModArctic = Config.Bind(
                        "Server-enforceable config", "Arctic Backpack: Speed Modifier", -0.15f,
                        new ConfigDescription("Wearing the backpack slows you down by this much.",
                        new AcceptableValueRange<float>(-1f, -0f),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 1 }));

        }

        //  This is the code appended to the NewExtendedItemData event that we're catching, and the argument passed in automatically is the newly generated extended item data.
        private static void OnNewExtendedItemData(ExtendedItemData itemData)
        {
            // I check whether the item created is of a type contained in backpackTypes
            if (backpackTypes.Contains(itemData.m_shared.m_name))
            {
                // Create an instance of an Inventory class
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
                (int)backpackSize.Value.x,
                (int)backpackSize.Value.y
                );

        }

        public static ItemDrop.ItemData GetEquippedBackpack()
        {
            // Get a list of all equipped items.
            List<ItemDrop.ItemData> equippedItems = Player.m_localPlayer?.GetInventory()?.GetEquipedtems();

            if (equippedItems is null) return null;

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
        public static bool CanOpenBackpack()
        {
            backpackEquipped = GetEquippedBackpack();

            // Return true if GetEquippedBackpack() does not return null.
            if (backpackEquipped != null)
            {
                return true;
            }

            // Return false if GetEquippedBackpack() returns null.
            Log.LogMessage("No backpack equipped. Can't open any.");
            return false;

        }

        public static void OpenBackpack()
        {
            var player = Player.m_localPlayer;

            backpackContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (backpackContainer == null)
                backpackContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();

            backpackContainer.m_inventory = backpackEquipped.Extended().GetComponent<BackpackComponent>().GetInventory();
            InventoryGui.instance.Show(backpackContainer);

        }

        public static void EjectBackpack(ItemDrop.ItemData item, Player player, Inventory backpackInventory)
        {
            var playerInventory = player.GetInventory();

            // Move the backpack to the player's Inventory if there's room.
            if (playerInventory.HaveEmptySlot())
            {
                playerInventory.MoveItemToThis(backpackInventory, item);
            }

            // Otherwise drop the backpack.
            else
            {
                Log.LogMessage("Clever... But you're still not gonna cause backpackception!");

                // Remove the backpack item from the Inventory instance and then drop the backpack item in front of the player.
                backpackInventory.RemoveItem(item);
                ItemDrop.DropItem(item, 1, player.transform.position + player.transform.forward + player.transform.up, player.transform.rotation);

                // OBS! ItemDrop.DropItem() causes OnNewExtendedItemData() and creates a new backpack with a new GUID,
                // but the new backpack is cloned from the first and is assigned the old one's GUID as well as its inventory reference,
                // so all items are preserved.
            }

        }

    }
}