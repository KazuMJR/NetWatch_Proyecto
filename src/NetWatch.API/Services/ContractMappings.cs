using NetWatch.API.Contracts;
using NetWatch.Data.Entities;

namespace NetWatch.API.Services;

public static class ContractMappings
{
    public static UserDto ToDto(this User x) => new(x.Id, x.FirstName, x.LastName, x.Email, x.Username, x.Role.Name, x.IsActive, x.RegisteredAtUtc);
    public static DeviceDto ToDto(this Device x) => new(x.Id, x.DeviceTypeId, x.DeviceType.Name, x.Name, x.IpAddress, x.MacAddress, x.Manufacturer, x.Model, x.OperatingSystem, x.Location, x.Status, x.Description, x.RegisteredAtUtc, x.LastPingAtUtc, x.LastResponseTimeMs, x.SubnetMask, x.Gateway, x.PrimaryDns, x.SecondaryDns, x.IsActive, x.MonitoringEnabled, x.ConsecutivePingFailures);
    public static MetricDto ToDto(this Metric x) => new(x.Id, x.DeviceId, x.Device.Name, x.CpuPercent, x.MemoryPercent, x.DiskPercent, x.TemperatureCelsius, x.NetworkTrafficMbps, x.ResponseTimeMs, x.RegisteredAtUtc);
    public static EventDto ToDto(this NetworkEvent x) => new(x.Id, x.DeviceId, x.Device.Name, x.EventType, x.Description, x.Severity, x.OccurredAtUtc);
    public static AlertDto ToDto(this Alert x) => new(x.Id, x.DeviceId, x.Device.Name, x.Title, x.Description, x.Level, x.Status, x.GeneratedAtUtc, x.AttendedAtUtc);
}

