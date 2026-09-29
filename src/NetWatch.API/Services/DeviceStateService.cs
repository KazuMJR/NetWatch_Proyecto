using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetWatch.API.Options;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Services;

public sealed class DeviceStateService(NetWatchDbContext db, IOptions<MonitoringOptions> options)
{
    private readonly MonitoringOptions _options = options.Value;

    public async Task ProcessPingResultAsync(Device device, bool success, decimal? responseTimeMs, CancellationToken cancellationToken = default)
    {
        if (!device.IsActive || !device.MonitoringEnabled) return;
        if (success)
        {
            device.ConsecutivePingFailures = 0;
            device.LastPingAtUtc = DateTime.UtcNow;
            device.LastResponseTimeMs = responseTimeMs;
            var lastMetric = await db.Metrics.Where(x => x.DeviceId == device.Id).OrderByDescending(x => x.RegisteredAtUtc).FirstOrDefaultAsync(cancellationToken);
            var target = lastMetric is not null && ExceedsThreshold(lastMetric) ? DeviceStatus.Warning : DeviceStatus.Active;
            await TransitionAsync(device, target, target == DeviceStatus.Active ? "Conectividad ICMP confirmada" : "Conectividad ICMP confirmada; continúa excedido un umbral de rendimiento", cancellationToken);
        }
        else
        {
            device.ConsecutivePingFailures++;
            if (device.ConsecutivePingFailures >= _options.FailuresBeforeDisconnected)
                await TransitionAsync(device, DeviceStatus.Disconnected, $"Sin respuesta ICMP después de {device.ConsecutivePingFailures} comprobaciones consecutivas", cancellationToken);
            else
                await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ProcessMetricAsync(Device device, Metric metric, CancellationToken cancellationToken = default)
    {
        device.ConsecutivePingFailures = 0;
        var warning = ExceedsThreshold(metric);
        var target = warning ? DeviceStatus.Warning : DeviceStatus.Active;
        var reason = warning ? WarningReason(metric) : "Las métricas volvieron a valores normales";
        await TransitionAsync(device, target, reason, cancellationToken);
    }

    public Task SetInactiveAsync(Device device, string reason, CancellationToken cancellationToken = default) =>
        TransitionAsync(device, DeviceStatus.Inactive, reason, cancellationToken);

    public bool ExceedsThreshold(Metric metric) => metric.CpuPercent >= _options.CpuWarningPercent
        || metric.MemoryPercent >= _options.MemoryWarningPercent
        || metric.DiskPercent >= _options.DiskWarningPercent
        || metric.TemperatureCelsius >= _options.TemperatureWarningCelsius;

    private string WarningReason(Metric metric)
    {
        var values = new List<string>();
        if (metric.CpuPercent >= _options.CpuWarningPercent) values.Add($"CPU {metric.CpuPercent:0.##}%");
        if (metric.MemoryPercent >= _options.MemoryWarningPercent) values.Add($"memoria {metric.MemoryPercent:0.##}%");
        if (metric.DiskPercent >= _options.DiskWarningPercent) values.Add($"disco {metric.DiskPercent:0.##}%");
        if (metric.TemperatureCelsius >= _options.TemperatureWarningCelsius) values.Add($"temperatura {metric.TemperatureCelsius:0.##} C");
        return "Umbral excedido: " + string.Join(", ", values);
    }

    private async Task TransitionAsync(Device device, DeviceStatus target, string reason, CancellationToken cancellationToken)
    {
        if (device.Status == target)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var previous = device.Status;
        device.Status = target;
        var now = DateTime.UtcNow;
        db.DeviceStateHistory.Add(new DeviceStateHistory { DeviceId = device.Id, PreviousStatus = previous, NewStatus = target, Description = reason, ChangedAtUtc = now });
        db.Events.Add(new NetworkEvent
        {
            DeviceId = device.Id,
            EventType = target switch { DeviceStatus.Disconnected => "Conectividad", DeviceStatus.Warning => "Rendimiento", DeviceStatus.Active => "Recuperación", _ => "Administración" },
            Description = $"Estado cambiado de {StatusLabel(previous)} a {StatusLabel(target)}. {reason}",
            Severity = target switch { DeviceStatus.Disconnected => EventSeverity.Critical, DeviceStatus.Warning => EventSeverity.High, DeviceStatus.Active => EventSeverity.Low, _ => EventSeverity.Low },
            OccurredAtUtc = now
        });

        if (target is DeviceStatus.Warning or DeviceStatus.Disconnected)
        {
            var title = target == DeviceStatus.Warning ? "Umbral de rendimiento excedido" : "Dispositivo desconectado";
            var pendingExists = await db.Alerts.AnyAsync(x => x.DeviceId == device.Id && x.Title == title && x.Status == AlertStatus.Pending, cancellationToken);
            if (!pendingExists)
            {
                db.Alerts.Add(new Alert
                {
                    DeviceId = device.Id, Title = title, Description = reason,
                    Level = target == DeviceStatus.Disconnected ? AlertLevel.Critical : AlertLevel.Warning,
                    Status = AlertStatus.Pending, GeneratedAtUtc = now
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string StatusLabel(DeviceStatus status) => status switch
    {
        DeviceStatus.Active => "Activo",
        DeviceStatus.Inactive => "Inactivo",
        DeviceStatus.Warning => "Advertencia",
        DeviceStatus.Disconnected => "Desconectado",
        _ => status.ToString()
    };
}
