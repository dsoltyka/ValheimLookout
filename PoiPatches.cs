using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace Lookout
{
    /// <summary>
    /// Tags interesting objects with a <see cref="PoiMarker"/> the moment the game creates them locally.
    /// Every patch is a postfix on the object's own initialisation, so there is no scene scanning at all.
    /// What a resource object yields is decided by <see cref="ResourceRules"/>, the same rules that build the catalog.
    /// </summary>
    [HarmonyPatch]
    internal static class PoiPatches
    {
        private const float OverworldMaxY = 3000f; // dungeon interiors are generated 5000 m up

        private static readonly List<ResourceRules.Yield> s_scratch = new List<ResourceRules.Yield>();

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
                PoiMarker.AttachFixed(location.gameObject, PoiCategory.Dungeon, LocationNames.Get(prefab), Icons.Dungeon);
            }
            else
            {
                PoiMarker.AttachFixed(location.gameObject, PoiCategory.Location, LocationNames.Get(prefab), Icons.Location);
            }
        }

        // ---- Resources: deposits (intact or fractured), single-hit destructibles, pickables ----

        [HarmonyPatch(typeof(Destructible), "Awake")]
        [HarmonyPostfix]
        private static void Destructible_Awake(Destructible __instance)
        {
            Guard(() => TagResource(__instance.gameObject, null));
        }

        [HarmonyPatch(typeof(MineRock), "Start")]
        [HarmonyPostfix]
        private static void MineRock_Start(MineRock __instance)
        {
            Guard(() => TagResource(__instance.gameObject, null));
        }

        [HarmonyPatch(typeof(MineRock5), "Awake")]
        [HarmonyPostfix]
        private static void MineRock5_Awake(MineRock5 __instance)
        {
            Guard(() => TagResource(__instance.gameObject, null));
        }

        [HarmonyPatch(typeof(Pickable), "Awake")]
        [HarmonyPostfix]
        private static void Pickable_Awake(Pickable __instance)
        {
            Guard(() => TagResource(__instance.gameObject, () => !__instance.GetPicked()));
        }

        private static void TagResource(GameObject go, Func<bool> isActive)
        {
            if (!HasValidZdo(go) || go.GetComponent<PoiMarker>() != null)
            {
                return;
            }

            s_scratch.Clear();
            string hoverName = ResourceRules.Collect(go, s_scratch);
            if (s_scratch.Count == 0)
            {
                return;
            }

            var items = new CatalogEntry[s_scratch.Count];
            for (int i = 0; i < s_scratch.Count; i++)
            {
                items[i] = Catalog.Register(s_scratch[i].Item, s_scratch[i].Source);
            }

            string label = !string.IsNullOrEmpty(hoverName) ? Catalog.Localize(hoverName) : null;
            PoiMarker.AttachResource(go, label, items, isActive);
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
