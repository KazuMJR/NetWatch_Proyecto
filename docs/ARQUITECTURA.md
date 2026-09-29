# Arquitectura de NetWatch NMS

## Flujo general

```text
Navegador ──HTTP(S)──> NetWatch.Web ──JWT/JSON──> NetWatch.API ──EF Core──> MySQL
                                                   │
                                                   ├── ICMP cada 30 s ──> Dispositivos
                                                   │
Agente C++ ──HTTP(S) + X-Agent-Key + JSON──────────┘
        cada 60 s: CPU, RAM, disco, temperatura, tráfico y latencia
```

El panel Blazor Server conserva el JWT en almacenamiento protegido de sesión. La API autoriza cada operación, por lo que ocultar botones en el panel no es el único control de acceso. MySQL no se publica fuera de la red interna de Docker.

## Responsabilidades

### NetWatch.API

- Autentica usuarios activos y emite JWT.
- Autoriza administradores y técnicos por rol.
- Valida usuarios, dispositivos, direcciones y métricas.
- Ejecuta pings ICMP en segundo plano cada 30 segundos.
- Declara desconexión después de 3 fallos consecutivos.
- Evalúa CPU/RAM (85 %), disco (90 %) y temperatura (80 °C).
- Evita alertas pendientes duplicadas y registra recuperaciones.
- Expone consultas de panel, detalle, eventos, alertas y reportes CSV.

### NetWatch.Web

- Proporciona inicio de sesión, panel de resumen y navegación por rol.
- Permite al administrador gestionar usuarios y dispositivos.
- Permite consultar detalle, historial, alertas, eventos y reportes.
- Ejecuta diagnóstico ping bajo demanda.

### NetWatch.Agent

- Lee configuración local `agent.json`.
- Obtiene métricas del sistema sin ejecutar comandos externos.
- Publica JSON cada 60 segundos a `POST /api/metrics/agent`.
- Autentica con `X-Agent-Key` y valida TLS cuando `verifyTls=true`.
- En Windows, la temperatura se reporta como `null` porque no existe una API estándar confiable sin drivers de fabricante.

### NetWatch.Data

- Define las entidades, relaciones, índices y restricciones.
- Guarda fechas en UTC.
- Implementa migraciones reproducibles de EF Core.
- Conserva historial de estado y eventos como registros inmutables desde la API.

## Modelo de estado

```text
registro ──> Inactive
ping correcto ──> Active
3 pings fallidos ──> Disconnected
métrica en umbral ──> Warning
métricas normalizadas ──> Active
```

Cada transición real genera historial. Las transiciones a `Warning` o `Disconnected` generan evento y alerta; una alerta pendiente equivalente impide duplicados. Cuando la condición se normaliza se registra la recuperación, pero la alerta conserva su estado hasta que un Administrador o Técnico la atienda, tal como establece RN-020.

## Roles

| Operación | Administrator | Technician |
|---|:---:|:---:|
| Ver panel, detalle, eventos y reportes | Sí | Sí |
| Ejecutar diagnóstico ping | Sí | Sí |
| Atender alertas | Sí | Sí |
| Crear/editar/desactivar dispositivos | Sí | No |
| Crear/editar/desactivar usuarios | Sí | No |

## Decisiones de persistencia

- Usuarios y dispositivos se desactivan lógicamente (`IsActive=false`); no se borran historiales.
- La IP debe ser única entre dispositivos activos.
- Los índices cubren fecha, dispositivo, estado y campos de búsqueda frecuentes.
- Los reportes limitan cada consulta a 5,000 filas para proteger recursos; para períodos largos deben consultarse rangos sucesivos.

## Configuración operativa

Las claves con doble guion bajo son variables de entorno de ASP.NET Core:

| Variable | Uso |
|---|---|
| `ConnectionStrings__NetWatch` | Conexión MySQL |
| `Jwt__Key` | Firma de tokens, mínimo 32 bytes |
| `Security__AgentApiKey` | Autenticación de agentes |
| `Monitoring__PingIntervalSeconds` | Intervalo ICMP, predeterminado 30 |
| `Monitoring__FailuresBeforeDisconnected` | Fallos antes de desconexión, predeterminado 3 |
| `Database__AutoMigrate` | Aplicar migraciones al iniciar |
| `Seed__*` | Usuarios iniciales; solo se crean si no existen |

## Recuperación y respaldo

Respaldar:

```bash
docker compose exec -T mysql mysqldump -unetwatch -p netwatch > netwatch-backup.sql
```

En este ejemplo interactivo MySQL solicitará la clave. Para restaurar en una instancia limpia:

```bash
docker compose exec -T mysql mysql -unetwatch -p netwatch < netwatch-backup.sql
```

Haga una prueba de restauración periódica; un archivo no verificado no debe considerarse un respaldo confiable.
