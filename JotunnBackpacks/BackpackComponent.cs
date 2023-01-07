/* BackpackComponent.cs
 * 
 * Converted by Vapok to no longer rely on Extended Item Data Framework.
 * Now utilized Iron Gate's m_customData object to store backpack data.
 *
 * Github: https://github.com/Vapok/JotunnBackpacks
 *
 * Automatically converts EIDF backpacks to new CustomData backpacks.
 * Backpacks created after this conversion can not be used in prior versions of JotunnBackpacks
 *
 */

using System;
using JotunnBackpacks.Data;
using UnityEngine;

// Setting this .cs file to the same namespace as JotunnBackpacks.cs, so that I can call methods from within JotunnBackpacks.cs here.
namespace JotunnBackpacks
{
    public class BackpackComponent : CustomItemData
    {
        public static string TypeID = "BackpackComponent";

        public Inventory backpack_inventory;

        public void SetInventory(Inventory inventoryInstance)
        {
            backpack_inventory = inventoryInstance;
            Save(backpack_inventory); // This writes the new data to the ItemData object, which will be saved whenever game saves the ItemData object.
        }

        public Inventory GetInventory()
        {
            return backpack_inventory;
        }

        public string Serialize()
        {
            // Store the Inventory as a ZPackage
            ZPackage pkg = new ZPackage();

            backpack_inventory.Save(pkg);

            string data = pkg.GetBase64();
            Value = data;

            // Return the data to be deserialized in the method below
            return data;
        }

        // This code is run on game start for objects with a BackpackComponent, and it converts the inventory info from string format (ZPackage) to object format (Inventory) so the game can use it.
        public void Deserialize(string data)
        {
            try
            {
                if (backpack_inventory is null)
                {
                    // Figure out which backpack type we are deserializing data for by accessing the ItemData of the base class.
                    var type = Item.m_shared.m_name;
                    backpack_inventory = JotunnBackpacks.NewInventoryInstance(type);
                }

                //Save data to Value
                Value = data;

                // Deserialising saved inventory data and storing it into the newly initialised Inventory instance.
                ZPackage pkg = new ZPackage(data);
                backpack_inventory.Load(pkg);

                Save(backpack_inventory);

            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Backpack info is corrupt!\n{ex}");
            }
        }

        public override void FirstLoad()
        {
            var name = Item.m_shared.m_name;

            // Check whether the item created is of a type contained in backpackTypes
            if (JotunnBackpacks.backpackTypes.Contains(name))
            {
                if (backpack_inventory != null)
                {
                    return;
                }

                //Check to see if we have old EIDF Component Data
                var oldBackpackData = EIDFLegacy.GetCustomItemFromCrafterName(Item);
                if (oldBackpackData != null)
                {
                    backpack_inventory = JotunnBackpacks.NewInventoryInstance(name);
                    Value = oldBackpackData;
                    Deserialize(Value);
                }
            }
        }

        public override void Load()
        {
            if (!string.IsNullOrEmpty(Value))
            {
                Deserialize(Value);
            }
            else
            {
                //Check to see if we have old EIDF Component Data
                var oldBackpackData = EIDFLegacy.GetCustomItemFromCrafterName(Item);
                if (oldBackpackData != null)
                {
                    Value = oldBackpackData;
                    Deserialize(Value);
                }
            }
        }

        public override void Save()
        {
            Value = Serialize();
        }

        public void Save(Inventory backpack)
        {
            backpack_inventory = backpack;
            Save();
        }

        public CustomItemData Clone()
        {
            return MemberwiseClone() as CustomItemData;
        }
    }

    public static class BackpackExtensions
    {
        public static GameObject InitializeCustomData(this ItemDrop.ItemData itemData)
        {
            var prefab = itemData.m_dropPrefab;
            if (prefab != null)
            {
                var itemDropPrefab = prefab.GetComponent<ItemDrop>();
                var instanceData = itemData.Data().GetOrCreate<BackpackComponent>();

                var prefabData = itemDropPrefab.m_itemData.Data().GetOrCreate<BackpackComponent>();

                instanceData.Save(prefabData.backpack_inventory);
                
                return itemDropPrefab.gameObject;
            }

            return null;
        }
    }
}
