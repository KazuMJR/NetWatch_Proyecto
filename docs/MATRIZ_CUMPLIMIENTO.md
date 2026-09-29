# Matriz de cumplimiento

Esta matriz relaciona lo pactado en **Proyecto NetWatch NMS.docx** con la implementación entregada. “Automatizada” significa que existe una prueba repetible en `RequirementsTests.cs`; las comprobaciones de interfaz, VirtualBox, ICMP real y HTTPS se ejecutan con el plan manual.

## Requerimientos funcionales

| ID | Cobertura | Evidencia principal | Verificación |
|---|---|---|---|
| RF-001 | Autenticación con JWT y usuario activo | `AuthController`, `TokenService`, página Login | CP-001, CP-002, CP-014 |
| RF-002 | Gestión de usuarios por rol | `UsersController`, página Users | CP-003, prueba manual |
| RF-003 | Registro de dispositivos | `DeviceService`, `DevicesController`, DeviceEdit | CP-004 |
| RF-004 | Edición y desactivación lógica | `DeviceService.UpdateAsync/DeactivateAsync` | prueba manual |
| RF-005 | Disponibilidad ICMP | `PingMonitoringWorker`, `DeviceStateService` | CP-006, CP-007, prueba en VMs |
| RF-006 | Recepción de métricas | `MetricsController`, agente C++ | CP-008, CP-009 |
| RF-007 | Historial de métricas | entidad `Metric`, endpoint GET metrics | CP-008, consulta manual |
| RF-008 | Alertas por cambio de estado | `DeviceStateService` | CP-007, CP-010, CP-011 |
| RF-009 | Dashboard actualizado desde datos almacenados | `DashboardController`, Dashboard.razor | CP-012 |
| RF-010 | Historial de eventos | `EventsController`, EventsAlerts.razor | CP-007, prueba manual |
| RF-011 | Reportes JSON/CSV filtrados | `ReportsController`, Reports.razor | CP-013 |
| RF-012 | Diagnóstico ping | `MonitoringController`, DeviceDetail.razor | prueba manual |
| RF-013 | Clasificación de dispositivo | `DeviceType`, datos iniciales, selector web | migración y prueba manual |
| RF-014 | IP, máscara, gateway y DNS | `Device`, `DeviceService.ValidateAsync`, DeviceEdit | CP-005 y prueba manual |

## Requerimientos no funcionales

| ID | Implementación |
|---|---|
| RNF-001 | JWT, hash de contraseña de ASP.NET Identity, autorización por rol y clave de agente comparada en tiempo constante. |
| RNF-002 | Validación de entrada, transacciones de EF Core, restricciones e índices en MySQL, desactivación lógica. |
| RNF-003 | UI Blazor responsiva y HTML estándar para navegadores modernos. |
| RNF-004 | Navegación por tareas, estados visibles, validaciones y mensajes de error. |
| RNF-005 | Consultas sin seguimiento, filtros, límites de resultados e índices por fechas/relaciones. |
| RNF-006 | Separación API/Web/Data/Agent, inyección de dependencias y opciones externas. |
| RNF-007 | Tablas de eventos e historial, solo consultables desde la interfaz. |
| RNF-008 | Validación TLS en agente y guía de reverse proxy HTTPS para producción. |
| RNF-009 | Docker Compose y guía VirtualBox sin hardware especializado. |
| RNF-010 | Claves primarias/foráneas, relaciones y comportamientos de borrado definidos en `NetWatchDbContext`. |

## Reglas de negocio

| ID | Regla aplicada en |
|---|---|
| RN-001 | `AuthController`: filtra `IsActive`; CP-014. |
| RN-002 | Atributos `Authorize` de Users/Devices y rutas web; CP-003. |
| RN-003 | Índices únicos y validación de username/email en `UsersController`. |
| RN-004 | `IPasswordHasher<User>`; CP-001/002. |
| RN-005 | FK obligatoria a `DeviceType` y carga inicial de cinco tipos. |
| RN-006 | `IPAddress.TryParse` y unicidad de IP entre activos; CP-005. |
| RN-007 | Máscara obligatoria, gateway/DNS opcionales pero validados. |
| RN-008 | `IsActive=false` en usuarios/dispositivos; no hay endpoints para borrar historia. |
| RN-009 | Estado `Inactive` al crear; CP-004. |
| RN-010 | `PingMonitoringWorker`, intervalo 30 s configurable. |
| RN-011 | Contador de fallos y cambio al tercero; CP-007. |
| RN-012 | Intervalo predeterminado de agente 60 s; ICMP funciona sin agente. |
| RN-013 | `MetricIngestionService.Validate`; CP-008/009. |
| RN-014 | `MonitoringOptions` y evaluación de umbrales; CP-010. |
| RN-015 | Enum `DeviceStatus` y transiciones centralizadas. |
| RN-016 | `DeviceStateHistory` creado en cada cambio real de estado. |
| RN-017 | `NetworkEvent` creado ante conectividad, rendimiento y recuperación. |
| RN-018 | Búsqueda de alerta pendiente equivalente antes de crear; CP-007/010. |
| RN-019 | Recuperación a activo, evento e historial; CP-011. |
| RN-020 | Endpoint `PUT /api/alerts/{id}/attend` para ambos roles. |
| RN-021 | No existen endpoints PUT/DELETE de métricas, eventos o historial. |
| RN-022 | Reportes de solo lectura con dispositivo y fechas; CP-013. |
| RN-023 | Fechas asignadas con `DateTime.UtcNow` en servidor/API. |
| RN-024 | HTTP restringido al laboratorio; agente verifica HTTPS/TLS en producción. |

## Casos de prueba

| Caso | Estado | Prueba automatizada |
|---|---|---|
| CP-001 Inicio de sesión válido | Implementado | `CP001_ValidLogin` |
| CP-002 Inicio de sesión inválido | Implementado | `CP002_InvalidLogin` |
| CP-003 Restricción del Técnico | Implementado | `CP003_TechnicianRestrictions` |
| CP-004 Registro de dispositivo | Implementado | `CP004_DeviceStartsInactive` |
| CP-005 IP duplicada | Implementado | `CP005_DuplicateIp` |
| CP-006 Ping exitoso | Implementado | `CP006_PingSuccess` |
| CP-007 Tres ping fallidos | Implementado | `CP007_ThreeFailedPings` |
| CP-008 Recepción de métricas | Implementado | `CP008_ValidMetric` |
| CP-009 Métrica inválida | Implementado | `CP009_InvalidMetric` |
| CP-010 Umbral de advertencia | Implementado | `CP010_WarningThreshold` |
| CP-011 Recuperación | Implementado | `CP011_Recovery` |
| CP-012 Consulta del dashboard | Implementado | `CP012_Dashboard` |
| CP-013 Generación de reporte | Implementado | `CP013_ReportFilters` |
| CP-014 Desactivación de usuario | Implementado | `CP014_InactiveUserLogin` |
