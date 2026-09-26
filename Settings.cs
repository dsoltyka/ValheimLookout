using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace POIRadar
{
    /// <summary>User-facing configuration. Everything applies immediately; pins are rebuilt on change.</summary>
    internal sealed class Settings
    {
        /// <summary>Nothing is pinned until the player opts in from the panel.</summary>
        public const string DefaultItems = "";

        public ConfigEntry<bool> ShowDungeons { get; }
        public ConfigEntry<bool> ShowOtherLocations { get; }
        public ConfigEntry<string> EnabledItems { get; }
        public ConfigEntry<bool> OnlyDiscoveredItems { get; }

        public ConfigEntry<float> MaxDistance { get; }
        public ConfigEntry<bool> ShowBuried { get; }
        public ConfigEntry<bool> OnlyExploredAreas { get; }
        public ConfigEntry<bool> ShowLabels { get; }
        public ConfigEntry<bool> LargeIcons { get; }
        public ConfigEntry<float> RefreshSeconds { get; }

        public ConfigEntry<KeyboardShortcut> ToggleKey { get; }
        public ConfigEntry<bool> MapButton { get; }

        /// <summary>Raised whenever any setting changes.</summary>
        public event Action Changed;

        private readonly HashSet<string> _enabled = new HashSet<string>(StringComparer.Ordinal);
        private string _parsedFrom;

        public Settings(ConfigFile config)
        {
            const string categories = "1 - What to pin";
            const string display = "2 - Display";
            const string ui = "3 - Interface";

            ShowDungeons = config.Bind(categories, "Dungeons", true,
                "Pin dungeon entrances: burial chambers, troll caves, sunken crypts, frost caves, infested mines and any other location with an interior.");
            ShowOtherLocations = config.Bind(categories, "OtherLocations", false,
                "Pin surface locations without an interior: ruins, abandoned camps, tar pits, runestones, altars, villages.");
            EnabledItems = config.Bind(categories, "Items", DefaultItems,
                "Comma-separated item tokens to pin, e.g. $item_copperore,$item_tinore. Any deposit, breakable or pickable that yields one of these gets a pin with that item's icon. " +
                "Empty by default: nothing is pinned until you opt in. Easiest to edit from the in-game panel (see Interface), which lists every item the world can yield.");

            OnlyDiscoveredItems = config.Bind(categories, "OnlyDiscoveredItems", true,
                "Only list and pin items your character has already discovered (picked up at least once). " +
                "Keeps the panel from spoiling items you have not found yet. Turn off to see everything the world can yield.");

            MaxDistance = config.Bind(display, "MaxDistance", 0f,
                new ConfigDescription("Only pin objects within this many meters of the player. 0 means everything the game has loaded around you (roughly 200 m).",
                    new AcceptableValueRange<float>(0f, 1000f)));
            ShowBuried = config.Bind(display, "ShowBuried", false,
                "Also pin deposits that are completely below the terrain surface, such as untouched silver veins. " +
                "Off by default so the radar does not give away hidden ore; partially exposed veins are always shown.");
            OnlyExploredAreas = config.Bind(display, "OnlyExploredAreas", false,
                "Only pin objects that sit on a part of the map you have already explored (including map data shared with you). " +
                "Off by default so pins always appear; turn on if you do not want the radar to hint at unexplored terrain.");
            ShowLabels = config.Bind(display, "ShowLabels", true,
                "Draw the name next to each pin (for example 'Copper deposit' or 'Burial Chambers').");
            LargeIcons = config.Bind(display, "LargeIcons", false,
                "Draw the pins at double size.");
            RefreshSeconds = config.Bind(display, "RefreshSeconds", 0.5f,
                new ConfigDescription("How often pins are re-evaluated (distance, picked state, new objects).",
                    new AcceptableValueRange<float>(0.1f, 5f)));

            ToggleKey = config.Bind(ui, "ToggleKey", new KeyboardShortcut(KeyCode.F7),
                "Key that opens and closes the POI Radar panel.");
            MapButton = config.Bind(ui, "MapButton", true,
                "Show a 'POI Radar' button in the corner of the large map that opens the panel.");

            config.SettingChanged += (_, __) => Changed?.Invoke();
        }

        public bool IsItemEnabled(string key)
        {
            EnsureParsed();
            return _enabled.Contains(key);
        }

        public void SetItemEnabled(string key, bool enabled)
        {
            EnsureParsed();
            bool changed = enabled ? _enabled.Add(key) : _enabled.Remove(key);
            if (!changed)
            {
                return;
            }

            var list = new List<string>(_enabled);
            list.Sort(StringComparer.Ordinal);
            string joined = string.Join(",", list);
            _parsedFrom = joined;              // keep the cache valid; the config write below fires Changed
            EnabledItems.Value = joined;
        }

        private void EnsureParsed()
        {
            string raw = EnabledItems.Value ?? string.Empty;
            if (ReferenceEquals(raw, _parsedFrom) || raw == _parsedFrom)
            {
                return;
            }

            _enabled.Clear();
            foreach (var part in raw.Split(','))
            {
                string token = part.Trim();
                if (token.Length > 0)
                {
                    _enabled.Add(token);
                }
            }
            _parsedFrom = raw;
        }
    }
}
