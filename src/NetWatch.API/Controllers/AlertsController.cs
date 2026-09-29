using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/alerts"), Authorize]
public sealed class AlertsController(NetWatchDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AlertDto>> Get([FromQuery] int? deviceId, [FromQuery] AlertStatus? status, [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        var query = db.Alerts.AsNoTracking().Include(x => x.Device).AsQueryable();
        if (deviceId.HasValue) query = query.Where(x => x.DeviceId == deviceId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.GeneratedAtUtc).Take(Math.Clamp(limit, 1, 2000)).Select(x => new AlertDto(x.Id, x.DeviceId, x.Device.Name, x.Title, x.Description, x.Level, x.Status, x.GeneratedAtUtc, x.AttendedAtUtc)).ToListAsync(ct);
    }

    [HttpPut("{id:long}/attend")]
    public async Task<IActionResult> Attend(long id, CancellationToken ct)
    {
        var alert = await db.Alerts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("No se encontró la alerta.");
        alert.Status = AlertStatus.Attended; alert.AttendedAtUtc = DateTime.UtcNow;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) alert.AttendedByUserId = userId;
        await db.SaveChangesAsync(ct); return NoContent();
    }
}
