# CS2 Internal Management System: Deployment & Testing Guide

This guide details how to integrate the generated Management List system into your Counter-Strike 2 internal cheat project.

## 1. Prerequisites & Dependencies

### Required Libraries
- **Dear ImGui**: Version 1.89+ (using the `imgui_impl_dx11` and `imgui_impl_win32` backends).
- **nlohmann/json**: For configuration serialization (single-header `json.hpp`).
- **C++ Standard**: C++20 or higher is required (uses `std::filesystem`, `std::variant`, and modern headers).
- **Compiler**: Visual Studio 2022 (v143 toolset) with `/MT` (Static Runtime) and `/std:c++20`.

## 2. Step-by-Step Integration

### Step A: File Setup
1. Copy all `.hpp` and `.cpp` files to your project's source directory.
2. Add them to your Visual Studio Solution.

### Step B: Initialization
In your DLL's main thread or entry point (`DLLMain`'s initialization function):
```cpp
// 1. Setup Config Path
ConfigManager::Get().SetConfigDirectory("C:\\CheatConfigs");

// 2. Register Features
// (Features are auto-registered if using the REGISTER_FEATURE macro at global scope)
```

### Step C: Rendering Hook
In your DirectX 11 `Present` hook:
```cpp
#include "ManagementGUI.hpp"

// Inside Present hook...
ImGui_ImplDX11_NewFrame();
ImGui_ImplWin32_NewFrame();
ImGui::NewFrame();

ManagementGUI::Get().Render(); // This renders the menu and active feature list

ImGui::Render();
ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
```

### Step D: Input Handling
1. **WndProc Hook**: Pass input to ImGui when the menu is open.
```cpp
extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

LRESULT CALLBACK HookedWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (ManagementGUI::Get().m_open && ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam))
        return true; // Block input to game when menu is open

    return CallWindowProc(OriginalWndProc, hWnd, msg, wParam, lParam);
}
```

2. **Hotkey Tick**: In your main cheat loop (or a dedicated thread):
```cpp
#include "HotkeySystem.hpp"

void CheatLoop() {
    while (true) {
        HotkeySystem::Get().Update(); // Process hotkey recording

        // Toggle menu with Insert
        if (HotkeySystem::Get().WasKeyPressed(VK_INSERT)) {
            ManagementGUI::Get().m_open = !ManagementGUI::Get().m_open;
        }

        // Run feature logic
        for (auto& f : FeatureRegistry::Get().GetFeatures()) {
            if (f->m_enabled) f->OnTick();
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
}
```

## 3. Extending the System

### Adding a New Feature
Create a new class inheriting from `CFeature` and use the registration macro:
```cpp
class MyNewFeature : public CFeature {
public:
    MyNewFeature() : CFeature("my_feat", "Super Feature", FeatureCategory::Visuals) {
        m_properties.push_back(std::make_shared<Property<bool>>("Exploit Mode", PropertyType::Bool,
            [this]{ return mode; }, [this](bool v){ mode = v; }));
    }
    void OnTick() override { /* Logic */ }
private:
    bool mode = false;
};
REGISTER_FEATURE(MyNewFeature, "my_feat", "Super Feature", FeatureCategory::Visuals);
```

### Adding New Property Types
1. Update `PropertyType` enum in `FeatureRegistry.hpp`.
2. Add a rendering case in `ManagementGUI::RenderProperty`.
3. Add serialization/deserialization logic in `ConfigManager`.

## 4. Troubleshooting Common Issues

- **Menu Not Showing**: Ensure your `Present` hook is active and `m_open` is true. Verify ImGui context is correctly initialized.
- **Input Lag**: Ensure `HotkeySystem::Update()` is not being called too frequently (e.g., without a small sleep in the loop) or that it's running in a separate high-priority thread.
- **Config Not Saving**: Check write permissions for the directory set in `SetConfigDirectory`.
- **Conflicts**: If using another overlay, ensure you are not clearing the depth stencil buffer after your draw calls.
