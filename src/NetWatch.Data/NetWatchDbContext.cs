using Microsoft.EntityFrameworkCore;
using NetWatch.Data.Entities;

namespace NetWatch.Data;

public sealed class NetWatchDbContext(DbContextOptions<NetWatchDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceType> DeviceTypes => Set<DeviceType>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Metric> Metrics => Set<Metric>();
    public DbSet<NetworkEvent> Events => Set<NetworkEvent>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<DeviceStateHistory> DeviceStateHistory => Set<DeviceStateHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users"); entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique(); entity.HasIndex(x => x.Username).IsUnique();
            entity.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DeviceType>(entity =>
        {
            entity.ToTable("DeviceTypes"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(150);
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            entity.Property(x => x.MacAddress).HasMaxLength(20); entity.Property(x => x.Manufacturer).HasMaxLength(50);
            entity.Property(x => x.Model).HasMaxLength(100); entity.Property(x => x.OperatingSystem).HasMaxLength(100);
            entity.Property(x => x.Location).HasMaxLength(100); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.SubnetMask).HasMaxLength(45).IsRequired(); entity.Property(x => x.Gateway).HasMaxLength(45);
            entity.Property(x => x.PrimaryDns).HasMaxLength(45); entity.Property(x => x.SecondaryDns).HasMaxLength(45);
            entity.Property(x => x.LastResponseTimeMs).HasPrecision(8, 2); entity.HasIndex(x => x.IpAddress);
            entity.HasOne(x => x.DeviceType).WithMany(x => x.Devices).HasForeignKey(x => x.DeviceTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Metric>(entity =>
        {
            entity.ToTable("Metrics"); entity.HasKey(x => x.Id);
            entity.Property(x => x.CpuPercent).HasPrecision(5, 2); entity.Property(x => x.MemoryPercent).HasPrecision(5, 2);
            entity.Property(x => x.DiskPercent).HasPrecision(5, 2); entity.Property(x => x.TemperatureCelsius).HasPrecision(5, 2);
            entity.Property(x => x.NetworkTrafficMbps).HasPrecision(10, 2); entity.Property(x => x.ResponseTimeMs).HasPrecision(8, 2);
            entity.HasIndex(x => new { x.DeviceId, x.RegisteredAtUtc });
            entity.HasOne(x => x.Device).WithMany(x => x.Metrics).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<NetworkEvent>(entity =>
        {
            entity.ToTable("Events"); entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(50).IsRequired(); entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.DeviceId, x.OccurredAtUtc });
            entity.HasOne(x => x.Device).WithMany(x => x.Events).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("Alerts"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(100).IsRequired(); entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); entity.HasIndex(x => new { x.DeviceId, x.Status, x.GeneratedAtUtc });
            entity.HasOne(x => x.Device).WithMany(x => x.Alerts).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DeviceStateHistory>(entity =>
        {
            entity.ToTable("DeviceStateHistory"); entity.HasKey(x => x.Id);
            entity.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(20); entity.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.DeviceId, x.ChangedAtUtc });
            entity.HasOne(x => x.Device).WithMany(x => x.StateHistory).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
