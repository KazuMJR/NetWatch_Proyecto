namespace NetWatch.Data.Entities;

public sealed class DeviceStateHistory
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public DeviceStatus PreviousStatus { get; set; }
    public DeviceStatus NewStatus { get; set; }
    public string? Description { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public Device Device { get; set; } = null!;
}
