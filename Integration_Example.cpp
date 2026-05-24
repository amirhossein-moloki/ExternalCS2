#include <windows.h>
#include <imgui.h>
#include <imgui_impl_dx11.h>
#include <imgui_impl_win32.h>
#include "ManagementGUI.hpp"
#include "HotkeySystem.hpp"
#include "FeatureRegistry.hpp"

/*
 * INTEGRATION GUIDE
 *
 * 1. DLLMain / Initialization:
 *    - Call ConfigManager::Get().SetConfigDirectory("C:\\Path\\To\\Configs");
 *    - Initialize your D3D11 device and context.
 *    - Initialize ImGui for Win32 and DX11.
 *
 * 2. Main Cheat Loop:
 *    - HotkeySystem::Get().Update();
 *    - if (HotkeySystem::Get().WasKeyPressed(VK_INSERT)) ManagementGUI::Get().m_open = !ManagementGUI::Get().m_open;
 *    - for (auto& f : FeatureRegistry::Get().GetFeatures()) { if (f->m_enabled) f->OnTick(); }
 *
 * 3. DX11 Present Hook:
 *    - ImGui_ImplDX11_NewFrame();
 *    - ImGui_ImplWin32_NewFrame();
 *    - ImGui::NewFrame();
 *    - ManagementGUI::Get().Render();
 *    - ImGui::Render();
 *    - ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
 *
 * 4. Window Procedure (WndProc) Hook:
 *    - Extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
 *    - if (ManagementGUI::Get().m_open && ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam)) return true;
 */

// Example Feature Implementation
class CAimbot : public CFeature {
public:
    CAimbot() : CFeature("aimbot", "Legit Aimbot", FeatureCategory::Aimbot) {
        m_enabled = false;
        m_hotkey = VK_LBUTTON;

        // Add properties
        auto fov = std::make_shared<Property<float>>("FOV", PropertyType::Float,
            [this]() { return m_fov; },
            [this](float v) { m_fov = v; }, 0.0f, 180.0f);
        m_properties.push_back(fov);

        auto smooth = std::make_shared<Property<float>>("Smoothing", PropertyType::Float,
            [this]() { return m_smoothing; },
            [this](float v) { m_smoothing = v; }, 1.0f, 20.0f);
        m_properties.push_back(smooth);
    }

    void OnTick() override {
        // Aimbot logic here
    }

private:
    float m_fov = 10.0f;
    float m_smoothing = 5.0f;
};

// Auto-register the example feature
REGISTER_FEATURE(CAimbot, "aimbot", "Legit Aimbot", FeatureCategory::Aimbot);
