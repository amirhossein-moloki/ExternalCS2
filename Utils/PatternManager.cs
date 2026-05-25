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

    public class PatternManager
    {
        private static readonly Dictionary<string, List<RecoilPoint>> _patterns = new(StringComparer.OrdinalIgnoreCase);
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

                    _patterns[weaponName] = points;
                    Console.WriteLine($"[PatternManager] Loaded {points.Count} points for {weaponName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PatternManager] Failed to load pattern {file}: {ex.Message}");
                }
            }
        }

        public static List<RecoilPoint>? GetPattern(string? weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return null;

            // Handle some common weapon name mappings if necessary
            // Current weapon names from EntityBase: Ak47, M4A1, M4A1Silencer, etc.
            // Files in patterns: ak47.csv, m4a1.csv, m4a4.csv, etc.

            string key = weaponName.ToLower();
            if (key == "m4a1silencer") key = "m4a1"; // Assumed mapping based on common patterns
            if (key == "galilar") key = "galil";
            if (key == "sg556") key = "sg553";

            if (_patterns.TryGetValue(key, out var pattern))
            {
                return pattern;
            }

            return null;
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
