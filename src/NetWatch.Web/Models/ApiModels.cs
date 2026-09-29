using System.ComponentModel.DataAnnotations;

namespace NetWatch.Web.Models;

public sealed class LoginModel
{
    [Required(ErrorMessage = "El usuario es obligatorio.")] public string Username { get; set; } = "";
    [Required(ErrorMessage = "La contraseña es obligatoria.")] public string Password { get; set; } = "";
}
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, int UserId, string FullName, string Username, string Role);
public sealed record UserDto(int Id, string FirstName, string LastName, string Email, string Username, string Role, bool IsActive, DateTime RegisteredAtUtc);
public sealed class UserForm
{
    [Required(ErrorMessage = "El nombre es obligatorio.")] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "El apellido es obligatorio.")] public string LastName { get; set; } = "";
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingrese un correo válido.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")] public string Username { get; set; } = "";
    [Required(ErrorMessage = "La contraseña es obligatoria."), MinLength(10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")] public string Password { get; set; } = "";
    public string Role { get; set; } = "Technician";
}
public sealed class UserEditForm
{
    [Required(ErrorMessage = "El nombre es obligatorio.")] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "El apellido es obligatorio.")] public string LastName { get; set; } = "";
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")] public string Username { get; set; } = "";
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingrese un correo válido.")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "El rol es obligatorio.")] public string Role { get; set; } = "Technician";
    public bool IsActive { get; set; }
    [MinLength(10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")] public string? NewPassword { get; set; }
}
public sealed record DeviceTypeDto(int Id, string Name, string? Description);
public sealed record DeviceDto(int Id, int DeviceTypeId, string DeviceType, string Name, string IpAddress, string? MacAddress, string? Manufacturer, string? Model, string? OperatingSystem, string? Location, string Status, string? Description, DateTime RegisteredAtUtc, DateTime? LastPingAtUtc, decimal? LastResponseTimeMs, string SubnetMask, string? Gateway, string? PrimaryDns, string? SecondaryDns, bool IsActive, bool MonitoringEnabled, int ConsecutivePingFailures);
public sealed class DeviceForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tipo de dispositivo.")] public int DeviceTypeId { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio.")] public string Name { get; set; } = "";
    [Required(ErrorMessage = "La dirección IP es obligatoria.")] public string IpAddress { get; set; } = "";
    public string? MacAddress { get; set; } public string? Manufacturer { get; set; } public string? Model { get; set; }
    public string? OperatingSystem { get; set; } public string? Location { get; set; } public string? Description { get; set; }
    [Required(ErrorMessage = "La máscara o prefijo es obligatorio.")] public string SubnetMask { get; set; } = "255.255.255.0"; public string? Gateway { get; set; }
    public string? PrimaryDns { get; set; } public string? SecondaryDns { get; set; } public bool MonitoringEnabled { get; set; }
}
public sealed record MetricDto(long Id, int DeviceId, string DeviceName, decimal CpuPercent, decimal MemoryPercent, decimal DiskPercent, decimal? TemperatureCelsius, decimal? NetworkTrafficMbps, decimal ResponseTimeMs, DateTime RegisteredAtUtc);
public sealed record EventDto(long Id, int DeviceId, string DeviceName, string EventType, string Description, string Severity, DateTime OccurredAtUtc);
public sealed record AlertDto(long Id, int DeviceId, string DeviceName, string Title, string Description, string Level, string Status, DateTime GeneratedAtUtc, DateTime? AttendedAtUtc);
public sealed record StateHistoryDto(long Id, int DeviceId, string PreviousStatus, string NewStatus, string? Description, DateTime ChangedAtUtc);
public sealed record DashboardDto(int Total, int Active, int Warning, int Disconnected, int Inactive, IReadOnlyList<AlertDto> RecentAlerts, IReadOnlyList<MetricDto> RecentMetrics);
public sealed record ReportRowDto(string Category, int DeviceId, string DeviceName, DateTime DateUtc, string Detail, decimal? Value1, decimal? Value2, decimal? Value3);
