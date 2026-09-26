using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace POIRadar
{
    /// <summary>
    /// Tags interesting objects with a <see cref="PoiMarker"/> the moment the game creates them locally.
    /// Every patch is a postfix on the object's own initialisation, so there is no scene scanning at all.
    /// </summary>
    [HarmonyPatch]
    internal static class PoiPatches
    {
        private const string StoneItem = "$item_stone";
        private const float OverworldMaxY = 3000f; // dungeon interiors are generated 5000 m up

        private static Settings S => Plugin.Settings;

        // ---- Locations (dungeon entrances + everything else placed by the world generator) ----

        [HarmonyPatch(typeof(Location), "Awake")]
        [HarmonyPostfix]
        private static void Location_Awake(Location __instance)
        {
            Guard(() => TagLocation(__instance));
        }

        private static void TagLocation(Location location)
        {
            if (location.transform.position.y > OverworldMaxY)
            {
                return;
            }

            string prefab = CleanName(location.gameObject.name);
            if (location.m_hasInterior)
            {
                PoiMarker.Attach(location.gameObject, PoiCategory.Dungeon, LocationNames.Get(prefab), Icons.Dungeon);
            }
            else
            {
                PoiMarker.Attach(location.gameObject, PoiCategory.Location, LocationNames.Get(prefab), Icons.Location);
            }
        }

        // ---- Ore deposits ----

        [HarmonyPatch(typeof(MineRock), "Start")]
        [HarmonyPostfix]
        private static void MineRock_Start(MineRock __instance)
        {
            Guard(() => TagDeposit(__instance.gameObject, __instance.m_dropItems, __instance.m_name));
        }

        [HarmonyPatch(typeof(MineRock5), "Awake")]
        [HarmonyPostfix]
        private static void MineRock5_Awake(MineRock5 __instance)
        {
            Guard(() => TagDeposit(__instance.gameObject, __instance.m_dropItems, __instance.m_name));
        }

        private static void TagDeposit(GameObject go, DropTable drops, string hoverName)
        {
            if (!HasValidZdo(go) || drops == null || drops.m_drops == null)
            {
                return;
            }

            // Plain boulders only drop stone; deposits drop an ore (possibly alongside stone).
            ItemDrop.ItemData ore = null;
            foreach (var drop in drops.m_drops)
            {
                var item = drop.m_item != null ? drop.m_item.GetComponent<ItemDrop>() : null;
                var data = item != null ? item.m_itemData : null;
                if (data?.m_shared == null || data.m_shared.m_name == StoneItem)
                {
                    continue;
                }
                ore = data;
                break;
            }
            if (ore == null)
            {
                return;
            }

            string label = !string.IsNullOrEmpty(hoverName) ? Localize(hoverName) : Localize(ore.m_shared.m_name);
            PoiMarker.Attach(go, PoiCategory.OreDeposit, label, SafeIcon(ore) ?? Icons.Generic);
        }

        // ---- Pickables ----

        [HarmonyPatch(typeof(Pickable), "Awake")]
        [HarmonyPostfix]
        private static void Pickable_Awake(Pickable __instance)
        {
            Guard(() => TagPickable(__instance));
        }

        private static void TagPickable(Pickable pickable)
        {
            var go = pickable.gameObject;
            if (!HasValidZdo(go))
            {
                return;
            }

            var item = pickable.m_itemPrefab != null ? pickable.m_itemPrefab.GetComponent<ItemDrop>() : null;
            var data = item != null ? item.m_itemData : null;
            if (data?.m_shared == null)
            {
                return;
            }

            if (IsExcludedPickable(CleanName(go.name), data.m_shared.m_name))
            {
                return;
            }

            string label = !string.IsNullOrEmpty(pickable.m_overrideName) ? Localize(pickable.m_overrideName) : Localize(data.m_shared.m_name);
            PoiMarker.Attach(go, PoiCategory.Pickable, label, SafeIcon(data) ?? Icons.Generic, () => !pickable.GetPicked());
        }

        private static bool IsExcludedPickable(string prefabName, string itemName)
        {
            string list = S.PickableExclude.Value;
            if (string.IsNullOrWhiteSpace(list))
            {
                return false;
            }

            foreach (var raw in list.Split(','))
            {
                string word = raw.Trim();
                if (word.Length == 0)
                {
                    continue;
                }
                if (prefabName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    itemName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- helpers ----

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"POI tagging failed: {e}");
            }
        }

        /// <summary>Objects without a networked ZDO are templates or ghosts, never real world objects.</summary>
        private static bool HasValidZdo(GameObject go)
        {
            var view = go.GetComponent<ZNetView>();
            return view != null && view.IsValid();
        }

        private static Sprite SafeIcon(ItemDrop.ItemData data)
        {
            try
            {
                return data.GetIcon();
            }
            catch
            {
                return null;
            }
        }

        private static string Localize(string token)
        {
            var localization = Localization.instance;
            if (localization == null || string.IsNullOrEmpty(token))
            {
                return token ?? string.Empty;
            }
            string text = localization.Localize(token);
            return string.IsNullOrEmpty(text) ? token : text;
        }

        internal static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return clone >= 0 ? name.Substring(0, clone) : name;
        }
    }

    /// <summary>Human-readable names for location prefabs, with a best-effort fallback for anything unknown.</summary>
    internal static class LocationNames
    {
        private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Dungeons
            { "Crypt2", "Burial Chambers" },
            { "Crypt3", "Burial Chambers" },
            { "Crypt4", "Burial Chambers" },
            { "TrollCave02", "Troll Cave" },
            { "SunkenCrypt4", "Sunken Crypt" },
            { "MountainCave02", "Frost Cave" },
            { "Mistlands_DvergrBossEntrance1", "Infested Mine" },
            { "Hildir_crypt", "Smouldering Tomb" },
            { "Hildir_cave", "Howling Cavern" },
            { "Hildir_plainsfortress", "Sealed Tower" },
            // Traders, bosses, notable surface spots
            { "Vendor_BlackForest", "Haldor" },
            { "Hildir_camp", "Hildir" },
            { "BogWitch_Camp", "Bog Witch" },
            { "Eikthyrnir", "Eikthyr Altar" },
            { "GDKing", "The Elder Altar" },
            { "Bonemass", "Bonemass Altar" },
            { "Dragonqueen", "Moder Altar" },
            { "GoblinKing", "Yagluth Altar" },
            { "Mistlands_DvergrBossEntrance2", "The Queen's Lair" },
            { "GoblinCamp2", "Fuling Village" },
            { "TarPit1", "Tar Pit" },
            { "TarPit2", "Tar Pit" },
            { "TarPit3", "Tar Pit" },
        };

        private static readonly Regex TrailingDigits = new Regex(@"\d+$", RegexOptions.Compiled);
        private static readonly Regex CamelBoundary = new Regex(@"(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        public static string Get(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return string.Empty;
            }
            if (Known.TryGetValue(prefabName, out var known))
            {
                return known;
            }

            string name = TrailingDigits.Replace(prefabName, string.Empty).Replace('_', ' ').Trim();
            name = CamelBoundary.Replace(name, " ");
            var sb = new StringBuilder(name.Length);
            bool newWord = true;
            foreach (char c in name)
            {
                if (c == ' ')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    newWord = true;
                    continue;
                }
                sb.Append(newWord ? char.ToUpperInvariant(c) : c);
                newWord = false;
            }
            return sb.ToString().Trim();
        }
    }
}
