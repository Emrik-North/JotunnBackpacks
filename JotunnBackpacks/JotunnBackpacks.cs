/* JotunnBackpacks.cs
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
using Log = Jotunn.Logger;
using BepInEx.Logging;
using JotunnBackpacks.Data;


/* TODOS
 * • Make backpacks never despawn when quickdropped.
 * • Check if Epic Loot is installed, and disable Weightless as a possible enchant for backpacks.
 */

namespace JotunnBackpacks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(eaqsGUID, BepInDependency.DependencyFlags.SoftDependency)] // This soft dependency is just here to check if it's installed on the client, see the GuiBar_Awake_Patch.

    // This attribute is set such that both server and clients need to have this mod, and the same version of this mod, otherwise the client cannot connect.
    // This is to prevent cases such as when a client logs on, creates a backpack, puts it in a container, logs off, and then another client without the mod opens the container, and the backpack gets destroyed.
    // Read more about Jotunn's NetworkCompatibilityAttribute here: https://valheim-modding.github.io/Jotunn/tutorials/networkcompatibility.html
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class JotunnBackpacks : BaseUnityPlugin
    {
        public const string PluginGUID = "JotunnBackpacks";
        public const string PluginName = "JotunnBackpacks";
        public const string PluginVersion = "2.1.3";
        public const string eaqsGUID = "randyknapp.mods.equipmentandquickslots";

        // Config entries
        public static ConfigEntry<KeyCode> hotKey_open;
        public static ConfigEntry<KeyCode> hotKey_drop;
        public static ConfigEntry<bool> outwardMode;
        public static ConfigEntry<Vector2> ruggedBackpackSize;
        public static ConfigEntry<Vector2> arcticBackpackSize;
        public static ConfigEntry<float> weightMultiplier;
        public static ConfigEntry<int> carryBonusRugged;
        public static ConfigEntry<int> carryBonusArctic;
        public static ConfigEntry<float> speedModRugged;
        public static ConfigEntry<float> speedModArctic;
        public static ConfigEntry<bool> freezingRugged;
        public static ConfigEntry<bool> freezingArctic;

        // Initialise variables
        public static Container backpackContainer; // Only need a single Container because only the contents (Inventory) vary between backpacks, not sizes.
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only $item_cape_ironbackpack and $item_cape_silverbackpack from CinnaBunn)
        private static ItemDrop.ItemData backpackEquipped; // Backpack object currently equipped
        public static string backpackInventoryName = "$ui_backpack_inventoryname";
        public static bool opening = false;

        private static ManualLogSource _logger;

        // Awake() is run when the game loads up.
        private void Awake()
        {
            _logger = base.Logger;
            CreateConfigValues();
            BackpackAssets.LoadAssets();
            BackpackAssets.AddStatusEffects();
            BackpackAssets.AddMockedItems();
            backpackTypes.Add("$item_cape_ironbackpack");
            backpackTypes.Add("$item_cape_silverbackpack");
            
            Harmony harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();

        }

        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;

            // These configs can be edited by local users.
            hotKey_open = Config.Bind(
                        "Local config", "Open Backpack", KeyCode.I,
                        new ConfigDescription("Hotkey to open backpack.",
                        null,
                        new ConfigurationManagerAttributes { Order = 3 }));

            hotKey_drop = Config.Bind(
                        "Local config", "Quickdrop Backpack", KeyCode.Y,
                        new ConfigDescription("Hotkey to quickly drop backpack while on the run.",
                        null,
                        new ConfigurationManagerAttributes { Order = 2 }));

            outwardMode = Config.Bind(
                        "Local config", "Outward Mode", false,
                        new ConfigDescription("You can use a hotkey to quickly drop your equipped backpack in order to run faster away from danger.",
                        null,
                        new ConfigurationManagerAttributes { Order = 1 }));

            // These configs are enforced by the server, but can be edited locally if in single-player.
            ruggedBackpackSize = Config.Bind(
                        "Server-enforceable config", "Rugged Backpack Size", new Vector2(6, 3),
                        new ConfigDescription("Backpack size (width, height).\nMax width is 8 unless you want to break things.",
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 9 }));
            arcticBackpackSize = Config.Bind(
                        "Server-enforceable config", "Arctic Backpack Size", new Vector2(6, 3),
                        new ConfigDescription("Backpack size (width, height).\nMax width is 8 unless you want to break things.",
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 8 }));

            weightMultiplier = Config.Bind(
                        "Server-enforceable config", "Weight Multiplier", 0.5f,
                        new ConfigDescription("The weight of items stored in the backpack gets multiplied by this value.",
                        new AcceptableValueRange<float>(0f, 1f), // range between 0f and 1f will make it display as a percentage slider
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 7 }));

            carryBonusRugged = Config.Bind(
                        "Server-enforceable config", "Rugged Backpack: Carry Bonus", 0,
                        new ConfigDescription("Increases your carry capacity by this much while wearing the backpack.",
                        new AcceptableValueRange<int>(0, 300),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 6 }));

            speedModRugged = Config.Bind(
                        "Server-enforceable config", "Rugged Backpack: Speed Modifier", -0.15f,
                        new ConfigDescription("Wearing the backpack slows you down by this much.",
                        new AcceptableValueRange<float>(-1f, -0f),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 5 }));

            carryBonusArctic = Config.Bind(
                        "Server-enforceable config", "Arctic Backpack: Carry Bonus", 0,
                        new ConfigDescription("Increases your carry capacity by this much while wearing the backpack.",
                        new AcceptableValueRange<int>(0, 300),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 4 }));

            speedModArctic = Config.Bind(
                        "Server-enforceable config", "Arctic Backpack: Speed Modifier", -0.15f,
                        new ConfigDescription("Wearing the backpack slows you down by this much.",
                        new AcceptableValueRange<float>(-1f, -0f),
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 3 }));

            freezingRugged = Config.Bind(
                        "Server-enforceable config", "Rugged: Prevent freezing/cold?", true,
                        new ConfigDescription("Wearing the backpack protects you against freezing/cold, just like capes.",
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 2 }));

            freezingArctic = Config.Bind(
                        "Server-enforceable config", "Arctic: Prevent freezing/cold?", true,
                        new ConfigDescription("Wearing the backpack protects you against freezing/cold, just like capes.",
                        null,
                        new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 1 }));

        }

 
        public static Inventory NewInventoryInstance(string name)
        {
            if (name.Equals("$item_cape_ironbackpack"))
            {
                return new Inventory(
                backpackInventoryName,
                null,
                (int)ruggedBackpackSize.Value.x,
                (int)ruggedBackpackSize.Value.y
                );
            }

            if (name.Equals("$item_cape_silverbackpack"))
            {
                return new Inventory(
                backpackInventoryName,
                null,
                (int)arcticBackpackSize.Value.x,
                (int)arcticBackpackSize.Value.y
                );
            }

            Log.LogError("Calling NewInventoryInstance with a name that doesn't match any existing backpack type!");
            return null;
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
                    return item.Data().GetOrCreate<BackpackComponent>().Item;
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

            if (!AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown(hotKey_open.Value) && CanOpenBackpack())
            {
                opening = true;
                OpenBackpack();
            }

            if (outwardMode.Value && !AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown(hotKey_drop.Value) && CanOpenBackpack())
            {
                QuickDropBackpack();
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
            if (Player.m_localPlayer == null)
                return;

            backpackContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (backpackContainer == null)
                backpackContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();

            var backpackItem = backpackEquipped.Data().GetOrCreate<BackpackComponent>();

            backpackContainer.m_inventory = backpackItem.GetInventory();

            if (backpackContainer.m_inventory == null)  
            {
                backpackContainer.m_inventory = JotunnBackpacks.NewInventoryInstance(backpackItem.Item.m_shared.m_name);
            }
            
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

            //Save Backpack Inventory
            var backpackComponent = item.Data().GetOrCreate<BackpackComponent>();
            backpackComponent.Save(backpackInventory);

        }

        private static void QuickDropBackpack()
        {
            Log.LogMessage("Quickdropping backpack.");

            var player = Player.m_localPlayer;
            var backpack = GetEquippedBackpack();

            // Unequip and remove backpack from player's back
            // We need to unequip the item BEFORE we drop it, otherwise when we pick it up again the game thinks
            // we had it equipped all along and fails to update player model, resulting in invisible backpack.
            player.RemoveEquipAction(backpack);
            player.UnequipItem(backpack, true);
            player.m_inventory.RemoveItem(backpack);

            // This drops a copy of the backpack itemDrop.itemData
            var itemDrop = ItemDrop.DropItem(backpack, 1, player.transform.position + player.transform.forward + player.transform.up, player.transform.rotation);


            // The following is just notes on various potential ways of making the backpack non-despawnable after quickdropping it
            //var component = itemDrop.gameObject.AddComponent<EffectArea>();
            //component.m_type = EffectArea.Type.PlayerBase;

            //Log.LogMessage($"backpack base?: {component.GetType()}");

            // itemDrop.m_nview.m_type = ZDO.ObjectType.Solid;

            //TODO: Make questitem?! UPDATE: NOT WORK
            //itemDrop.m_itemData.m_shared.m_questItem = true;
            //Log.LogMessage($"is backpack quest item: {itemDrop.m_itemData.m_shared.m_questItem}"); // questitem does not work. Backpack still despawns.
            //CreateBackpackStone(player, backpack);

            // TODO: Check how workbenches protect against despawn

        }

        /*
        private static void CreateBackpackStone(Player player, ItemDrop.ItemData backpack)
        {
            var backpackGo = backpack.m_dropPrefab.gameObject;

            var fijfij = Player.m_localPlayer.m_inventory;

            //backpackGo.layer = 10; // Does this convert the go to a piece?! I probably have to create an instance first.
            //backpackGo.GetComponent<ZNetView>().m_type = ZDO.ObjectType.Solid;

            //var rb = backpackGo.GetComponent<Rigidbody>();
            //rb.mass = 1;
            //rb.useGravity = true;
            //rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            //rb.angularDrag = 0.05f;

            //var ts = backpackGo.AddComponent<TombStone>();
            //ts.m_text = "Backpack";

            //var fl = backpackGo.AddComponent<Floating>();
            //fl = Player.m_localPlayer.m_tombstone.GetComponent<Floating>();

            var stoneGo = Instantiate(backpackGo, player.GetCenterPoint(), player.transform.rotation);

            
            var itemData = stoneGo.GetComponent<ItemDrop.ItemData>();

            //PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
            //TombStone backpackTombStone = stoneGo.GetComponent<TombStone>();

            

            //Log.LogMessage($"backpackTombStone: {backpackTombStone == null}"); // true

            //backpackTombStone.Setup(playerProfile.GetName(), playerProfile.GetPlayerID()); // NRE
        }
        */


    }


}