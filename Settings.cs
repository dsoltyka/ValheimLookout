using System;
using BepInEx.Configuration;

namespace POIRadar
{
    /// <summary>User-facing configuration. Everything applies immediately; pins are rebuilt on change.</summary>
    internal sealed class Settings
    {
        public ConfigEntry<bool> ShowDungeons { get; }
        public ConfigEntry<bool> ShowOreDeposits { get; }
        public ConfigEntry<bool> ShowPickables { get; }
        public ConfigEntry<bool> ShowOtherLocations { get; }

        public ConfigEntry<float> MaxDistance { get; }
        public ConfigEntry<bool> OnlyExploredAreas { get; }
        public ConfigEntry<bool> ShowLabels { get; }
        public ConfigEntry<bool> LargeIcons { get; }
        public ConfigEntry<float> RefreshSeconds { get; }
        public ConfigEntry<string> PickableExclude { get; }

        /// <summary>Raised whenever any setting changes.</summary>
        public event Action Changed;

        public Settings(ConfigFile config)
        {
            const string categories = "1 - Categories";
            const string display = "2 - Display";

            ShowDungeons = config.Bind(categories, "Dungeons", true,
                "Pin dungeon entrances: burial chambers, troll caves, sunken crypts, frost caves, infested mines and any other location with an interior.");
            ShowOreDeposits = config.Bind(categories, "OreDeposits", true,
                "Pin mineable deposits that drop something other than plain stone: copper, tin, silver, obsidian, black marble, flametal and so on. The pin uses the ore's own icon.");
            ShowPickables = config.Bind(categories, "Pickables", false,
                "Pin pickable resources: berries, mushrooms, thistle, dandelions, barley, flax, etc. Can be very busy.");
            ShowOtherLocations = config.Bind(categories, "OtherLocations", false,
                "Pin surface locations without an interior: ruins, abandoned camps, tar pits, runestones, altars, villages.");

            MaxDistance = config.Bind(display, "MaxDistance", 0f,
                new ConfigDescription("Only pin objects within this many meters of the player. 0 means everything the game has loaded around you (roughly 200 m).",
                    new AcceptableValueRange<float>(0f, 1000f)));
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
            PickableExclude = config.Bind(display, "PickableExclude", "Branch,Stone,Flint",
                "Comma-separated list of words. Pickables whose prefab or item name contains any of them are never pinned.");

            config.SettingChanged += (_, __) => Changed?.Invoke();
        }
    }
}
