using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetWatch.API.Contracts;
using NetWatch.API.Controllers;
using NetWatch.API.Options;
using NetWatch.API.Services;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.Tests;

public sealed class RequirementsTests
{
    private static NetWatchDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NetWatchDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new NetWatchDbContext(options);
        db.Roles.AddRange(new Role { Id = 1, Name = RoleNames.Administrator }, new Role { Id = 2, Name = RoleNames.Technician });
        db.DeviceTypes.Add(new DeviceType { Id = 1, Name = "Virtual Machine" }); db.SaveChanges(); return db;
    }
    private static DeviceStateService State(NetWatchDbContext db) => new(db, Options.Create(new MonitoringOptions()));
    private static Device Device(int id = 1, string ip = "192.168.56.10") => new() { Id = id, DeviceTypeId = 1, Name = $"VM-{id}", IpAddress = ip, SubnetMask = "255.255.255.0", IsActive = true, MonitoringEnabled = true, Status = DeviceStatus.Inactive };
    private static DeviceUpsertRequest Request(string ip = "192.168.56.10") => new(1, "VM", ip, null, null, null, "Linux", "Lab", null, "255.255.255.0", "192.168.56.1", null, null, false);

    [Fact(DisplayName = "CP-001 valid login credentials verify")]
    public void CP001_ValidLogin()
    {
        var hasher = new PasswordHasher<User>(); var user = new User { RoleId = 1, FirstName = "Admin", LastName = "User", Email = "a@n.test", Username = "admin", PasswordHash = "", IsActive = true };
        user.PasswordHash = hasher.HashPassword(user, "Correct.Password.1!");
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, "Correct.Password.1!"));
    }

    [Fact(DisplayName = "CP-002 invalid login credentials are rejected")]
    public void CP002_InvalidLogin()
    {
        var hasher = new PasswordHasher<User>(); var user = new User { RoleId = 1, FirstName = "Admin", LastName = "User", Email = "a@n.test", Username = "admin", PasswordHash = "" };
        user.PasswordHash = hasher.HashPassword(user, "Correct.Password.1!");
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, "wrong"));
    }

    [Fact(DisplayName = "CP-003 technician cannot manage users or devices")]
    public void CP003_TechnicianRestrictions()
    {
        var userPolicy = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();
        var createPolicy = typeof(DevicesController).GetMethod(nameof(DevicesController.Create))!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(RoleNames.Administrator, userPolicy!.Roles); Assert.Equal(RoleNames.Administrator, createPolicy!.Roles);
    }

    [Fact(DisplayName = "CP-004 registered device starts inactive")]
    public async Task CP004_DeviceStartsInactive()
    {
        await using var db = CreateDb(); var service = new DeviceService(db, State(db));
        var device = await service.CreateAsync(Request()); Assert.Equal(DeviceStatus.Inactive, device.Status);
    }

    [Fact(DisplayName = "CP-005 duplicate active IP is rejected")]
    public async Task CP005_DuplicateIp()
    {
        await using var db = CreateDb(); db.Devices.Add(Device()); await db.SaveChangesAsync();
        var service = new DeviceService(db, State(db)); await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request()));
    }

    [Fact(DisplayName = "CP-006 successful ping activates device and updates timestamp")]
    public async Task CP006_PingSuccess()
    {
        await using var db = CreateDb(); var d = Device(); db.Devices.Add(d); await db.SaveChangesAsync();
        await State(db).ProcessPingResultAsync(d, true, 2.5m); Assert.Equal(DeviceStatus.Active, d.Status); Assert.NotNull(d.LastPingAtUtc);
    }

    [Fact(DisplayName = "CP-007 three failed pings disconnect and create history event alert")]
    public async Task CP007_ThreeFailedPings()
    {
        await using var db = CreateDb(); var d = Device(); d.Status = DeviceStatus.Active; db.Devices.Add(d); await db.SaveChangesAsync(); var state = State(db);
        await state.ProcessPingResultAsync(d, false, null); await state.ProcessPingResultAsync(d, false, null); await state.ProcessPingResultAsync(d, false, null);
        Assert.Equal(DeviceStatus.Disconnected, d.Status); Assert.Single(db.DeviceStateHistory); Assert.Single(db.Events); Assert.Single(db.Alerts);
    }

    [Fact(DisplayName = "CP-008 valid agent metric is stored")]
    public async Task CP008_ValidMetric()
    {
        await using var db = CreateDb(); db.Devices.Add(Device()); await db.SaveChangesAsync(); var service = new MetricIngestionService(db, State(db));
        await service.IngestAsync(new MetricIngestRequest(1, 20, 30, 40, null, 1.2m, 3)); Assert.Equal(1, await db.Metrics.CountAsync());
    }

    [Fact(DisplayName = "CP-009 invalid metric or unknown device is rejected")]
    public async Task CP009_InvalidMetric()
    {
        await using var db = CreateDb(); var service = new MetricIngestionService(db, State(db));
        await Assert.ThrowsAsync<ArgumentException>(() => service.IngestAsync(new MetricIngestRequest(1, 101, 30, 40, null, null, 3)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.IngestAsync(new MetricIngestRequest(99, 10, 30, 40, null, null, 3)));
    }

    [Fact(DisplayName = "CP-010 threshold creates warning and alert")]
    public async Task CP010_WarningThreshold()
    {
        await using var db = CreateDb(); var d = Device(); d.Status = DeviceStatus.Active; db.Devices.Add(d); await db.SaveChangesAsync();
        await new MetricIngestionService(db, State(db)).IngestAsync(new MetricIngestRequest(1, 85, 20, 20, null, null, 1));
        Assert.Equal(DeviceStatus.Warning, d.Status); Assert.Single(db.Alerts.Where(x => x.Level == AlertLevel.Warning));
    }

    [Fact(DisplayName = "CP-011 normalized metric recovers device to active")]
    public async Task CP011_Recovery()
    {
        await using var db = CreateDb(); var d = Device(); d.Status = DeviceStatus.Warning; db.Devices.Add(d); await db.SaveChangesAsync();
        await new MetricIngestionService(db, State(db)).IngestAsync(new MetricIngestRequest(1, 10, 20, 30, null, null, 1));
        Assert.Equal(DeviceStatus.Active, d.Status); Assert.Contains(db.DeviceStateHistory, x => x.NewStatus == DeviceStatus.Active);
    }

    [Fact(DisplayName = "CP-012 dashboard reports devices alerts and metrics")]
    public async Task CP012_Dashboard()
    {
        await using var db = CreateDb(); var d = Device(); d.Status = DeviceStatus.Active; db.Devices.Add(d); db.Metrics.Add(new Metric { Device = d, CpuPercent = 1, MemoryPercent = 2, DiskPercent = 3, ResponseTimeMs = 1 }); db.Alerts.Add(new Alert { Device = d, Title = "Test", Description = "Test", Level = AlertLevel.Info }); await db.SaveChangesAsync();
        var dashboard = await new DashboardController(db).Get(default); Assert.Equal(1, dashboard.Total); Assert.Single(dashboard.RecentAlerts); Assert.Single(dashboard.RecentMetrics);
    }

    [Fact(DisplayName = "CP-013 report respects device and date filters")]
    public async Task CP013_ReportFilters()
    {
        await using var db = CreateDb(); var d1 = Device(1); var d2 = Device(2, "192.168.56.11"); db.Devices.AddRange(d1, d2); db.Metrics.AddRange(new Metric { Device = d1, CpuPercent = 1, MemoryPercent = 2, DiskPercent = 3, TemperatureCelsius = 40, NetworkTrafficMbps = 5, ResponseTimeMs = 1, RegisteredAtUtc = DateTime.UtcNow }, new Metric { Device = d2, CpuPercent = 4, MemoryPercent = 5, DiskPercent = 6, ResponseTimeMs = 1, RegisteredAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync();
        var rows = await new ReportsController(db).Get("metrics", 1, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), default); Assert.Single(rows); Assert.Equal(1, rows[0].DeviceId); Assert.Equal(40, rows[0].TemperatureCelsius); Assert.Equal(5, rows[0].NetworkTrafficMbps);
    }

    [Fact(DisplayName = "CP-014 inactive user cannot sign in")]
    public async Task CP014_InactiveUserLogin()
    {
        await using var db = CreateDb(); var role = await db.Roles.FindAsync(1); var hasher = new PasswordHasher<User>(); var user = new User { RoleId = 1, Role = role!, FirstName = "Admin", LastName = "User", Email = "a@n.test", Username = "admin", PasswordHash = "", IsActive = false }; user.PasswordHash = hasher.HashPassword(user, "Correct.Password.1!"); db.Users.Add(user); await db.SaveChangesAsync();
        var token = new TokenService(Options.Create(new JwtOptions { Key = "a-very-long-test-key-with-at-least-32-bytes" })); var result = await new AuthController(db, hasher, token).Login(new LoginRequest("admin", "Correct.Password.1!"), default); Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact(DisplayName = "CP-015 administrator can rename a user and duplicate usernames are rejected")]
    public async Task CP015_RenameUser()
    {
        await using var db = CreateDb();
        var role = await db.Roles.SingleAsync(x => x.Name == RoleNames.Technician);
        var first = new User { RoleId = role.Id, Role = role, FirstName = "Técnico", LastName = "Uno", Email = "uno@n.test", Username = "technician", PasswordHash = "hash", IsActive = true };
        var second = new User { RoleId = role.Id, Role = role, FirstName = "Técnico", LastName = "Dos", Email = "dos@n.test", Username = "ocupado", PasswordHash = "hash", IsActive = true };
        db.Users.AddRange(first, second); await db.SaveChangesAsync();

        var controller = new UsersController(db, new PasswordHasher<User>());
        await controller.Update(first.Id, new UpdateUserRequest("Técnico", "Uno", "tecnico", "uno@n.test", RoleNames.Technician, true, null), default);
        Assert.Equal("tecnico", first.Username);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Update(first.Id, new UpdateUserRequest("Técnico", "Uno", "ocupado", "uno@n.test", RoleNames.Technician, true, null), default));
    }

    [Fact(DisplayName = "CP-016 seeding recognizes a renamed user by email")]
    public async Task CP016_SeedRecognizesRenamedUser()
    {
        await using var db = CreateDb();
        var role = await db.Roles.SingleAsync(x => x.Name == RoleNames.Technician);
        db.Users.Add(new User { RoleId = role.Id, Role = role, FirstName = "NetWatch", LastName = "Técnico", Email = "technician@netwatch.local", Username = "tecnico", PasswordHash = "hash", IsActive = true });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:AutoMigrate"] = "false",
            ["Seed:TechnicianUsername"] = "technician",
            ["Seed:TechnicianPassword"] = "Clave.Temporal.2026!",
            ["Seed:TechnicianEmail"] = "technician@netwatch.local"
        }).Build();

        await new DbInitializer(db, new PasswordHasher<User>(), configuration).InitializeAsync();

        Assert.Single(await db.Users.ToListAsync());
        Assert.Equal("tecnico", (await db.Users.SingleAsync()).Username);
    }

    [Fact(DisplayName = "CP-017 CSV export is organized for localized Excel")]
    public async Task CP017_ExcelFriendlyCsv()
    {
        await using var db = CreateDb();
        var device = Device();
        db.Devices.Add(device);
        db.Metrics.Add(new Metric { Device = device, CpuPercent = 1.25m, MemoryPercent = 2.5m, DiskPercent = 3.75m, TemperatureCelsius = 42, NetworkTrafficMbps = 0.5m, ResponseTimeMs = 7.25m, RegisteredAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var file = await new ReportsController(db).Csv("metrics", device.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), default);
        var csv = Encoding.UTF8.GetString(file.FileContents);

        Assert.StartsWith("\uFEFFsep=;", csv);
        Assert.Contains("CPU (%);RAM (%);Disco (%);Temperatura (°C);Tráfico (Mbps);Respuesta (ms)", csv);
        Assert.Contains(";1.25;2.5;3.75;42;0.5;7.25", csv);
    }
}
