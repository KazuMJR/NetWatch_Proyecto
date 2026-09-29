using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.Data;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/reports"), Authorize]
public sealed class ReportsController(NetWatchDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ReportRowDto>> Get([FromQuery] string type = "metrics", [FromQuery] int? deviceId = null, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken ct = default)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-7); var to = toUtc ?? DateTime.UtcNow;
        return type.ToLowerInvariant() switch
        {
            "events" => await EventRows(deviceId, from, to, ct),
            "alerts" => await AlertRows(deviceId, from, to, ct),
            "states" => await StateRows(deviceId, from, to, ct),
            _ => await MetricRows(deviceId, from, to, ct)
        };
    }

    [HttpGet("csv")]
    public async Task<FileContentResult> Csv([FromQuery] string type = "metrics", [FromQuery] int? deviceId = null, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null, CancellationToken ct = default)
    {
        var rows = await Get(type, deviceId, fromUtc, toUtc, ct);
        var isMetricReport = !new[] { "events", "alerts", "states" }.Contains(type.ToLowerInvariant());
        var csv = new StringBuilder("sep=;\r\n");

        if (isMetricReport)
        {
            csv.AppendLine("Fecha UTC;ID del dispositivo;Dispositivo;CPU (%);RAM (%);Disco (%);Temperatura (\u00b0C);Tráfico (Mbps);Respuesta (ms)");
            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(";", new[]
                {
                    Esc(row.DateUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    row.DeviceId.ToString(CultureInfo.InvariantCulture),
                    Esc(row.DeviceName),
                    Format(row.CpuPercent),
                    Format(row.MemoryPercent),
                    Format(row.DiskPercent),
                    Format(row.TemperatureCelsius),
                    Format(row.NetworkTrafficMbps),
                    Format(row.ResponseTimeMs)
                }));
            }
        }
        else
        {
            csv.AppendLine("Fecha UTC;ID del dispositivo;Dispositivo;Categoría;Detalle");
            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(";", new[]
                {
                    Esc(row.DateUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    row.DeviceId.ToString(CultureInfo.InvariantCulture),
                    Esc(row.DeviceName),
                    Esc(row.Category),
                    Esc(row.Detail)
                }));
            }
        }

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(content, "text/csv; charset=utf-8", $"netwatch-{TypeLabel(type)}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private async Task<IReadOnlyList<ReportRowDto>> MetricRows(int? id, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = db.Metrics.AsNoTracking().Include(x => x.Device).Where(x => x.RegisteredAtUtc >= from && x.RegisteredAtUtc <= to);
        if (id.HasValue) q = q.Where(x => x.DeviceId == id);
        var items = await q.OrderByDescending(x => x.RegisteredAtUtc).ToListAsync(ct);
        return items.Select(x => new ReportRowDto("Métrica", x.DeviceId, x.Device.Name, x.RegisteredAtUtc, "Métricas de rendimiento recopiladas", x.CpuPercent, x.MemoryPercent, x.DiskPercent, x.TemperatureCelsius, x.NetworkTrafficMbps, x.ResponseTimeMs)).ToList();
    }
    private async Task<IReadOnlyList<ReportRowDto>> EventRows(int? id, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = db.Events.AsNoTracking().Include(x => x.Device).Where(x => x.OccurredAtUtc >= from && x.OccurredAtUtc <= to); if (id.HasValue) q = q.Where(x => x.DeviceId == id);
        var items = await q.OrderByDescending(x => x.OccurredAtUtc).ToListAsync(ct);
        return items.Select(x => new ReportRowDto("Evento", x.DeviceId, x.Device.Name, x.OccurredAtUtc, EventTypeLabel(x.EventType) + " | " + SeverityLabel(x.Severity.ToString()) + " | " + TranslateText(x.Description), null, null, null, null, null, null)).ToList();
    }
    private async Task<IReadOnlyList<ReportRowDto>> AlertRows(int? id, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = db.Alerts.AsNoTracking().Include(x => x.Device).Where(x => x.GeneratedAtUtc >= from && x.GeneratedAtUtc <= to); if (id.HasValue) q = q.Where(x => x.DeviceId == id);
        var items = await q.OrderByDescending(x => x.GeneratedAtUtc).ToListAsync(ct);
        return items.Select(x => new ReportRowDto("Alerta", x.DeviceId, x.Device.Name, x.GeneratedAtUtc, TranslateText(x.Title) + " | " + SeverityLabel(x.Level.ToString()) + " | " + AlertStatusLabel(x.Status.ToString()) + " | " + TranslateText(x.Description), null, null, null, null, null, null)).ToList();
    }
    private async Task<IReadOnlyList<ReportRowDto>> StateRows(int? id, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = db.DeviceStateHistory.AsNoTracking().Include(x => x.Device).Where(x => x.ChangedAtUtc >= from && x.ChangedAtUtc <= to); if (id.HasValue) q = q.Where(x => x.DeviceId == id);
        var items = await q.OrderByDescending(x => x.ChangedAtUtc).ToListAsync(ct);
        return items.Select(x => new ReportRowDto("Estado", x.DeviceId, x.Device.Name, x.ChangedAtUtc, StatusLabel(x.PreviousStatus.ToString()) + " → " + StatusLabel(x.NewStatus.ToString()) + " | " + TranslateText(x.Description), null, null, null, null, null, null)).ToList();
    }
    private static string TypeLabel(string type) => type.ToLowerInvariant() switch { "events" => "eventos", "alerts" => "alertas", "states" => "estados", _ => "metricas" };
    private static string StatusLabel(string value) => value switch { "Active" => "Activo", "Inactive" => "Inactivo", "Warning" => "Advertencia", "Disconnected" => "Desconectado", _ => value };
    private static string SeverityLabel(string value) => value switch { "Info" => "Informativa", "Low" => "Baja", "Medium" => "Media", "High" => "Alta", "Warning" => "Advertencia", "Critical" => "Crítica", _ => value };
    private static string AlertStatusLabel(string value) => value switch { "Pending" => "Pendiente", "Attended" => "Atendida", _ => value };
    private static string EventTypeLabel(string value) => value switch { "Connectivity" => "Conectividad", "Performance" => "Rendimiento", "Recovery" => "Recuperación", "Administration" => "Administración", _ => value };
    private static string TranslateText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value
            .Replace("State changed from", "Estado cambiado de", StringComparison.OrdinalIgnoreCase)
            .Replace("No ICMP response after", "Sin respuesta ICMP después de", StringComparison.OrdinalIgnoreCase)
            .Replace("consecutive checks", "comprobaciones consecutivas", StringComparison.OrdinalIgnoreCase)
            .Replace("Threshold exceeded:", "Umbral excedido:", StringComparison.OrdinalIgnoreCase)
            .Replace("Metrics returned to normal values", "Las métricas volvieron a valores normales", StringComparison.OrdinalIgnoreCase)
            .Replace("Performance threshold exceeded", "Umbral de rendimiento excedido", StringComparison.OrdinalIgnoreCase)
            .Replace("Device disconnected", "Dispositivo desconectado", StringComparison.OrdinalIgnoreCase)
            .Replace("ICMP connectivity confirmed", "Conectividad ICMP confirmada", StringComparison.OrdinalIgnoreCase)
            .Replace("memory ", "memoria ", StringComparison.OrdinalIgnoreCase)
            .Replace("disk ", "disco ", StringComparison.OrdinalIgnoreCase)
            .Replace("temperature ", "temperatura ", StringComparison.OrdinalIgnoreCase)
            .Replace("Active", "Activo", StringComparison.OrdinalIgnoreCase)
            .Replace("Inactive", "Inactivo", StringComparison.OrdinalIgnoreCase)
            .Replace("Warning", "Advertencia", StringComparison.OrdinalIgnoreCase)
            .Replace("Disconnected", "Desconectado", StringComparison.OrdinalIgnoreCase)
            .Replace(" to ", " a ", StringComparison.OrdinalIgnoreCase);
    }
    private static string Esc(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    private static string Format(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
