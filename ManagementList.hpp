#include "FeatureRegistry.hpp"
#include <deque>
#include <chrono>
#include <iomanip>
#include <sstream>

struct LogEntry {
    std::string timestamp;
    std::string message;
};

class ManagementList {
public:
    static ManagementList& Get() {
        static ManagementList instance;
        return instance;
    }

    void AddLog(const std::string& message) {
        auto now = std::chrono::system_clock::now();
        auto in_time_t = std::chrono::system_clock::to_time_t(now);

        struct tm time_info;
        localtime_s(&time_info, &in_time_t);

        std::stringstream ss;
        ss << std::put_time(&time_info, "%H:%M:%S");

        m_logs.push_front({ ss.str(), message });
        if (m_logs.size() > 10) m_logs.pop_back();
    }

    const std::deque<LogEntry>& GetLogs() const {
        return m_logs;
    }

    // Toggle a feature and log the change
    void ToggleFeature(std::shared_ptr<CFeature> feature) {
        feature->m_enabled = !feature->m_enabled;
        if (feature->m_enabled) feature->OnEnable();
        else feature->OnDisable();

        std::string status = feature->m_enabled ? "Enabled" : "Disabled";
        AddLog(feature->m_displayName + " " + status);
    }

    // Dependency check helper
    bool IsFeatureActive(const std::string& internalName) {
        auto f = FeatureRegistry::Get().FindByName(internalName);
        return f && f->m_enabled;
    }

private:
    std::deque<LogEntry> m_logs;
    ManagementList() = default;
};
