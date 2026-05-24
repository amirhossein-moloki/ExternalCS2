#pragma once
#include <string>
#include <vector>
#include <filesystem>
#include <fstream>
#include "FeatureRegistry.hpp"
#include "ManagementList.hpp"

// Assuming nlohmann/json is available as per requirements
#include <nlohmann/json.hpp>

using json = nlohmann::json;
namespace fs = std::filesystem;

class ConfigManager {
public:
    static ConfigManager& Get() {
        static ConfigManager instance;
        return instance;
    }

    void SetConfigDirectory(const std::string& path) {
        m_configDir = path;
        if (!fs::exists(m_configDir)) fs::create_directories(m_configDir);
    }

    std::vector<std::string> GetProfileList() {
        std::vector<std::string> profiles;
        for (const auto& entry : fs::directory_iterator(m_configDir)) {
            if (entry.path().extension() == ".json") {
                profiles.push_back(entry.path().stem().string());
            }
        }
        return profiles;
    }

    bool SaveConfig(const std::string& name) {
        json j;
        auto& registry = FeatureRegistry::Get();

        for (auto& feature : registry.GetFeatures()) {
            json f_j;
            f_j["enabled"] = feature->m_enabled;
            f_j["hotkey"] = feature->m_hotkey;

            json props_j = json::object();
            for (auto& prop : feature->m_properties) {
                SaveProperty(props_j, prop);
            }
            f_j["properties"] = props_j;
            j[feature->m_internalName] = f_j;
        }

        std::ofstream file(m_configDir / (name + ".json"));
        if (!file.is_open()) return false;

        file << j.dump(4);
        ManagementList::Get().AddLog("Saved config: " + name);
        return true;
    }

    bool LoadConfig(const std::string& name) {
        std::ifstream file(m_configDir / (name + ".json"));
        if (!file.is_open()) return false;

        json j;
        file >> j;

        auto& registry = FeatureRegistry::Get();
        for (auto& feature : registry.GetFeatures()) {
            if (j.contains(feature->m_internalName)) {
                auto& f_j = j[feature->m_internalName];
                feature->m_enabled = f_j.value("enabled", false);
                feature->m_hotkey = f_j.value("hotkey", 0);

                if (f_j.contains("properties")) {
                    auto& props_j = f_j["properties"];
                    for (auto& prop : feature->m_properties) {
                        LoadProperty(props_j, prop);
                    }
                }

                if (feature->m_enabled) feature->OnEnable();
                else feature->OnDisable();
            }
        }

        ManagementList::Get().AddLog("Loaded config: " + name);
        return true;
    }

    std::string ExportConfigString() {
        json j = SerializeAll();
        return j.dump();
    }

    bool ImportConfigString(const std::string& data) {
        try {
            json j = json::parse(data);
            return DeserializeAll(j);
        } catch (...) {
            return false;
        }
    }

private:
    fs::path m_configDir = "configs";
    ConfigManager() = default;

    json SerializeAll() {
        json j;
        auto& registry = FeatureRegistry::Get();
        for (auto& feature : registry.GetFeatures()) {
            json f_j;
            f_j["enabled"] = feature->m_enabled;
            f_j["hotkey"] = feature->m_hotkey;
            json props_j = json::object();
            for (auto& prop : feature->m_properties) {
                SaveProperty(props_j, prop);
            }
            f_j["properties"] = props_j;
            j[feature->m_internalName] = f_j;
        }
        return j;
    }

    bool DeserializeAll(const json& j) {
        auto& registry = FeatureRegistry::Get();
        for (auto& feature : registry.GetFeatures()) {
            if (j.contains(feature->m_internalName)) {
                auto& f_j = j[feature->m_internalName];
                feature->m_enabled = f_j.value("enabled", false);
                feature->m_hotkey = f_j.value("hotkey", 0);
                if (f_j.contains("properties")) {
                    auto& props_j = f_j["properties"];
                    for (auto& prop : feature->m_properties) {
                        LoadProperty(props_j, prop);
                    }
                }
                if (feature->m_enabled) feature->OnEnable();
                else feature->OnDisable();
            }
        }
        return true;
    }

    void SaveProperty(json& j, std::shared_ptr<IProperty> prop) {
        switch (prop->GetType()) {
        case PropertyType::Bool:
            j[prop->GetName()] = std::static_pointer_cast<Property<bool>>(prop)->Get(); break;
        case PropertyType::Int:
            j[prop->GetName()] = std::static_pointer_cast<Property<int>>(prop)->Get(); break;
        case PropertyType::Float:
            j[prop->GetName()] = std::static_pointer_cast<Property<float>>(prop)->Get(); break;
        case PropertyType::Combo:
            j[prop->GetName()] = std::static_pointer_cast<Property<int>>(prop)->Get(); break;
        case PropertyType::Color: {
            auto c = std::static_pointer_cast<Property<Color>>(prop)->Get();
            j[prop->GetName()] = { c.r, c.g, c.b, c.a };
            break;
        }
        }
    }

    void LoadProperty(const json& j, std::shared_ptr<IProperty> prop) {
        if (!j.contains(prop->GetName())) return;

        switch (prop->GetType()) {
        case PropertyType::Bool:
            std::static_pointer_cast<Property<bool>>(prop)->Set(j[prop->GetName()].get<bool>()); break;
        case PropertyType::Int:
            std::static_pointer_cast<Property<int>>(prop)->Set(j[prop->GetName()].get<int>()); break;
        case PropertyType::Float:
            std::static_pointer_cast<Property<float>>(prop)->Set(j[prop->GetName()].get<float>()); break;
        case PropertyType::Combo:
            std::static_pointer_cast<Property<int>>(prop)->Set(j[prop->GetName()].get<int>()); break;
        case PropertyType::Color: {
            auto arr = j[prop->GetName()].get<std::vector<float>>();
            if (arr.size() == 4)
                std::static_pointer_cast<Property<Color>>(prop)->Set(Color(arr[0], arr[1], arr[2], arr[3]));
            break;
        }
        }
    }
};
