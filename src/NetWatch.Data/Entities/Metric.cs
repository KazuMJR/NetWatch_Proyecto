namespace NetWatch.Data.Entities;

public sealed class Metric
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public decimal CpuPercent { get; set; }
    public decimal MemoryPercent { get; set; }
    public decimal DiskPercent { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public decimal? NetworkTrafficMbps { get; set; }
    public decimal ResponseTimeMs { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public Device Device { get; set; } = null!;
}

