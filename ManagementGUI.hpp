#pragma once
#include <imgui.h>
#include <imgui_internal.h>
#include <vector>
#include <string>
#include "FeatureRegistry.hpp"
#include "ManagementList.hpp"
#include "ConfigManager.hpp"
#include "HotkeySystem.hpp"

class ManagementGUI {
public:
    static ManagementGUI& Get() {
        static ManagementGUI instance;
        return instance;
    }

    bool m_open = false;

    void Render() {
        if (!m_open) return;

        SetupStyles();

        ImGui::SetNextWindowSize(ImVec2(800, 500), ImGuiCond_FirstUseEver);
        if (ImGui::Begin("Cheat Management System", &m_open, ImGuiWindowFlags_NoCollapse)) {

            // Sidebar
            ImGui::BeginChild("Sidebar", ImVec2(200, 0), true);
            RenderSidebar();
            ImGui::EndChild();

            ImGui::SameLine();

            // Main Content
            ImGui::BeginGroup();
            RenderHeader();
            ImGui::Separator();

            ImGui::BeginChild("Content", ImVec2(0, -ImGui::GetFrameHeightWithSpacing()), false);
            RenderMainContent();
            ImGui::EndChild();

            RenderFooter();
            ImGui::EndGroup();
        }
        ImGui::End();

        RenderActiveFeatures();
    }

private:
    FeatureCategory m_activeTab = FeatureCategory::Aimbot;
    char m_searchBuffer[64] = "";

    ManagementGUI() = default;

    void SetupStyles() {
        auto& style = ImGui::GetStyle();
        style.WindowRounding = 6.0f;
        style.ChildRounding = 4.0f;
        style.FrameRounding = 4.0f;
        style.ScrollbarRounding = 9.0f;

        auto colors = style.Colors;
        colors[ImGuiCol_WindowBg] = ImColor(13, 17, 23, 255);
        colors[ImGuiCol_ChildBg] = ImColor(13, 17, 23, 255);
        colors[ImGuiCol_Border] = ImColor(48, 54, 61, 255);
        colors[ImGuiCol_FrameBg] = ImColor(22, 27, 34, 255);
        colors[ImGuiCol_Header] = ImColor(33, 38, 45, 255);
        colors[ImGuiCol_HeaderHovered] = ImColor(48, 54, 61, 255);
        colors[ImGuiCol_HeaderActive] = ImColor(56, 63, 71, 255);
        colors[ImGuiCol_Button] = ImColor(33, 38, 45, 255);
        colors[ImGuiCol_ButtonHovered] = ImColor(48, 54, 61, 255);
        colors[ImGuiCol_ButtonActive] = ImColor(56, 63, 71, 255);
        colors[ImGuiCol_CheckMark] = ImColor(46, 160, 67, 255);
        colors[ImGuiCol_SliderGrab] = ImColor(88, 166, 255, 255);
        colors[ImGuiCol_Text] = ImColor(201, 209, 217, 255);
    }

    void RenderSidebar() {
        auto renderTab = [&](const char* label, FeatureCategory cat) {
            bool active = m_activeTab == cat;
            if (active) ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.35f, 0.65f, 1.0f, 1.0f));

            if (ImGui::Selectable(label, active, 0, ImVec2(0, 30))) {
                m_activeTab = cat;
            }

            if (active) ImGui::PopStyleColor();
        };

        renderTab("Aimbot", FeatureCategory::Aimbot);
        renderTab("ESP", FeatureCategory::ESP);
        renderTab("Visuals", FeatureCategory::Visuals);
        renderTab("Radar", FeatureCategory::Radar);
        renderTab("Triggerbot", FeatureCategory::Triggerbot);
        renderTab("Misc", FeatureCategory::Misc);
        ImGui::Separator();
        renderTab("Configs", FeatureCategory::Configs);
        renderTab("Stats/Log", FeatureCategory::Stats);
    }

    void RenderHeader() {
        ImGui::SetNextItemWidth(250);
        ImGui::InputTextWithHint("##Search", "Filter features...", m_searchBuffer, sizeof(m_searchBuffer));

        ImGui::SameLine(ImGui::GetContentRegionAvail().x - 100);
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "FPS: %.1f", ImGui::GetIO().Framerate);
    }

    void RenderMainContent() {
        if (m_activeTab == FeatureCategory::Configs) {
            RenderConfigTab();
            return;
        }

        if (m_activeTab == FeatureCategory::Stats) {
            RenderLogTab();
            return;
        }

        if (ImGui::BeginTable("FeatureTable", 3, ImGuiTableFlags_RowBg)) {
            ImGui::TableSetupColumn("Feature", ImGuiTableColumnFlags_WidthStretch);
            ImGui::TableSetupColumn("Control", ImGuiTableColumnFlags_WidthFixed, 150);
            ImGui::TableSetupColumn("Hotkey", ImGuiTableColumnFlags_WidthFixed, 100);
            ImGui::TableHeadersRow();

            for (auto& feature : FeatureRegistry::Get().GetFeatures()) {
                if (feature->m_category != m_activeTab) continue;

                // Search filter
                if (strlen(m_searchBuffer) > 0) {
                    if (feature->m_displayName.find(m_searchBuffer) == std::string::npos) continue;
                }

                ImGui::TableNextRow();
                ImGui::TableNextColumn();
                ImGui::Text("%s", feature->m_displayName.c_str());

                ImGui::TableNextColumn();
                bool enabled = feature->m_enabled;
                if (!feature->m_enabled && !feature->CanEnable()) {
                    ImGui::BeginDisabled();
                    ImGui::Checkbox(("##" + feature->m_internalName).c_str(), &enabled);
                    if (ImGui::IsItemHovered(ImGuiHoveredFlags_AllowWhenDisabled)) {
                        ImGui::SetTooltip("Dependency not met or conflict detected.");
                    }
                    ImGui::EndDisabled();
                } else {
                    if (ImGui::Checkbox(("##" + feature->m_internalName).c_str(), &enabled)) {
                        feature->m_enabled = enabled;
                        if (feature->m_enabled) feature->OnEnable();
                        else feature->OnDisable();
                        ManagementList::Get().AddLog(feature->m_displayName + (feature->m_enabled ? " Enabled" : " Disabled"));
                    }
                }

                ImGui::TableNextColumn();
                std::string btnLabel = HotkeySystem::Get().GetKeyName(feature->m_hotkey);
                if (HotkeySystem::Get().IsRecording() && HotkeySystem::Get().GetKeyName(feature->m_hotkey) == "...") {
                    btnLabel = "[...]";
                }

                if (ImGui::Button((btnLabel + "##hk" + feature->m_internalName).c_str(), ImVec2(-1, 0))) {
                    HotkeySystem::Get().StartRecording(&feature->m_hotkey);
                }

                // Render sub-properties
                for (auto& prop : feature->m_properties) {
                    if (!prop->IsVisible()) continue;

                    ImGui::TableNextRow();
                    ImGui::TableNextColumn();
                    ImGui::Indent(20.0f);
                    ImGui::TextDisabled("%s", prop->GetName().c_str());
                    ImGui::Unindent(20.0f);

                    ImGui::TableNextColumn();
                    RenderProperty(prop);

                    ImGui::TableNextColumn(); // Empty for hotkey column
                }
            }
            ImGui::EndTable();
        }
    }

    void RenderProperty(std::shared_ptr<IProperty> prop) {
        std::string id = "##prop" + prop->GetName();
        switch (prop->GetType()) {
        case PropertyType::Bool: {
            auto p = std::static_pointer_cast<Property<bool>>(prop);
            bool val = p->Get();
            if (ImGui::Checkbox(id.c_str(), &val)) p->Set(val);
            break;
        }
        case PropertyType::Int: {
            auto p = std::static_pointer_cast<Property<int>>(prop);
            int val = p->Get();
            if (ImGui::SliderInt(id.c_str(), &val, p->m_min, p->m_max)) p->Set(val);
            break;
        }
        case PropertyType::Float: {
            auto p = std::static_pointer_cast<Property<float>>(prop);
            float val = p->Get();
            if (ImGui::SliderFloat(id.c_str(), &val, p->m_min, p->m_max, "%.2f")) p->Set(val);
            break;
        }
        case PropertyType::Combo: {
            auto p = std::static_pointer_cast<Property<int>>(prop);
            int val = p->Get();
            if (ImGui::Combo(id.c_str(), &val, [](void* data, int idx, const char** out_text) {
                auto items = (std::vector<std::string>*)data;
                if (idx < 0 || idx >= (int)items->size()) return false;
                *out_text = (*items)[idx].c_str();
                return true;
            }, (void*)&p->m_comboItems, (int)p->m_comboItems.size())) {
                p->Set(val);
            }
            break;
        }
        case PropertyType::Color: {
            auto p = std::static_pointer_cast<Property<Color>>(prop);
            Color c = p->Get();
            float col[4] = { c.r, c.g, c.b, c.a };
            if (ImGui::ColorEdit4(id.c_str(), col, ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_AlphaBar)) {
                p->Set(Color(col[0], col[1], col[2], col[3]));
            }
            break;
        }
        }
    }

    void RenderConfigTab() {
        static char profileName[64] = "default";
        ImGui::InputText("Profile Name", profileName, 64);

        if (ImGui::Button("Save Config", ImVec2(120, 30))) {
            ConfigManager::Get().SaveConfig(profileName);
        }
        ImGui::SameLine();
        if (ImGui::Button("Load Config", ImVec2(120, 30))) {
            ConfigManager::Get().LoadConfig(profileName);
        }

        static char exportBuffer[1024] = "";
        if (ImGui::Button("Export to Clip", ImVec2(120, 30))) {
            std::string exported = ConfigManager::Get().ExportConfigString();
            ImGui::SetClipboardText(exported.c_str());
            strncpy(exportBuffer, exported.c_str(), sizeof(exportBuffer)-1);
        }
        ImGui::SameLine();
        if (ImGui::Button("Import from Clip", ImVec2(120, 30))) {
            ConfigManager::Get().ImportConfigString(ImGui::GetClipboardText());
        }

        ImGui::Separator();
        ImGui::Text("Available Profiles:");
        for (auto& name : ConfigManager::Get().GetProfileList()) {
            if (ImGui::Selectable(name.c_str())) {
                strncpy(profileName, name.c_str(), sizeof(profileName)-1);
                profileName[sizeof(profileName)-1] = '\0';
            }
        }
    }

    void RenderLogTab() {
        for (auto& entry : ManagementList::Get().GetLogs()) {
            ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "[%s]", entry.timestamp.c_str());
            ImGui::SameLine();
            ImGui::Text("%s", entry.message.c_str());
        }
    }

    void RenderFooter() {
        auto& logs = ManagementList::Get().GetLogs();
        if (!logs.empty()) {
            ImGui::TextColored(ImVec4(0.3f, 0.3f, 0.3f, 1.0f), "Last action: %s", logs.front().message.c_str());
        }
    }

    void RenderActiveFeatures() {
        // Visual indicators for active features in-game
        ImGui::SetNextWindowPos(ImVec2(10, 10));
        ImGui::Begin("##ActiveFeatures", nullptr, ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoNav);

        ImGui::TextColored(ImVec4(0.35f, 0.65f, 1.0f, 1.0f), "CS2 INTERNAL");
        ImGui::Separator();

        for (auto& feature : FeatureRegistry::Get().GetFeatures()) {
            if (feature->m_enabled) {
                ImGui::Text("%s", feature->m_displayName.c_str());
            }
        }
        ImGui::End();
    }
};
