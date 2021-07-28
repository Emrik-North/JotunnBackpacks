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
        public Inventory eidf_inventory;
        public float eidf_weight; // We need to store weight info here in order to preserve it in between game exit/start, since the game itself doesn't save changes to m_weight by default
        private readonly char customDelimiter = 'ø';

        public BackpackComponent(ExtendedItemData parent) : base(typeof(BackpackComponent).AssemblyQualifiedName, parent)
        {
            // No code needed here.
        }

        public void SetInventory(Inventory inventoryInstance)
        {
            eidf_inventory = inventoryInstance;
            Save(); // This writes the new data to the ItemData object, which will be saved whenever game saves the ItemData object.
        }

        public Inventory GetInventory()
        {
            return eidf_inventory;
        }

        public void SetWeight(float weight)
        {
            eidf_weight = weight;
            Save(); // TODO: SetWeight() is called very frequently while in inventory screen, so this is costly. Rather just save during normal player save.
        }

        // NOTE that this doesn't update the item's weight in game. It just stores its unique weight into m_crafterName via EIDF.
        // We hook in to LoadExtendedItemData() to set its real in-game weight to equal its eidf_weight whenever the item is loaded in game.
        public float GetWeight()
        {
            return eidf_weight;
        }

        public override string Serialize()
        {
            // Store the Inventory as a ZPackage
            ZPackage pkg = new ZPackage();
            eidf_inventory.Save(pkg);
            string inventoryData = pkg.GetBase64();

            // Return both the inventoryData and the backpackItemWeight in a combined string to be deserialized in the method below
            return inventoryData + customDelimiter + eidf_weight.ToString();
        }

        // This code is run on game start for objects with a BackpackComponent, and it converts the inventory information from string format (ZPackage) to object format (Inventory) so the game can use it.
        public override void Deserialize(string data)
        {
            try
            {
                // First split the data string
                string[] dataArray = data.Split(customDelimiter);
                string inventoryData = dataArray[0];

                // If the dataArray contains weight info, set weightData to it, otherwise set it to 0
                float weightData = dataArray.Length > 1 ? float.Parse(dataArray[1]) : 0;

                // When the game closes, it saves data from ItemData objects by storing it as strings in the save file, and then it destroys all instances of objects.
                // So upon game start, we need to initialise new objects and store the saved data into those.
                // If you don't reinitialise your Inventory objects on game start, you'll get a NullReferenceError when the game tries to access those inventories.

                // Check if backpackInventory already refers to an Inventory instance, and initialise one for it if not.
                // I think if you only log out, and don't close the game, all object instances aren't destroyed? If so, we don't want to reinitialise them upon login.
                if (eidf_inventory is null)
                {
                    eidf_inventory = JotunnBackpacks.NewInventoryInstance();
                }

                // Deserialising saved inventory data and storing it into the newly initialised Inventory instance.
                ZPackage pkg = new ZPackage(inventoryData);
                eidf_inventory.Load(pkg);

                // Initialise the backpack item weight as a float
                eidf_weight = weightData;

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
