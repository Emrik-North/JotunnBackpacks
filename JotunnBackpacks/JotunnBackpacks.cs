// JotunnBackpacks
// A Valheim mod using Jötunn
// Used to demonstrate the libraries capabilities
// 
// File:    JotunnBackpacks.cs
// Project: JotunnBackpacks

using BepInEx;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace JotunnBackpacks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class JotunnBackpacks : BaseUnityPlugin
    {
        public const string PluginGUID = "Emrik-North.JotunnBackpacks";
        public const string PluginName = "JotunnBackpacks";
        public const string PluginVersion = "0.0.1";

        // Asset and prefab loading
        private AssetBundle EmbeddedResourceBundle;
        private GameObject BackpackIronPrefab;
        private GameObject BackpackSilverPrefab;

        // Configuration values
        private ConfigEntry<string> StringConfig;
        private ConfigEntry<float> FloatConfig;
        private ConfigEntry<int> IntegerConfig;
        private ConfigEntry<bool> BoolConfig;

        private void Awake()
        {
            // Load, create and init your custom mod stuff
            CreateConfigValues();
            LoadAssets();
            AddTranslations();
            AddMockedItems();
        }

        // Create some sample configuration values
        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;

            // Add client config which can be edited in every local instance independently
            // Emrik: Add backpack open hotkey config to this
            StringConfig = Config.Bind("Client config", "LocalString", "Some string", "Client side string");
            FloatConfig = Config.Bind("Client config", "LocalFloat", 0.5f, new ConfigDescription("Client side float with a value range", new AcceptableValueRange<float>(0f, 1f)));
            IntegerConfig = Config.Bind("Client config", "LocalInteger", 2, new ConfigDescription("Client side integer without a range"));
            BoolConfig = Config.Bind("Client config", "LocalBool", false, new ConfigDescription("Client side bool / checkbox"));

            // Add server config which gets pushed to all clients connecting and can only be edited by admins
            // In local/single player games the player is always considered the admin
            Config.Bind("Server config", "StringValue1", "StringValue", new ConfigDescription("Server side string", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind("Server config", "FloatValue1", 750f, new ConfigDescription("Server side float", new AcceptableValueRange<float>(0f, 1000f), new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind("Server config", "IntegerValue1", 200, new ConfigDescription("Server side integer", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
            Config.Bind("Server config", "BoolValue1", false, new ConfigDescription("Server side bool", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Add a client side custom input key for the EvilSword
            // EvilSwordSpecialConfig = Config.Bind("Client config", "EvilSword Special Attack", KeyCode.B, new ConfigDescription("Key to unleash evil with the Evil Sword"));

            // You can subscribe to a global event when config got synced initially and on changes
            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                if (attr.InitialSynchronization)
                {
                    Jotunn.Logger.LogMessage("Initial Config sync event received");
                }
                else
                {
                    Jotunn.Logger.LogMessage("Config sync event received");
                }
            };
        }

        // Examples for reading and writing configuration values
        private void ReadAndWriteConfigValues()
        {
            // Reading configuration entry
            string readValue = StringConfig.Value;
            // or
            float readBoxedValue = (float)Config["Client config", "LocalFloat"].BoxedValue;

            // Writing configuration entry
            IntegerConfig.Value = 150;
            // or
            Config["Client config", "LocalBool"].BoxedValue = true;
        }

        // Various forms of asset loading
        private void LoadAssets()
        {
            // Load asset bundle from embedded resources
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(JotunnBackpacks).Assembly.GetManifestResourceNames())}");
            EmbeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("eviesbackpacks", typeof(JotunnBackpacks).Assembly);
            BackpackIronPrefab = EmbeddedResourceBundle.LoadAsset<GameObject>("Assets/Evie/CapeIronBackpack.prefab");
            BackpackSilverPrefab = EmbeddedResourceBundle.LoadAsset<GameObject>("Assets/Evie/CapeSilverBackpack.prefab");
        }

        private void AddTranslations()
        {
            LocalizationManager.Instance.AddLocalization(new LocalizationConfig("English")
            {
                Translations = {
                    {"item_cape_silverbackpack", "Fine Backpack" },
                    {"item_cape_silverbackpack_description", "A fine backpack with silver backsupport." },
                    {"se_silverbackpackstrength_start", "Your carry capacity has been increased." },
                    {"item_ironbackpackstrength", "Backpack equipped" },
                    {"item_cape_ironbackpack", "Rugged Backpack" },
                    {"item_cape_ironbackpack_description", "A rugged backpack with iron backsupport." },
                    {"se_ironbackpackstrength_start", "Your carry capacity has been increased." },
                    { "item_silverbackpackstrength", "Backpack equipped" }


                }
            });
        }

        // Implementation of assets using mocks, adding recipe's manually without the config abstraction
        private void AddMockedItems()
        {
            // Iron Backpack
            if (!BackpackIronPrefab) Jotunn.Logger.LogWarning($"Failed to load asset from bundle: {EmbeddedResourceBundle}");
            else
            {
                // Create and add a custom item
                CustomItem CI = new CustomItem(BackpackIronPrefab, true);
                ItemManager.Instance.AddItem(CI);

                //Create and add a custom recipe
                Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = "Recipe_CapeIronBackpack";
                recipe.m_item = BackpackIronPrefab.GetComponent<ItemDrop>();
                recipe.m_craftingStation = Mock<CraftingStation>.Create("piece_workbench");
                var ingredients = new List<Piece.Requirement>
                {
                    MockRequirement.Create("LeatherScraps", 8),
                    MockRequirement.Create("DeerHide", 2),
                    MockRequirement.Create("Iron", 4),
                };
                recipe.m_resources = ingredients.ToArray();
                CustomRecipe CR = new CustomRecipe(recipe, true, true);
                ItemManager.Instance.AddRecipe(CR);

                //Enable BoneReorder
                BoneReorder.ApplyOnEquipmentChanged();
            }

            // Silver Backpack
            if (!BackpackSilverPrefab) Jotunn.Logger.LogWarning($"Failed to load asset from bundle: {EmbeddedResourceBundle}");
            else
            {
                // Create and add a custom item
                CustomItem CI = new CustomItem(BackpackSilverPrefab, true);
                ItemManager.Instance.AddItem(CI);

                //Create and add a custom recipe
                Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = "Recipe_CapeSilverBackpack";
                recipe.m_item = BackpackSilverPrefab.GetComponent<ItemDrop>();
                recipe.m_craftingStation = Mock<CraftingStation>.Create("piece_workbench");
                var ingredients = new List<Piece.Requirement>
                {
                    MockRequirement.Create("LeatherScraps", 8),
                    MockRequirement.Create("DeerHide", 2),
                    MockRequirement.Create("Silver", 4),
                };
                recipe.m_resources = ingredients.ToArray();
                CustomRecipe CR = new CustomRecipe(recipe, true, true);
                ItemManager.Instance.AddRecipe(CR);

                //Enable BoneReorder
                BoneReorder.ApplyOnEquipmentChanged();
            }
            EmbeddedResourceBundle.Unload(false);
        }
    }
}