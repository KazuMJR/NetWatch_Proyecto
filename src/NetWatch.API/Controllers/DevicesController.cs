using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.API.Services;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/devices"), Authorize]
public sealed class DevicesController(NetWatchDbContext db, DeviceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<DeviceDto>> Get([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.Devices.AsNoTracking().Include(x => x.DeviceType).AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return await query.OrderBy(x => x.Name).Select(x => new DeviceDto(x.Id, x.DeviceTypeId, x.DeviceType.Name, x.Name, x.IpAddress, x.MacAddress, x.Manufacturer, x.Model, x.OperatingSystem, x.Location, x.Status, x.Description, x.RegisteredAtUtc, x.LastPingAtUtc, x.LastResponseTimeMs, x.SubnetMask, x.Gateway, x.PrimaryDns, x.SecondaryDns, x.IsActive, x.MonitoringEnabled, x.ConsecutivePingFailures)).ToListAsync(ct);
    }

    [HttpGet("types")]
    public async Task<IReadOnlyList<DeviceTypeDto>> Types(CancellationToken ct) => await db.DeviceTypes.AsNoTracking().OrderBy(x => x.Name).Select(x => new DeviceTypeDto(x.Id, x.Name, x.Description)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DeviceDto>> GetById(int id, CancellationToken ct)
    {
        var device = await db.Devices.AsNoTracking().Include(x => x.DeviceType).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("No se encontró el dispositivo.");
        return Ok(device.ToDto());
    }

    [HttpGet("{id:int}/history")]
    public async Task<IReadOnlyList<StateHistoryDto>> History(int id, CancellationToken ct) =>
        await db.DeviceStateHistory.AsNoTracking().Where(x => x.DeviceId == id).OrderByDescending(x => x.ChangedAtUtc).Take(200).Select(x => new StateHistoryDto(x.Id, x.DeviceId, x.PreviousStatus, x.NewStatus, x.Description, x.ChangedAtUtc)).ToListAsync(ct);

    [Authorize(Roles = RoleNames.Administrator), HttpPost]
    public async Task<ActionResult<DeviceDto>> Create(DeviceUpsertRequest request, CancellationToken ct)
    {
        var device = await service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id = device.Id }, device.ToDto());
    }

    [Authorize(Roles = RoleNames.Administrator), HttpPut("{id:int}")]
    public async Task<ActionResult<DeviceDto>> Update(int id, DeviceUpsertRequest request, CancellationToken ct) => Ok((await service.UpdateAsync(id, request, ct)).ToDto());

    [Authorize(Roles = RoleNames.Administrator), HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct) { await service.DeactivateAsync(id, ct); return NoContent(); }
}
