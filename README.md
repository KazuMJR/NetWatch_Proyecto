# NetWatch NMS

Sistema de monitoreo de red para un laboratorio académico con máquinas Windows y Linux. Incluye API REST, panel web en español, persistencia MySQL, monitoreo ICMP, agente multiplataforma de métricas, alertas, reportes visuales y despliegue con Docker.

**[Descargar la documentación final del proyecto (Word)](docs/Proyecto-NetWatch-NMS.docx)**

El documento reúne el análisis, los requisitos, el diseño, la implementación, las pruebas y las mejoras solicitadas por el catedrático. Se incluye la versión revisada por el autor, conservando su formato y contenido.

## Funcionalidades principales

- **Panel principal:** actualización automática y comprobación manual de conectividad, conteos por estado, alertas y métricas recientes.
- **Dispositivos:** inventario, configuración de red, validaciones, ayuda en los campos y consulta de métricas e historial.
- **Eventos y alertas:** registro de desconexiones, recuperación y umbrales de rendimiento; atención de alertas.
- **Reportes:** filtros por categoría, dispositivo y fechas; opción de incluir todos los datos, gráficos y exportación a Excel, CSV y PDF vertical con vista previa.
- **Usuarios:** acceso con roles de administrador y técnico y gestión de usuarios según los permisos asignados.
- **Ayuda interactiva:** recorrido por las pantallas y Nero, la mascota animada que permanece en una esquina por defecto y permite activar el movimiento libre.

Las temperaturas estimadas de máquinas virtuales se identifican como simuladas; no representan lecturas de un sensor físico. Las estadísticas de reportes describen los registros filtrados y no equivalen a un porcentaje de disponibilidad de la red.

## Componentes

| Componente | Tecnología | Ubicación |
|---|---|---|
| API y motor de monitoreo | ASP.NET Core Web API / C# / .NET 8 | `src/NetWatch.API` |
| Panel de administración | Blazor Server / .NET 8 | `src/NetWatch.Web` |
| Datos y migraciones | EF Core 8 / MySQL 8 | `src/NetWatch.Data` |
| Agente de dispositivos | C++20 / WinHTTP en Windows / libcurl en Linux | `src/NetWatch.Agent` |
| Estadísticas compartidas | C# / .NET 8 | `src/NetWatch.Reporting` |
| Pruebas del sistema | xUnit / EF Core InMemory | `tests/NetWatch.Tests` |
| Pruebas de mascota y guía | Node.js / pruebas integradas | `tests/NetWatch.Web.Tests` |
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

Resultado esperado: compilación sin errores y todas las pruebas aprobadas. La revisión de octubre incluye **29 pruebas .NET** y **24 pruebas JavaScript**. Para la mascota y la guía, con Node.js instalado, ejecute:

```powershell
node --test tests/NetWatch.Web.Tests/*.test.cjs
```

Si Smart App Control bloquea ensamblados de desarrollo, reinicie primero el Host, abra Docker Desktop y use `scripts\Test-NetWatch-Docker.ps1`; ejecuta y valida la suite .NET dentro del SDK Linux.

## Mejoras solicitadas por el catedrático

- **Nero discreto:** inicia en la esquina inferior derecha, sin pasear ni abrir consejos automáticamente. Su menú permite elegir cualquiera de las cuatro esquinas, volver al modo libre y pausar animaciones. La elección se recuerda en ese navegador.
- **Reportes visuales:** gráfico circular por categoría, líneas de consumo y actividad, comparación por dispositivo y promedios/máximos sobre los filtros aplicados. Las leyendas CPU/RAM/Disco alternan las series.
- **Exportación:** PDF vertical con resumen estadístico y detalle; Excel con estadísticas, barras de datos y hojas por categoría.
- Los promedios excluyen ausentes e incluyen ceros. Las gráficas describen registros, no porcentajes de disponibilidad. La biblioteca compartida `NetWatch.Reporting` mantiene los mismos cálculos entre web y exportaciones.

## Documentación

| Documento | Contenido |
|---|---|
| **[Documentación final del proyecto — Word](docs/Proyecto-NetWatch-NMS.docx)** | Documento académico revisado, con diseño, requisitos, pruebas y sección de mejoras solicitadas por el catedrático. |
| [Guía de implementación](docs/GUIA_IMPLEMENTACION.md) | Instalación del laboratorio, máquinas virtuales, agentes y despliegue. |
| [Arquitectura y operación](docs/ARQUITECTURA.md) | Componentes y funcionamiento del sistema. |
| [Referencia de la API](docs/API.md) | Rutas y operaciones de la API. |
| [Matriz de cumplimiento](docs/MATRIZ_CUMPLIMIENTO.md) | Relación entre requisitos, implementación y evidencias. |
| [Plan de pruebas](docs/PRUEBAS.md) | Pruebas automatizadas y procedimientos de aceptación manual. |
| [Controles de seguridad](SECURITY.md) | Manejo de credenciales y consideraciones de despliegue. |

Para descargar el documento Word desde GitHub, abra el enlace y seleccione **Download raw file** (descargar archivo). GitHub puede no mostrar una vista previa del archivo `.docx`.

Como referencia del montaje inicial se conservan la [lista de preparación de las VMs](docs/PASOS_PENDIENTES_USUARIO.md) y el [estado del host registrado durante la instalación](docs/ESTADO_HOST.md). Estos documentos históricos no describen el estado actual de ejecución de las máquinas.

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
