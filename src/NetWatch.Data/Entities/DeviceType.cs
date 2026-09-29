namespace NetWatch.Data.Entities;

public sealed class DeviceType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}

