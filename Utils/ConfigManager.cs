using System.IO;
using System.Text.Json;

namespace CS2GameHelper.Utils;

public class ConfigManager
{
    private const string ConfigFile = "config.json";

    // Основные флаги
    public bool AimBot { get; set; } = true;
    public bool BombTimer { get; set; } = true;
    // УДАЛЕНО: public bool EspAimCrosshair { get; set; } = true;
    public bool SkeletonEsp { get; set; } = true;
    public bool SkeletonShowHeadCircle { get; set; } = true;
    public bool TriggerBot { get; set; } = true;
    public Keys AimBotKey { get; set; } = Keys.LButton;
    public Keys TriggerBotKey { get; set; } = Keys.LMenu;
    public Keys MenuToggleKey { get; set; } = Keys.Insert;
    public bool TeamCheck { get; set; } = true;
    public bool AimBotAutoShoot { get; set; } = true;
    public bool DebugMode { get; set; } = false;

    // New dedicated RCS config object
    private RcsConfig _rcs = new();
    public RcsConfig Rcs
    {
        get => _rcs;
        set => _rcs = value;
    }

    // Вложенные настройки ESP
    public EspConfig Esp { get; set; } = new();

    // Вложенные классы конфигурации
    public class EspConfig
    {
        public BoxConfig Box { get; set; } = new();
        public RadarConfig Radar { get; set; } = new();
        public AimCrosshairConfig AimCrosshair { get; set; } = new();

        public class BoxConfig
        {
            public bool Enabled { get; set; } = true;
            public bool ShowBox { get; set; } = true;
            public bool ShowName { get; set; } = true;
            public bool ShowHealthBar { get; set; } = true;
            public bool ShowHealthText { get; set; } = true;
            public int HealthPosition { get; set; } = 0; // 0: Left, 1: Top, 2: Right, 3: Bottom
            public bool ShowDistance { get; set; } = true;
            public bool ShowWeaponName { get; set; } = true;
            public bool ShowArmor { get; set; } = true;
            public bool ShowVisibilityIndicator { get; set; } = true;
            public bool ShowFlags { get; set; } = true;
            public string EnemyColor { get; set; } = "FF8B0000";   // DarkRed
            public string TeamColor { get; set; } = "FF00008B";     // DarkBlue
            public string VisibleAlpha { get; set; } = "FF";
            public string InvisibleAlpha { get; set; } = "88";
        }

        public class RadarConfig
        {
            public bool Enabled { get; set; } = true;
            public int Size { get; set; } = 150;
            public int X { get; set; } = 50;
            public int Y { get; set; } = 50;
            public float MaxDistance { get; set; } = 100.0f;
            public bool ShowLocalPlayer { get; set; } = true;
            public bool ShowDirectionArrow { get; set; } = true;
            public string EnemyColor { get; set; } = "FFFF0000";   // Red
            public string TeamColor { get; set; } = "FF0000FF";    // Blue
            public string VisibleAlpha { get; set; } = "FF";
            public string InvisibleAlpha { get; set; } = "88";
        }

        public class AimCrosshairConfig
        {
            public bool Enabled { get; set; } = true;
            public int Radius { get; set; } = 6;
            // ARGB hex string
            public string Color { get; set; } = "FFFFFFFF";
            // Recoil scale multiplier applied to punch angles
            public float RecoilScale { get; set; } = 2f;

            // ---- v2.0: FOV Circle ----
            // Draws a circle around the screen center representing the aim FOV radius.
            public bool ShowFovCircle { get; set; } = false;
            // Radius in pixels (screen-space). Independent of game FOV for simplicity & predictability.
            public int FovCircleRadius { get; set; } = 120;
            // ARGB hex
            public string FovCircleColor { get; set; } = "80FFFFFF";
        }
    }

    // v2.0: Vote Teller
    public VoteTellerConfig VoteTeller { get; set; } = new();

    public class VoteTellerConfig
    {
        public bool Enabled { get; set; } = true;
        // ARGB hex strings
        public string ColorT { get; set; } = "FFFF8C00";   // OrangeRed-ish
        public string ColorCT { get; set; } = "FF00BFFF";  // DeepSkyBlue
        public string ColorAll { get; set; } = "FFFFFFFF";
        public int X { get; set; } = 10;
        public int Y { get; set; } = 350;
    }

    // Spectator list settings
    public SpectatorListConfig SpectatorList { get; set; } = new();

    public class SpectatorListConfig
    {
        public bool Enabled { get; set; } = true;
    }

    public Dictionary<string, WeaponProfile> WeaponProfiles { get; set; } = new();

    public class WeaponProfile
    {
        public float Multiple { get; set; } = 1.0f;
        public float SleepDivider { get; set; } = 1.0f;
        public float SleepSuber { get; set; } = 0.0f;
        public float JitterTiming { get; set; } = 0.0f;
        public float JitterMovement { get; set; } = 0.0f;
    }

    public MovementConfig Movement { get; set; } = new();
    public class MovementConfig
    {
        public bool Bhop { get; set; } = false;
        public bool AutoStrafe { get; set; } = false;
    }

    public AdvancedVisualsConfig AdvancedVisuals { get; set; } = new();
    public class AdvancedVisualsConfig
    {
        public bool GlowEsp { get; set; } = false;
        public bool Backtracking { get; set; } = false;
    }

    public FollowRcsConfig FollowRcs { get; set; } = new();
    public class FollowRcsConfig
    {
        public bool Enabled { get; set; } = false;
        public float DotSize { get; set; } = 3.0f;
        public string Color { get; set; } = "FF0000FF"; // Blue
    }

    public NoRecoilConfig NoRecoil { get; set; } = new();
    public class NoRecoilConfig
    {
        public bool PerfectNoRecoil { get; set; } = false;
        public bool NoVisualRecoil { get; set; } = false;
    }

    // Hit sound and on-screen hit text configuration
    public HitSoundConfig HitSound { get; set; } = new();

    // v2.1: AimBot tuning (вынесли магические числа)
    public AimBotTuningConfig AimBotTuning { get; set; } = new();

    public class AimBotTuningConfig
    {
        public double HumanReactThreshold { get; set; } = 30.0;
        public double HumanEaseDistancePixels { get; set; } = 35.0;
        public double HumanMinimumGain { get; set; } = 0.15;
        public int LockJitterStartMs { get; set; } = 600;
        public int LockJitterStrongMs { get; set; } = 1500;
        public int MinShootIntervalMs { get; set; } = 100;
        public double AimSmoothing { get; set; } = 3.0;
        public int AimUpdateIntervalMs { get; set; } = 500;
        // 0 → случайный seed (недетерминированный). Другое → воспроизводимый джиттер.
        public int HumanizationSeed { get; set; } = 0;
    }

    public class RcsConfig
    {
        public bool Enabled { get; set; } = true;
        public float GlobalScale { get; set; } = 2.0f;
        public float Sensitivity { get; set; } = 1.0f;
    }

    public class HitSoundConfig
    {
        public bool Enabled { get; set; } = true;
        // Colors are ARGB hex strings like "FFFF0000" (opaque red)
        public string HitColor { get; set; } = "FFFFFFFF"; // white
        public string HeadshotColor { get; set; } = "FFFFD700"; // gold
        // Text shown on screen for hit and headshot
        public string HitText { get; set; } = "HIT";
        public string HeadshotText { get; set; } = "HEADSHOT";
        // Paths to sound files (can be absolute or relative to app base dir)
        public string HitSoundFile { get; set; } = "assets/sounds/hit.wav";
        public string HeadshotSoundFile { get; set; } = "assets/sounds/headshot.wav";
        // Advanced settings
        public int HeadshotDamageThreshold { get; set; } = 100;   // урон ≥ 100 → хедшот
        public double TextDurationSeconds { get; set; } = 1.5;    // длительность текста в секундах
    }

    public static ConfigManager Load()
    {
        try
        {
            if (!File.Exists(ConfigFile))
            {
                var defaultConfig = Default();
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigFile);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            options.Converters.Add(new KeysJsonConverter());

            ConfigManager? config;
            try
            {
                config = JsonSerializer.Deserialize<ConfigManager>(json, options);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Config] Error parsing {ConfigFile}: {ex.Message}");
                // Instead of overwriting, we use defaults but keep the file as-is for the user to fix.
                config = Default();
            }

            config ??= Default();
            config.Esp ??= new EspConfig();
            config.Esp.Box ??= new EspConfig.BoxConfig();
            config.Esp.Radar ??= new EspConfig.RadarConfig();
            config.Esp.AimCrosshair ??= new EspConfig.AimCrosshairConfig();
            config.SpectatorList ??= new SpectatorListConfig();
            config.HitSound ??= new HitSoundConfig();
            config.VoteTeller ??= new VoteTellerConfig();
            config.AimBotTuning ??= new AimBotTuningConfig();
            config.Rcs ??= new RcsConfig();
            config.WeaponProfiles ??= new Dictionary<string, WeaponProfile>();
            config.Movement ??= new MovementConfig();
            config.AdvancedVisuals ??= new AdvancedVisualsConfig();
            config.FollowRcs ??= new FollowRcsConfig();
            config.NoRecoil ??= new NoRecoilConfig();

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Unexpected error loading {ConfigFile}: {ex.Message}");
            return Default();
        }
    }

    /// <summary>v2.0: reload config from disk and copy values into this instance. Returns true on success.</summary>
    public bool ReloadInPlace()
    {
        try
        {
            var fresh = Load();
            CopyFrom(fresh);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>v2.0: reset all values to defaults and persist.</summary>
    public bool ResetDefaults()
    {
        try
        {
            var def = Default();
            CopyFrom(def);
            Save(this);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>v2.0: save self to disk.</summary>
    public bool SaveCurrent()
    {
        try { Save(this); return true; } catch { return false; }
    }

    private void CopyFrom(ConfigManager other)
    {
        AimBot = other.AimBot;
        AimBotAutoShoot = other.AimBotAutoShoot;
        BombTimer = other.BombTimer;
        SkeletonEsp = other.SkeletonEsp;
        SkeletonShowHeadCircle = other.SkeletonShowHeadCircle;
        TriggerBot = other.TriggerBot;
        AimBotKey = other.AimBotKey;
        TriggerBotKey = other.TriggerBotKey;
        MenuToggleKey = other.MenuToggleKey;
        TeamCheck = other.TeamCheck;

        Esp ??= new EspConfig();
        Esp.Box ??= new EspConfig.BoxConfig();
        Esp.Radar ??= new EspConfig.RadarConfig();
        Esp.AimCrosshair ??= new EspConfig.AimCrosshairConfig();

        // Box
        Esp.Box.Enabled = other.Esp.Box.Enabled;
        Esp.Box.ShowBox = other.Esp.Box.ShowBox;
        Esp.Box.ShowName = other.Esp.Box.ShowName;
        Esp.Box.ShowHealthBar = other.Esp.Box.ShowHealthBar;
        Esp.Box.ShowHealthText = other.Esp.Box.ShowHealthText;
        Esp.Box.HealthPosition = other.Esp.Box.HealthPosition;
        Esp.Box.ShowDistance = other.Esp.Box.ShowDistance;
        Esp.Box.ShowWeaponName = other.Esp.Box.ShowWeaponName;
        Esp.Box.ShowArmor = other.Esp.Box.ShowArmor;
        Esp.Box.ShowVisibilityIndicator = other.Esp.Box.ShowVisibilityIndicator;
        Esp.Box.ShowFlags = other.Esp.Box.ShowFlags;
        Esp.Box.EnemyColor = other.Esp.Box.EnemyColor;
        Esp.Box.TeamColor = other.Esp.Box.TeamColor;
        Esp.Box.VisibleAlpha = other.Esp.Box.VisibleAlpha;
        Esp.Box.InvisibleAlpha = other.Esp.Box.InvisibleAlpha;

        // Radar
        Esp.Radar.Enabled = other.Esp.Radar.Enabled;
        Esp.Radar.Size = other.Esp.Radar.Size;
        Esp.Radar.X = other.Esp.Radar.X;
        Esp.Radar.Y = other.Esp.Radar.Y;
        Esp.Radar.MaxDistance = other.Esp.Radar.MaxDistance;
        Esp.Radar.ShowLocalPlayer = other.Esp.Radar.ShowLocalPlayer;
        Esp.Radar.ShowDirectionArrow = other.Esp.Radar.ShowDirectionArrow;
        Esp.Radar.EnemyColor = other.Esp.Radar.EnemyColor;
        Esp.Radar.TeamColor = other.Esp.Radar.TeamColor;
        Esp.Radar.VisibleAlpha = other.Esp.Radar.VisibleAlpha;
        Esp.Radar.InvisibleAlpha = other.Esp.Radar.InvisibleAlpha;

        // AimCrosshair + FovCircle
        Esp.AimCrosshair.Enabled = other.Esp.AimCrosshair.Enabled;
        Esp.AimCrosshair.Radius = other.Esp.AimCrosshair.Radius;
        Esp.AimCrosshair.Color = other.Esp.AimCrosshair.Color;
        Esp.AimCrosshair.RecoilScale = other.Esp.AimCrosshair.RecoilScale;
        Esp.AimCrosshair.ShowFovCircle = other.Esp.AimCrosshair.ShowFovCircle;
        Esp.AimCrosshair.FovCircleRadius = other.Esp.AimCrosshair.FovCircleRadius;
        Esp.AimCrosshair.FovCircleColor = other.Esp.AimCrosshair.FovCircleColor;

        SpectatorList ??= new SpectatorListConfig();
        SpectatorList.Enabled = other.SpectatorList.Enabled;

        HitSound ??= new HitSoundConfig();
        HitSound.Enabled = other.HitSound.Enabled;
        HitSound.HitColor = other.HitSound.HitColor;
        HitSound.HeadshotColor = other.HitSound.HeadshotColor;
        HitSound.HitText = other.HitSound.HitText;
        HitSound.HeadshotText = other.HitSound.HeadshotText;
        HitSound.HitSoundFile = other.HitSound.HitSoundFile;
        HitSound.HeadshotSoundFile = other.HitSound.HeadshotSoundFile;
        HitSound.HeadshotDamageThreshold = other.HitSound.HeadshotDamageThreshold;
        HitSound.TextDurationSeconds = other.HitSound.TextDurationSeconds;

        VoteTeller ??= new VoteTellerConfig();
        VoteTeller.Enabled = other.VoteTeller.Enabled;
        VoteTeller.ColorT = other.VoteTeller.ColorT;
        VoteTeller.ColorCT = other.VoteTeller.ColorCT;
        VoteTeller.ColorAll = other.VoteTeller.ColorAll;
        VoteTeller.X = other.VoteTeller.X;
        VoteTeller.Y = other.VoteTeller.Y;

        AimBotTuning ??= new AimBotTuningConfig();
        AimBotTuning.HumanReactThreshold = other.AimBotTuning.HumanReactThreshold;
        AimBotTuning.HumanEaseDistancePixels = other.AimBotTuning.HumanEaseDistancePixels;
        AimBotTuning.HumanMinimumGain = other.AimBotTuning.HumanMinimumGain;
        AimBotTuning.LockJitterStartMs = other.AimBotTuning.LockJitterStartMs;
        AimBotTuning.LockJitterStrongMs = other.AimBotTuning.LockJitterStrongMs;
        AimBotTuning.MinShootIntervalMs = other.AimBotTuning.MinShootIntervalMs;
        AimBotTuning.AimSmoothing = other.AimBotTuning.AimSmoothing;
        AimBotTuning.AimUpdateIntervalMs = other.AimBotTuning.AimUpdateIntervalMs;
        AimBotTuning.HumanizationSeed = other.AimBotTuning.HumanizationSeed;

        Rcs ??= new RcsConfig();
        Rcs.Enabled = other.Rcs.Enabled;
        Rcs.GlobalScale = other.Rcs.GlobalScale;
        Rcs.Sensitivity = other.Rcs.Sensitivity;

        WeaponProfiles = new Dictionary<string, WeaponProfile>(other.WeaponProfiles);
        Movement = new MovementConfig
        {
            Bhop = other.Movement.Bhop,
            AutoStrafe = other.Movement.AutoStrafe
        };
        AdvancedVisuals = new AdvancedVisualsConfig
        {
            GlowEsp = other.AdvancedVisuals.GlowEsp,
            Backtracking = other.AdvancedVisuals.Backtracking
        };
        FollowRcs = new FollowRcsConfig
        {
            Enabled = other.FollowRcs.Enabled,
            DotSize = other.FollowRcs.DotSize,
            Color = other.FollowRcs.Color
        };
        NoRecoil = new NoRecoilConfig
        {
            PerfectNoRecoil = other.NoRecoil.PerfectNoRecoil,
            NoVisualRecoil = other.NoRecoil.NoVisualRecoil
        };
    }

    public static void Save(ConfigManager options, string fileName = ConfigFile)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            jsonOptions.Converters.Add(new KeysJsonConverter());

            var json = JsonSerializer.Serialize(options, jsonOptions);
            File.WriteAllText(fileName, json);
        }
        catch
        {
            // Игнор
        }
    }

    public static string Export(ConfigManager options)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        jsonOptions.Converters.Add(new KeysJsonConverter());
        return JsonSerializer.Serialize(options, jsonOptions);
    }

    public static ConfigManager? Import(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new KeysJsonConverter());
            return JsonSerializer.Deserialize<ConfigManager>(json, options);
        }
        catch { return null; }
    }

    public static ConfigManager Default()
    {
        var config = new ConfigManager
        {
            // Основные флаги
            AimBot = true,
            AimBotAutoShoot = true,
            BombTimer = true,
            // УДАЛЕНО: EspAimCrosshair = true,
            SkeletonEsp = true,
            SkeletonShowHeadCircle = true,
            TriggerBot = true,
            AimBotKey = Keys.LButton,
            TriggerBotKey = Keys.LMenu,
            MenuToggleKey = Keys.Insert,
            TeamCheck = true,

            Esp = new EspConfig
            {
                Box = new EspConfig.BoxConfig
                {
                    Enabled = true,
                    ShowBox = true,
                    ShowName = true,
                    ShowHealthBar = true,
                    ShowHealthText = true,
                    HealthPosition = 0,
                    ShowDistance = true,
                    ShowWeaponName = true,
                    ShowArmor = true,
                    ShowVisibilityIndicator = true,
                    ShowFlags = true,
                    EnemyColor = "FF8B0000",
                    TeamColor = "FF00008B",
                    VisibleAlpha = "FF",
                    InvisibleAlpha = "88"
                },
                Radar = new EspConfig.RadarConfig
                {
                    Enabled = true,
                    Size = 150,
                    X = 50,
                    Y = 50,
                    MaxDistance = 100.0f,
                    ShowLocalPlayer = true,
                    ShowDirectionArrow = true,
                    EnemyColor = "FFFF0000",
                    TeamColor = "FF0000FF",
                    VisibleAlpha = "FF",
                    InvisibleAlpha = "88"
                },
                AimCrosshair = new EspConfig.AimCrosshairConfig
                {
                    Enabled = true,
                    Radius = 6,
                    Color = "FFFFFFFF",
                    RecoilScale = 2f
                }
            },
            SpectatorList = new SpectatorListConfig
            {
                Enabled = true
            },
            HitSound = new HitSoundConfig
            {
                Enabled = true,
                HitColor = "FFFFFFFF",
                HeadshotColor = "FFFFD700",
                HitText = "HIT",
                HeadshotText = "HEADSHOT",
                HitSoundFile = "assets/sounds/hit.wav",
                HeadshotSoundFile = "assets/sounds/headshot.wav",
                HeadshotDamageThreshold = 100,
                TextDurationSeconds = 1.5
            },
            AimBotTuning = new AimBotTuningConfig(),
            Rcs = new RcsConfig
            {
                Enabled = true,
                GlobalScale = 2.0f,
                Sensitivity = 1.0f
            },
            WeaponProfiles = new Dictionary<string, WeaponProfile>
            {
                { "ak47", new WeaponProfile { Multiple = 6.0f, SleepDivider = 6.0f, SleepSuber = -0.1f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "m4a4", new WeaponProfile { Multiple = 4.0f, SleepDivider = 4.0f, SleepSuber = -0.5f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "m4a1", new WeaponProfile { Multiple = 4.0f, SleepDivider = 4.0f, SleepSuber = -0.6f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "galil", new WeaponProfile { Multiple = 4.0f, SleepDivider = 4.0f, SleepSuber = -0.8f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "famas", new WeaponProfile { Multiple = 4.0f, SleepDivider = 4.0f, SleepSuber = -0.4f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "sg553", new WeaponProfile { Multiple = 4.0f, SleepDivider = 4.0f, SleepSuber = -0.9f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "aug", new WeaponProfile { Multiple = 4.0f, SleepDivider = 4.0f, SleepSuber = -0.9f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "p90", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -0.7f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "bizon", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = 0.9f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "ump45", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -0.4f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "mac10", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -2.2f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "mp5sd", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = 0.0f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "mp7", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = 0.1f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "mp9", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -0.3f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "m249", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -1.0f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "negev", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -1.5f, JitterTiming = 1.0f, JitterMovement = 1.0f } },
                { "cz75", new WeaponProfile { Multiple = 3.0f, SleepDivider = 3.0f, SleepSuber = -3.0f, JitterTiming = 1.0f, JitterMovement = 1.0f } }
            },
            Movement = new MovementConfig(),
            AdvancedVisuals = new AdvancedVisualsConfig(),
            FollowRcs = new FollowRcsConfig(),
            NoRecoil = new NoRecoilConfig()
        };
        return config;
    }
}