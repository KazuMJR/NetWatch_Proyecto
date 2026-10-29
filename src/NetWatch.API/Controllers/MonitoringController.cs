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
        var result = await ProbeAsync(device.IpAddress, ct);
        if (device.MonitoringEnabled) await stateService.ProcessPingResultAsync(device, result.Success, result.ResponseTimeMs, ct);
        return Ok(new { deviceId, result.Success, result.Status, result.ResponseTimeMs });
    }

    [HttpPost("ping-all")]
    public async Task<ActionResult> PingAll(CancellationToken ct)
    {
        var devices = await db.Devices.Where(x => x.IsActive && x.MonitoringEnabled).OrderBy(x => x.Name).ToListAsync(ct);
        var attempts = Math.Max(1, options.Value.FailuresBeforeDisconnected);
        var probes = await Task.WhenAll(devices.Select(device => ProbeUntilConfirmedAsync(device.IpAddress, attempts, ct)));
        var results = new List<object>(devices.Count);

        for (var index = 0; index < devices.Count; index++)
        {
            var device = devices[index];
            var deviceProbes = probes[index];
            foreach (var probe in deviceProbes)
                await stateService.ProcessPingResultAsync(device, probe.Success, probe.ResponseTimeMs, ct);

            var last = deviceProbes[^1];
            results.Add(new { device.Id, device.Name, last.Success, last.Status, last.ResponseTimeMs, Attempts = deviceProbes.Count });
        }

        return Ok(new
        {
            CheckedAtUtc = DateTime.UtcNow,
            Total = results.Count,
            Responsive = probes.Count(group => group[^1].Success),
            Unresponsive = probes.Count(group => !group[^1].Success),
            Results = results
        });
    }

    private async Task<IReadOnlyList<PingProbeResult>> ProbeUntilConfirmedAsync(string ipAddress, int attempts, CancellationToken ct)
    {
        var results = new List<PingProbeResult>(attempts);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var result = await ProbeAsync(ipAddress, ct);
            results.Add(result);
            if (result.Success) break;
        }
        return results;
    }

    private async Task<PingProbeResult> ProbeAsync(string ipAddress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var ping = new Ping();
        var watch = Stopwatch.StartNew();
        try
        {
            var reply = await ping.SendPingAsync(ipAddress, options.Value.PingTimeoutMilliseconds);
            watch.Stop();
            var success = reply.Status == IPStatus.Success;
            return new(success, reply.Status.ToString(), success ? (decimal)watch.Elapsed.TotalMilliseconds : null);
        }
        catch (Exception ex) when (ex is PingException or InvalidOperationException)
        {
            return new(false, "PingError", null);
        }
    }

    private sealed record PingProbeResult(bool Success, string Status, decimal? ResponseTimeMs);
}
