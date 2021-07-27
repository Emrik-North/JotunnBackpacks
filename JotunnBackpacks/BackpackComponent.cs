/* BackpackComponent.cs
 * 
 * I'm mostly following Randy Knapp's wonderfwl guide to their Extended Item Data Framework here, and I'm not sure about all the syntax, myself.
 * https://github.com/RandyKnapp/ValheimMods/tree/main/ExtendedItemDataFramework#readme
 * 
 */

using System;
using ExtendedItemDataFramework;

// Setting this .cs file to the same namespace as JotunnBackpacks.cs, so that I can call methods from within JotunnBackpacks.cs here.
namespace JotunnBackpacks
{
    public class BackpackComponent : BaseExtendedItemComponent
    {
        public Inventory backpackInventory;
        public float backpackItemWeight;
        private char customDelimiter = 'ø';

        public BackpackComponent(ExtendedItemData parent) : base(typeof(BackpackComponent).AssemblyQualifiedName, parent)
        {
            // No code needed here.
        }

        public void SetInventory(Inventory inventoryInstance)
        {
            backpackInventory = inventoryInstance;
            Save(); // This writes the new data to the ItemData object, which will be saved whenever game saves the ItemData object.
        }

        public Inventory GetInventory()
        {
            return backpackInventory;
        }

        public void SetItemWeight(float weight)
        {
            backpackItemWeight = weight;
            Save();
        }

        public float GetItemWeight()
        {
            return backpackItemWeight;
        }

        public override string Serialize()
        {
            // Store the Inventory as a ZPackage
            ZPackage pkg = new ZPackage();
            backpackInventory.Save(pkg);
            string inventoryData = pkg.GetBase64();

            // Return both the inventoryData and the backpackItemWeight in a combined string to be deserialized in the method below
            return inventoryData + customDelimiter + backpackItemWeight.ToString();
        }

        // This code is run on game start for objects with a BackpackComponent, and it converts the inventory information from string format (ZPackage) to object format (Inventory) so the game can use it.
        public override void Deserialize(string data)
        {
            try
            {
                // First split the data string
                string[] dataArray = data.Split(customDelimiter);
                string inventoryData = dataArray[0];
                string weightData = dataArray[1];

                // When the game closes, it saves data from ItemData objects by storing it as strings in the save file, and then it destroys all instances of objects.
                // So upon game start, we need to initialise new objects and store the saved data into those.
                // If you don't reinitialise your Inventory objects on game start, you'll get a NullReferenceError when the game tries to access those inventories.

                // Check if backpackInventory already refers to an Inventory instance, and initialise one for it if not.
                // I think if you only log out, and don't close the game, all object instances aren't destroyed? If so, we don't want to reinitialise them upon login.
                if (backpackInventory is null)
                {
                    backpackInventory = JotunnBackpacks.NewInventoryInstance();
                }

                // Deserialising saved inventory data and storing it into the newly initialised Inventory instance.
                ZPackage pkg = new ZPackage(inventoryData);
                backpackInventory.Load(pkg);

                // Initialise the backpack item weight as a float
                backpackItemWeight = float.Parse(weightData);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Backpack info is corrupt!\n{ex}");
            }
        }

        // Just following Randy Knapp's guide here
        public override BaseExtendedItemComponent Clone()
        {
            return MemberwiseClone() as BaseExtendedItemComponent;
        }
    }
}
