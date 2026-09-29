#pragma once
#include <optional>

struct SystemMetrics {
    double cpuPercent{0};
    double memoryPercent{0};
    double diskPercent{0};
    std::optional<double> temperatureCelsius;
    std::optional<double> networkTrafficMbps;
};

class MetricsCollector {
public:
    SystemMetrics collect();
private:
    unsigned long long previousNetworkBytes_{0};
    long long previousNetworkMillis_{0};
};

