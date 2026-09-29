using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.Data;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/events"), Authorize]
public sealed class EventsController(NetWatchDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EventDto>> Get([FromQuery] int? deviceId, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        var query = db.Events.AsNoTracking().Include(x => x.Device).AsQueryable();
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId);
        if (fromUtc.HasValue) query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        return await query.OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(limit, 1, 2000)).Select(x => new EventDto(x.Id, x.DeviceId, x.Device.Name, x.EventType, x.Description, x.Severity, x.OccurredAtUtc)).ToListAsync(ct);
    }
}

