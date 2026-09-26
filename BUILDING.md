# Building

The project imports shared MSBuild props from the [ValheimModBuild](https://github.com/dsoltyka/ValheimModBuild) repo, which must be cloned next to this one:

```
<parent>/
  ValheimModBuild/
  ValheimPOIRadar/
```

Then:

```
dotnet build                                          # build + copy DLL into your Thunderstore profile
dotnet build -c Release -p:ThunderstorePack=true      # also write thunderstore/manifest.json and the upload zip
```

Paths to the game and the BepInEx profile are configured once in `ValheimModBuild/Valheim.Local.props` (see that repo's README).

## How it works

There is no scene scanning. Harmony postfixes on `Location.Awake`, `Destructible.Awake`, `MineRock.Start`, `MineRock5.Awake` and `Pickable.Awake` attach a `PoiMarker` component to interesting objects as the game creates them locally:

* A `Location` with `m_hasInterior` is a dungeon entrance; without it, a surface location. Interiors themselves sit 5000 m up and are skipped.
* An untouched deposit is a `Destructible`: big veins fracture into a `MineRock5` (`m_spawnWhenDestroyed`), so that prefab's drop table is inspected; small ones like tin drop through `DropOnDestroyed`. Already-hit deposits are `MineRock` / `MineRock5` objects. In every case a drop table containing anything other than stone or wood marks a resource, and the first such item supplies the label and icon.
* A `Pickable` supplies its item's icon and reports picked state so pins hide while it regrows.

`PoiMarker` registers itself in a static set on enable and leaves it on disable/destroy, removing its pin. `PoiRadar` runs on a timer and, for every marker, adds or removes a `Minimap.PinType.None` pin with a custom sprite and `save: false`. That is the same mechanism the game uses for its own location icons, and it survives the map clearing its pins on load because the radar re-adds anything no longer present.

## Releasing

1. Bump `<Version>` in `POIRadar.csproj` and add a `CHANGELOG.md` entry.
2. Run the pack command above. It regenerates `thunderstore/manifest.json` from the csproj and writes `thunderstore/POIRadar-<version>.zip`.
3. Commit, tag `v<version>`, push, and upload the zip through the normal Thunderstore upload form under the `dsoltyka` team.

Thunderstore renders `README.md` from inside the zip as the mod page, so keep that file user-facing. Anything between the `github-only` markers is stripped from the packaged copy, and images must use absolute URLs.
