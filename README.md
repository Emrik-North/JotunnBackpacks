# JötunnBackpacks

A work in progress. Almost done.

This mod introduces two backpacks (thanks to Evie) to the game. They increase your carry capacity and lets you open their inventories with a configurable hotkey (_i_ by default). Items stored in the backpacks have their weight reduced by 25%, but they also reduce your movement speed.

Each backpack has its own separate inventory that's preserved even when you toss the backpack to another player on the server. You also cannot teleport with unteleportable items inside the backpack. This check is recursive, so you can't exploit it by putting backpacks inside backpacks. Don't be silly! You also cannot exploit the weight reduction by putting backpacks inside backpacks, since the weight reduction only applies to the innermost backpack(s). Please, stop being silly!

(todo image)

### Credit goes to
 * [Cinnabunn/Evie](https://github.com/capnbubs) for their 'eviesbackpacks' assets inside [JotunnModExample](https://github.com/Valheim-Modding/JotunnModExample/tree/master/JotunnModExample/AssetsEmbedded).
 * Randy Knapp for their [Extended Item Framework](https://github.com/RandyKnapp/ValheimMods/tree/main/ExtendedItemDataFramework), without which I wouldn't be able to do this project.
 * Aedenthorn for their [BackpackRedux](https://github.com/aedenthorn/ValheimMods/blob/master/BackpackRedux/) mod, which I learned a great deal from.
 * [sbtoonz](https://github.com/VMP-Valheim/Back_packs) for initial help with the IsTeleportable_Patch.
 * The Jotunn Team for creating [Jotunn: The Valheim Library](https://valheim-modding.github.io/Jotunn/index.html), which makes modding life a lot more convenient.

Most of this project is the result of the hard work of these people. All I've done is combine their efforts into this mod and smoothed out issues.

### Todo
 * There seems to be an incompatibility with [RRRNpcs](https://valheim.thunderstore.io/package/neurodr0me/RRRNpcs/) [RRRBetterRaids](https://valheim.thunderstore.io/package/neurodr0me/RRRBetterRaids/). Will want to find a solution to that before public release.
 * Test how this works together with [Server Side Characters](https://valheim.thunderstore.io/package/HackShardGaming/World_of_Valheim_SSC/).
 * Test whether Jotunn's NetworkCompatibility attribute works properly.
 * Change _GetTotalWeight_Patch_ to add Inventory weights directly to backpack ExtendedItemData m_weight, and also eject backpacks-inside-backpacks as a Prefix before GetTotalWeight()
 * Do a final check for all features, and try my darndest to break things before it's ready for official release.

### Server info
 * Both server and client need to have this mod installed, otherwise you might find yourself losing backpack contents from time to time. If the server has it installed, it checks whether connecting clients have it installed and denies connection if they don't.

### Compatibility notes
 * [BackpackRedux](https://www.nexusmods.com/valheim/mods/1333) (incompatible)
 * Hopefully no more when this is released.

Please let me know if you find any additional bugs, issues or incompatibilities.

### Links
Thunderstore: (link)  
Nexusmods: (link)  
GitHub: https://github.com/Emrik-North/JotunnBackpacks  
