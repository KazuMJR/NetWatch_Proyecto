using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Services;

public sealed class DbInitializer(NetWatchDbContext db, IPasswordHasher<User> hasher, IConfiguration configuration)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (configuration.GetValue("Database:AutoMigrate", true))
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await EnsureRoleAsync(RoleNames.Administrator, "Administración completa de la plataforma", cancellationToken);
        await EnsureRoleAsync(RoleNames.Technician, "Monitoreo, alertas, reportes y diagnósticos", cancellationToken);
        foreach (var (name, description) in new[]
        {
            ("Servidor", "Servidor físico o virtual"),
            ("Computadora", "Equipo de escritorio o portátil"),
            ("Router", "Enrutador de red"),
            ("Switch", "Conmutador de red"),
            ("Máquina Virtual", "Equipo monitoreado virtualizado")
        })
        {
            if (!await db.DeviceTypes.AnyAsync(x => x.Name == name, cancellationToken))
                db.DeviceTypes.Add(new DeviceType { Name = name, Description = description });
        }
        await db.SaveChangesAsync(cancellationToken);

        await EnsureUserAsync(
            configuration["Seed:AdminUsername"], configuration["Seed:AdminPassword"],
            configuration["Seed:AdminEmail"] ?? "admin@netwatch.local", "NetWatch", "Administrador",
            RoleNames.Administrator, cancellationToken);
        await EnsureUserAsync(
            configuration["Seed:TechnicianUsername"], configuration["Seed:TechnicianPassword"],
            configuration["Seed:TechnicianEmail"] ?? "technician@netwatch.local", "NetWatch", "Técnico",
            RoleNames.Technician, cancellationToken);
    }

    private async Task EnsureRoleAsync(string name, string description, CancellationToken cancellationToken)
    {
        if (!await db.Roles.AnyAsync(x => x.Name == name, cancellationToken))
            db.Roles.Add(new Role { Name = name, Description = description });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUserAsync(string? username, string? password, string email, string firstName, string lastName, string roleName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || await db.Users.AnyAsync(x => x.Username == username, cancellationToken)) return;
        var role = await db.Roles.SingleAsync(x => x.Name == roleName, cancellationToken);
        var user = new User { Username = username.Trim(), Email = email.Trim().ToLowerInvariant(), FirstName = firstName, LastName = lastName, RoleId = role.Id, PasswordHash = string.Empty };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
