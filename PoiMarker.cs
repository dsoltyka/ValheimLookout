using System;
using System.Collections.Generic;
using UnityEngine;

namespace POIRadar
{
    internal enum PoiCategory
    {
        Dungeon,
        OreDeposit,
        Pickable,
        Location,
    }

    /// <summary>
    /// Attached to a world object that should show on the minimap. Registers itself while the object is alive so the
    /// radar never has to search the scene; when the game unloads or destroys the object the pin goes with it.
    /// </summary>
    internal sealed class PoiMarker : MonoBehaviour
    {
        private static readonly HashSet<PoiMarker> s_all = new HashSet<PoiMarker>();

        public static IReadOnlyCollection<PoiMarker> All => s_all;

        public PoiCategory Category { get; private set; }
        public string Label { get; private set; }
        public Sprite Icon { get; private set; }

        /// <summary>The live minimap pin, or null while hidden.</summary>
        public Minimap.PinData Pin;

        private Func<bool> _isActive;

        /// <summary>False while the object is temporarily uninteresting, e.g. a picked berry bush waiting to respawn.</summary>
        public bool IsActive
        {
            get
            {
                try
                {
                    return _isActive == null || _isActive();
                }
                catch
                {
                    return false;
                }
            }
        }

        public static PoiMarker Attach(GameObject target, PoiCategory category, string label, Sprite icon, Func<bool> isActive = null)
        {
            var marker = target.GetComponent<PoiMarker>();
            if (marker == null)
            {
                marker = target.AddComponent<PoiMarker>();
            }
            marker.Category = category;
            marker.Label = label ?? string.Empty;
            marker.Icon = icon;
            marker._isActive = isActive;
            return marker;
        }

        private void OnEnable()
        {
            s_all.Add(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            PoiRadar.RemovePin(this);
        }
    }
}
