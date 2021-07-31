# JötunnBackpacks

This mod introduces two backpack models (thanks to Cinnabunn!) to the game. You can open them with a hotkey (_i_ by default) and store items in them.

(todo image)

### Features
* Each backpack has their own separate inventory, and their inventories are preserved even when you toss your backpack to a friend.
* Storing items in the backpack reduces their weight by 50% by default (configurable).
* You can also configure how much they modify your carry capacity and movement speed.
* All configs (except hotkey) are server-enforceable.
* [Localization support](https://valheim-modding.github.io/Jotunn/tutorials/localization.html#example-json-file) due to Jotunn. Please let me know if you want to add a translation for your language!
* You cannot teleport with unteleportable items in the backpack.
* Nor can you put a backpack inside a backpack in order to get around this limitation!

### Server
* Should be installed on the server and on all clients. If the mod is on the server, it will disconnect clients without the mod.

### Credit
 * **Evie/CinnaBunn** for their 'eviesbackpacks' assets inside [JotunnModExample](https://github.com/Valheim-Modding/JotunnModExample/tree/master/JotunnModExample/AssetsEmbedded).
 * **Aedenthorn** for their [BackpackRedux](https://www.nexusmods.com/valheim/mods/1333) mod, which I derived and learned a lot from. _Feel free to [donate](https://www.nexusmods.com/valheim/users/18901754) to show appreciation!_ :)
 * **Randy Knapp** for their [Extended Item Framework](https://github.com/RandyKnapp/ValheimMods/tree/main/ExtendedItemDataFramework), without which this project would have been much harder.
 * **[sbtoonz/Zarboz](https://github.com/VMP-Valheim/Back_packs)** for guidance and help with various things like setting ZNetView().m_persistent=true.
 * **The Jotunn Team** for creating [Jotunn: The Valheim Library](https://valheim-modding.github.io/Jotunn/index.html), which is the framework this mod uses.
 * **MarcoPogo** and **Jules** for helping me with some questions I had in the [Jotunn Discord](https://discord.gg/DdUt6g7gyA).

Most of this project is the result of the hard work of these awesome people!


### Compatibility Notes
 * Compatible with _[Project Auga](https://projectauga.com/)!_
 * _No mod conflicts that I know of yet._

Please let me know if you find any additional bugs, issues or incompatibilities.

### How to Install
0. _(Optional)_ Use a mod manager like [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/) or alternatives.
1. Install [BepInEx for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
2. Install [Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)
3. Install [Extended Item Data Framework](https://valheim.thunderstore.io/package/RandyKnapp/ExtendedItemDataFramework/)
4. Install this mod.

For manual install, you want to drag the _JotunnBackpacks_ folder into the BepInEx/Plugins folder. The _JotunnBackpacks_ folder should contain _JotunnBackpacks.dll_ and _Translations_.

### Links
Thunderstore: (link)  
Nexusmods: (link)  
GitHub: https://github.com/Emrik-North/JotunnBackpacks  
