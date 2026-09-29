# Estado del Host Windows

Verificado el 23 de septiembre de 2026 en el Host físico Windows 11 Pro 25H2, compilación 26200.9457.

## Instalado y comprobado

| Componente | Estado comprobado |
|---|---|
| Virtualización | El sistema detecta un hipervisor y la seguridad basada en virtualización está activa |
| Oracle VirtualBox | 7.2.20 r175154 |
| Red host-only | `192.168.56.1/24`, DHCP desactivado, adaptador activo |
| WSL | 2.7.14.0, kernel 6.18.33.2-2 |
| Docker Desktop | 4.92.0; motor WSL 2 en ejecución después del reinicio |
| Docker CLI | 29.8.0 |
| Docker Compose | 5.5.1 |
| .NET SDK | 8.0.425 |
| CMake | 4.4.3 |
| Visual Studio Build Tools | 2022 17.14.41 con compilador C++ x64 |
| Límite WSL/Docker | 2 GB RAM, 2 procesadores y 1 GB de swap en `%UserProfile%\.wslconfig` |
| Firewall | TCP 8080 y 8081 permitidos únicamente desde `192.168.56.0/24` |
| Adminer | En ejecución en `127.0.0.1:8082`, accesible únicamente desde el Host |
| Docker Compose | MySQL, API, Web y Adminer construidos y en ejecución |
| Pruebas automatizadas | 14 ejecutadas, 14 aprobadas, 0 fallidas y 0 omitidas |

El agente Windows fue recompilado con el runtime de C++ enlazado estáticamente para funcionar en VMs limpias sin instalar Visual C++ Redistributable. Binario: `src\NetWatch.Agent\build\Release\netwatch-agent.exe`. Tamaño verificado: 1,593,344 bytes. SHA-256: `0BA29583693FDC255E9D03AAF71DB93E2C0F32E5D5C85E5652FE9C0FEA7B2CA9`. Sus únicas dependencias dinámicas son `KERNEL32.dll`, `WINHTTP.dll` e `IPHLPAPI.dll`, incluidas en Windows.

La solución .NET se restauró y compiló en Release con 0 errores y 0 advertencias. Smart App Control bloquea el ensamblado xUnit local con `0x800711C7`, por lo que se ejecutó `scripts\Test-NetWatch-Docker.ps1` dentro del contenedor oficial `mcr.microsoft.com/dotnet/sdk:8.0.425`. La verificación final posterior al arranque produjo **14 aprobadas, 0 fallidas, 0 omitidas**, con una duración de 391 ms. Evidencia: `TestResults\netwatch-docker-20260923-224553.trx`.

## Estado después del reinicio

El Host fue reiniciado, WSL 2 puede iniciar y Docker Desktop muestra **Engine running**. La imagen de pruebas quedó en caché; el contenedor temporal fue eliminado automáticamente al terminar.

## Laboratorio central en ejecución

Se creó `.env` con claves aleatorias fuertes y se creó `NETWATCH-CREDENCIALES-LOCALES.txt`. El archivo de credenciales está excluido de Git y su ACL permite acceso únicamente al usuario actual, SYSTEM y Administradores. No se deben volver a ejecutar los inicializadores salvo que se decida reconstruir el laboratorio desde cero.

Servicios comprobados:

- Panel NetWatch: `http://localhost:8080`
- Salud de la API: `http://localhost:8081/health`
- Adminer para visualizar MySQL: `http://localhost:8082`

Las tres rutas devolvieron HTTP 200. Se verificó el inicio de sesión de los usuarios Administrator y Technician, el acceso autenticado al dashboard y el rechazo HTTP 401 de una petición anónima. MySQL está saludable y aplicó la migración/semillas iniciales.

El 24 de septiembre de 2026 se añadió `iputils-ping` a la imagen de la API. Se comprobó ICMP desde el contenedor ejecutado como usuario no privilegiado hacia la primera VM (`192.168.56.10`): 2 respuestas de 2, 0 % de pérdida. La prueba mediante `POST /api/diagnostics/ping/1` fue exitosa y `Windows-Cliente` pasó a estado `Active`.

El 28 de septiembre de 2026 quedó instalada y conectada la segunda VM, `NW-XUBUNTU`, con Xubuntu 26.04.1 LTS. Usa NAT en `enp0s3` y la red host-only `192.168.56.20/24` en `enp0s8`, sin gateway ni DNS en esta última. El Host responde al ping con 0 % de pérdida y la VM alcanza `http://192.168.56.1:8081/health`. Se registró como `Linux-Servidor`, `deviceId=2`; el servicio systemd `netwatch-agent.service` está habilitado y activo. NetWatch confirmó estado `Active`, respuesta ICMP y métricas persistidas de CPU, RAM, disco y tráfico cada 60 segundos. También se reinició la VM y se comprobó que el agente volvió automáticamente como `enabled` y `active` y continuó enviando métricas. La temperatura aparece sin valor porque VirtualBox no expone normalmente un sensor térmico al sistema invitado.

El 28 de septiembre de 2026 también quedó instalada y conectada `NW-SRV2025` con Windows Server 2025 Standard Evaluation, Server Core. Usa NAT en `Ethernet 2` y la dirección estática host-only `192.168.56.30/24` en `Ethernet`, sin gateway ni DNS en esta última. El Host obtiene respuesta ICMP con 0 % de pérdida y el servidor alcanza el puerto 8081 y `/health`. Se registró como `Windows-Servidor`, `deviceId=3`; la tarea programada `NetWatch Agent` quedó en estado `Running`. Tras reiniciar el servidor, la tarea y el proceso se iniciaron automáticamente y NetWatch continuó recibiendo métricas de CPU, RAM, disco y tráfico.

Consumo medido con los servicios estabilizados:

| Contenedor | Memoria aproximada | Límite |
|---|---:|---:|
| MySQL | 267 MiB | 768 MiB |
| API | 92 MiB | 512 MiB |
| Web | 35 MiB | 384 MiB |
| Adminer | 15 MiB | 96 MiB |

Total aproximado de los contenedores: **409 MiB**, además de la sobrecarga de WSL/Docker limitada globalmente a 2 GB.

En Adminer seleccione MySQL, servidor `mysql`, y use los valores MySQL del archivo `NETWATCH-CREDENCIALES-LOCALES.txt`.

## Trabajo pendiente del usuario

Las tres VMs están instaladas, conectadas, registradas y enviando métricas: `Windows-Cliente` (`192.168.56.10`, `deviceId=1`), `Linux-Servidor` (`192.168.56.20`, `deviceId=2`) y `Windows-Servidor` (`192.168.56.30`, `deviceId=3`). En las tres se verificó el inicio automático del agente. Las carpetas temporales se retiraron, Windows Server se ajustó a 2 vCPU y 2,048 MB, se aplicaron las tareas finales y se tomó su instantánea. Solo falta ejecutar y documentar la prueba integral simultánea, el ciclo desconexión/recuperación, los permisos del técnico y la exportación de reportes. La secuencia breve y los comandos exactos están en `docs\PASOS_PENDIENTES_USUARIO.md`; la explicación completa permanece en `docs\GUIA_IMPLEMENTACION.md`.
