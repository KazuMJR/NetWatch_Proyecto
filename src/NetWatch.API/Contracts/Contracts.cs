using System.ComponentModel.DataAnnotations;
using NetWatch.Data.Entities;

namespace NetWatch.API.Contracts;

public sealed record LoginRequest(
    [Required(ErrorMessage = "El usuario es obligatorio.")] string Username,
    [Required(ErrorMessage = "La contraseña es obligatoria.")] string Password);
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, int UserId, string FullName, string Username, string Role);

public sealed record UserDto(int Id, string FirstName, string LastName, string Email, string Username, string Role, bool IsActive, DateTime RegisteredAtUtc);
public sealed record CreateUserRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")] string FirstName,
    [Required(ErrorMessage = "El apellido es obligatorio."), MaxLength(100, ErrorMessage = "El apellido no puede superar 100 caracteres.")] string LastName,
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingrese un correo válido."), MaxLength(120, ErrorMessage = "El correo no puede superar 120 caracteres.")] string Email,
    [Required(ErrorMessage = "El nombre de usuario es obligatorio."), MaxLength(50, ErrorMessage = "El nombre de usuario no puede superar 50 caracteres.")] string Username,
    [Required(ErrorMessage = "La contraseña es obligatoria."), MinLength(10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres."), MaxLength(128, ErrorMessage = "La contraseña no puede superar 128 caracteres.")] string Password,
    [Required(ErrorMessage = "El rol es obligatorio.")] string Role);
public sealed record UpdateUserRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")] string FirstName,
    [Required(ErrorMessage = "El apellido es obligatorio."), MaxLength(100, ErrorMessage = "El apellido no puede superar 100 caracteres.")] string LastName,
    [Required(ErrorMessage = "El nombre de usuario es obligatorio."), MaxLength(50, ErrorMessage = "El nombre de usuario no puede superar 50 caracteres.")] string Username,
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingrese un correo válido."), MaxLength(120, ErrorMessage = "El correo no puede superar 120 caracteres.")] string Email,
    [Required(ErrorMessage = "El rol es obligatorio.")] string Role,
    bool IsActive,
    [MinLength(10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres."), MaxLength(128, ErrorMessage = "La contraseña no puede superar 128 caracteres.")] string? NewPassword);

public sealed record DeviceTypeDto(int Id, string Name, string? Description);
public sealed record DeviceDto(
    int Id, int DeviceTypeId, string DeviceType, string Name, string IpAddress, string? MacAddress,
    string? Manufacturer, string? Model, string? OperatingSystem, string? Location, DeviceStatus Status,
    string? Description, DateTime RegisteredAtUtc, DateTime? LastPingAtUtc, decimal? LastResponseTimeMs,
    string SubnetMask, string? Gateway, string? PrimaryDns, string? SecondaryDns,
    bool IsActive, bool MonitoringEnabled, int ConsecutivePingFailures);

public sealed record DeviceUpsertRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tipo de dispositivo.")] int DeviceTypeId,
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")] string Name,
    [Required(ErrorMessage = "La dirección IP es obligatoria."), MaxLength(45, ErrorMessage = "La dirección IP no puede superar 45 caracteres.")] string IpAddress,
    [MaxLength(20)] string? MacAddress,
    [MaxLength(50)] string? Manufacturer,
    [MaxLength(100)] string? Model,
    [MaxLength(100)] string? OperatingSystem,
    [MaxLength(100)] string? Location,
    [MaxLength(1000)] string? Description,
    [Required(ErrorMessage = "La máscara o prefijo es obligatorio."), MaxLength(45, ErrorMessage = "La máscara o prefijo no puede superar 45 caracteres.")] string SubnetMask,
    [MaxLength(45)] string? Gateway,
    [MaxLength(45)] string? PrimaryDns,
    [MaxLength(45)] string? SecondaryDns,
    bool MonitoringEnabled);

public sealed record MetricIngestRequest(
    [Range(1, int.MaxValue, ErrorMessage = "El identificador del dispositivo no es válido.")] int DeviceId,
    decimal CpuPercent,
    decimal MemoryPercent,
    decimal DiskPercent,
    decimal? TemperatureCelsius,
    decimal? NetworkTrafficMbps,
    decimal ResponseTimeMs);
public sealed record MetricDto(long Id, int DeviceId, string DeviceName, decimal CpuPercent, decimal MemoryPercent, decimal DiskPercent, decimal? TemperatureCelsius, decimal? NetworkTrafficMbps, decimal ResponseTimeMs, DateTime RegisteredAtUtc);
public sealed record EventDto(long Id, int DeviceId, string DeviceName, string EventType, string Description, EventSeverity Severity, DateTime OccurredAtUtc);
public sealed record AlertDto(long Id, int DeviceId, string DeviceName, string Title, string Description, AlertLevel Level, AlertStatus Status, DateTime GeneratedAtUtc, DateTime? AttendedAtUtc);
public sealed record StateHistoryDto(long Id, int DeviceId, DeviceStatus PreviousStatus, DeviceStatus NewStatus, string? Description, DateTime ChangedAtUtc);
public sealed record DashboardDto(int Total, int Active, int Warning, int Disconnected, int Inactive, IReadOnlyList<AlertDto> RecentAlerts, IReadOnlyList<MetricDto> RecentMetrics);
public sealed record ReportRowDto(string Category, int DeviceId, string DeviceName, DateTime DateUtc, string Detail, decimal? Value1, decimal? Value2, decimal? Value3);
