#pragma once
#include <filesystem>
#include <string>

struct AgentConfig {
    std::string apiUrl;
    std::string apiKey;
    int deviceId{0};
    int intervalSeconds{60};
    bool verifyTls{true};
    static AgentConfig load(const std::filesystem::path& path);
};

