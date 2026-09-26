# POI Radar

A client-side Valheim mod that pins nearby points of interest on your minimap.

As you explore, everything the game has loaded around you that is worth a detour shows up as a pin on both the minimap and the large map. Walk away and the pins go with the terrain; nothing is written to your map save, so your real pins stay clean.

<!-- Screenshot goes here once docs/screenshot.png exists:
![Ore deposits and a burial chamber pinned on the minimap](https://raw.githubusercontent.com/dsoltyka/ValheimPOIRadar/main/docs/screenshot.png)
-->

**On by default**

* **Dungeons**: burial chambers, troll caves, sunken crypts, frost caves, infested mines and any other location with an interior, including modded ones.
* **Ore deposits**: copper, tin, silver, obsidian, black marble, flametal and anything else you can mine that drops more than plain stone. Each pin uses the ore's own item icon, so you can tell them apart at a glance. Deposits hidden completely under the ground, like untouched silver, stay hidden unless you opt in with `ShowBuried`.

**Off by default** (turn on in the config)

* **Pickables**: berries, mushrooms, thistle, dandelions, barley, flax and friends. Picked plants disappear from the map until they regrow. A word filter (`Branch,Stone,Flint` by default) keeps junk off the map.
* **Other locations**: surface spots placed by the world generator, such as ruins, abandoned camps, tar pits, runestones, altars and villages.

Pins show for the area the game keeps loaded around you (roughly 200 m). An optional `MaxDistance` trims that further, and `OnlyExploredAreas` hides pins that would land on map you have not uncovered yet.

## Installation

Install with r2modman / Thunderstore Mod Manager, or drop `POIRadar.dll` into `BepInEx/plugins`.

Requires [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Client-side only: it reads what your game has already loaded and draws on your own minimap. The server needs nothing and other players are unaffected.

## Configuration

`BepInEx/config/dsoltyka.POIRadar.cfg` is created on first launch. Every setting applies immediately.

| Section | Setting | Default | Meaning |
| --- | --- | --- | --- |
| Categories | `Dungeons` | true | Pin dungeon entrances |
| Categories | `OreDeposits` | true | Pin mineable deposits with the ore's icon |
| Categories | `Pickables` | false | Pin berries, mushrooms, plants |
| Categories | `OtherLocations` | false | Pin surface locations (ruins, camps, tar pits, altars) |
| Display | `MaxDistance` | 0 | Only pin within this many meters; 0 = everything loaded |
| Display | `ShowBuried` | false | Also pin deposits fully below the terrain, such as untouched silver veins. Partially exposed veins always show |
| Display | `OnlyExploredAreas` | false | Only pin objects on map you have already explored (or had shared with you), so the radar never hints at unexplored terrain |
| Display | `ShowLabels` | true | Draw the name next to each pin |
| Display | `LargeIcons` | false | Double-size pins |
| Display | `RefreshSeconds` | 0.5 | How often pins are re-evaluated |
| Display | `PickableExclude` | Branch,Stone,Flint | Pickables whose name contains any of these words are skipped |

## Source

Code is on GitHub at [dsoltyka/ValheimPOIRadar](https://github.com/dsoltyka/ValheimPOIRadar).

<!-- github-only -->
## Building

See [BUILDING.md](BUILDING.md). This section is stripped from the copy of the README that ships to Thunderstore.
<!-- /github-only -->

## License

MIT. See [LICENSE](https://github.com/dsoltyka/ValheimPOIRadar/blob/main/LICENSE).
