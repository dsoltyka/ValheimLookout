using System;
using System.IO;
using System.Reflection;
using Jotunn.Utils;
using UnityEngine;

namespace POIRadar
{
    /// <summary>Sprites embedded under Assets/, loaded once on first use.</summary>
    internal static class Icons
    {
        private static Sprite s_dungeon;
        private static Sprite s_location;
        private static Sprite s_generic;

        public static Sprite Dungeon => s_dungeon ?? (s_dungeon = Load("dungeon.png"));
        public static Sprite Location => s_location ?? (s_location = Load("location.png"));
        public static Sprite Generic => s_generic ?? (s_generic = Load("generic.png"));

        private static Sprite Load(string resourceName)
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Plugin.Log.LogWarning($"Embedded {resourceName} not found.");
                        return null;
                    }

                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        var texture = AssetUtils.LoadImage(memory.ToArray());
                        if (texture == null)
                        {
                            Plugin.Log.LogWarning($"Embedded {resourceName} could not be decoded.");
                            return null;
                        }
                        texture.filterMode = FilterMode.Bilinear;
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                        sprite.name = "POIRadar." + resourceName;
                        return sprite;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not load {resourceName}: {e.Message}");
                return null;
            }
        }
    }
}
