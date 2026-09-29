namespace NetWatch.Data.Entities;

public enum DeviceStatus { Inactive, Active, Warning, Disconnected }
public enum EventSeverity { Low, Medium, High, Critical }
public enum AlertLevel { Info, Warning, Critical }
public enum AlertStatus { Pending, Attended }

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string Technician = "Technician";
}

