#include "FeatureRegistry.hpp"

#include "FeatureRegistry.hpp"

bool CFeature::CanEnable() {
    for (const auto& dep : m_dependencies) {
        auto f = FeatureRegistry::Get().FindByName(dep);
        if (f && !f->m_enabled) return false;
    }
    for (const auto& conf : m_conflicts) {
        auto f = FeatureRegistry::Get().FindByName(conf);
        if (f && f->m_enabled) return false;
    }
    return true;
}
