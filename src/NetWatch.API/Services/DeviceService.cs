using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NetWatch.API.Contracts;
using NetWatch.Data;
using NetWatch.Data.Entities;

namespace NetWatch.API.Services;

public sealed partial class DeviceService(NetWatchDbContext db, DeviceStateService stateService)
{
    public async Task<Device> CreateAsync(DeviceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, null, cancellationToken);
        var device = new Device
        {
            DeviceTypeId = request.DeviceTypeId, Name = request.Name.Trim(), IpAddress = IPAddress.Parse(request.IpAddress.Trim()).ToString(),
            MacAddress = Normalize(request.MacAddress), Manufacturer = Normalize(request.Manufacturer), Model = Normalize(request.Model),
            OperatingSystem = Normalize(request.OperatingSystem), Location = Normalize(request.Location), Description = Normalize(request.Description),
            SubnetMask = request.SubnetMask.Trim(), Gateway = NormalizeIp(request.Gateway), PrimaryDns = NormalizeIp(request.PrimaryDns),
            SecondaryDns = NormalizeIp(request.SecondaryDns), MonitoringEnabled = request.MonitoringEnabled,
            Status = DeviceStatus.Inactive, IsActive = true, RegisteredAtUtc = DateTime.UtcNow
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(device).Reference(x => x.DeviceType).LoadAsync(cancellationToken);
        return device;
    }

    public async Task<Device> UpdateAsync(int id, DeviceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var device = await db.Devices.Include(x => x.DeviceType).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new KeyNotFoundException("No se encontró el dispositivo.");
        await ValidateAsync(request, id, cancellationToken);
        var wasEnabled = device.MonitoringEnabled;
        device.DeviceTypeId = request.DeviceTypeId; device.Name = request.Name.Trim(); device.IpAddress = IPAddress.Parse(request.IpAddress.Trim()).ToString();
        device.MacAddress = Normalize(request.MacAddress); device.Manufacturer = Normalize(request.Manufacturer); device.Model = Normalize(request.Model);
        device.OperatingSystem = Normalize(request.OperatingSystem); device.Location = Normalize(request.Location); device.Description = Normalize(request.Description);
        device.SubnetMask = request.SubnetMask.Trim(); device.Gateway = NormalizeIp(request.Gateway); device.PrimaryDns = NormalizeIp(request.PrimaryDns);
        device.SecondaryDns = NormalizeIp(request.SecondaryDns); device.MonitoringEnabled = request.MonitoringEnabled;
        if (wasEnabled && !request.MonitoringEnabled) await stateService.SetInactiveAsync(device, "Monitoreo deshabilitado por el administrador", cancellationToken);
        else await db.SaveChangesAsync(cancellationToken);
        await db.Entry(device).Reference(x => x.DeviceType).LoadAsync(cancellationToken);
        return device;
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new KeyNotFoundException("No se encontró el dispositivo.");
        device.IsActive = false; device.MonitoringEnabled = false;
        await stateService.SetInactiveAsync(device, "Dispositivo desactivado lógicamente", cancellationToken);
    }

    private async Task ValidateAsync(DeviceUpsertRequest request, int? id, CancellationToken cancellationToken)
    {
        if (!await db.DeviceTypes.AnyAsync(x => x.Id == request.DeviceTypeId, cancellationToken)) throw new ArgumentException("El tipo de dispositivo no es válido.");
        if (!IPAddress.TryParse(request.IpAddress?.Trim(), out var ip)) throw new ArgumentException("La dirección IP no es válida.");
        if (!ValidSubnet(request.SubnetMask, ip.AddressFamily)) throw new ArgumentException("La máscara de subred o el prefijo CIDR no es válido.");
        foreach (var value in new[] { request.Gateway, request.PrimaryDns, request.SecondaryDns })
            if (!string.IsNullOrWhiteSpace(value) && !IPAddress.TryParse(value.Trim(), out _)) throw new ArgumentException($"La dirección de red no es válida: {value}.");
        if (!string.IsNullOrWhiteSpace(request.MacAddress) && !MacRegex().IsMatch(request.MacAddress.Trim())) throw new ArgumentException("La dirección MAC no es válida.");
        var normalized = ip.ToString();
        if (await db.Devices.AnyAsync(x => x.IsActive && x.IpAddress == normalized && (!id.HasValue || x.Id != id.Value), cancellationToken))
            throw new InvalidOperationException("Otro dispositivo activo ya utiliza esta dirección IP.");
    }

    private static bool ValidSubnet(string value, AddressFamily addressFamily)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith('/')) normalized = normalized[1..];
        if (int.TryParse(normalized, out var prefix))
            return addressFamily == AddressFamily.InterNetwork ? prefix is >= 0 and <= 32 : prefix is >= 0 and <= 128;
        if (addressFamily != AddressFamily.InterNetwork || !IPAddress.TryParse(normalized, out var mask) || mask.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = mask.GetAddressBytes();
        var bits = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        var inverse = ~bits;
        return (inverse & (inverse + 1)) == 0;
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeIp(string? value) => string.IsNullOrWhiteSpace(value) ? null : IPAddress.Parse(value.Trim()).ToString();
    [GeneratedRegex("^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$")]
    private static partial Regex MacRegex();
}
