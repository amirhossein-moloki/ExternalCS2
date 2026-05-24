using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Utils.Registry
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FeatureAttribute : Attribute
    {
        public string InternalName { get; }
        public string DisplayName { get; }
        public string Category { get; }

        public FeatureAttribute(string internalName, string displayName, string category)
        {
            InternalName = internalName;
            DisplayName = displayName;
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FeaturePropertyAttribute : Attribute
    {
        public string DisplayName { get; }
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }

        public FeaturePropertyAttribute(string displayName, double min = 0, double max = 1, double step = 0.1)
        {
            DisplayName = displayName;
            Min = min;
            Max = max;
            Step = step;
        }
    }

    public class FeatureInfo
    {
        public string InternalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public object Instance { get; set; } = null!;
        public List<FeaturePropertyInfo> Properties { get; set; } = new();
        public bool Enabled { get; set; }
        public Keys Hotkey { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<string> Conflicts { get; set; } = new();

        public bool CanEnable()
        {
            foreach (var dep in Dependencies)
            {
                var f = FeatureRegistry.Features.FirstOrDefault(x => x.InternalName == dep);
                if (f != null && !f.Enabled) return false;
            }
            foreach (var conf in Conflicts)
            {
                var f = FeatureRegistry.Features.FirstOrDefault(x => x.InternalName == conf);
                if (f != null && f.Enabled) return false;
            }
            return true;
        }
    }

    public class FeaturePropertyInfo
    {
        public string DisplayName { get; set; } = string.Empty;
        public PropertyInfo Property { get; set; } = null!;
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }
        public Type Type => Property.PropertyType;
    }

    public static class FeatureRegistry
    {
        private static List<FeatureInfo> _features = new();
        public static IReadOnlyList<FeatureInfo> Features => _features;

        public static void Discover(params object[] instances)
        {
            foreach (var instance in instances)
            {
                var type = instance.GetType();
                var attr = type.GetCustomAttribute<FeatureAttribute>();
                if (attr == null) continue;

                var info = new FeatureInfo
                {
                    InternalName = attr.InternalName,
                    DisplayName = attr.DisplayName,
                    Category = attr.Category,
                    Instance = instance
                };

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var propAttr = prop.GetCustomAttribute<FeaturePropertyAttribute>();
                    if (propAttr == null) continue;

                    info.Properties.Add(new FeaturePropertyInfo
                    {
                        DisplayName = propAttr.DisplayName,
                        Property = prop,
                        Min = propAttr.Min,
                        Max = propAttr.Max,
                        Step = propAttr.Step
                    });
                }

                _features.Add(info);
            }
        }
    }
}
