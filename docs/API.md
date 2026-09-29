# Referencia de API

Base local: `http://localhost:8081`. Las respuestas usan JSON y los nombres de enumeraciones aparecen como texto (`Active`, `Warning`, etc.). Las fechas son UTC en formato ISO 8601.

## Autenticación

`POST /api/auth/login` no requiere token.

```json
{
  "username": "admin",
  "password": "su-clave"
}
```

El resto de endpoints de usuario requiere:

```text
Authorization: Bearer <token>
```

El receptor de agentes no usa JWT; exige la cabecera `X-Agent-Key` con el mismo secreto configurado en la API.

## Endpoints

| Método | Ruta | Acceso | Propósito |
|---|---|---|---|
| POST | `/api/auth/login` | Público | Autenticar usuario activo |
| GET | `/api/dashboard` | Autenticado | Resumen, alertas y métricas recientes |
| GET | `/api/users` | Administrador | Listar usuarios |
| POST | `/api/users` | Administrador | Crear usuario |
| PUT | `/api/users/{id}` | Administrador | Editar o desactivar usuario |
| GET | `/api/devices` | Autenticado | Listar dispositivos |
| GET | `/api/devices/types` | Autenticado | Listar tipos |
| GET | `/api/devices/{id}` | Autenticado | Detalle y última métrica |
| GET | `/api/devices/{id}/history` | Autenticado | Historial de estados |
| POST | `/api/devices` | Administrador | Registrar dispositivo |
| PUT | `/api/devices/{id}` | Administrador | Editar dispositivo |
| DELETE | `/api/devices/{id}` | Administrador | Desactivación lógica |
| POST | `/api/diagnostics/ping/{deviceId}` | Autenticado | Ping manual |
| POST | `/api/metrics/agent` | Clave de agente | Registrar métrica |
| GET | `/api/metrics?deviceId=&fromUtc=&toUtc=` | Autenticado | Historial de métricas |
| GET | `/api/events?deviceId=&fromUtc=&toUtc=` | Autenticado | Historial de eventos |
| GET | `/api/alerts?deviceId=&status=` | Autenticado | Consultar alertas |
| PUT | `/api/alerts/{id}/attend` | Autenticado | Marcar alerta atendida |
| GET | `/api/reports?type=&deviceId=&fromUtc=&toUtc=` | Autenticado | Reporte JSON |
| GET | `/api/reports/csv?type=&deviceId=&fromUtc=&toUtc=` | Autenticado | Descargar CSV |
| GET | `/health` | Público | Estado básico del proceso API |

Valores de `type` en reportes: `metrics`, `events`, `alerts`, `states`.

## Registro de dispositivo

```json
{
  "deviceTypeId": 1,
  "name": "Servidor Linux 01",
  "ipAddress": "192.168.56.20",
  "macAddress": "08:00:27:AA:BB:CC",
  "manufacturer": "VirtualBox",
  "model": "VM",
  "operatingSystem": "Ubuntu Server 24.04",
  "location": "Laboratorio",
  "description": "Equipo de pruebas",
  "subnetMask": "255.255.255.0",
  "gateway": "192.168.56.1",
  "primaryDns": "1.1.1.1",
  "secondaryDns": "8.8.8.8",
  "monitoringEnabled": true
}
```

El dispositivo inicia en `Inactive` aunque ya responda en red; el siguiente ciclo de ping lo cambia a `Active`.

## Publicación de métricas

```bash
curl -X POST http://192.168.56.1:8081/api/metrics/agent \
  -H "Content-Type: application/json" \
  -H "X-Agent-Key: SU_CLAVE_DE_AGENTE" \
  -d '{"deviceId":1,"cpuPercent":25.1,"memoryPercent":44.7,"diskPercent":61.2,"temperatureCelsius":52.0,"networkTrafficMbps":3.4,"responseTimeMs":4.2}'
```

Reglas de validación:

- `deviceId` debe existir, estar activo y tener monitoreo habilitado.
- CPU, RAM y disco deben estar entre 0 y 100.
- Temperatura, tráfico y latencia no pueden ser negativos.
- La API asigna la hora UTC; no confía en el reloj del agente.
- CPU/RAM >= 85 %, disco >= 90 % o temperatura >= 80 °C provocan `Warning`.

## Códigos esperados

- `200/201/204`: operación correcta.
- `400`: datos o configuración inválidos.
- `401`: credenciales, token o clave de agente inválidos.
- `403`: rol sin permisos.
- `404`: entidad inexistente.
- `409`: conflicto de unicidad o regla de negocio.
