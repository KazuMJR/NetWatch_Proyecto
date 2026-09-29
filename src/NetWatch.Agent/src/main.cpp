#include "Config.hpp"
#include "SystemMetrics.hpp"
#include <chrono>
#include <cmath>
#include <filesystem>
#include <iomanip>
#include <iostream>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <thread>

#ifdef _WIN32
#include <windows.h>
#include <winhttp.h>
#else
#include <curl/curl.h>
#endif

namespace {
std::string number(const std::optional<double>& value){if(!value.has_value()||!std::isfinite(*value))return"null";std::ostringstream s;s<<std::fixed<<std::setprecision(2)<<*value;return s.str();}
std::string number(double value){return number(std::optional<double>(value));}
std::string payload(int deviceId,const SystemMetrics& m,double responseMs){std::ostringstream s;s<<"{\"deviceId\":"<<deviceId<<",\"cpuPercent\":"<<number(m.cpuPercent)<<",\"memoryPercent\":"<<number(m.memoryPercent)<<",\"diskPercent\":"<<number(m.diskPercent)<<",\"temperatureCelsius\":"<<number(m.temperatureCelsius)<<",\"networkTrafficMbps\":"<<number(m.networkTrafficMbps)<<",\"responseTimeMs\":"<<number(responseMs)<<"}";return s.str();}

#ifdef _WIN32
class InternetHandle {
public:
    explicit InternetHandle(HINTERNET value = nullptr) : value_(value) {}
    ~InternetHandle(){if(value_)WinHttpCloseHandle(value_);}
    InternetHandle(const InternetHandle&) = delete;
    InternetHandle& operator=(const InternetHandle&) = delete;
    operator HINTERNET() const{return value_;}
    explicit operator bool() const{return value_ != nullptr;}
private:
    HINTERNET value_;
};

std::wstring widen(const std::string& value){
    if(value.empty())return {};
    const int size=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,value.data(),static_cast<int>(value.size()),nullptr,0);
    if(size<=0)throw std::runtime_error("Invalid UTF-8 text in agent configuration.");
    std::wstring result(static_cast<size_t>(size),L'\0');
    if(MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,value.data(),static_cast<int>(value.size()),result.data(),size)<=0)
        throw std::runtime_error("Cannot convert agent configuration to UTF-16.");
    return result;
}

bool post(const AgentConfig& config,const std::string& json,double& elapsedMs){
    const auto url=widen(config.apiUrl);
    URL_COMPONENTS parts{};
    parts.dwStructSize=sizeof(parts);
    parts.dwSchemeLength=static_cast<DWORD>(-1);
    parts.dwHostNameLength=static_cast<DWORD>(-1);
    parts.dwUrlPathLength=static_cast<DWORD>(-1);
    parts.dwExtraInfoLength=static_cast<DWORD>(-1);
    if(!WinHttpCrackUrl(url.c_str(),0,0,&parts)){
        std::cerr<<"Invalid API URL. WinHTTP error: "<<GetLastError()<<'\n';
        return false;
    }

    const std::wstring host(parts.lpszHostName,parts.dwHostNameLength);
    std::wstring path=parts.dwUrlPathLength?std::wstring(parts.lpszUrlPath,parts.dwUrlPathLength):L"/";
    if(parts.dwExtraInfoLength)path.append(parts.lpszExtraInfo,parts.dwExtraInfoLength);
    const bool secure=parts.nScheme==INTERNET_SCHEME_HTTPS;

    InternetHandle session(WinHttpOpen(L"NetWatch-Agent/1.0",WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY,
        WINHTTP_NO_PROXY_NAME,WINHTTP_NO_PROXY_BYPASS,0));
    if(!session){std::cerr<<"Cannot initialize WinHTTP. Error: "<<GetLastError()<<'\n';return false;}
    WinHttpSetTimeouts(session,15000,15000,15000,15000);

    InternetHandle connection(WinHttpConnect(session,host.c_str(),parts.nPort,0));
    if(!connection){std::cerr<<"Cannot connect to API. WinHTTP error: "<<GetLastError()<<'\n';return false;}
    InternetHandle request(WinHttpOpenRequest(connection,L"POST",path.c_str(),nullptr,WINHTTP_NO_REFERER,
        WINHTTP_DEFAULT_ACCEPT_TYPES,secure?WINHTTP_FLAG_SECURE:0));
    if(!request){std::cerr<<"Cannot create HTTP request. WinHTTP error: "<<GetLastError()<<'\n';return false;}

    if(secure&&!config.verifyTls){
        DWORD flags=SECURITY_FLAG_IGNORE_UNKNOWN_CA|SECURITY_FLAG_IGNORE_CERT_DATE_INVALID|
            SECURITY_FLAG_IGNORE_CERT_CN_INVALID|SECURITY_FLAG_IGNORE_CERT_WRONG_USAGE;
        if(!WinHttpSetOption(request,WINHTTP_OPTION_SECURITY_FLAGS,&flags,sizeof(flags))){
            std::cerr<<"Cannot configure TLS verification. WinHTTP error: "<<GetLastError()<<'\n';
            return false;
        }
    }

    const auto headers=L"Content-Type: application/json\r\nX-Agent-Key: "+widen(config.apiKey);
    const auto started=std::chrono::steady_clock::now();
    const bool sent=WinHttpSendRequest(request,headers.c_str(),static_cast<DWORD>(-1),
        const_cast<char*>(json.data()),static_cast<DWORD>(json.size()),static_cast<DWORD>(json.size()),0)!=FALSE;
    const bool received=sent&&WinHttpReceiveResponse(request,nullptr)!=FALSE;
    elapsedMs=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-started).count();
    if(!received){std::cerr<<"Network error. WinHTTP code: "<<GetLastError()<<'\n';return false;}

    DWORD status=0;
    DWORD statusSize=sizeof(status);
    if(!WinHttpQueryHeaders(request,WINHTTP_QUERY_STATUS_CODE|WINHTTP_QUERY_FLAG_NUMBER,
        WINHTTP_HEADER_NAME_BY_INDEX,&status,&statusSize,WINHTTP_NO_HEADER_INDEX)){
        std::cerr<<"Cannot read HTTP status. WinHTTP error: "<<GetLastError()<<'\n';
        return false;
    }
    if(status<200||status>=300)std::cerr<<"API returned HTTP "<<status<<'\n';
    return status>=200&&status<300;
}
#else
size_t discard(char* data,size_t size,size_t count,void*){return size*count;}
bool post(const AgentConfig& config,const std::string& json,double& elapsedMs){CURL* curl=curl_easy_init();if(!curl)throw std::runtime_error("Cannot initialize libcurl.");curl_slist* headers=nullptr;headers=curl_slist_append(headers,"Content-Type: application/json");headers=curl_slist_append(headers,("X-Agent-Key: "+config.apiKey).c_str());curl_easy_setopt(curl,CURLOPT_URL,config.apiUrl.c_str());curl_easy_setopt(curl,CURLOPT_HTTPHEADER,headers);curl_easy_setopt(curl,CURLOPT_POSTFIELDS,json.c_str());curl_easy_setopt(curl,CURLOPT_TIMEOUT,15L);curl_easy_setopt(curl,CURLOPT_WRITEFUNCTION,discard);curl_easy_setopt(curl,CURLOPT_SSL_VERIFYPEER,config.verifyTls?1L:0L);curl_easy_setopt(curl,CURLOPT_SSL_VERIFYHOST,config.verifyTls?2L:0L);auto code=curl_easy_perform(curl);long http=0;double seconds=0;curl_easy_getinfo(curl,CURLINFO_RESPONSE_CODE,&http);curl_easy_getinfo(curl,CURLINFO_TOTAL_TIME,&seconds);elapsedMs=seconds*1000.0;curl_slist_free_all(headers);curl_easy_cleanup(curl);if(code!=CURLE_OK)std::cerr<<"Network error: "<<curl_easy_strerror(code)<<'\n';else if(http<200||http>=300)std::cerr<<"API returned HTTP "<<http<<'\n';return code==CURLE_OK&&http>=200&&http<300;}
#endif
}

int main(int argc,char** argv){try{const auto path=argc>1?std::filesystem::path(argv[1]):std::filesystem::path("agent.json");const auto config=AgentConfig::load(path);MetricsCollector collector;double lastResponseMs=0;
#ifndef _WIN32
if(curl_global_init(CURL_GLOBAL_DEFAULT)!=CURLE_OK)throw std::runtime_error("Cannot initialize libcurl globally.");
#endif
std::cout<<"NetWatch Agent started for device "<<config.deviceId<<". Interval: "<<config.intervalSeconds<<" seconds.\n";while(true){const auto metrics=collector.collect();const auto json=payload(config.deviceId,metrics,lastResponseMs);if(post(config,json,lastResponseMs))std::cout<<"Metrics sent successfully. API latency: "<<lastResponseMs<<" ms\n";std::this_thread::sleep_for(std::chrono::seconds(config.intervalSeconds));}
#ifndef _WIN32
curl_global_cleanup();
#endif
return 0;}catch(const std::exception& ex){std::cerr<<"Fatal error: "<<ex.what()<<'\n';return 1;}}
