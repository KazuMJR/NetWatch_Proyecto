#include "SystemMetrics.hpp"
#include <algorithm>
#include <cctype>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

#ifdef _WIN32
#include <windows.h>
#include <iphlpapi.h>
#else
#include <sys/statvfs.h>
#include <unistd.h>
#endif

namespace {
long long nowMillis() { return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now().time_since_epoch()).count(); }

#ifdef _WIN32
unsigned long long fileTime(const FILETIME& value) { return (static_cast<unsigned long long>(value.dwHighDateTime) << 32) | value.dwLowDateTime; }
double cpuUsage() {
    FILETIME idle1{}, kernel1{}, user1{}, idle2{}, kernel2{}, user2{}; GetSystemTimes(&idle1, &kernel1, &user1);
    std::this_thread::sleep_for(std::chrono::milliseconds(250)); GetSystemTimes(&idle2, &kernel2, &user2);
    const auto idle = fileTime(idle2) - fileTime(idle1); const auto total = (fileTime(kernel2) - fileTime(kernel1)) + (fileTime(user2) - fileTime(user1));
    return total == 0 ? 0 : std::clamp(100.0 * static_cast<double>(total - idle) / static_cast<double>(total), 0.0, 100.0);
}
double memoryUsage() { MEMORYSTATUSEX status{sizeof(status)}; GlobalMemoryStatusEx(&status); return static_cast<double>(status.dwMemoryLoad); }
double diskUsage() { ULARGE_INTEGER freeBytes{}, totalBytes{}, totalFree{}; if (!GetDiskFreeSpaceExW(L"C:\\", &freeBytes, &totalBytes, &totalFree) || totalBytes.QuadPart == 0) return 0; return 100.0 * (1.0 - static_cast<double>(totalFree.QuadPart) / static_cast<double>(totalBytes.QuadPart)); }
unsigned long long networkBytes() {
    ULONG size=0;
    if(GetIfTable(nullptr,&size,FALSE)!=ERROR_INSUFFICIENT_BUFFER||size==0)return 0;
    std::vector<unsigned char> buffer(size);
    auto* table=reinterpret_cast<PMIB_IFTABLE>(buffer.data());
    if(GetIfTable(table,&size,FALSE)!=NO_ERROR)return 0;
    unsigned long long bytes=0;
    for(DWORD i=0;i<table->dwNumEntries;i++){
        const auto& row=table->table[i];
        if(row.dwType!=IF_TYPE_SOFTWARE_LOOPBACK&&row.dwOperStatus==MIB_IF_OPER_STATUS_OPERATIONAL)
            bytes+=static_cast<unsigned long long>(row.dwInOctets)+row.dwOutOctets;
    }
    return bytes;
}
std::optional<double> temperature() { return std::nullopt; }
#else
struct CpuTimes { unsigned long long idle{}, total{}; };
CpuTimes readCpu(){std::ifstream f("/proc/stat");std::string label;unsigned long long user,nice,system,idle,iowait,irq,softirq,steal;f>>label>>user>>nice>>system>>idle>>iowait>>irq>>softirq>>steal;return{idle+iowait,user+nice+system+idle+iowait+irq+softirq+steal};}
double cpuUsage(){auto a=readCpu();std::this_thread::sleep_for(std::chrono::milliseconds(250));auto b=readCpu();auto total=b.total-a.total;return total?std::clamp(100.0*(1.0-static_cast<double>(b.idle-a.idle)/total),0.0,100.0):0;}
double memoryUsage(){std::ifstream f("/proc/meminfo");std::string key,unit;unsigned long long value,total=0,available=0;while(f>>key>>value>>unit){if(key=="MemTotal:")total=value;else if(key=="MemAvailable:")available=value;}return total?100.0*(1.0-static_cast<double>(available)/total):0;}
double diskUsage(){struct statvfs s{};if(statvfs("/",&s)!=0||s.f_blocks==0)return 0;return 100.0*(1.0-static_cast<double>(s.f_bavail)/s.f_blocks);}
unsigned long long networkBytes(){std::ifstream f("/proc/net/dev");std::string line;unsigned long long total=0;while(std::getline(f,line)){auto colon=line.find(':');if(colon==std::string::npos)continue;auto name=line.substr(0,colon);name.erase(std::remove_if(name.begin(),name.end(),[](unsigned char c){return std::isspace(c);}),name.end());if(name=="lo")continue;std::istringstream s(line.substr(colon+1));unsigned long long rx=0,tx=0,drop;s>>rx;for(int i=0;i<7;i++)s>>drop;s>>tx;total+=rx+tx;}return total;}
std::optional<double> temperature(){
    std::error_code error;
    const std::filesystem::directory_iterator end;
    for(std::filesystem::directory_iterator entry("/sys/class/thermal", error); !error && entry != end; entry.increment(error)){
        auto path=entry->path()/"temp"; std::ifstream f(path); double millidegrees;
        if(f>>millidegrees)return millidegrees/1000.0;
    }
    return std::nullopt;
}
#endif
}

SystemMetrics MetricsCollector::collect() {
    SystemMetrics result; result.cpuPercent=cpuUsage(); result.memoryPercent=memoryUsage(); result.diskPercent=diskUsage(); result.temperatureCelsius=temperature();
    const auto bytes=networkBytes(); const auto current=nowMillis();
    if(previousNetworkBytes_>0&&bytes>=previousNetworkBytes_&&current>previousNetworkMillis_) result.networkTrafficMbps=(bytes-previousNetworkBytes_)*8.0/((current-previousNetworkMillis_)/1000.0)/1'000'000.0;
    previousNetworkBytes_=bytes; previousNetworkMillis_=current; return result;
}
