namespace NetWatch.Data.Entities;

public sealed class Alert
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public AlertLevel Level { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Pending;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AttendedAtUtc { get; set; }
    public int? AttendedByUserId { get; set; }
    public Device Device { get; set; } = null!;
}

