using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetWatch.API.Options;
using NetWatch.API.Services;
using NetWatch.Data;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/diagnostics"), Authorize]
public sealed class MonitoringController(NetWatchDbContext db, DeviceStateService stateService, IOptions<MonitoringOptions> options) : ControllerBase
{
    [HttpPost("ping/{deviceId:int}")]
    public async Task<ActionResult> PingDevice(int deviceId, CancellationToken ct)
    {
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == deviceId && x.IsActive, ct) ?? throw new KeyNotFoundException("No se encontró un dispositivo activo.");
        using var ping = new Ping(); var watch = Stopwatch.StartNew();
        try
        {
            var reply = await ping.SendPingAsync(device.IpAddress, options.Value.PingTimeoutMilliseconds);
            watch.Stop(); var success = reply.Status == IPStatus.Success;
            if (device.MonitoringEnabled) await stateService.ProcessPingResultAsync(device, success, success ? (decimal)watch.Elapsed.TotalMilliseconds : null, ct);
            return Ok(new { deviceId, success, status = reply.Status.ToString(), responseTimeMs = success ? watch.Elapsed.TotalMilliseconds : (double?)null });
        }
        catch (PingException)
        {
            if (device.MonitoringEnabled) await stateService.ProcessPingResultAsync(device, false, null, ct);
            return Ok(new { deviceId, success = false, status = "PingError", responseTimeMs = (double?)null });
        }
    }
}
