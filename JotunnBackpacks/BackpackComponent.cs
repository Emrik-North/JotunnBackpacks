using System;
using ExtendedItemDataFramework;

public class BackpackComponent : BaseExtendedItemComponent
{
    public Inventory backpackInventory;

    public BackpackComponent(ExtendedItemData parent) : base(typeof(BackpackComponent).AssemblyQualifiedName, parent)
    {
        // Don't think I need to run any code here. Creating the class is all that's needed? I don't even understand the syntax here.
    }

    public void SetBackpackInventory(Inventory inventoryInstance)
    {
        backpackInventory = inventoryInstance;
        Save(); // This writes the new data to the ItemData object, which will be saved whenever game saves the ItemData object.
    }

    public override string Serialize()
    {
        Jotunn.Logger.LogMessage("SERIALIZING");

        ZPackage pkg = new ZPackage();
        backpackInventory.Save(pkg);
        string data = pkg.GetBase64();

        return data;
    }

    // This code is run on game start for objects with a BackpackComponent, and it converts the inventory information from string format (ZPackage) to object format (Inventory) so the game can use it.
    public override void Deserialize(string data)
    {
        try
        {
            ZPackage pkg = new ZPackage(data);
            Jotunn.Logger.LogMessage($"DESERIALIZING: \n data: {data}\n");
            backpackInventory.Load(pkg); // NullReferenceError from hell TODO.

        }
        catch (Exception ex)
        {
            Jotunn.Logger.LogError($"Backpack info is corrupt!\n{ex}");
        }
    }

    // Just following Randy Knapp's guide here: https://github.com/RandyKnapp/ValheimMods/tree/main/ExtendedItemDataFramework#readme
    public override BaseExtendedItemComponent Clone()
    {
        return MemberwiseClone() as BaseExtendedItemComponent;
    }
}