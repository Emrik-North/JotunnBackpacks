/* BackpackAssets.cs
 * 
 * Based off of JotunnModExample, including eviesbackpacks made by CinnaBunn (Evie).
 * https://github.com/Valheim-Modding/JotunnModExample/
 * 
 */

using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;

 
namespace JotunnBackpacks
{
    public class BackpackAssets
    {
        // Asset and prefab loading
        private static AssetBundle EmbeddedResourceBundle;
        private static GameObject BackpackIronPrefab;
        private static GameObject BackpackSilverPrefab;
        //private static GameObject BackpackIronQuickdropPrefab; // TODO
        //private static GameObject BackpackSilverQuickdropPrefab;
        private static CustomStatusEffect ruggedBackpackEffect;
        private static CustomStatusEffect arcticBackpackEffect;

        public static void LoadAssets()
        {
            // Load backpack asset bundle from embedded resources
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(",", typeof(JotunnBackpacks).Assembly.GetManifestResourceNames())}");
            EmbeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("vapokbackpacks", typeof(JotunnBackpacks).Assembly);
            BackpackIronPrefab = EmbeddedResourceBundle.LoadAsset<GameObject>("Assets/vapok/CapeIronBackpack.prefab");
            BackpackSilverPrefab = EmbeddedResourceBundle.LoadAsset<GameObject>("Assets/vapok/CapeSilverBackpack.prefab");
        }
        
        public static void AddStatusEffects()
        {
            SE_Stats effectsRuggedBackpack = ScriptableObject.CreateInstance<SE_Stats>();
            effectsRuggedBackpack.name = "SE_RuggedBackpack";
            effectsRuggedBackpack.m_name = "$se_ruggedbackpack";
            effectsRuggedBackpack.m_startMessageType = MessageHud.MessageType.TopLeft;
            effectsRuggedBackpack.m_startMessage = "$se_ruggedbackpackeffects_start";
            effectsRuggedBackpack.m_addMaxCarryWeight = JotunnBackpacks.carryBonusRugged.Value;
            // effectsRuggedBackpack.m_attributes = StatusEffect.StatusAttribute.ColdResistance; // Doesn't work and I don't know why yet

            ruggedBackpackEffect = new CustomStatusEffect(effectsRuggedBackpack, fixReference: false);
            ItemManager.Instance.AddStatusEffect(ruggedBackpackEffect);


            SE_Stats effectsArcticBackpack = ScriptableObject.CreateInstance<SE_Stats>();
            effectsArcticBackpack.name = "SE_ArcticBackpack";
            effectsArcticBackpack.m_name = "$se_arcticbackpack";
            effectsArcticBackpack.m_startMessageType = MessageHud.MessageType.TopLeft;
            effectsArcticBackpack.m_startMessage = "$se_arcticbackpackeffects_start";
            effectsArcticBackpack.m_addMaxCarryWeight = JotunnBackpacks.carryBonusArctic.Value;

            arcticBackpackEffect = new CustomStatusEffect(effectsArcticBackpack, fixReference: false);
            ItemManager.Instance.AddStatusEffect(arcticBackpackEffect);

        }

        // Implementation of assets using mocks, adding recipes manually without the config abstraction
        public static void AddMockedItems()
        {
            var frostResistance = new HitData.DamageModPair() { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant};
            // Iron Backpack
            if (!BackpackIronPrefab) Jotunn.Logger.LogWarning($"Failed to load asset from bundle: {EmbeddedResourceBundle}");
            else
            {
                // Create and add a custom item
                CustomItem CI = new CustomItem(BackpackIronPrefab, true);
                ItemManager.Instance.AddItem(CI);

                // TODO: Prevent item despawning by making it a "player base" object
                //var component = CI.ItemDrop.gameObject.AddComponent<EffectArea>();
                //component.m_type = EffectArea.Type.PlayerBase;
                //component.enabled = true;


                // Update the backpack's stats from configs
                var itemData = CI.ItemDrop.m_itemData;
                itemData.m_shared.m_maxDurability = 1000f;
                itemData.m_shared.m_movementModifier = JotunnBackpacks.speedModRugged.Value;
                itemData.m_shared.m_equipStatusEffect = ruggedBackpackEffect.StatusEffect;
                if (JotunnBackpacks.freezingRugged.Value)
                    itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair> { frostResistance };

                // We have to set this to make sure the backpack doesn't immediately despawn if dropped on the ground and the player logs out
                CI.ItemPrefab.gameObject.GetComponent<ZNetView>().m_persistent = true; // Thanks, Zarboz!!

                // Create and add a custom recipe
                Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = "Recipe_CapeIronBackpack";
                recipe.m_item = BackpackIronPrefab.GetComponent<ItemDrop>();
                recipe.m_craftingStation = Mock<CraftingStation>.Create("piece_workbench");
                var ingredients = new List<Piece.Requirement>
                {
                    MockRequirement.Create("LeatherScraps", 8),
                    MockRequirement.Create("DeerHide", 2),
                    MockRequirement.Create("Bronze", 2),
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

                // Update the backpack's stats from configs
                var itemData = CI.ItemDrop.m_itemData;
                itemData.m_shared.m_maxDurability = 1000f;
                itemData.m_shared.m_movementModifier = JotunnBackpacks.speedModArctic.Value;
                itemData.m_shared.m_equipStatusEffect = arcticBackpackEffect.StatusEffect;
                if (JotunnBackpacks.freezingArctic.Value)
                    itemData.m_shared.m_damageModifiers = new List<HitData.DamageModPair> { frostResistance };

                // We have to set this to make sure the backpack doesn't immediately despawn if dropped on the ground and the player logs out
                CI.ItemPrefab.gameObject.GetComponent<ZNetView>().m_persistent = true; // Thanks, Zarboz!!

                //Create and add a custom recipe
                Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
                recipe.name = "Recipe_CapeSilverBackpack";
                recipe.m_item = BackpackSilverPrefab.GetComponent<ItemDrop>();
                recipe.m_craftingStation = Mock<CraftingStation>.Create("piece_workbench");
                var ingredients = new List<Piece.Requirement>
                {
                    MockRequirement.Create("LeatherScraps", 8),
                    MockRequirement.Create("WolfPelt", 2),
                    MockRequirement.Create("Silver", 2),
                };
                recipe.m_resources = ingredients.ToArray();
                CustomRecipe CR = new CustomRecipe(recipe, true, true);
                ItemManager.Instance.AddRecipe(CR);

                //Enable BoneReorder
                BoneReorder.ApplyOnEquipmentChanged();
            }


            // TODO
            // Iron Backpack Piece (for quickdrop)
            /*
            if (!BackpackIronPrefab) Jotunn.Logger.LogWarning($"Failed to load asset from bundle: {EmbeddedResourceBundle}");
            else
            {
                // Create and add a custom item
                CustomItem CI = new CustomItem(BackpackIronQuickdropPrefab, true);
                ItemManager.Instance.AddItem(CI);

                // TODO: Need to add extended inventory component to it


            }
            */
            EmbeddedResourceBundle.Unload(false);
        }
    }
}