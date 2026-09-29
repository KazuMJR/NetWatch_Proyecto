using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetWatch.API.Options;
using NetWatch.Data;

namespace NetWatch.API.Services;

public sealed class PingMonitoringWorker(IServiceScopeFactory scopeFactory, IOptions<MonitoringOptions> options, ILogger<PingMonitoringWorker> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.PingIntervalSeconds)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckDevicesAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "ICMP monitoring cycle failed"); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task CheckDevicesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NetWatchDbContext>();
        var stateService = scope.ServiceProvider.GetRequiredService<DeviceStateService>();
        var ids = await db.Devices.Where(x => x.IsActive && x.MonitoringEnabled).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            var device = await db.Devices.SingleAsync(x => x.Id == id, cancellationToken);
            try
            {
                using var ping = new Ping();
                var stopwatch = Stopwatch.StartNew();
                var reply = await ping.SendPingAsync(device.IpAddress, _options.PingTimeoutMilliseconds);
                stopwatch.Stop();
                await stateService.ProcessPingResultAsync(device, reply.Status == IPStatus.Success, reply.Status == IPStatus.Success ? (decimal)stopwatch.Elapsed.TotalMilliseconds : null, cancellationToken);
            }
            catch (Exception ex) when (ex is PingException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Ping failed for device {DeviceId} ({IpAddress})", device.Id, device.IpAddress);
                await stateService.ProcessPingResultAsync(device, false, null, cancellationToken);
            }
        }
    }
}

