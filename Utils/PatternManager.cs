using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CS2GameHelper.Utils
{
    public class RecoilPoint
    {
        public float Dx { get; set; }
        public float Dy { get; set; }
        public int Delay { get; set; }
    }

    public class WeaponInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<RecoilPoint> Pattern { get; set; } = new();
        public float Multiple { get; set; } = 1.0f;
        public float SleepDivider { get; set; } = 1.0f;
        public float SleepSuber { get; set; } = 0.0f;
        public float JitterTiming { get; set; } = 0.0f;
        public float JitterMovement { get; set; } = 0.0f;
    }

    public class PatternManager
    {
        private static readonly Dictionary<string, WeaponInfo> _weapons = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string _patternsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patterns");

        public static void LoadPatterns()
        {
            if (!Directory.Exists(_patternsDir))
            {
                Console.WriteLine($"[PatternManager] Patterns directory not found: {_patternsDir}");
                return;
            }

            var files = Directory.GetFiles(_patternsDir, "*.csv");
            foreach (var file in files)
            {
                var weaponName = Path.GetFileNameWithoutExtension(file).ToLower();
                var points = new List<RecoilPoint>();

                try
                {
                    var lines = File.ReadAllLines(file);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 3)
                        {
                            if (float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float dx) &&
                                float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float dy) &&
                                float.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float delay))
                            {
                                points.Add(new RecoilPoint
                                {
                                    Dx = dx,
                                    Dy = dy,
                                    Delay = (int)delay
                                });
                            }
                        }
                    }

                    _weapons[weaponName] = new WeaponInfo { Name = weaponName, Pattern = points };
                    ApplyWeaponDefaults(_weapons[weaponName]);

                    SubdividePattern(_weapons[weaponName]);

                    Console.WriteLine($"[PatternManager] Loaded {points.Count} points for {weaponName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PatternManager] Failed to load pattern {file}: {ex.Message}");
                }
            }
        }

        private static void SubdividePattern(WeaponInfo info)
        {
            if (info.Multiple <= 1 || info.Pattern.Count == 0) return;

            var subdivided = new List<RecoilPoint>();
            foreach (var point in info.Pattern)
            {
                float baseDx = point.Dx / info.Multiple;
                float baseDy = point.Dy / info.Multiple;

                float remainingDx = point.Dx;
                float remainingDy = point.Dy;

                for (int j = 0; j < (int)info.Multiple; j++)
                {
                    RecoilPoint subPoint;
                    if (j == (int)info.Multiple - 1)
                    {
                        subPoint = new RecoilPoint { Dx = remainingDx, Dy = remainingDy, Delay = point.Delay };
                    }
                    else
                    {
                        subPoint = new RecoilPoint { Dx = baseDx, Dy = baseDy, Delay = point.Delay };
                        remainingDx -= baseDx;
                        remainingDy -= baseDy;
                    }
                    subdivided.Add(subPoint);
                }
            }
            info.Pattern = subdivided;
        }

        private static void ApplyWeaponDefaults(WeaponInfo info)
        {
            var config = ConfigManager.Load();
            if (config.WeaponProfiles.TryGetValue(info.Name.ToLower(), out var profile))
            {
                info.Multiple = profile.Multiple;
                info.SleepDivider = profile.SleepDivider;
                info.SleepSuber = profile.SleepSuber;
                info.JitterTiming = profile.JitterTiming;
                info.JitterMovement = profile.JitterMovement;
                return;
            }

            switch (info.Name.ToLower())
            {
                case "ak47": info.Multiple = 6.0f; info.SleepDivider = 6.0f; info.SleepSuber = -0.1f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "m4a4": info.Multiple = 4.0f; info.SleepDivider = 4.0f; info.SleepSuber = -0.5f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "m4a1": info.Multiple = 4.0f; info.SleepDivider = 4.0f; info.SleepSuber = -0.6f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "galil": info.Multiple = 4.0f; info.SleepDivider = 4.0f; info.SleepSuber = -0.8f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "famas": info.Multiple = 4.0f; info.SleepDivider = 4.0f; info.SleepSuber = -0.4f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "sg553": info.Multiple = 4.0f; info.SleepDivider = 4.0f; info.SleepSuber = -0.9f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "aug": info.Multiple = 4.0f; info.SleepDivider = 4.0f; info.SleepSuber = -0.9f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "p90": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -0.7f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "bizon": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = 0.9f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "ump45": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -0.4f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "mac10": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -2.2f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "mp5sd": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = 0.0f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "mp7": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = 0.1f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "mp9": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -0.3f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "m249": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -1.0f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "negev": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -1.5f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                case "cz75": info.Multiple = 3.0f; info.SleepDivider = 3.0f; info.SleepSuber = -3.0f; info.JitterTiming = 1.0f; info.JitterMovement = 1.0f; break;
                default: info.Multiple = 1.0f; info.SleepDivider = 1.0f; info.SleepSuber = 0.0f; break;
            }
        }

        public static WeaponInfo? GetWeaponInfo(string? weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return null;

            string key = weaponName.ToLower();
            if (key == "m4a1silencer") key = "m4a1";
            if (key == "galilar") key = "galil";
            if (key == "sg556") key = "sg553";
            if (key == "cz75a") key = "cz75";

            if (_weapons.TryGetValue(key, out var info))
            {
                return info;
            }

            return null;
        }

        public static List<RecoilPoint>? GetPattern(string? weaponName)
        {
            return GetWeaponInfo(weaponName)?.Pattern;
        }

        public static RecoilPoint? GetPoint(string? weaponName, int shotIndex)
        {
            var pattern = GetPattern(weaponName);
            if (pattern == null || shotIndex < 0 || shotIndex >= pattern.Count)
                return null;

            return pattern[shotIndex];
        }
    }
}
