using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.API.Services;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Controllers;

[ApiController, Route("api/users"), Authorize(Roles = RoleNames.Administrator)]
public sealed class UsersController(NetWatchDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> Get(CancellationToken ct) =>
        await db.Users.AsNoTracking().Include(x => x.Role).OrderBy(x => x.Username).Select(x => new UserDto(x.Id, x.FirstName, x.LastName, x.Email, x.Username, x.Role.Name, x.IsActive, x.RegisteredAtUtc)).ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim(); var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Username == username, ct)) throw new InvalidOperationException("El nombre de usuario ya está registrado.");
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw new InvalidOperationException("El correo electrónico ya está registrado.");
        var role = await FindRoleAsync(request.Role, ct);
        var user = new User { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = email, Username = username, RoleId = role.Id, Role = role, PasswordHash = string.Empty, IsActive = true };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("No se encontró el usuario.");
        var username = request.Username.Trim(); var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Id != id && x.Username == username, ct)) throw new InvalidOperationException("El nombre de usuario ya está registrado.");
        if (await db.Users.AnyAsync(x => x.Id != id && x.Email == email, ct)) throw new InvalidOperationException("El correo electrónico ya está registrado.");
        var role = await FindRoleAsync(request.Role, ct);
        user.FirstName = request.FirstName.Trim(); user.LastName = request.LastName.Trim(); user.Username = username; user.Email = email; user.RoleId = role.Id; user.Role = role; user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.NewPassword)) user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct); return Ok(user.ToDto());
    }

    private async Task<Role> FindRoleAsync(string name, CancellationToken ct)
    {
        if (name is not (RoleNames.Administrator or RoleNames.Technician)) throw new ArgumentException("El rol debe ser Administrador o Técnico.");
        return await db.Roles.SingleAsync(x => x.Name == name, ct);
    }
}
