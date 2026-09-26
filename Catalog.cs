using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lookout
{
    [Flags]
    internal enum PoiSource
    {
        None = 0,
        Deposit = 1,    // mine rocks: ore veins, obsidian, black marble...
        Pickable = 2,   // berries, mushrooms, plants
        Breakable = 4,  // single-hit objects with a drop table: tin, guck sacks, bone piles, nests...
    }

    /// <summary>One item that some nearby object can yield. Players opt in per item.</summary>
    internal sealed class CatalogEntry
    {
        public string Key;          // shared item name token, e.g. "$item_copperore"
        public string Name;         // localized
        public Sprite Icon;
        public PoiSource Sources;
        public int Nearby;          // live count of active markers yielding this item
    }

    /// <summary>
    /// The single set of rules for "what does this object yield". Used both to build the catalog from every prefab
    /// the game knows and to tag live objects, so the list in the UI always matches what can actually be pinned.
    /// </summary>
    internal static class ResourceRules
    {
        /// <summary>Things every rock, stump and log drops. Never worth a pin and never offered in the list.</summary>
        private static readonly HashSet<string> Bulk = new HashSet<string>(StringComparer.Ordinal)
        {
            "$item_stone", "$item_wood", "$item_finewood", "$item_roundlog", "$item_elderbark", "$item_blackwood",
        };

        public struct Yield
        {
            public ItemDrop.ItemData Item;
            public PoiSource Source;
        }

        /// <summary>Collects everything a prefab or live object can yield. Returns a hover name when the object has one.</summary>
        public static string Collect(GameObject go, List<Yield> into)
        {
            string hoverName = null;

            var pickable = go.GetComponent<Pickable>();
            if (pickable != null)
            {
                var item = ItemOf(pickable.m_itemPrefab);
                if (item != null && !Bulk.Contains(item.m_shared.m_name))
                {
                    into.Add(new Yield { Item = item, Source = PoiSource.Pickable });
                }
                if (!string.IsNullOrEmpty(pickable.m_overrideName))
                {
                    hoverName = pickable.m_overrideName;
                }
                return hoverName;
            }

            var rock5 = go.GetComponent<MineRock5>();
            if (rock5 != null)
            {
                AddDrops(rock5.m_dropItems, PoiSource.Deposit, into);
                return rock5.m_name;
            }

            var rock = go.GetComponent<MineRock>();
            if (rock != null)
            {
                AddDrops(rock.m_dropItems, PoiSource.Deposit, into);
                return rock.m_name;
            }

            var destructible = go.GetComponent<Destructible>();
            if (destructible != null && destructible.m_destructibleType == DestructibleType.Default)
            {
                // Intact vein: it fractures into a mine rock; that prefab knows the drops.
                var frac = destructible.m_spawnWhenDestroyed;
                if (frac != null)
                {
                    var fracRock5 = frac.GetComponent<MineRock5>();
                    if (fracRock5 != null)
                    {
                        AddDrops(fracRock5.m_dropItems, PoiSource.Deposit, into);
                        return fracRock5.m_name;
                    }
                    var fracRock = frac.GetComponent<MineRock>();
                    if (fracRock != null)
                    {
                        AddDrops(fracRock.m_dropItems, PoiSource.Deposit, into);
                        return fracRock.m_name;
                    }
                }

                var dropper = go.GetComponent<DropOnDestroyed>();
                if (dropper != null)
                {
                    AddDrops(dropper.m_dropWhenDestroyed, PoiSource.Breakable, into);
                }
            }

            return hoverName;
        }

        private static void AddDrops(DropTable table, PoiSource source, List<Yield> into)
        {
            if (table?.m_drops == null)
            {
                return;
            }
            foreach (var drop in table.m_drops)
            {
                var item = ItemOf(drop.m_item);
                if (item != null && !Bulk.Contains(item.m_shared.m_name))
                {
                    into.Add(new Yield { Item = item, Source = source });
                }
            }
        }

        private static ItemDrop.ItemData ItemOf(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }
            var drop = prefab.GetComponent<ItemDrop>();
            var data = drop != null ? drop.m_itemData : null;
            return data?.m_shared != null && !string.IsNullOrEmpty(data.m_shared.m_name) ? data : null;
        }
    }

    /// <summary>Every item the world can yield, discovered by scanning the game's prefab list once per world load.</summary>
    internal static class Catalog
    {
        private static readonly Dictionary<string, CatalogEntry> s_entries = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        private static readonly List<ResourceRules.Yield> s_scratch = new List<ResourceRules.Yield>();
        private static int s_scannedPrefabCount = -1;

        public static IEnumerable<CatalogEntry> Entries => s_entries.Values;

        /// <summary>Raised when a new item appears in the catalog.</summary>
        public static event Action Changed;

        public static bool TryGet(string key, out CatalogEntry entry) => s_entries.TryGetValue(key, out entry);

        /// <summary>Scans ZNetScene's prefab list; cheap to call often, it only re-scans when the list grows (modded prefabs).</summary>
        public static void EnsureScanned()
        {
            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null || scene.m_prefabs.Count == s_scannedPrefabCount)
            {
                return;
            }
            s_scannedPrefabCount = scene.m_prefabs.Count;

            int before = s_entries.Count;
            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }
                try
                {
                    s_scratch.Clear();
                    ResourceRules.Collect(prefab, s_scratch);
                    foreach (var yield in s_scratch)
                    {
                        Register(yield.Item, yield.Source, raise: false);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogDebug($"Catalog: skipped {prefab.name}: {e.Message}");
                }
            }

            Plugin.Log.LogDebug($"Catalog: {s_entries.Count} pinnable items from {s_scannedPrefabCount} prefabs");
            if (s_entries.Count != before)
            {
                Changed?.Invoke();
            }
        }

        public static CatalogEntry Register(ItemDrop.ItemData item, PoiSource source, bool raise = true)
        {
            string key = item.m_shared.m_name;
            if (!s_entries.TryGetValue(key, out var entry))
            {
                entry = new CatalogEntry
                {
                    Key = key,
                    Name = Localize(key),
                    Icon = SafeIcon(item),
                    Sources = source,
                };
                s_entries.Add(key, entry);
                if (raise)
                {
                    Changed?.Invoke();
                }
            }
            else
            {
                entry.Sources |= source;
                if (entry.Icon == null)
                {
                    entry.Icon = SafeIcon(item);
                }
            }
            return entry;
        }

        public static void ResetNearbyCounts()
        {
            foreach (var entry in s_entries.Values)
            {
                entry.Nearby = 0;
            }
        }

        private static Sprite SafeIcon(ItemDrop.ItemData item)
        {
            try
            {
                return item.GetIcon();
            }
            catch
            {
                return null;
            }
        }

        internal static string Localize(string token)
        {
            var localization = Localization.instance;
            if (localization == null || string.IsNullOrEmpty(token))
            {
                return token ?? string.Empty;
            }
            string text = localization.Localize(token);
            return string.IsNullOrEmpty(text) ? token : text;
        }
    }
}
