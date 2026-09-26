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

        private readonly HashSet<Minimap.PinData> _livePins = new HashSet<Minimap.PinData>();
        private float _nextRefresh;
        private bool _rebuildRequested;

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
            bool labels = S.ShowLabels.Value;
            bool large = S.LargeIcons.Value;

            foreach (var marker in PoiMarker.All)
            {
                bool wanted = IsEnabled(marker.Category)
                              && marker.IsActive
                              && (maxDistance <= 0f || Vector3.Distance(origin, marker.transform.position) <= maxDistance);

                if (!wanted)
                {
                    RemovePin(marker);
                    continue;
                }

                if (marker.Pin != null && _livePins.Contains(marker.Pin))
                {
                    continue;
                }

                var pin = map.AddPin(marker.transform.position, Minimap.PinType.None, labels ? marker.Label : string.Empty, save: false, isChecked: false);
                pin.m_icon = marker.Icon != null ? marker.Icon : Icons.Generic;
                pin.m_doubleSize = large;
                marker.Pin = pin;
            }
        }

        private static bool IsEnabled(PoiCategory category)
        {
            switch (category)
            {
                case PoiCategory.Dungeon: return S.ShowDungeons.Value;
                case PoiCategory.OreDeposit: return S.ShowOreDeposits.Value;
                case PoiCategory.Pickable: return S.ShowPickables.Value;
                case PoiCategory.Location: return S.ShowOtherLocations.Value;
                default: return false;
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
