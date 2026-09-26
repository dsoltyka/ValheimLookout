using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace POIRadar
{
    /// <summary>
    /// Keeps the minimap in sync with the registered <see cref="PoiMarker"/>s: adds an unsaved pin for every marker
    /// that is enabled, active and in range, and removes it again when any of that stops being true.
    /// Pins use the same mechanism the game uses for its own location icons (a None-type pin with a custom sprite),
    /// so they draw on both the small minimap and the large map and are never written to the save.
    /// </summary>
    internal sealed class PoiRadar : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        // Minimap.IsExplored(Vector3 worldPos) is private; it also counts map data shared by other players.
        private static readonly System.Func<Minimap, Vector3, bool> IsExplored =
            AccessTools.MethodDelegate<System.Func<Minimap, Vector3, bool>>(
                AccessTools.Method(typeof(Minimap), "IsExplored", new[] { typeof(Vector3) }));

        private readonly HashSet<Minimap.PinData> _livePins = new HashSet<Minimap.PinData>();
        private float _nextRefresh;
        private bool _rebuildRequested;
        private RadarUi _ui;

        private static Settings S => Plugin.Settings;

        private void OnEnable()
        {
            S.Changed += RequestRebuild;
        }

        private void OnDisable()
        {
            S.Changed -= RequestRebuild;
            RemoveAllPins();
        }

        private void Update()
        {
            if (_ui == null)
            {
                _ui = gameObject.AddComponent<RadarUi>();
            }

            if (S.ToggleKey.Value.IsDown() && Player.m_localPlayer != null)
            {
                _ui.Toggle();
            }

            if (Time.time < _nextRefresh)
            {
                return;
            }
            _nextRefresh = Time.time + S.RefreshSeconds.Value;
            Refresh();
        }

        private void Refresh()
        {
            var map = Minimap.instance;
            var player = Player.m_localPlayer;
            if (map == null || player == null)
            {
                // Out of the world: the minimap (and every pin with it) is gone. Forget the stale handles.
                foreach (var marker in PoiMarker.All)
                {
                    marker.Pin = null;
                }
                return;
            }

            Catalog.EnsureScanned();
            _ui.EnsureMapButton(map);

            if (_rebuildRequested)
            {
                _rebuildRequested = false;
                RemoveAllPins();
            }

            // The game clears all pins when map data loads; anything we hold that is no longer listed must be re-added.
            _livePins.Clear();
            foreach (var pin in PinsRef(map))
            {
                _livePins.Add(pin);
            }

            Vector3 origin = player.transform.position;
            float maxDistance = S.MaxDistance.Value;
            bool exploredOnly = S.OnlyExploredAreas.Value && IsExplored != null;
            bool hideBuried = !S.ShowBuried.Value;
            bool labels = S.ShowLabels.Value;
            bool large = S.LargeIcons.Value;
            bool discoveredOnly = S.OnlyDiscoveredItems.Value;
            System.Func<CatalogEntry, bool> allowed = e => S.IsItemEnabled(e.Key) && (!discoveredOnly || player.IsKnownMaterial(e.Key));

            Catalog.ResetNearbyCounts();

            foreach (var marker in PoiMarker.All)
            {
                bool active = marker.IsActive;
                if (active)
                {
                    foreach (var item in marker.Items)
                    {
                        item.Nearby++;
                    }
                }

                CatalogEntry item0 = null;
                bool enabled;
                switch (marker.Category)
                {
                    case PoiCategory.Dungeon: enabled = S.ShowDungeons.Value; break;
                    case PoiCategory.Location: enabled = S.ShowOtherLocations.Value; break;
                    default:
                        item0 = marker.FirstItem(allowed);
                        enabled = item0 != null;
                        break;
                }

                Vector3 position = marker.transform.position;
                bool wanted = enabled
                              && active
                              && (maxDistance <= 0f || Vector3.Distance(origin, position) <= maxDistance)
                              && (!exploredOnly || IsExplored(map, position))
                              && !(hideBuried && marker.Category == PoiCategory.Resource && marker.IsBuried);

                if (!wanted)
                {
                    RemovePin(marker);
                    continue;
                }

                if (marker.Pin != null && _livePins.Contains(marker.Pin))
                {
                    continue;
                }

                string label = marker.HoverName ?? item0?.Name ?? string.Empty;
                Sprite icon = marker.FixedIcon ?? item0?.Icon ?? Icons.Generic;

                var pin = map.AddPin(position, Minimap.PinType.None, labels ? label : string.Empty, save: false, isChecked: false);
                pin.m_icon = icon;
                pin.m_doubleSize = large;
                marker.Pin = pin;
            }
        }

        public static void RemovePin(PoiMarker marker)
        {
            if (marker.Pin == null)
            {
                return;
            }

            var map = Minimap.instance;
            if (map != null)
            {
                map.RemovePin(marker.Pin);
            }
            marker.Pin = null;
        }

        private static void RemoveAllPins()
        {
            foreach (var marker in PoiMarker.All)
            {
                RemovePin(marker);
            }
        }

        private void RequestRebuild()
        {
            _rebuildRequested = true;
            _nextRefresh = 0f;
        }
    }
}
