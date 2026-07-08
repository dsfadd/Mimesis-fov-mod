using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MimesisFovMod
{
    /// <summary>
    /// BepInEx entry point. Owns the configuration, wires up the Harmony patches and
    /// drives the per-frame FOV re-assert.
    /// </summary>
    [BepInEx.BepInPlugin(Guid, Name, Version)]
    public class FovModPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.dsfadd.mimesis.fovmod";
        public const string Name = "MIMESIS FOV Mod";
        public const string Version = "1.0.0";

        // Absolute clamp for the stored value, independent of the in-game slider bounds.
        private const float HardMin = 30f;
        private const float HardMax = 120f;

        public static FovModPlugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        public static ConfigEntry<float> Fov;
        public static ConfigEntry<float> MinFov;
        public static ConfigEntry<float> MaxFov;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Fov = Config.Bind("General", "FieldOfView", 60f,
                new ConfigDescription(
                    "Vertical field of view of the first-person player camera, in degrees. The game default is 60.",
                    new AcceptableValueRange<float>(HardMin, HardMax)));

            MinFov = Config.Bind("UI", "SliderMin", 60f,
                "Lower bound of the FOV slider shown in the in-game Settings screen.");
            MaxFov = Config.Bind("UI", "SliderMax", 100f,
                "Upper bound of the FOV slider shown in the in-game Settings screen.");

            // Any change to the value (menu slider or edited config file) is applied immediately.
            Fov.SettingChanged += (_, __) => CameraFov.Apply();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            Log.LogInfo($"{Name} v{Version} loaded. FOV = {Fov.Value}");
        }

        private void LateUpdate()
        {
            CameraFov.Tick();
            SettingsUiInjector.KeepLabel();
        }
    }
}
