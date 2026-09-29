namespace NetWatch.Data.Entities;

public sealed class NetworkEvent
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public required string EventType { get; set; }
    public required string Description { get; set; }
    public EventSeverity Severity { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Device Device { get; set; } = null!;
}

