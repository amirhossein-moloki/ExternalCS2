using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2GameHelper.Utils.Registry
{
    public class LogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public static class ManagementList
    {
        private static List<LogEntry> _logs = new();
        public static IReadOnlyList<LogEntry> Logs => _logs;

        public static void AddLog(string message)
        {
            _logs.Insert(0, new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Message = message
            });

            if (_logs.Count > 10)
                _logs.RemoveAt(_logs.Count - 1);
        }

        public static void ToggleFeature(FeatureInfo feature)
        {
            feature.Enabled = !feature.Enabled;

            // Invoke Start/Stop if the instance is a ThreadedServiceBase
            var startMethod = feature.Instance.GetType().GetMethod("Start");
            var stopMethod = feature.Instance.GetType().GetMethod("Stop");

            if (feature.Enabled)
                startMethod?.Invoke(feature.Instance, null);
            else
                stopMethod?.Invoke(feature.Instance, null);

            AddLog($"{feature.DisplayName} {(feature.Enabled ? "Enabled" : "Disabled")}");
        }

        public static List<FeatureInfo> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return FeatureRegistry.Features.ToList();

            return FeatureRegistry.Features
                .Where(f => f.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            f.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
