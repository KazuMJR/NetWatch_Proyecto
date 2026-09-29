using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Services;

public sealed class MetricIngestionService(NetWatchDbContext db, DeviceStateService stateService)
{
    public async Task<Metric> IngestAsync(MetricIngestRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePercent(request.CpuPercent, nameof(request.CpuPercent));
        ValidatePercent(request.MemoryPercent, nameof(request.MemoryPercent));
        ValidatePercent(request.DiskPercent, nameof(request.DiskPercent));
        if (request.TemperatureCelsius is < -100 or > 250) throw new ArgumentException("La temperatura está fuera del rango aceptado.");
        if (request.NetworkTrafficMbps < 0) throw new ArgumentException("El tráfico de red no puede ser negativo.");
        if (request.ResponseTimeMs < 0) throw new ArgumentException("El tiempo de respuesta no puede ser negativo.");
        var device = await db.Devices.Include(x => x.DeviceType).SingleOrDefaultAsync(x => x.Id == request.DeviceId && x.IsActive && x.MonitoringEnabled, cancellationToken)
            ?? throw new KeyNotFoundException("No se encontró un dispositivo activo con monitoreo habilitado.");
        var metric = new Metric
        {
            DeviceId = request.DeviceId, CpuPercent = request.CpuPercent, MemoryPercent = request.MemoryPercent,
            DiskPercent = request.DiskPercent, TemperatureCelsius = request.TemperatureCelsius,
            NetworkTrafficMbps = request.NetworkTrafficMbps, ResponseTimeMs = request.ResponseTimeMs,
            RegisteredAtUtc = DateTime.UtcNow, Device = device
        };
        db.Metrics.Add(metric);
        await stateService.ProcessMetricAsync(device, metric, cancellationToken);
        return metric;
    }

    private static void ValidatePercent(decimal value, string name)
    {
        if (value is < 0 or > 100) throw new ArgumentException($"{name} debe estar entre 0 y 100.");
    }
}
