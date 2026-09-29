using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public sealed class DashboardController(NetWatchDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<DashboardDto> Get(CancellationToken ct)
    {
        var statuses = await db.Devices.AsNoTracking().Where(x => x.IsActive).GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var alerts = await db.Alerts.AsNoTracking().Include(x => x.Device).Where(x => x.Status == AlertStatus.Pending).OrderByDescending(x => x.GeneratedAtUtc).Take(10).Select(x => new AlertDto(x.Id, x.DeviceId, x.Device.Name, x.Title, x.Description, x.Level, x.Status, x.GeneratedAtUtc, x.AttendedAtUtc)).ToListAsync(ct);
        var metrics = await db.Metrics.AsNoTracking().Include(x => x.Device).OrderByDescending(x => x.RegisteredAtUtc).Take(10).Select(x => new MetricDto(x.Id, x.DeviceId, x.Device.Name, x.CpuPercent, x.MemoryPercent, x.DiskPercent, x.TemperatureCelsius, x.NetworkTrafficMbps, x.ResponseTimeMs, x.RegisteredAtUtc)).ToListAsync(ct);
        var total = statuses.Values.Sum();
        return new DashboardDto(total, statuses.GetValueOrDefault(DeviceStatus.Active), statuses.GetValueOrDefault(DeviceStatus.Warning), statuses.GetValueOrDefault(DeviceStatus.Disconnected), statuses.GetValueOrDefault(DeviceStatus.Inactive), alerts, metrics);
    }
}

