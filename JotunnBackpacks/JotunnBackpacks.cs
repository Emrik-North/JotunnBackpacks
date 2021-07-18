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
// Remove excessive Jotunn.Logger things once you've figured out that stuff works.

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

        // Asset and prefab loading
        private AssetBundle EmbeddedResourceBundle;
        private GameObject BackpackIronPrefab;
        private GameObject BackpackSilverPrefab;

        // Configuration values from Aedenthorn's BackpackRedux. Setting some of these manually.
        public static ConfigEntry<string> hotKey;
        public static Vector2 backpackSize;
        public static float backpackWeightMult;

        // From Aedenthorn's Backpack Redux
        private static string assetPath;
        private static bool opening = false;
        private static string backpackFileName;
        private static Container backpackContainer;
        private static Inventory backpackInventory;

        private static JotunnBackpacks context; // What does context = this; mean?
        private Harmony harmony;

        // Emrik's adventures
        public static List<string> backpackList = new List<string>(); // This is to store all unique IDs for each backpack
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only CapeIronBackpack and CapeSilverBackpack)
        public static string backpackEquipped; // Backpack currently equipped

        // The advantage to using List instead of Array is that I don't need to assign a size to the List while declaring it.
        // On the other hand, Arrays are faster _because_ all the memory locations used are reserved at the beginning.

        private void Awake()
        {
            LoadAssets();
            AddTranslations();
            AddMockedItems();

            hotKey = Config.Bind<string>("General", "HotKey", "i", "Hotkey to open backpack.");

            // Manually setting values for Aedenthorn's BackpackRedux configs.
            backpackSize = new Vector2(6, 3);
            backpackWeightMult = 0.8f; // Items stored in backpacks are 20% lighter.

            backpackTypes.Add("CapeIronBackpack");
            backpackTypes.Add("CapeSilverBackpack");


            // backpackList.Add(backpackType1);
            // backpackList.Add(backpackType2);

            // This is where all the backpack files will be stored
            assetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), typeof(JotunnBackpacks).Namespace);
            if (!Directory.Exists(assetPath))
            {
                Jotunn.Logger.LogMessage("Creating mod folder");
                Directory.CreateDirectory(assetPath);
            }

            // Creates an enumerated list of all files in the assetPath starting with "backpack_"
            var backpackFiles = Directory.EnumerateFiles(assetPath, "backpack_*");
            foreach (string fileName in backpackFiles) // I'm assuming the foreach loop does nothing if the array has zero elements, so it doesn't add null to my bakpackList...
            {
                // Adds all the file names of backpacks in the directory to the backpackList
                backpackList.Add(fileName);
                Jotunn.Logger.LogMessage($"Found backpack: {fileName}\n");
            }

            harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();
        }

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

        // Implementation of assets using mocks, adding recipes manually without the config abstraction
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

        // Integrating Aedenthorn's Backpack Redux: START
        private void Update()
        {
            if (!Player.m_localPlayer || !ZNetScene.instance)
                return;

            if (!AedenthornUtils.IgnoreKeyPresses(true) && AedenthornUtils.CheckKeyDown(hotKey.Value) && CanOpenBackpack())
            {
                opening = true;
                OpenBackpack(backpackEquipped);
            }
        }

        private static bool CanOpenBackpack() // TODO: For now this sets backpackEquipped to a backpack TYPE. Make it into a unique backpack ID.
        {
            // Get a list of all equipped items.
            List<ItemDrop.ItemData> equippedItems = Player.m_localPlayer.GetInventory().GetEquipedtems();

            // Go through all the equipped items, match them for any of the names in backpackTypes.
            // If a match is found, set backpackEquipped to that name and return true.
            string name;
            for (int i = 0; i <= equippedItems.Count; i++)
            {
                name = equippedItems[i].m_dropPrefab?.name;
                if (backpackTypes.Contains(name)) {
                    Jotunn.Logger.LogMessage($"Equipped backpack found: {name}");
                    backpackEquipped = name;
                    return true;
                }
            }

            // Return false if no match is found between backpackTypes and equippedItems.
            return false;
        }

        private static void OpenBackpack(string backpackID) // TODO: For now, this opens a backpack TYPE. Make it open a unique backpack ID.
        {
            // TODO: When you try to open a backpack, it should query for if this backpack has a unique ID (or backpackfile) associated with it. And if that ID is null, it should create a unique ID for it.

            backpackContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (backpackContainer == null)
                backpackContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();
            backpackContainer.m_name = backpackID;
            AccessTools.FieldRefAccess<Container, Inventory>(backpackContainer, "m_inventory") = backpackInventory;
            InventoryGui.instance.Show(backpackContainer);
        }

        private static void LoadBackpackInventory() // TODO: LoadBackpackInventory(someBackpackID)
        {
            // TODO: specify which inventory to open
            backpackInventory = new Inventory("Backpack", null, (int)backpackSize.x, (int)backpackSize.y); // "Backpack" is the string displayed above the backpack inventory when opened.
            if (File.Exists(Path.Combine(assetPath, backpackFileName)))
            {
                try
                {
                    string input = File.ReadAllText(Path.Combine(assetPath, backpackFileName));
                    ZPackage pkg = new ZPackage(input);

                    backpackInventory.Load(pkg);
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogWarning($"Backpack file corrupt!\n{ex}");
                }
            }
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
                var profile = ___m_profiles.Find(p => p.GetFilename() == (string)typeof(Game).GetField("m_profileFilename", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
                backpackFileName = "backpack_" + Path.GetFileNameWithoutExtension(profile.GetFilename()) + "_" + backpackEquipped; // TODO: This won't work because backpackEquipped can be null on exeucuting this line.
                LoadBackpackInventory(); // TODO: LoadBackpackInventory(someBackpackID)
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
                    backpackInventory = backpackContainer.GetInventory();
                    ___m_currentContainer = null;
                }
                else if (CanOpenBackpack())
                {
                    OpenBackpack(backpackEquipped);
                }
            }
        }

        [HarmonyPatch(typeof(Game), "SavePlayerProfile")]
        static class SavePlayerProfile_Patch
        {
            static void Prefix()
            {
                if (backpackInventory != null)
                {
                    ZPackage zpackage = new ZPackage();
                    backpackInventory.Save(zpackage);
                    string output = zpackage.GetBase64();

                    File.WriteAllText(Path.Combine(assetPath, backpackFileName), output);

                }

            }
        }

        [HarmonyPatch(typeof(Inventory), "GetTotalWeight")]
        [HarmonyPriority(Priority.Last)]
        static class GetTotalWeight_Patch
        {
            static void Postfix(Inventory __instance, ref float __result)
            {
                if (!backpackContainer || !Player.m_localPlayer)
                    return;
                if (__instance == Player.m_localPlayer.GetInventory())
                {
                    if (new StackFrame(2).ToString().IndexOf("OverrideGetTotalWeight") > -1)
                    {
                        return;
                    }
                    __result += backpackInventory.GetTotalWeight();
                }
                else if (__instance == backpackInventory)
                {
                    __result *= backpackWeightMult;
                }
            }
        }

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            static bool Prefix(Console __instance)
            {
                string text = __instance.m_input.text;
                if (text.ToLower().Equals($"{typeof(JotunnBackpacks).Namespace.ToLower()} reset"))
                {
                    context.Config.Reload();
                    context.Config.Save();

                    if (Player.m_localPlayer)
                        LoadBackpackInventory(); // TODO: LoadBackpackInventory(someBackpackID)

                    Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { $"{context.Info.Metadata.Name} config reloaded" }).GetValue();
                    return false;
                }
                return true;
            }
        }

        // This particular patch I got from sbtoonz on GitHub: https://github.com/VMP-Valheim/Back_packs/blob/master/JotunnModStub/BackPacks.cs
        [HarmonyPatch(typeof(Inventory), "IsTeleportable")]
        private static class PatchTeleportable
        {
            private static bool InvCanTP;

            public static bool Prefix()
            {
                foreach (ItemDrop.ItemData item in backpackInventory.m_inventory)
                {
                    if (!item.m_shared.m_teleportable)
                    {
                        return false;
                    }
                }
                InvCanTP = true;

                return InvCanTP;
            }
        }


    }
}