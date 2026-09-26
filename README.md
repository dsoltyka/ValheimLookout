# POI Radar

A client-side Valheim mod that pins nearby points of interest on your minimap, and lets you choose exactly what counts.

As you explore, everything the game has loaded around you that you have opted into shows up as a pin on both the minimap and the large map. Walk away and the pins go with the terrain; nothing is written to your map save, so your real pins stay clean.

<!-- Screenshot goes here once docs/screenshot.png exists:
![Ore deposits and a burial chamber pinned on the minimap](https://raw.githubusercontent.com/dsoltyka/ValheimPOIRadar/main/docs/screenshot.png)
-->

## What it pins

**Out of the box**

* **Dungeon entrances**: burial chambers, troll caves, sunken crypts, frost caves, infested mines and any other location with an interior, including modded ones.
* **Ore**: copper, tin, silver, iron scrap, obsidian, black marble and flametal. Untouched veins count, not just ones someone has already hit. Each pin uses the ore's own icon.

**Everything else is opt-in, from the panel**

Press **F7** (configurable), or click the **POI Radar** button in the top-left corner of the large map. The panel lists every item the world can yield from something you can mine, break or pick: ores, but also guck, ancient seeds, feathers, soft tissue, yggdrasil wood, berries, mushrooms, and whatever new biomes or mods add. Tick an item and every deposit, breakable or bush that yields it gets a pin with that item's icon. A filter box narrows the list, and a number on each row shows how many sources are loaded near you right now.

The list is built by scanning the game's own object definitions when you load in, so it is complete for your install rather than a hand-picked set.

Two more switches live at the top of the panel:

* **Other locations**: surface spots placed by the world generator, such as ruins, abandoned camps, tar pits, runestones, altars and villages.
* **Buried deposits**: pin veins that are completely under the ground, like untouched silver. Off by default so the radar does not give hidden ore away; partially exposed veins always show.
* **Only explored map**: hide pins that would land on map you have not uncovered yet.

Pins show for the area the game keeps loaded around you (roughly 200 m). An optional `MaxDistance` trims that further.

## Installation

Install with r2modman / Thunderstore Mod Manager, or drop `POIRadar.dll` into `BepInEx/plugins`.

Requires [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Client-side only: it reads what your game has already loaded and draws on your own minimap. The server needs nothing and other players are unaffected.

## Configuration

`BepInEx/config/dsoltyka.POIRadar.cfg` is created on first launch. Every setting applies immediately, whether changed from the panel, an in-game config manager, or by editing the file.

| Section | Setting | Default | Meaning |
| --- | --- | --- | --- |
| What to pin | `Dungeons` | true | Pin dungeon entrances |
| What to pin | `OtherLocations` | false | Pin surface locations (ruins, camps, tar pits, altars) |
| What to pin | `Items` | the ores | Comma-separated item tokens to pin; the panel edits this for you |
| Display | `MaxDistance` | 0 | Only pin within this many meters; 0 = everything loaded |
| Display | `ShowBuried` | false | Also pin deposits fully below the terrain |
| Display | `OnlyExploredAreas` | false | Only pin objects on map you have explored (or had shared with you) |
| Display | `ShowLabels` | true | Draw the name next to each pin |
| Display | `LargeIcons` | false | Double-size pins |
| Display | `RefreshSeconds` | 0.5 | How often pins are re-evaluated |
| Interface | `ToggleKey` | F7 | Opens and closes the panel |
| Interface | `MapButton` | true | Show the POI Radar button on the large map |

## Source

Code is on GitHub at [dsoltyka/ValheimPOIRadar](https://github.com/dsoltyka/ValheimPOIRadar).

<!-- github-only -->
## Building

See [BUILDING.md](BUILDING.md). This section is stripped from the copy of the README that ships to Thunderstore.
<!-- /github-only -->

## License

MIT. See [LICENSE](https://github.com/dsoltyka/ValheimPOIRadar/blob/main/LICENSE).
