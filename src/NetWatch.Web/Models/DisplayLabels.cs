namespace NetWatch.Web.Models;

public static class DisplayLabels
{
    public static string Role(string? value) => value switch
    {
        "Administrator" => "Administrador",
        "Technician" => "Técnico",
        _ => value ?? "-"
    };

    public static string DeviceStatus(string? value) => value switch
    {
        "Active" => "Activo",
        "Inactive" => "Inactivo",
        "Warning" => "Advertencia",
        "Disconnected" => "Desconectado",
        _ => value ?? "-"
    };

    public static string AlertStatus(string? value) => value switch
    {
        "Pending" => "Pendiente",
        "Attended" => "Atendida",
        _ => value ?? "-"
    };

    public static string Severity(string? value) => value switch
    {
        "Info" => "Informativa",
        "Low" => "Baja",
        "Medium" => "Media",
        "High" => "Alta",
        "Warning" => "Advertencia",
        "Critical" => "Crítica",
        _ => value ?? "-"
    };

    public static string EventType(string? value) => value switch
    {
        "Connectivity" => "Conectividad",
        "Performance" => "Rendimiento",
        "Recovery" => "Recuperación",
        "Administration" => "Administración",
        _ => value ?? "-"
    };

    public static string Category(string? value) => value switch
    {
        "Metric" => "Métrica",
        "Event" => "Evento",
        "Alert" => "Alerta",
        "State" => "Estado",
        _ => value ?? "-"
    };

    public static string DeviceType(string? value) => value switch
    {
        "Computer" => "Computadora",
        "Server" => "Servidor",
        "Virtual Machine" => "Máquina virtual",
        "Router" => "Enrutador",
        "Switch" => "Conmutador",
        _ => value ?? "-"
    };

    public static string Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";

        var translated = value switch
        {
            "Performance threshold exceeded" => "Umbral de rendimiento excedido",
            "Device disconnected" => "Dispositivo desconectado",
            "ICMP connectivity confirmed" => "Conectividad ICMP confirmada",
            "ICMP connectivity confirmed; a performance threshold remains exceeded" => "Conectividad ICMP confirmada; continúa excedido un umbral de rendimiento",
            "Metrics returned to normal values" => "Las métricas volvieron a valores normales",
            "Monitoring disabled by administrator" => "Monitoreo deshabilitado por el administrador",
            "Device logically deactivated" => "Dispositivo desactivado lógicamente",
            _ => value
        };

        return translated
            .Replace("State changed from", "Estado cambiado de", StringComparison.OrdinalIgnoreCase)
            .Replace("No ICMP response after", "Sin respuesta ICMP después de", StringComparison.OrdinalIgnoreCase)
            .Replace("consecutive checks", "comprobaciones consecutivas", StringComparison.OrdinalIgnoreCase)
            .Replace("Threshold exceeded:", "Umbral excedido:", StringComparison.OrdinalIgnoreCase)
            .Replace("Metrics returned to normal values", "Las métricas volvieron a valores normales", StringComparison.OrdinalIgnoreCase)
            .Replace("Performance threshold exceeded", "Umbral de rendimiento excedido", StringComparison.OrdinalIgnoreCase)
            .Replace("Device disconnected", "Dispositivo desconectado", StringComparison.OrdinalIgnoreCase)
            .Replace("ICMP connectivity confirmed", "Conectividad ICMP confirmada", StringComparison.OrdinalIgnoreCase)
            .Replace("Monitoring disabled by administrator", "Monitoreo deshabilitado por el administrador", StringComparison.OrdinalIgnoreCase)
            .Replace("Device logically deactivated", "Dispositivo desactivado lógicamente", StringComparison.OrdinalIgnoreCase)
            .Replace("memory ", "memoria ", StringComparison.OrdinalIgnoreCase)
            .Replace("disk ", "disco ", StringComparison.OrdinalIgnoreCase)
            .Replace("temperature ", "temperatura ", StringComparison.OrdinalIgnoreCase)
            .Replace("Traffic ", "Tráfico ", StringComparison.OrdinalIgnoreCase)
            .Replace("Response ", "Respuesta ", StringComparison.OrdinalIgnoreCase)
            .Replace("Active", "Activo", StringComparison.OrdinalIgnoreCase)
            .Replace("Inactive", "Inactivo", StringComparison.OrdinalIgnoreCase)
            .Replace("Warning", "Advertencia", StringComparison.OrdinalIgnoreCase)
            .Replace("Disconnected", "Desconectado", StringComparison.OrdinalIgnoreCase)
            .Replace("Critical", "Crítica", StringComparison.OrdinalIgnoreCase)
            .Replace("Pending", "Pendiente", StringComparison.OrdinalIgnoreCase)
            .Replace("Attended", "Atendida", StringComparison.OrdinalIgnoreCase)
            .Replace(" to ", " a ", StringComparison.OrdinalIgnoreCase);
    }
}
