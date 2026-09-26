using System;
using System.Collections.Generic;
using UnityEngine;

namespace POIRadar
{
    internal enum PoiCategory
    {
        Dungeon,
        Location,
        Resource,
    }

    /// <summary>
    /// Attached to a world object that can show on the minimap. Registers itself while the object is alive so the
    /// radar never has to search the scene; when the game unloads or destroys the object the pin goes with it.
    /// </summary>
    internal sealed class PoiMarker : MonoBehaviour
    {
        private static readonly HashSet<PoiMarker> s_all = new HashSet<PoiMarker>();

        public static IReadOnlyCollection<PoiMarker> All => s_all;

        public PoiCategory Category { get; private set; }

        /// <summary>Hover name of the object when it has one (e.g. "Copper deposit"), else null.</summary>
        public string HoverName { get; private set; }

        /// <summary>Fixed icon for dungeons/locations; resources use the icon of the item they are pinned for.</summary>
        public Sprite FixedIcon { get; private set; }

        /// <summary>For resources: everything this object can yield. The player opts in per item.</summary>
        public CatalogEntry[] Items { get; private set; } = Array.Empty<CatalogEntry>();

        /// <summary>The live minimap pin, or null while hidden.</summary>
        public Minimap.PinData Pin;

        private Func<bool> _isActive;
        private float? _topY;

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

        /// <summary>
        /// True when no part of the object's colliders reaches above the terrain, i.e. it is fully underground
        /// (an untouched silver vein). The object's own height is cached; the ground is re-queried because it can be dug.
        /// </summary>
        public bool IsBuried
        {
            get
            {
                var zones = ZoneSystem.instance;
                if (zones == null)
                {
                    return false;
                }

                if (!_topY.HasValue)
                {
                    float top = transform.position.y;
                    foreach (var collider in GetComponentsInChildren<Collider>())
                    {
                        if (collider != null && collider.enabled && !collider.isTrigger)
                        {
                            top = Mathf.Max(top, collider.bounds.max.y);
                        }
                    }
                    _topY = top;
                }

                return zones.GetGroundHeight(transform.position, out float ground) && _topY.Value < ground - 0.25f;
            }
        }

        public static PoiMarker AttachFixed(GameObject target, PoiCategory category, string label, Sprite icon)
        {
            var marker = Get(target);
            marker.Category = category;
            marker.HoverName = label;
            marker.FixedIcon = icon;
            marker.Items = Array.Empty<CatalogEntry>();
            marker._isActive = null;
            return marker;
        }

        public static PoiMarker AttachResource(GameObject target, string hoverName, CatalogEntry[] items, Func<bool> isActive)
        {
            var marker = Get(target);
            marker.Category = PoiCategory.Resource;
            marker.HoverName = hoverName;
            marker.FixedIcon = null;
            marker.Items = items ?? Array.Empty<CatalogEntry>();
            marker._isActive = isActive;
            return marker;
        }

        /// <summary>First item the player has opted into, or null when none of this object's yields are enabled.</summary>
        public CatalogEntry FirstEnabledItem(Settings settings)
        {
            foreach (var item in Items)
            {
                if (settings.IsItemEnabled(item.Key))
                {
                    return item;
                }
            }
            return null;
        }

        private static PoiMarker Get(GameObject target)
        {
            var marker = target.GetComponent<PoiMarker>();
            return marker != null ? marker : target.AddComponent<PoiMarker>();
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
