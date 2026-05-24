#pragma once
#include <string>
#include <vector>
#include <memory>
#include <variant>
#include <functional>
#include <map>

// Feature Categories
enum class FeatureCategory {
    Aimbot,
    ESP,
    Visuals,
    Radar,
    Triggerbot,
    Misc,
    Configs,
    Hotkeys,
    Stats
};

// Property Types supported by the UI
enum class PropertyType {
    Bool,
    Int,
    Float,
    Combo,
    Color,
    Hotkey
};

struct Color {
    float r, g, b, a;
    Color(float r = 1.f, float g = 1.f, float b = 1.f, float a = 1.f) : r(r), g(g), b(b), a(a) {}
};

// Base interface for properties
class IProperty {
public:
    virtual ~IProperty() = default;
    virtual std::string GetName() const = 0;
    virtual PropertyType GetType() const = 0;
    virtual bool IsVisible() const = 0;
};

// Generic Property Implementation
template<typename T>
class Property : public IProperty {
public:
    using Getter = std::function<T()>;
    using Setter = std::function<void(T)>;

    Property(std::string name, PropertyType type, Getter g, Setter s, T min = {}, T max = {})
        : m_name(name), m_type(type), m_getter(g), m_setter(s), m_min(min), m_max(max) {}

    std::string GetName() const override { return m_name; }
    PropertyType GetType() const override { return m_type; }
    bool IsVisible() const override { return m_visibleCondition(); }

    T Get() const { return m_getter(); }
    void Set(T val) { m_setter(val); }

    void SetVisibleCondition(std::function<bool()> cond) { m_visibleCondition = cond; }

    T m_min, m_max;
    std::vector<std::string> m_comboItems; // Only for Combo type

private:
    std::string m_name;
    PropertyType m_type;
    Getter m_getter;
    Setter m_setter;
    std::function<bool()> m_visibleCondition = [] { return true; };
};

// Base class for all cheat features
class CFeature {
public:
    CFeature(std::string internalName, std::string displayName, FeatureCategory category)
        : m_internalName(internalName), m_displayName(displayName), m_category(category), m_enabled(false), m_hotkey(0) {}

    virtual ~CFeature() = default;

    // Metadata
    std::string m_internalName;
    std::string m_displayName;
    FeatureCategory m_category;

    // State
    bool m_enabled;
    int m_hotkey; // VK_ code

    // Dependency system: List of internal names of features that MUST be enabled for this to work
    std::vector<std::string> m_dependencies;

    // Conflict system: List of internal names of features that MUST be disabled for this to work
    std::vector<std::string> m_conflicts;

    // Properties exposed to UI/Config
    std::vector<std::shared_ptr<IProperty>> m_properties;

    virtual bool CanEnable();

    virtual void OnEnable() {}
    virtual void OnDisable() {}
    virtual void OnTick() {} // Called in game loop
};

// Singleton Registry
class FeatureRegistry {
public:
    static FeatureRegistry& Get() {
        static FeatureRegistry instance;
        return instance;
    }

    void RegisterFeature(std::shared_ptr<CFeature> feature) {
        m_features.push_back(feature);
    }

    const std::vector<std::shared_ptr<CFeature>>& GetFeatures() const {
        return m_features;
    }

    std::shared_ptr<CFeature> FindByName(const std::string& name) {
        for (auto& f : m_features) {
            if (f->m_internalName == name) return f;
        }
        return nullptr;
    }

private:
    std::vector<std::shared_ptr<CFeature>> m_features;
    FeatureRegistry() = default;
};

// Macro for easy feature registration
#define REGISTER_FEATURE(Class, InternalName, DisplayName, Category) \
    inline bool Class##_registered = []() { \
        FeatureRegistry::Get().RegisterFeature(std::make_shared<Class>(InternalName, DisplayName, Category)); \
        return true; \
    }();
