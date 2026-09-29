namespace NetWatch.Data.Entities;

public sealed class Device
{
    public int Id { get; set; }
    public int DeviceTypeId { get; set; }
    public required string Name { get; set; }
    public required string IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Inactive;
    public string? Description { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPingAtUtc { get; set; }
    public decimal? LastResponseTimeMs { get; set; }
    public required string SubnetMask { get; set; }
    public string? Gateway { get; set; }
    public string? PrimaryDns { get; set; }
    public string? SecondaryDns { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MonitoringEnabled { get; set; }
    public int ConsecutivePingFailures { get; set; }
    public DeviceType DeviceType { get; set; } = null!;
    public ICollection<Metric> Metrics { get; set; } = new List<Metric>();
    public ICollection<NetworkEvent> Events { get; set; } = new List<NetworkEvent>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<DeviceStateHistory> StateHistory { get; set; } = new List<DeviceStateHistory>();
}

