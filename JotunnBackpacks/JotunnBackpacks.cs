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
// using ExtendedItemDataFramework; // Maybe we can make it work without this dependency

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
        private static string assetPath;
        private static bool opening = false;
        private static Container backpackContainer; // Only need a single Container, I think, because only the contents (Inventory) vary between backpacks, not sizes.
        private static Inventory backpackInventory; // = new Inventory("Backpack", null, (int)backpackSize.x, (int)backpackSize.y);

        private static JotunnBackpacks context; // What does context = this; mean?
        private Harmony harmony;

        // Emrik's adventures
        public static List<string> backpackList = new List<string>(); // This is to store all unique IDs for each backpack
        public static List<string> backpackTypes = new List<string>(); // All the types of backpacks (currently only CapeIronBackpack and CapeSilverBackpack)
        public static Dictionary<string, Inventory> backpackDict = new Dictionary<string, Inventory>(); // This needs to be loaded up at start and saved on quit.
        public static string backpackEquipped; // Backpack currently equipped

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

            // TODO: This probably doesn't need to be inside Awake(), test moving it
            backpackTypes.Add("CapeIronBackpack");
            backpackTypes.Add("CapeSilverBackpack");

            // This is where all the backpack files will be stored
            assetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), typeof(JotunnBackpacks).Namespace);
            if (!Directory.Exists(assetPath))
            {
                Jotunn.Logger.LogMessage("Creating mod folder");
                Directory.CreateDirectory(assetPath);
            }

            // Get all the backpack infos stored in the JotunnBackpacks folder, if there are any there.
            // TODO: NOTE that this will even load the backpack infos that you generated in other worlds/servers, but you won't be able to access them because you can't access their unique IDs.
            // It's inefficient, though, since you'll not be using those infos in the world you're playing in. Will this be a problem?
            LoadBackpackDictFromFile();

            harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();
        }

        private static void LoadBackpackDictFromFile()
        {
            // Loads all backpack filenames and inventories from where they were stored in assetPath and saves them in backpackDict

            // Creates an enumerated list of all files in the assetPath
            // List<string> backpackFiles = new List<string> (Directory.EnumerateFiles(assetPath, ""));
            var backpackFiles = Directory.EnumerateFiles(assetPath); // This gets a list of filenames INCLUDING their full path, e.g. "D:\Games\SteamLibrary\steamapps\common\Valheim\BepInEx\plugins\JotunnBackpacks\JotunnBackpacks\MyBackpackID"
            string fileName;

            // The assetPath contains the names of backpacks, and their corresponding contents stored in the form of ZPackages
            foreach (string fileFullPath in backpackFiles)
            {
                fileName = Path.GetFileName(fileFullPath);
                Jotunn.Logger.LogMessage($"Found file: {fileName}\n");

                // Create a new instance of an Inventory class
                // If we stored the inventory without creating a new instance for each backpack, each backpack would just refer to the same inventory
                Inventory backpackInventory = new Inventory("Backpack", null, (int)6, (int)3);

                try
                {
                    // The contents of backpack files are stored in ZPackage format.
                    string contents = File.ReadAllText(fileFullPath);

                    // Create an instance of a ZPackage and store the contents in there.
                    ZPackage pkg = new ZPackage(contents);      

                    // Load the ZPackage into the newly created inventory
                    backpackInventory.Load(pkg); // TODO: NullReferenceException from hell!
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogWarning($"Backpack file corrupt!\n{ex}");
                }

                // Add all the file names and contents into the backpackDict
                backpackDict.Add(fileName, backpackInventory);
                Jotunn.Logger.LogMessage($"Added to backpackDict: {fileName}\n");

                // TODO Q: Should I initialize the backpacks with null contents in this dictionary, and only load their inventories when the backpack is opened, or when checking for character weight?
                // Would that save on load time and/or RAM in case of a large number of backpacks? What would be more efficient?
                // Should I clear backpackInventory at the end of this function, so that nothing can get to the last loaded inventory by accident?
            }
        }
        private static string GetEquippedBackpack() // TODO: For now this gets a backpack TYPE. Make it into a unique backpack ID.
        {
            // Get a list of all equipped items.
            List<ItemDrop.ItemData> equippedItems = Player.m_localPlayer.GetInventory().GetEquipedtems();

            // Go through all the equipped items, match them for any of the names in backpackTypes.
            // If a match is found, return the name.
            string name;
            for (int i = 0; i <= equippedItems.Count; i++)
            {
                name = equippedItems[i].m_dropPrefab?.name; // TODO: Maybe I can use .m_crafterName, and store the unique IDs into crafternames?
                if (backpackTypes.Contains(name))
                {
                    Jotunn.Logger.LogMessage($"Equipped backpack found: {name}");
                    return name;
                }
            }

            // Return null if no backpacks are found.
            return null;
        }

        // TODO: This method is from Aedenthorn's BackpackRedux, and I'm not entirely sure what it does yet. WTF is a ZNetScene?
        // Also, I can't see this method being called anywhere?
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

        private static void OpenBackpack() // TODO: For now, this opens a backpack TYPE. Make it open a unique backpack ID.
        {
            // TODO: When you try to open a backpack, it should query for if this backpack has a unique ID (or backpackfile) associated with it. And if that ID is null, it should create a unique ID for it.

            backpackContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (backpackContainer == null)
                backpackContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();

            // CanOpenBackpack() is always executed before this code, so backpackEquipped has a value, otherwise OpenBackback wouldn't get executed in the first place
            backpackContainer.m_name = backpackEquipped;

            // Checks to see if backpackEquipped already has an entry in the backpackDict, and loads it into backpackInventory if so, otherwise it creates a new Inventory instance for this backpack and stores that to backpackInventory
            LoadBackpackInventory(backpackEquipped);

            AccessTools.FieldRefAccess<Container, Inventory>(backpackContainer, "m_inventory") = backpackInventory;
            InventoryGui.instance.Show(backpackContainer);
        }

        private static void LoadBackpackInventory(string backpackName)
        {
            // If backpackDict already contains backpackName, open the inventory from the dictionary
            if (backpackDict.ContainsKey(backpackName))
            {
                try
                {
                    backpackInventory = backpackDict[backpackName];
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogWarning($"Backpack file corrupt!\n{ex}");
                }
            }

            else
            {
                // If backpackDict does not have an existing inventory corresponding to the name, create a new one
                backpackInventory = new Inventory("Backpack", null, (int)backpackSize.x, (int)backpackSize.y); // "Backpack" is the string displayed above the backpack inventory when opened.

                // And add it to the backpackDict
                backpackDict.Add(backpackName, backpackInventory);
                Jotunn.Logger.LogMessage($"Opening new backpack, adding reference to backpackDict for: {backpackName}");
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
                    backpackInventory = backpackContainer.GetInventory();
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
                if (backpackDict != null)
                {
                    // Declare the output variable before the loop, so it doesn't have to be declared (and a memory location found for it) during each iteration
                    // Hopefwly this is more efficient, but maybe that's all taken care of by the compiler anyhow, who knows.
                    string output;

                    // This goes through all the pairs in backpackDict, and stores them into assetPath (get the contents of inventories as ZPackages first)
                    foreach (KeyValuePair<string, Inventory> backpackEntry in backpackDict)
                    {
                        ZPackage zpackage = new ZPackage();
                        backpackEntry.Value.Save(zpackage); // Saves the backpackEntry's Inventory to the zpackage.
                        output = zpackage.GetBase64();

                        Jotunn.Logger.LogMessage($"Trying to save the following backpack inventory: {backpackEntry.Value}\n");
                        Jotunn.Logger.LogMessage($"Trying to save the following zpackage: {output}\n");

                        // Write it to file
                        File.WriteAllText(Path.Combine(assetPath, backpackEntry.Key), output);
                        Jotunn.Logger.LogMessage($"Saved to file: {backpackEntry.Key}\n");
                    }
                }
            }
        }

        // TODO: I haven't touched this yet at all. I should do that.
        [HarmonyPatch(typeof(Inventory), "GetTotalWeight")]
        [HarmonyPriority(Priority.Last)]
        static class GetTotalWeight_Patch
        {
            static void Postfix(Inventory __instance, ref float __result)
            {

                // Temporarily disabling this whole thingy while I test other things!
                return;

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

        // TODO: I guess this allows the user to reset their backpacks in-game in case something goes wrong. I could probably remove it?
        [HarmonyPatch(typeof(Console), "InputText")] // TODO: What does this patch do? Can I remove it?
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
                        LoadBackpackDictFromFile();
                        // LoadBackpackInventory();

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
                return true; // Disable this patch for now, while I work on other stuff

                // TODO:
                // foreach (item in inventory)
                // {
                //    if (is a backpack)
                //    {
                //       go through its inventory and check for non-teleportable stuff, return false as soon as first offender is found
                //    }
                // }

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