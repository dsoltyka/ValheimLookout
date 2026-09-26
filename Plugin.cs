using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ValheimModShared;

namespace Lookout
{
    /// <summary>
    /// Entry point. Client-side only: it reads the objects the game has already loaded around the player
    /// and draws unsaved pins on the local minimap. Nothing is sent to or required from the server.
    /// </summary>
    [BepInPlugin(PluginGuid, BuildInfo.Name, BuildInfo.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dsoltyka.Lookout";

        internal static ManualLogSource Log { get; private set; }
        internal static Settings Settings { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);
            ConfigWatcher.Watch(Config, Log);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PoiPatches));

            gameObject.AddComponent<PinManager>();

            Log.LogInfo($"{BuildInfo.Name} {BuildInfo.Version} loaded");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
