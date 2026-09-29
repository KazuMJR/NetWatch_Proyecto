#include "Config.hpp"
#include <fstream>
#include <regex>
#include <sstream>
#include <stdexcept>

namespace {
std::string readAll(const std::filesystem::path& path) {
    std::ifstream file(path);
    if (!file) throw std::runtime_error("Cannot open configuration file: " + path.string());
    std::ostringstream buffer; buffer << file.rdbuf(); return buffer.str();
}
std::string stringValue(const std::string& json, const std::string& key) {
    std::regex pattern("\\\"" + key + "\\\"\\s*:\\s*\\\"([^\\\"]*)\\\""); std::smatch match;
    if (!std::regex_search(json, match, pattern)) throw std::runtime_error("Missing configuration value: " + key);
    return match[1].str();
}
int intValue(const std::string& json, const std::string& key) {
    std::regex pattern("\\\"" + key + "\\\"\\s*:\\s*([0-9]+)"); std::smatch match;
    if (!std::regex_search(json, match, pattern)) throw std::runtime_error("Missing numeric configuration value: " + key);
    return std::stoi(match[1].str());
}
bool boolValue(const std::string& json, const std::string& key, bool fallback) {
    std::regex pattern("\\\"" + key + "\\\"\\s*:\\s*(true|false)"); std::smatch match;
    return std::regex_search(json, match, pattern) ? match[1].str() == "true" : fallback;
}
}

AgentConfig AgentConfig::load(const std::filesystem::path& path) {
    const auto json = readAll(path); AgentConfig result;
    result.apiUrl = stringValue(json, "apiUrl"); result.apiKey = stringValue(json, "apiKey");
    result.deviceId = intValue(json, "deviceId"); result.intervalSeconds = intValue(json, "intervalSeconds");
    result.verifyTls = boolValue(json, "verifyTls", true);
    if (result.deviceId <= 0 || result.intervalSeconds < 10 || result.apiUrl.empty() || result.apiKey.empty()) throw std::runtime_error("Invalid agent configuration.");
    return result;
}

