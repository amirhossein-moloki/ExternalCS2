#pragma once
#include <windows.h>
#include <vector>
#include <string>
#include <map>

class HotkeySystem {
public:
    static HotkeySystem& Get() {
        static HotkeySystem instance;
        return instance;
    }

    bool IsKeyDown(int vk) {
        return GetAsyncKeyState(vk) & 0x8000;
    }

    bool WasKeyPressed(int vk) {
        bool isDown = IsKeyDown(vk);
        bool wasDown = m_keyStates[vk];
        m_keyStates[vk] = isDown;
        return isDown && !wasDown;
    }

    void StartRecording(int* target) {
        m_isRecording = true;
        m_recordingTarget = target;
    }

    bool IsRecording() const { return m_isRecording; }

    void Update() {
        if (!m_isRecording) return;

        for (int i = 1; i < 256; i++) {
            if (IsKeyDown(i)) {
                if (i == VK_ESCAPE) {
                    *m_recordingTarget = 0;
                } else {
                    *m_recordingTarget = i;
                }
                m_isRecording = false;
                break;
            }
        }
    }

    static std::string GetKeyName(int vk) {
        if (vk == 0) return "None";
        if (vk >= 'A' && vk <= 'Z') return std::string(1, (char)vk);
        if (vk >= '0' && vk <= '9') return std::string(1, (char)vk);

        switch (vk) {
            case VK_LBUTTON: return "Mouse 1";
            case VK_RBUTTON: return "Mouse 2";
            case VK_MBUTTON: return "Mouse 3";
            case VK_XBUTTON1: return "Mouse 4";
            case VK_XBUTTON2: return "Mouse 5";
            case VK_SHIFT: return "Shift";
            case VK_CONTROL: return "Ctrl";
            case VK_MENU: return "Alt";
            case VK_CAPITAL: return "Caps";
            case VK_ESCAPE: return "Esc";
            case VK_SPACE: return "Space";
            case VK_INSERT: return "Ins";
            case VK_DELETE: return "Del";
            default: return "Key " + std::to_string(vk);
        }
    }

private:
    std::map<int, bool> m_keyStates;
    bool m_isRecording = false;
    int* m_recordingTarget = nullptr;
    HotkeySystem() = default;
};
