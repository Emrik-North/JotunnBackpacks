// JotunnBackpacks
// A Valheim mod using Jötunn
// Used to demonstrate the libraries capabilities
// 
// File:    JotunnBackpacks.cs
// Project: JotunnBackpacks

// TODO: 
// Check backpack for unteleportable items before going through portal.
// Use ExtendedItemDataFramework to attach a separate inventory to each backpack.

using System;
using ExtendedItemDataFramework; // Maybe we can make it work without this dependency


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
        Save(); // This writes the new item data to the ItemData object, which will be saved whenever game saves the ItemData object.
    }

    // TODO: Do I need the "override" keywords here?
    // When the game closes, it needs to save all the stuff in string format, so we need to convert the objects (like Inventory) to strings (like ZPackages) when the game closes, otherwise we can't load them up later.
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
            backpackInventory.Load(pkg); // NullReferenceError from hell TODO. Is the BackpackInventory even an instance here? How does this method reference the instance??? Should look into how EIDF was coded.

            // TODO: Manually call the Serialize method for each backpack in a on_player_save (or something) patch. Also keep a list of all known backpack in a dictionary, so you know which backpacks to save (serialize) on game exit.

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