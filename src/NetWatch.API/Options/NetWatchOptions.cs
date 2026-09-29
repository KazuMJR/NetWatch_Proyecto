namespace NetWatch.API.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "NetWatch.API";
    public string Audience { get; set; } = "NetWatch.Clients";
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 480;
}

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";
    public int PingIntervalSeconds { get; set; } = 30;
    public int PingTimeoutMilliseconds { get; set; } = 2000;
    public int FailuresBeforeDisconnected { get; set; } = 3;
    public decimal CpuWarningPercent { get; set; } = 85;
    public decimal MemoryWarningPercent { get; set; } = 85;
    public decimal DiskWarningPercent { get; set; } = 90;
    public decimal TemperatureWarningCelsius { get; set; } = 80;
}
