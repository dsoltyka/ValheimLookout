# Building

The project imports shared MSBuild props from the [ValheimModBuild](https://github.com/dsoltyka/ValheimModBuild) repo, which must be cloned next to this one:

```
<parent>/
  ValheimModBuild/
  ValheimLookout/
```

Then:

```
dotnet build                                          # build + copy DLL into your Thunderstore profile
dotnet build -c Release -p:ThunderstorePack=true      # also write thunderstore/manifest.json and the upload zip
```

Paths to the game and the BepInEx profile are configured once in `ValheimModBuild/Valheim.Local.props` (see that repo's README).

## How it works

**Rules** (`ResourceRules.Collect`) decide what an object yields, and are the single source of truth for both the catalog and live tagging:

* `Pickable`: its item.
* `MineRock` / `MineRock5`: the drop table. These are deposits that have already been hit.
* `Destructible` of the Default type: if it fractures into a mine rock (`m_spawnWhenDestroyed`), that prefab's drop table; otherwise its `DropOnDestroyed` table. Tree-type destructibles (saplings, bushes) are ignored, which is what keeps resin and cones off the map.
* Stone and wood are treated as bulk and never listed.

**Catalog** (`Catalog.EnsureScanned`) runs the rules over every prefab in `ZNetScene.m_prefabs` once per world load (and again if the list grows), producing one `CatalogEntry` per item with its localized name and icon. This is what the panel lists.

**Tagging** (`PoiPatches`) postfixes `Location.Awake`, `Destructible.Awake`, `MineRock.Start`, `MineRock5.Awake` and `Pickable.Awake` and attaches a `PoiMarker` holding the object's catalog entries. Locations with `m_hasInterior` are dungeons; interiors themselves sit 5000 m up and are skipped.

**Radar** (`PoiRadar`) runs on a timer. For every marker it checks category/item enablement, active state (picked bushes), distance, explored-only and buried rules, then adds or removes a `Minimap.PinType.None` pin with a custom sprite and `save: false`. That is the same mechanism the game uses for its own location icons, and it survives the map clearing its pins on load because the radar re-adds anything no longer present. It also counts how many active markers yield each item, which the panel shows.

**Panel** (`RadarUi`) is built from Jötunn's `GUIManager` wood-panel helpers under `CustomGUIFront`, toggled by a `KeyboardShortcut` or a button added to `Minimap.m_largeRoot`. Item toggles write the `Items` config entry, which fires the normal `SettingChanged` path.

## Releasing

1. Bump `<Version>` in `Lookout.csproj` and add a `CHANGELOG.md` entry.
2. Run the pack command above. It regenerates `thunderstore/manifest.json` from the csproj and writes `thunderstore/Lookout-<version>.zip`.
3. Commit, tag `v<version>`, push, and upload the zip through the normal Thunderstore upload form under the `dsoltyka` team.

Thunderstore renders `README.md` from inside the zip as the mod page, so keep that file user-facing. Anything between the `github-only` markers is stripped from the packaged copy, and images must use absolute URLs.
