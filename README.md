# NetWatch NMS

Implementación completa del sistema de monitoreo de red descrito en **Proyecto NetWatch NMS.docx**. Incluye API REST, panel web, persistencia MySQL, monitoreo ICMP, agente multiplataforma de métricas, alertas, reportes, migraciones, despliegue con Docker y pruebas automatizadas de los 14 casos de prueba pactados.

## Componentes

| Componente | Tecnología | Ubicación |
|---|---|---|
| API y motor de monitoreo | ASP.NET Core Web API / C# / .NET 8 | `src/NetWatch.API` |
| Panel de administración | Blazor Server / .NET 8 | `src/NetWatch.Web` |
| Datos y migraciones | EF Core 8 / MySQL 8 | `src/NetWatch.Data` |
| Agente de dispositivos | C++20 / WinHTTP en Windows / libcurl en Linux | `src/NetWatch.Agent` |
| Pruebas | xUnit / EF Core InMemory | `tests/NetWatch.Tests` |
| Despliegue | Docker Compose / systemd / Task Scheduler | `docker-compose.yml`, `deploy` |

## Inicio rápido del laboratorio

Requisitos: Windows 10/11 o Linux, Docker Desktop/Engine con Compose v2, y puertos 8080, 8081 y 8082 libres.

Para el laboratorio VirtualBox en un host de 16 GB, la guía usa Windows 11 Enterprise Evaluation 25H2, Xubuntu 26.04.1 LTS Desktop y Windows Server 2025 Standard Evaluation Server Core. Incluye enlaces oficiales, distribución exacta de RAM/CPU, las IP `192.168.56.1`, `.10`, `.20` y `.30`, paquetes y plantillas de configuración.

En PowerShell, desde la raíz del proyecto:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Install-Host-Prerequisites.ps1
# Reinicie Windows una vez, abra Docker Desktop y vuelva a esta carpeta.
.\scripts\Initialize-Environment.ps1 `
  -AdminPassword 'UnaClaveAdminSegura!' `
  -TechnicianPassword 'UnaClaveTecnicoSegura!' `
  -MySqlPassword 'UnaClaveMySqlSegura!' `
  -MySqlRootPassword 'OtraClaveRootSegura!'
.\scripts\Start-Lab.ps1
```

Abra `http://localhost:8080` e ingrese con el usuario `admin` y la clave configurada. La API responde en `http://localhost:8081/health`. La primera ejecución crea la base, aplica la migración y carga roles, tipos de dispositivo y usuarios iniciales.

> `appsettings.Development.json` no contiene contraseñas utilizables. Configure las credenciales mediante variables de entorno o use el despliegue con Docker, que obtiene los secretos exclusivamente de `.env`.

## Verificación

Con .NET SDK 8 instalado:

```powershell
.\scripts\Test-NetWatch.ps1
```

Resultado esperado: compilación sin errores y **14 pruebas aprobadas**.

Si Smart App Control bloquea ensamblados de desarrollo, reinicie primero el Host, abra Docker Desktop y use `scripts\Test-NetWatch-Docker.ps1`; ejecuta y valida las mismas 14 pruebas dentro del SDK Linux.

## Documentación

- [Pasos pendientes para terminar las tres VMs](docs/PASOS_PENDIENTES_USUARIO.md)
- [Guía completa de implementación](docs/GUIA_IMPLEMENTACION.md)
- [Estado comprobado del Host Windows](docs/ESTADO_HOST.md)
- [Arquitectura y operación](docs/ARQUITECTURA.md)
- [Referencia de la API](docs/API.md)
- [Matriz de cumplimiento](docs/MATRIZ_CUMPLIMIENTO.md)
- [Plan de pruebas](docs/PRUEBAS.md)
- [Controles de seguridad](SECURITY.md)

## Puertos predeterminados

| Servicio | Puerto del host | Exposición |
|---|---:|---|
| Panel web | 8080/TCP | Usuarios del laboratorio |
| API | 8081/TCP | Panel y agentes |
| Adminer | 8082/TCP | Solo `127.0.0.1` del Host Windows |
| MySQL | 3306/TCP interno | Solo red Docker; no se publica al host |

## Credenciales y datos

- `.env` contiene secretos y está excluido de Git.
- `src/NetWatch.Agent/agent.json` contiene la clave del agente y también está excluido.
- MySQL se guarda en el volumen `netwatch_mysql`; `docker compose down` no borra datos.
- `scripts\Stop-Lab.ps1 -RemoveDatabase` **sí elimina permanentemente** ese volumen.

## Licencia académica

Proyecto de referencia para el curso Análisis de Sistemas II. Revise las políticas de su institución antes de reutilizarlo fuera del entorno académico.
