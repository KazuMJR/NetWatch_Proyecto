using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.API.Services;
using NetWatch.Data;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/metrics")]
public sealed class MetricsController(NetWatchDbContext db, MetricIngestionService service, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous, HttpPost("agent")]
    public async Task<ActionResult<MetricDto>> Ingest(MetricIngestRequest request, CancellationToken ct)
    {
        if (!ValidAgentKey(Request.Headers["X-Agent-Key"].ToString())) return Unauthorized(new { error = "La clave de API del agente no es válida." });
        return Ok((await service.IngestAsync(request, ct)).ToDto());
    }

    [Authorize, HttpGet]
    public async Task<IReadOnlyList<MetricDto>> Get([FromQuery] int? deviceId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        var query = db.Metrics.AsNoTracking().Include(x => x.Device).AsQueryable();
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId);
        if (fromUtc.HasValue) query = query.Where(x => x.RegisteredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.RegisteredAtUtc <= toUtc.Value);
        return await query.OrderByDescending(x => x.RegisteredAtUtc).Take(Math.Clamp(limit, 1, 2000)).Select(x => new MetricDto(x.Id, x.DeviceId, x.Device.Name, x.CpuPercent, x.MemoryPercent, x.DiskPercent, x.TemperatureCelsius, x.NetworkTrafficMbps, x.ResponseTimeMs, x.RegisteredAtUtc)).ToListAsync(ct);
    }

    private bool ValidAgentKey(string supplied)
    {
        var expected = configuration["Security:AgentApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        var a = Encoding.UTF8.GetBytes(expected); var b = Encoding.UTF8.GetBytes(supplied);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
