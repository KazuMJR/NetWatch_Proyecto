# Plan de pruebas y aceptación

## 1. Pruebas automatizadas

Desde la raíz:

```powershell
.\scripts\Test-NetWatch.ps1
```

Criterio de aceptación: compilación sin errores y 14/14 pruebas aprobadas. Estas pruebas cubren las reglas centrales con una base aislada en memoria; no sustituyen la prueba integrada de MySQL, Docker, red e interfaz.

## 2. Preparación de la prueba integrada

1. Inicie NetWatch con Docker siguiendo `GUIA_IMPLEMENTACION.md`.
2. Cree las VMs Windows, Linux y Servidor en la red host-only `192.168.56.0/24`.
3. Configure, por ejemplo, Windows `192.168.56.10`, Linux `192.168.56.20` y Servidor `192.168.56.30`.
4. Desde el host verifique el ping a las tres direcciones.
5. Registre los dispositivos con monitoreo habilitado.
6. Instale el agente en los equipos que deban reportar métricas; un equipo sin agente se supervisa solo por ICMP.
7. Use un reloj en UTC o anote la conversión; la aplicación guarda la hora del servidor en UTC.

## 3. Ejecución manual de CP-001 a CP-014

| Caso | Procedimiento | Resultado esperado |
|---|---|---|
| CP-001 | Iniciar sesión con administrador activo. | Acceso al dashboard y opciones de administrador. |
| CP-002 | Intentar una clave incorrecta. | Mensaje de rechazo; no se crea sesión. |
| CP-003 | Ingresar como técnico e intentar abrir `/users` y `/devices/new`. Repetir contra la API. | La UI no ofrece gestión y la API responde 403. |
| CP-004 | Registrar una VM con datos válidos. | Se guarda como `Inactive`; después del primer ping correcto cambia a `Active`. |
| CP-005 | Registrar otro dispositivo activo con la misma IP. | Error 409 y no se duplica el registro. |
| CP-006 | Mantener la VM encendida y esperar hasta 30 s. | `LastPingAtUtc` se actualiza y el estado queda `Active`. |
| CP-007 | Apagar la VM o bloquear ICMP y esperar 3 ciclos (aprox. 90 s). | `Disconnected`, historial, evento y una alerta crítica. |
| CP-008 | Iniciar agente con ID/clave correctos y esperar 60 s. | Aparece una métrica en detalle/dashboard. |
| CP-009 | Enviar con curl CPU=101 y luego deviceId inexistente. | Respuesta 400 y 404; no se persiste la métrica. |
| CP-010 | Enviar CPU=85 mediante curl. | Estado `Warning`, historial, evento y alerta. |
| CP-011 | Enviar luego CPU/RAM/disco por debajo de umbrales. | Estado `Active` y recuperación registrada. |
| CP-012 | Revisar panel tras las pruebas anteriores. | Conteos, alertas y métricas concuerdan con los datos. |
| CP-013 | Generar CSV para un dispositivo y rango corto. | Solo aparecen filas del filtro; los datos fuente permanecen. |
| CP-014 | Desactivar un usuario, cerrar su sesión e intentar reingresar. | El registro sigue listado como inactivo y el acceso es rechazado. |

## 4. Pruebas adicionales recomendadas

### Validación de red

- IP, gateway o DNS con texto: debe responder 400.
- MAC inválida: debe responder 400.
- Máscara ausente o prefijo mayor a 128: debe responder 400.
- Desactivar monitoreo: el dispositivo pasa a `Inactive` y deja de recibir ping del trabajador.

### Alertas

- Mantener el mismo umbral durante varios envíos: debe existir solo una alerta pendiente equivalente.
- Marcar alerta atendida: no debe modificar el estado técnico.
- Recuperar métricas: debe registrar la recuperación sin borrar historia; la alerta solo cambia a atendida por acción del usuario.

### Seguridad

- Acceder a endpoint protegido sin token: 401.
- Usar token de técnico para crear usuario/dispositivo: 403.
- Enviar métrica sin `X-Agent-Key` o con clave errónea: 401.
- Comprobar que `.env` y `agent.json` no aparecen en `git status`.
- En producción, verificar que HTTP redirige a HTTPS y el agente falla ante certificado inválido.

### Persistencia y reinicio

1. Registre datos y ejecute `docker compose restart`.
2. Confirme que usuarios, dispositivos e historia permanecen.
3. Genere un respaldo, levante una instalación limpia y restaure.

## 5. Evidencias para la revisión académica

Conserve por cada ejecución: fecha/hora, versión o commit, responsable, pasos, resultado esperado, resultado obtenido, captura o log y estado Aprobado/Fallido. No incluya secretos en capturas ni adjunte `.env`.

Comandos de diagnóstico:

```powershell
docker compose ps
docker compose logs --tail 200 api
docker compose logs --tail 200 web
docker compose logs --tail 200 mysql
Invoke-RestMethod http://localhost:8081/health
```

En Linux para el agente:

```bash
systemctl status netwatch-agent --no-pager
journalctl -u netwatch-agent -n 100 --no-pager
```

En Windows para el agente:

```powershell
Get-ScheduledTask -TaskName 'NetWatch Agent'
Get-ScheduledTaskInfo -TaskName 'NetWatch Agent'
```
