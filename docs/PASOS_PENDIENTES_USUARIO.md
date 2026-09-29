# Pasos pendientes para completar el laboratorio

Esta guía empieza en el estado actual: el Host Windows ya está preparado, Docker está funcionando y los cuatro servicios de NetWatch están en ejecución. Lo que queda requiere interacción dentro de los instaladores de las tres máquinas virtuales.

## Estado actual al 28 de septiembre de 2026

- `NW-WIN11`: completa, `192.168.56.10`, `deviceId=1`, monitoreo ICMP y agente activos.
- `NW-XUBUNTU`: completa, `192.168.56.20`, `deviceId=2`, monitoreo ICMP y agente systemd activos incluso después de reiniciar; las métricas ya aparecen en NetWatch.
- `NW-SRV2025`: instalada, `192.168.56.30`, `deviceId=3`, monitoreo ICMP y agente activos incluso después de reiniciar; las métricas ya aparecen en NetWatch.

Las tres máquinas ya funcionan, se retiraron los archivos temporales de Windows Server, sus recursos quedaron ajustados y se tomó la instantánea final. Solo resta ejecutar la prueba integral descrita en la sección 8 y conservar las capturas y archivos CSV como evidencia.

## 1. Abrir y comprobar lo que ya funciona

1. Abra `http://localhost:8080`.
2. Consulte `NETWATCH-CREDENCIALES-LOCALES.txt` en la raíz del proyecto e ingrese con el usuario administrador indicado. No comparta ese archivo ni lo copie dentro de las VMs.
3. Abra `http://localhost:8082` para ver MySQL con Adminer. Use:

   - Sistema: `MySQL`
   - Servidor: `mysql`
   - Base, usuario y contraseña: los valores de la sección Adminer del archivo de credenciales.
4. Si reinicia el Host, abra Docker Desktop, espere **Engine running** y ejecute desde la raíz del proyecto:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Start-Lab.ps1
docker compose ps
```

No vuelva a ejecutar `Initialize-Environment.ps1` ni `Initialize-Lab-Automatically.ps1`: el entorno y las claves ya existen.

## 2. Descargar las tres ISO

Todas deben ser x64/AMD64:

| Máquina | Versión elegida | Descarga oficial |
|---|---|---|
| `NW-WIN11` | Windows 11 Enterprise Evaluation 25H2, x64, español | [ISO de Microsoft](https://aka.ms/Win11E-ISO-25H2-es-es) |
| `NW-XUBUNTU` | Xubuntu 26.04.1 LTS Desktop AMD64, con XFCE | [ISO de Xubuntu](https://cdimage.ubuntu.com/xubuntu/releases/26.04/release/xubuntu-26.04.1-desktop-amd64.iso) |
| `NW-SRV2025` | Windows Server 2025 Evaluation, x64, español | [ISO de Microsoft](https://aka.ms/WinServ2025iso-eses) |

Windows 11 Evaluation dura 90 días y Windows Server Evaluation 180 días. En Server seleccione **Windows Server 2025 Standard Evaluation (Server Core Installation)**. Xubuntu incluye escritorio gráfico ligero.

## 3. Crear las VMs sin exceder los 16 GB

Mantenga Docker limitado a 2 GB, como ya está configurado. Cree discos VDI de asignación dinámica:

| VM | RAM | CPU | Disco | Video |
|---|---:|---:|---:|---:|
| `NW-WIN11` | 4,096 MB | 2 vCPU | 64 GB | 64 MB |
| `NW-XUBUNTU` | 2,048 MB | 2 vCPU | 30 GB | 64 MB, VMSVGA |
| `NW-SRV2025` | 2,048 MB | 2 vCPU | 40 GB | 32 MB |

En las tres VMs:

1. Adaptador 1: **NAT** para instalación y actualizaciones.
2. Adaptador 2: **Adaptador solo-anfitrión**, conectado a la red cuya IP del Host es `192.168.56.1/24`.
3. Marque **Cable conectado**.
4. Desactive audio, USB, puerto serie, webcam, aceleración 3D y virtualización anidada cuando no sean necesarios.
5. Instale y actualice una VM a la vez mientras las otras están apagadas. Instale VirtualBox Guest Additions y tome una instantánea con la VM apagada al terminar.

Para Windows 11 habilite EFI, Secure Boot y TPM 2.0. En Xubuntu seleccione la instalación de aplicaciones **Predeterminada**, no Extendida. En Windows Server use Standard Server Core; no elija Datacenter ni Desktop Experience.

## 4. Configurar la red host-only

No agregue gateway ni DNS al Adaptador 2. El Adaptador 1 NAT conserva DHCP y proporciona Internet.

### Windows 11

Copie `deploy\windows\Configure-NetWatchNetwork.ps1` a la VM. Abra PowerShell como Administrador, confirme el nombre de la segunda interfaz con `Get-NetAdapter` y ejecute:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Configure-NetWatchNetwork.ps1 -IPAddress 192.168.56.10 -InterfaceAlias 'Ethernet 2'
```

### Xubuntu

Después de actualizar Xubuntu, instale las dependencias y reinicie:

```bash
sudo apt update
sudo apt full-upgrade -y
sudo apt install -y virtualbox-guest-utils virtualbox-guest-x11 openssh-server ca-certificates curl iputils-ping build-essential cmake libcurl4-openssl-dev
sudo reboot
```

Copie la carpeta `deploy/linux`, identifique el Adaptador 2 con `ip -br address` y ejecute, sustituyendo `enp0s8` si aparece otro nombre:

```bash
chmod +x deploy/linux/configure-network.sh
sudo deploy/linux/configure-network.sh enp0s8
ip -br address
```

La IP esperada es `192.168.56.20/24`.

### Windows Server

Copie el mismo script de Windows y ejecute como Administrador:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Configure-NetWatchNetwork.ps1 -IPAddress 192.168.56.30 -InterfaceAlias 'Ethernet 2'
```

### Comprobación desde el Host

```powershell
ping 192.168.56.10
ping 192.168.56.20
ping 192.168.56.30
Test-NetConnection 192.168.56.1 -Port 8081
```

Desde cada VM abra o consulte `http://192.168.56.1:8081/health`. No continúe hasta que las tres respondan al ping del Host y puedan alcanzar la API.

## 5. Registrar las tres VMs en NetWatch

En `http://localhost:8080`, vaya a **Dispositivos > Registrar dispositivo** y cree:

| Nombre | Tipo sugerido | IP | Máscara | Monitoreo |
|---|---|---|---|---|
| `Windows-Cliente` | Computadora | `192.168.56.10` | `255.255.255.0` | Habilitado |
| `Linux-Servidor` | Servidor o Máquina Virtual | `192.168.56.20` | `255.255.255.0` | Habilitado |
| `Windows-Servidor` | Servidor | `192.168.56.30` | `255.255.255.0` | Habilitado |

Deje gateway y DNS vacíos en estos registros. Abra el detalle de cada dispositivo y anote su número en la URL, por ejemplo `/devices/1` significa `deviceId=1`. Cada VM debe tener un ID diferente.

## 6. Generar los paquetes de agente

En el Host, desde la raíz del proyecto, sustituya `ID_WIN11`, `ID_LINUX` e `ID_SERVER` por los tres números reales:

```powershell
.\scripts\New-AgentPackage.ps1 -Platform Windows -DeviceId ID_WIN11 -Name 'Windows-Cliente'
.\scripts\New-AgentPackage.ps1 -Platform Linux -DeviceId ID_LINUX -Name 'Linux-Servidor'
.\scripts\New-AgentPackage.ps1 -Platform Windows -DeviceId ID_SERVER -Name 'Windows-Servidor'
```

Los paquetes aparecerán en `artifacts\agents`. Cada uno contiene un `agent.json` ya conectado a la API y con la clave correcta. Esas carpetas contienen un secreto: no las suba a Internet ni las comparta fuera del laboratorio.

Puede transferirlas mediante una carpeta compartida temporal de VirtualBox. En Windows aparece normalmente como `\\VBOXSVR\NombreCompartido`; en Xubuntu, con Guest Additions, como `/media/sf_NombreCompartido`. Retire la carpeta compartida al finalizar.

## 7. Instalar los agentes

### Windows 11 y Windows Server

Copie el paquete correspondiente dentro de cada VM. Abra PowerShell como Administrador en esa carpeta:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NetWatchAgent.ps1 -AgentExecutable .\netwatch-agent.exe -AgentConfiguration .\agent.json
Get-ScheduledTask -TaskName 'NetWatch Agent'
Get-ScheduledTaskInfo -TaskName 'NetWatch Agent'
```

Antes de instalar también puede ejecutar `./netwatch-agent.exe ./agent.json` y esperar el mensaje `Metrics sent successfully`.

Si Windows 11 bloquea el ejecutable local por Smart App Control, siga únicamente la sección **Si Windows bloquea el agente con 0x800711C7** de `GUIA_IMPLEMENTACION.md`. Tome una instantánea antes de cambiar esa protección. No desactive políticas de seguridad en el Host físico.

### Xubuntu

Abra una terminal dentro del paquete Linux:

```bash
cmake -S ./NetWatch.Agent -B ./NetWatch.Agent/build -DCMAKE_BUILD_TYPE=Release
cmake --build ./NetWatch.Agent/build --parallel
chmod +x ./install-agent.sh
sudo ./install-agent.sh ./NetWatch.Agent/build/netwatch-agent ./agent.json
systemctl status netwatch-agent --no-pager
journalctl -u netwatch-agent -n 50 --no-pager
```

## 8. Prueba final obligatoria

1. Espere 30 segundos: los tres dispositivos deben pasar a `Active` por ICMP.
2. Espere 60 segundos: cada detalle debe mostrar CPU, RAM y disco enviados por su agente.
3. Inicie sesión como técnico y confirme que puede consultar pero no administrar usuarios ni dispositivos.
4. Apague una VM. Después de tres intentos, aproximadamente 90 segundos, debe quedar `Disconnected` y crear evento/alerta.
5. Enciéndala; debe volver a `Active` y registrar la recuperación.
6. Marque la alerta como atendida y confirme que el estado del dispositivo no cambia por esa acción.
7. Genere reportes de métricas, eventos, alertas y estados; descargue cada CSV.
8. Ejecute en el Host:

```powershell
.\scripts\Test-NetWatch-Docker.ps1
docker compose ps
docker stats --no-stream
```

Anote resultados y evidencias siguiendo `docs\PRUEBAS.md`. La validación automatizada actual ya es **14/14**, pero las pruebas de red y agentes solo pueden completarse después de crear las VMs.

## 9. Uso diario y apagado seguro

Arranque en este orden: Docker Desktop, Xubuntu, Windows Server y Windows 11. Mantenga el uso de memoria del Host por debajo de 85 %. Para trabajo cotidiano encienda únicamente la VM que esté probando; use las tres simultáneamente solo para la prueba integral.

Para detener NetWatch conservando la base de datos:

```powershell
.\scripts\Stop-Lab.ps1
```

No use `-RemoveDatabase` ni `docker compose down -v` salvo que quiera borrar intencionalmente toda la base MySQL.

Para volver a iniciar:

```powershell
.\scripts\Start-Lab.ps1
```

La guía ampliada, incluidos diagnóstico, seguridad y despliegue con HTTPS, está en `docs\GUIA_IMPLEMENTACION.md`.
