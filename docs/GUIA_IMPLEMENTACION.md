# Guía de implementación y configuración

Esta es la secuencia recomendada para montar, demostrar y validar NetWatch NMS sin omitir dependencias. La topología propuesta mantiene el laboratorio aislado y permite que el host ejecute el servidor central.

## 1. Topología de laboratorio

| Equipo | Interfaz host-only | IP sugerida | Función |
|---|---|---:|---|
| Host Windows | VirtualBox Host-Only | `192.168.56.1/24` | Docker: Web, API y MySQL |
| VM Windows | Adaptador 2 host-only | `192.168.56.10/24` | Dispositivo + agente |
| VM Linux | Adaptador 2 host-only | `192.168.56.20/24` | Dispositivo + agente |
| VM Windows Servidor | Adaptador 2 host-only | `192.168.56.30/24` | Servidor adicional + agente o solo ICMP |

El Adaptador 1 de cada VM puede permanecer en NAT para descargar actualizaciones. El Adaptador 2 debe usar la misma red host-only. No configure gateway en la interfaz host-only; el gateway del sistema puede seguir siendo el del adaptador NAT.

Puertos:

- `8080/TCP`: panel web desde el navegador.
- `8081/TCP`: API, salud y recepción de agentes.
- `8082/TCP`: Adminer para visualizar MySQL; disponible únicamente en el Host Windows.
- ICMP echo: del host hacia las VMs.
- MySQL 3306 no se publica fuera de Docker.

Aunque se observan cuatro equipos en la tabla, solo se ejecutarán **tres máquinas virtuales**. El Host Windows es la computadora física y también ejecutará Docker.

## 2. Recursos e instalaciones para un host con 16 GB

### Versiones exactas que debe descargar

Versiones verificadas el **23 de septiembre de 2026**. Las tres imágenes deben ser **x64/AMD64**, no ARM64:

| VM | Descarga exacta | Selección durante la instalación | Fuente oficial |
|---|---|---|---|
| VM Windows `.10` | Windows 11 Enterprise Evaluation 25H2, x64, español | Windows 11 Enterprise Evaluation, no LTSC | [Descargar ISO oficial en español](https://aka.ms/Win11E-ISO-25H2-es-es) · [página de Microsoft](https://www.microsoft.com/es-es/evalcenter/download-windows-11-enterprise) |
| VM Linux `.20` | `xubuntu-26.04.1-desktop-amd64.iso` | Xubuntu Desktop con XFCE, selección predeterminada | [Descargar ISO oficial](https://cdimage.ubuntu.com/xubuntu/releases/26.04/release/xubuntu-26.04.1-desktop-amd64.iso) · [SHA256SUMS](https://cdimage.ubuntu.com/xubuntu/releases/26.04/release/SHA256SUMS) |
| VM Windows Servidor `.30` | Windows Server 2025 Evaluation, x64, español | **Windows Server 2025 Standard Evaluation (Server Core Installation)** | [Descargar ISO oficial en español](https://aka.ms/WinServ2025iso-eses) · [página de Microsoft](https://www.microsoft.com/es-es/evalcenter/download-windows-server-2025) |

Windows 11 Enterprise Evaluation funciona durante 90 días y Windows Server Evaluation durante 180 días. Son apropiados para el laboratorio académico; para conservarlo después deberá usar licencias válidas. En Windows Server elija **Standard**, no Datacenter, y **Server Core**, no Desktop Experience.

Xubuntu sí tiene escritorio gráfico. Se eligió en lugar de Ubuntu Desktop con GNOME porque XFCE es considerablemente más ligero y permite mantener las tres VMs activas en un host de 16 GB. Xubuntu 26.04 es LTS y recibe soporte hasta abril de 2029.

Descargue únicamente desde esos sitios oficiales. Los enlaces `aka.ms` son redirecciones oficiales de Microsoft y pueden iniciar la descarga directamente. Si una redirección falla, use la página de Microsoft de la misma fila y seleccione **Español > Enterprise 64 bits** o **Español > ISO 64 bits**, respectivamente.

Verifique las imágenes antes de conectarlas a VirtualBox:

```powershell
Get-FileHash 'C:\Ruta\Windows11Enterprise25H2.iso' -Algorithm SHA256
Get-FileHash 'C:\Ruta\xubuntu-26.04.1-desktop-amd64.iso' -Algorithm SHA256
Get-FileHash 'C:\Ruta\WindowsServer2025.iso' -Algorithm SHA256
```

- Para Windows 11 compare con el [PDF de hashes publicado por Microsoft](https://aka.ms/Win11-Hash-PDF).
- Para Xubuntu el SHA256 correcto de `xubuntu-26.04.1-desktop-amd64.iso` es `0cd5160f72ae6b7a26be7a29248b60cbd579b9e6aa8fbd274e95264da0ade43d`.
- Para Windows Server descargue únicamente mediante el enlace oficial de Microsoft y conserve el nombre original del archivo.

### Distribución recomendada

| Equipo o servicio | RAM | CPU virtual | Disco virtual dinámico | Observaciones |
|---|---:|---:|---:|---|
| Host Windows físico | reservar alrededor de 6 GB | no aplica | 100 GB libres recomendados | VirtualBox, Docker y navegador |
| Docker Desktop | límite 2,048 MB | 2 CPU | 20 GB o más | Durante la primera construcción puede subirlo temporalmente a 4 GB |
| VM Windows | 4,096 MB | 2 vCPU | 64 GB | Windows 11 Enterprise 25H2 |
| VM Linux | 2,048 MB | 2 vCPU | 30 GB | Xubuntu 26.04.1 Desktop XFCE |
| VM Windows Servidor | 2,048 MB | 2 vCPU | 40 GB | Windows Server 2025 Standard Core |

Las tres VMs reservan 8 GB y Docker queda limitado a 2 GB. Esto deja aproximadamente 6 GB para Windows host y el margen de VirtualBox. Los discos deben ser VDI de **asignación dinámica**; sus tamaños son máximos, no espacio ocupado inmediatamente.

Para mantener las tres VMs encendidas con 16 GB se recomienda **Windows Server Core**. Si necesita Desktop Experience, Microsoft recomienda 4 GB: asígnelos, pero cierre la VM Windows cliente cuando no sea parte de la prueba y espere más paginación durante la prueba integral.

Si el procesador tiene 8 o más procesadores lógicos, use la asignación de la tabla. Si tiene solo 4, asigne 2 vCPU a Windows 11, 1 a Xubuntu y 1 a Windows Server Core. No asigne a una sola VM más de la mitad de los procesadores lógicos del host.

No aumente RAM solo porque VirtualBox lo permite. Primero arranque Docker y las VMs, abra Administrador de tareas y confirme que la memoria total se mantenga debajo de 85 %. Cierre navegadores y aplicaciones pesadas, y ejecute las VMs sin mantener abiertas sus ventanas cuando no necesite interactuar. No reduzca Windows 11 por debajo de 4 GB, Xubuntu por debajo de 2 GB ni Server Core por debajo de 2 GB; apague una VM cuando no participe en la prueba.

### Lo que se instala en el Host Windows

Obligatorio:

1. Virtualización Intel VT-x o AMD-V habilitada en BIOS/UEFI.
2. Oracle VirtualBox 7 y, si necesita USB/RDP de VirtualBox, el Extension Pack de la misma versión.
3. Docker Desktop usando el backend WSL 2 y Docker Compose v2.
4. Git para Windows.
5. Un navegador moderno y PowerShell.
6. Esta carpeta del proyecto NetWatch.

Opcional pero recomendado para desarrollar o verificar:

- .NET SDK 8 para ejecutar `scripts\Test-NetWatch.ps1`.
- Visual Studio 2022 Build Tools con **Desktop development with C++** y CMake para compilar una sola vez el agente Windows. En Windows el agente usa WinHTTP, incluido en el sistema, y no requiere vcpkg ni libcurl.

No instale MySQL directamente en el host: el contenedor `mysql` ya lo proporciona. Tampoco instale Visual Studio completo si solo necesita Build Tools.

### Instalación automatizada del Host Windows

El proyecto incluye `scripts\Install-Host-Prerequisites.ps1`. Ejecútelo una sola vez desde PowerShell en la raíz del proyecto:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Install-Host-Prerequisites.ps1
```

Windows mostrará una solicitud de Control de cuentas de usuario; seleccione **Sí**. El script descarga desde fuentes oficiales e instala VirtualBox 7.2.20, WSL 2, Docker Desktop, .NET SDK 8, CMake 4.4.3 y Visual Studio 2022 Build Tools con C++. También compila `netwatch-agent.exe`, crea las reglas de firewall restringidas a la red host-only, copia la plantilla de límite de WSL a `%UserProfile%\.wslconfig` si no existe y guarda el registro en `host-setup.log`.

El script no reinicia Windows. Cuando termine, reinicie el Host Windows una vez para completar WSL y Virtual Machine Platform; después abra Docker Desktop y espere a que indique que el motor está funcionando. No vuelva a ejecutar el instalador si todos los componentes aparecen como instalados. El script detecta los componentes existentes y omite la mayoría de ellos, pero una instalación completa puede tardar varios minutos y descargar varios gigabytes.

Si solamente desea preparar el entorno de ejecución y no compilar el agente todavía, puede usar `-SkipBuildTools -SkipAgentBuild`. Para este proyecto se recomienda la ejecución completa.

### Limitar Docker Desktop

En Docker Desktop, abra **Settings > Resources** y establezca 2 CPU y 2 GB de memoria. Si Docker usa WSL 2 y no aparece el control de memoria, cree `%UserProfile%\.wslconfig` en el Host Windows:

```ini
[wsl2]
memory=2GB
processors=2
swap=1GB
localhostForwarding=true
```

Puede tomar `deploy\windows\wslconfig.example` como plantilla. Si ya existe `.wslconfig`, integre estas opciones sin borrar otros ajustes que necesite. Luego ejecute `wsl --shutdown` y vuelva a abrir Docker Desktop. El archivo `docker-compose.yml` limita además MySQL a 768 MB, la API a 512 MB, el panel a 384 MB y Adminer a 96 MB.

### Lo que se instala en la VM Windows

- Windows 11 Enterprise Evaluation 25H2 x64 en español, con todas las actualizaciones de seguridad.
- VirtualBox Guest Additions, recomendado pero no obligatorio.
- `netwatch-agent.exe`, su `agent.json` y los scripts de `deploy\windows`.

No necesita Docker, MySQL, .NET, Visual Studio, Git ni CMake. Compile el agente Windows una vez en el Host y copie el mismo ejecutable a esta VM y a Windows Servidor.

### Lo que se instala en la VM Linux

Use **Xubuntu 26.04.1 LTS Desktop de 64 bits**, que incluye el escritorio ligero XFCE. Instale Guest Additions y las dependencias del agente:

```bash
sudo apt update
sudo apt full-upgrade -y
sudo apt install -y virtualbox-guest-utils virtualbox-guest-x11 openssh-server ca-certificates curl iputils-ping build-essential cmake libcurl4-openssl-dev
sudo reboot
```

`build-essential`, CMake y los archivos de desarrollo de libcurl solo se requieren para compilar. Después de instalar el servicio puede retirarlos si necesita recuperar espacio, conservando `libcurl4`, `ca-certificates` y el binario del agente. En el instalador elija la selección predeterminada, no la extendida, para reducir consumo y espacio.

### Lo que se instala en la VM Windows Servidor

- Windows Server 2025 Standard Evaluation de 64 bits, instalación Server Core, con 2,048 MB. Si la evaluación exige Desktop Experience, asígnele 4,096 MB y no espere el mismo margen de memoria con las tres VMs activas.
- VirtualBox Guest Additions, si la edición seleccionada lo admite.
- El mismo `netwatch-agent.exe` compilado en el Host, un `agent.json` con el ID de esta VM y los scripts de `deploy\windows`.

No instale Docker, MySQL ni herramientas de compilación. IIS es opcional para simular carga de servidor; NetWatch no lo necesita. Para demostrar monitoreo sin agente, puede detener temporalmente la tarea del agente y conservar ICMP habilitado.

### Ajustes de VirtualBox para ahorrar recursos

- Use discos VDI dinámicos y controlador SATA.
- Desactive audio, webcam, USB y puerto serie cuando no sean necesarios.
- Desactive aceleración 3D. Use 64 MB de video en Windows, 64 MB en Xubuntu y 32 MB en Windows Server Core.
- No habilite virtualización anidada dentro de las VMs.
- Ejecute Windows Servidor en modo sin interfaz de VirtualBox cuando ya esté configurado. Xubuntu puede ejecutarse así cuando no necesite ver su escritorio.
- Mantenga el archivo de paginación de Windows administrado por el sistema.
- Tome una instantánea con la VM apagada después de configurarla; evite cadenas largas de snapshots.
- Para reducir carga, inicie en orden: Docker, VM Linux, VM Windows Servidor y finalmente VM Windows. Espere a que cada una termine de arrancar.
- El Adaptador NAT puede desconectarse después de instalar actualizaciones; el adaptador host-only debe permanecer activo.

### Orden recomendado para preparar sin saturar el host

1. Con todas las VMs apagadas, instale Docker, inicialice `.env` y ejecute la primera construcción de las imágenes.
2. Si la primera construcción termina por falta de memoria, suba temporalmente WSL a 4 GB, mantenga las VMs apagadas, construya y después restaure `memory=2GB` y ejecute `wsl --shutdown`.
3. Cierre Docker y prepare una VM a la vez: Windows, Linux y finalmente Windows Server.
4. Compile el agente Linux con las otras dos VMs apagadas. Mantenga Xubuntu en 2,048 MB para que el escritorio XFCE siga siendo utilizable.
5. Solo entonces inicie Docker y las tres VMs para ejecutar las pruebas integrales.

## 3. Preparar VirtualBox

1. En VirtualBox abra **Herramientas > Red > Redes solo-anfitrión**.
2. Cree o seleccione una red con IPv4 `192.168.56.1` y máscara `255.255.255.0`.
3. Desactive DHCP para usar las IP fijas de esta guía, o reserve direcciones y verifique que no se repitan.
4. En cada VM, configure:
   - Adaptador 1: NAT, opcional pero recomendado durante la instalación.
   - Adaptador 2: Adaptador solo-anfitrión, conectado a la red anterior.
   - Marque **Cable conectado**.
5. Para Windows 11 seleccione la plantilla de 64 bits, habilite EFI, Secure Boot y TPM 2.0, y asigne 2 vCPU, 4,096 MB y un VDI dinámico de 64 GB.
6. Para Xubuntu seleccione Linux/Ubuntu de 64 bits, 2 vCPU, 2,048 MB, 64 MB de video y un VDI dinámico de 30 GB.
7. Para Windows Server seleccione Windows Server 2025 de 64 bits, 2 vCPU, 2,048 MB y un VDI dinámico de 40 GB; elija Standard Evaluation Server Core durante la instalación.
8. Arranque las VMs y configure sus IP.

### Crear e instalar las tres VMs

#### 1. VM Windows 11

1. Nombre: `NW-WIN11`; tipo: Microsoft Windows; versión: Windows 11 (64-bit).
2. Asigne 4,096 MB, 2 vCPU y un VDI dinámico de 64 GB.
3. En **Sistema** habilite EFI, Secure Boot y TPM 2.0. Deje la virtualización anidada deshabilitada.
4. En **Red** configure Adaptador 1 NAT y Adaptador 2 Host-Only.
5. Monte la ISO Windows 11 Enterprise 25H2 x64 en la unidad óptica y arranque.
6. Seleccione español, instalación personalizada y el disco virtual vacío. Esta evaluación no requiere clave de producto.
7. Complete la cuenta solicitada con el Adaptador NAT conectado, ejecute Windows Update hasta que no queden actualizaciones y reinicie.
8. En PowerShell como administrador ejecute `Rename-Computer -NewName 'NW-WIN11' -Restart`.
9. Instale Guest Additions desde **Dispositivos > Insertar imagen de CD de las Guest Additions** y tome una instantánea con la VM apagada.

#### 2. VM Xubuntu con escritorio

1. Nombre: `NW-XUBUNTU`; tipo: Linux; versión: Ubuntu (64-bit).
2. Asigne 2,048 MB, 2 vCPU y un VDI dinámico de 30 GB.
3. Use controlador gráfico VMSVGA, 64 MB de memoria de video y aceleración 3D deshabilitada.
4. Configure Adaptador 1 NAT y Adaptador 2 Host-Only; monte `xubuntu-26.04.1-desktop-amd64.iso`.
5. Seleccione **Instalar Xubuntu**, idioma español y la selección de aplicaciones **Predeterminada**, no Extendida.
6. Use todo el disco virtual; esto solo borra el VDI de esa VM, no el disco del Host Windows.
7. Configure el nombre del equipo `nw-xubuntu`, cree un usuario administrador y desactive el inicio de sesión automático.
8. Reinicie, expulse la ISO, ejecute las instalaciones de la sección **Lo que se instala en la VM Linux** y tome una instantánea con la VM apagada.

#### 3. VM Windows Server

1. Nombre: `NW-SRV2025`; tipo: Microsoft Windows; versión: Windows 2025 (64-bit), o Windows 2022 (64-bit) si VirtualBox aún no muestra 2025.
2. Asigne 2,048 MB, 2 vCPU y un VDI dinámico de 40 GB.
3. Configure Adaptador 1 NAT y Adaptador 2 Host-Only; monte la ISO de Windows Server 2025 Evaluation x64.
4. En el instalador elija exactamente **Windows Server 2025 Standard Evaluation (Server Core Installation)**. No seleccione Datacenter ni Desktop Experience.
5. Instale en el disco virtual vacío y establezca una contraseña segura para `Administrator`.
6. Inicie sesión, ejecute `sconfig`, cambie el nombre a `NW-SRV2025`, configure fecha/zona horaria y ejecute todas las actualizaciones.
7. Mantenga NAT hasta activar la evaluación por Internet dentro de los primeros 10 días. Guest Additions es opcional en Server Core.
8. Reinicie y tome una instantánea con la VM apagada.

Después de instalar cada sistema, retire su ISO de la unidad óptica para que no vuelva a iniciar el instalador.

### Host Windows

En `ncpa.cpl`, confirme que **VirtualBox Host-Only Network** use únicamente:

- IPv4: `192.168.56.1`
- Máscara: `255.255.255.0`
- Puerta de enlace: vacía
- DNS: vacío

Después abra PowerShell como administrador y marque esa red como privada. Sustituya el alias si Windows muestra otro nombre:

```powershell
Get-NetAdapter
Get-NetIPAddress -AddressFamily IPv4
Set-NetConnectionProfile -InterfaceAlias 'Ethernet de solo-anfitrión de VirtualBox' -NetworkCategory Private
```

No configure puerta de enlace ni DNS en la NIC host-only: la salida a Internet de las VMs se obtiene por el Adaptador 1 NAT.

### VM Windows (`192.168.56.10`) y VM Windows Servidor (`192.168.56.30`)

Abra PowerShell como administrador en cada VM, identifique la segunda interfaz y ejecute el bloque. En la VM Windows use `.10`; en la VM Windows Servidor cambie solamente `$NetWatchIp` a `.30`:

```powershell
Get-NetAdapter
$NetWatchAdapter = 'Ethernet 2'
$NetWatchIp = '192.168.56.10' # use 192.168.56.30 en Windows Server
Set-NetIPInterface -InterfaceAlias $NetWatchAdapter -Dhcp Disabled
New-NetIPAddress -InterfaceAlias $NetWatchAdapter -IPAddress $NetWatchIp -PrefixLength 24
Set-NetConnectionProfile -InterfaceAlias $NetWatchAdapter -NetworkCategory Private
New-NetFirewallRule -DisplayName 'NetWatch ICMPv4 Echo' `
  -Protocol ICMPv4 -IcmpType 8 -Direction Inbound -Action Allow `
  -Profile Any -RemoteAddress 192.168.56.1
Get-NetIPAddress -InterfaceAlias $NetWatchAdapter -AddressFamily IPv4
```

También puede copiar `deploy\windows\Configure-NetWatchNetwork.ps1` a cada VM y ejecutar la variante correspondiente:

```powershell
# VM Windows
.\Configure-NetWatchNetwork.ps1 -IPAddress 192.168.56.10 -InterfaceAlias 'Ethernet 2'

# VM Windows Servidor
.\Configure-NetWatchNetwork.ps1 -IPAddress 192.168.56.30 -InterfaceAlias 'Ethernet 2'
```

Si la interfaz ya tiene una IP fija incorrecta, elimine solamente esa dirección con `Remove-NetIPAddress -InterfaceAlias $NetWatchAdapter -IPAddress DIRECCION_ANTERIOR -Confirm:$false` y repita el bloque. No cambie la interfaz NAT por accidente. No agregue gateway ni DNS a esta interfaz.

### Xubuntu invitado

Xubuntu usa NetworkManager. Identifique la interfaz del Adaptador 2 con `ip -br address` y use el script incluido, sustituyendo `enp0s8` si su nombre es diferente:

```bash
chmod +x deploy/linux/configure-network.sh
sudo deploy/linux/configure-network.sh enp0s8
nmcli connection show netwatch-hostonly
ip -br address
```

La plantilla `deploy/linux/60-netwatch-host-only.yaml.example` queda disponible únicamente para distribuciones que administren la red directamente con Netplan y no con NetworkManager.

Si usa UFW, ICMP no se habilita con una regla de puerto. La configuración normal de Xubuntu permite echo; si fue endurecida, revise `/etc/ufw/before.rules` y la política ICMP antes de modificarla.

La interfaz host-only de Linux tampoco lleva gateway ni DNS. El adaptador NAT conserva DHCP y proporciona Internet.

### Validar desde el host

```powershell
ping 192.168.56.10
ping 192.168.56.20
ping 192.168.56.30
```

No continúe hasta obtener respuesta de las tres VMs. Si falla, confirme IP/prefijo, cable virtual, tipo de adaptador, perfil de firewall y que no exista otra red `192.168.56.0/24` en una VPN o red física.

## 4. Levantar el servidor central con Docker

### Requisitos

- Docker Desktop con motor Linux y Compose v2, o Docker Engine + complemento Compose.
- Docker Desktop limitado a 2 GB de RAM y 2 procesadores como se indicó en la sección 2.
- Aproximadamente 6 GB disponibles para Windows host antes de encender las VMs.
- Puertos 8080, 8081 y 8082 libres.
- Este directorio completo copiado al equipo host.

Desde PowerShell en la raíz:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Initialize-Environment.ps1 `
  -AdminPassword 'Use-Una-Clave-Admin-Larga!' `
  -TechnicianPassword 'Use-Una-Clave-Tecnico-Larga!' `
  -MySqlPassword 'Use-Una-Clave-MySql-Larga!' `
  -MySqlRootPassword 'Use-Otra-Clave-Root-Larga!'
.\scripts\Start-Lab.ps1
```

El script bloquea valores de ejemplo. Guarde las claves en un gestor de contraseñas y no comparta `.env`.

Compruebe:

```powershell
docker compose ps
docker stats --no-stream
Invoke-RestMethod http://localhost:8081/health
Test-NetConnection 192.168.56.1 -Port 8081
```

Los límites incluidos en `docker-compose.yml` son 768 MB para MySQL, 512 MB para API, 384 MB para Web y 96 MB para Adminer; el límite global de Docker Desktop sigue siendo necesario para controlar cachés y procesos auxiliares.

Desde cada VM:

```text
http://192.168.56.1:8081/health
```

El instalador del Host incluye `scripts\Configure-Host-Firewall.ps1`. Ejecútelo como administrador para crear reglas limitadas por dirección remota a la subred del laboratorio. Se usa `Profile Any` porque una interfaz host-only sin gateway puede no recibir categoría de red; `RemoteAddress` evita abrir los puertos a la red física o Internet:

```powershell
.\scripts\Configure-Host-Firewall.ps1
```

Abra `http://localhost:8080`, ingrese como `admin` y cambie/gestione usuarios desde el panel. El usuario inicial solo se crea si no existe; cambiar luego `ADMIN_PASSWORD` no cambia automáticamente su hash.

### Visualizar MySQL con Adminer

Abra `http://localhost:8082` únicamente desde el Host Windows e ingrese:

| Campo de Adminer | Valor |
|---|---|
| Sistema | `MySQL` |
| Servidor | `mysql` |
| Usuario | valor `MYSQL_USER` de `.env` |
| Contraseña | valor `MYSQL_PASSWORD` de `.env` |
| Base de datos | valor `MYSQL_DATABASE` de `.env`, normalmente `netwatch` |

Adminer permite mostrar tablas, registros y ejecutar consultas durante la demostración. Está ligado a `127.0.0.1`, por lo que no se abre a las VMs ni a la red física. No use la contraseña root para la exposición y no publique el puerto 8082 en producción.

## 5. Registrar las máquinas

En **Dispositivos > Registrar** cree una entrada por VM:

| Nombre recomendado | Tipo | IP | Agente |
|---|---|---:|---|
| `Windows-Cliente` | `Computadora` | `192.168.56.10` | Sí |
| `Linux-Servidor` | `Máquina Virtual` o `Servidor` | `192.168.56.20` | Sí |
| `Windows-Servidor` | `Servidor` | `192.168.56.30` | Sí, o solo ICMP durante esa prueba |

- Nombre: único y reconocible; no reutilice nombres o direcciones.
- Máscara: `255.255.255.0` o `/24`.
- Gateway/DNS: déjelos vacíos porque el registro documenta la interfaz host-only.
- Active **Habilitar monitoreo**.

Anote el ID que aparece en la URL de detalle, por ejemplo `/devices/1`. Ese valor enlaza cada agente con su dispositivo. Un dispositivo nuevo comienza `Inactive`; debe pasar a `Active` dentro del primer ciclo de 30 segundos si ICMP responde.

## 6. Compilar el agente C++

El agente necesita C++20 y CMake 3.20+. En Windows usa WinHTTP, incluido en el sistema; en Linux usa libcurl. Para no cargar las VMs, compile el ejecutable Windows una sola vez en el host y cópielo a ambas VMs Windows. Compile la versión Linux dentro de la VM Linux, porque el binario de Windows no es compatible.

### Windows 10/11 y Windows Server: compilar una vez en el host

El instalador automatizado del Host ya realiza esta compilación. Para repetirla manualmente, use una consola Developer PowerShell con Visual Studio 2022 Build Tools, CMake y Git instalados:

```powershell
cmake -S .\src\NetWatch.Agent -B .\src\NetWatch.Agent\build `
  -A x64
cmake --build .\src\NetWatch.Agent\build --config Release
```

Binario esperado: `src\NetWatch.Agent\build\Release\netwatch-agent.exe`. Copie el mismo archivo a `192.168.56.10` y `192.168.56.30`; no instale Visual Studio, CMake ni libcurl dentro de esas VMs.

### Ubuntu/Debian

```bash
sudo apt update
sudo apt install -y build-essential cmake libcurl4-openssl-dev
cmake -S src/NetWatch.Agent -B src/NetWatch.Agent/build -DCMAKE_BUILD_TYPE=Release
cmake --build src/NetWatch.Agent/build --parallel
```

Binario esperado: `src/NetWatch.Agent/build/netwatch-agent`.

## 7. Configurar e instalar el agente

Copie `src/NetWatch.Agent/agent.example.json` como `agent.json`. No edite el ejemplo; cree un archivo por equipo:

```json
{
  "apiUrl": "http://192.168.56.1:8081/api/metrics/agent",
  "apiKey": "EL_MISMO_VALOR_DE_AGENT_API_KEY_EN_EL_ENV_DEL_SERVIDOR",
  "deviceId": 1,
  "intervalSeconds": 60,
  "verifyTls": true
}
```

Use el `deviceId` exacto de esa VM. Cree tres archivos separados; todos usan la misma URL y clave, pero nunca comparten `deviceId`:

| VM | Archivo que debe terminar instalado | `deviceId` |
|---|---|---:|
| Windows `.10` | `C:\Program Files\NetWatch Agent\agent.json` | ID de `Windows-Cliente` |
| Linux `.20` | `/etc/netwatch/agent.json` | ID de `Linux-Servidor` |
| Windows Server `.30` | `C:\Program Files\NetWatch Agent\agent.json` | ID de `Windows-Servidor` |

En HTTP de laboratorio `verifyTls` no interviene; manténgalo `true` para que un cambio futuro a HTTPS sea seguro. Proteja cada archivo porque contiene una credencial.

### Instalar en Windows

Copie el ejecutable, `agent.json` y `deploy\windows\Install-NetWatchAgent.ps1` a la VM. Abra PowerShell como administrador:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NetWatchAgent.ps1 `
  -AgentExecutable .\netwatch-agent.exe `
  -AgentConfiguration .\agent.json
Get-ScheduledTask -TaskName 'NetWatch Agent'
Get-ScheduledTaskInfo -TaskName 'NetWatch Agent'
```

Se instala en `C:\Program Files\NetWatch Agent` como una tarea de inicio ejecutada por SYSTEM. Para probar antes, ejecute `netwatch-agent.exe agent.json` en consola y espere “Metrics sent successfully”.

#### Si Windows bloquea el agente con `0x800711C7`

El agente académico se compila localmente y no posee un certificado comercial. Smart App Control puede bloquear ejecutables desconocidos sin firma; Microsoft indica que, en modo de aplicación, solo admite código reconocido o firmado por una entidad del programa de raíces de confianza. No desactive esta protección en el Host principal de manera automática.

En la **VM Windows 11 desechable del laboratorio**, pruebe primero el agente. Si Windows muestra “Una directiva de Control de aplicaciones bloqueó este archivo”, tome una instantánea de la VM y abra **Seguridad de Windows > Control de aplicaciones y navegador > Configuración de Control inteligente de aplicaciones > Desactivado**. Esta decisión es de un solo sentido: para volver a activarlo normalmente hay que restablecer o reinstalar Windows. La alternativa para un entorno real es firmar el agente con un certificado de firma de código emitido por un proveedor confiable. Consulte [Smart App Control](https://learn.microsoft.com/windows/apps/develop/smart-app-control/overview) y la [guía de firma de Microsoft](https://learn.microsoft.com/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control).

Windows Server Core no suele presentar la interfaz de Smart App Control; si una política empresarial equivalente bloquea el ejecutable, no la desactive: solicite al administrador una regla de confianza o un certificado aprobado.

Desinstalación:

```powershell
.\Uninstall-NetWatchAgent.ps1
```

### Instalar en Linux

Desde la raíz copiada a la VM:

```bash
chmod +x deploy/linux/install-agent.sh
sudo deploy/linux/install-agent.sh src/NetWatch.Agent/build/netwatch-agent src/NetWatch.Agent/agent.json
systemctl status netwatch-agent --no-pager
journalctl -u netwatch-agent -f
```

Se crea el usuario sin inicio de sesión `netwatch`, se protege `/etc/netwatch/agent.json` con modo 0640 y el servicio se reinicia si falla.

## 8. Validar operación

Valide primero las 14 pruebas automatizadas. En un equipo que permita ensamblados de desarrollo puede usar:

```powershell
.\scripts\Test-NetWatch.ps1
```

Si el Host bloquea el ensamblado xUnit con `0x800711C7`, no confunda “No hay ninguna prueba disponible” con un éxito. Después del reinicio y con Docker Desktop activo, ejecute las mismas pruebas dentro del contenedor Linux:

```powershell
.\scripts\Test-NetWatch-Docker.ps1
```

Ambos scripts validan explícitamente que el resultado sea **14 ejecutadas y 14 aprobadas**.

1. Espere 30 segundos y confirme `Active` por ICMP.
2. Espere 60 segundos y confirme CPU/RAM/disco en detalle del dispositivo.
3. Apague una VM. Después de aproximadamente 90 segundos debe quedar `Disconnected` con historial, evento y alerta.
4. Enciéndala. El siguiente ping debe registrar recuperación y `Active` si no hay umbral excedido.
5. Marque la alerta como atendida; el estado técnico no debe cambiar por esa acción.
6. Genere reportes de métricas, eventos, alertas y estados por dispositivo/fecha, y descargue CSV.
7. Ejecute todos los pasos de `PRUEBAS.md`.

## 9. Operación diaria

Para que el host de 16 GB no se sature:

1. Inicie Docker Desktop y espere a que `docker compose ps` muestre MySQL, API, Web y Adminer en ejecución; los tres primeros tienen comprobación de salud.
2. Inicie la VM Linux, luego Windows Server y al final Windows cliente.
3. En el Administrador de tareas mantenga el uso sostenido de memoria por debajo de 85 %. Si lo supera, cierre aplicaciones del host o apague temporalmente la VM que no participe en la prueba; no baje Xubuntu de 2,048 MB si utilizará su escritorio.
4. Apague en orden inverso. Detenga Docker Desktop al terminar la sesión si necesita recuperar hasta 2 GB.

La prueba integral sí requiere las tres VMs encendidas a la vez; para desarrollo cotidiano basta Docker y la VM que se esté probando.

```powershell
docker compose ps
docker compose logs --tail 100 api
docker compose restart api web
.\scripts\Stop-Lab.ps1
```

`Stop-Lab.ps1` conserva MySQL. Use `-RemoveDatabase` solo cuando quiera destruir intencionalmente todos los datos del laboratorio.

Actualización del código:

```powershell
git pull
docker compose up --build -d
```

Antes de actualizar, respalde MySQL y ejecute las pruebas. La API aplica migraciones pendientes al iniciar.

## 10. Despliegue fuera del laboratorio

HTTP solo está permitido en la red virtual aislada. Para producción:

1. Use un servidor Linux actualizado, DNS real y firewall.
2. Cambie todos los secretos y no reutilice los del laboratorio.
3. Coloque Nginx u otro reverse proxy delante de los contenedores.
4. Instale certificado confiable (por ejemplo, ACME/Let's Encrypt) y habilite TLS 1.2/1.3.
5. Use `deploy/nginx/netwatch.conf.example` como base, ajustando dominio y rutas de certificado.
6. Cambie `NETWATCH_BIND_ADDRESS=127.0.0.1` en `.env`, reconstruya los servicios y exponga solo 80/443 mediante Nginx.
7. Configure agentes con `https://netwatch.su-dominio/api/metrics/agent` y `verifyTls: true`.
8. Restrinja `Cors__AllowedOrigins__0` al origen HTTPS si clientes de navegador acceden directamente a la API.
9. Centralice logs, configure respaldo automatizado y pruebe restauraciones.
10. Rote `AGENT_API_KEY` coordinando API y todos los agentes. Rote `JWT_KEY` durante una ventana que acepte cerrar sesiones activas.

El reverse proxy debe soportar WebSocket para Blazor Server. No publique MySQL ni desactive la validación TLS del agente.

## 11. Solución de problemas

| Síntoma | Verificación |
|---|---|
| API no inicia | `docker compose logs api`; revise claves, conexión y salud de MySQL. |
| Web abre pero login falla | Compruebe `api`, usuario inicial y que no cambió la clave solo en `.env` después de sembrar. |
| VM responde desde otra VM pero no desde host | Adaptador equivocado, firewall invitado o conflicto VPN/ruta. |
| El host responde al ping pero NetWatch no | Revise `docker compose logs api`, confirme `NET_RAW` y la ruta desde Docker Desktop hacia la red host-only. Si la política de su host bloquea ICMP desde contenedores, ejecute API/Web nativamente en el host o use un host Linux/VM servidor unido a esa red. |
| Dispositivo queda Inactive | Active monitoreo y espere el primer ping. |
| Queda Disconnected aunque el agente envía | El estado de conectividad exige ICMP; permita echo o documente que ese equipo no puede usar esta comprobación. |
| Agente obtiene HTTP 401 | `apiKey` no coincide exactamente con `AGENT_API_KEY`. |
| Agente obtiene HTTP 404 | `deviceId` no existe o la URL no termina en `/api/metrics/agent`. |
| Métricas no aparecen | Dispositivo desactivado/monitoreo apagado, servicio detenido o salida al puerto 8081 bloqueada. |
| Windows no informa temperatura | Es el comportamiento previsto; se envía `null`. |
| Error TLS | Corrija DNS/cadena/fecha del equipo; no use `verifyTls=false` como arreglo de producción. |
| `0x800711C7` o “Control de aplicaciones bloqueó este archivo” | Consulte la subsección de Smart App Control. En el Host use `Test-NetWatch-Docker.ps1`; en la VM Windows del laboratorio decida entre un certificado confiable o desactivar Smart App Control después de tomar una instantánea. |
| El host supera 85 % de RAM | Verifique la asignación fija de cada VM, `docker stats` y el límite de WSL. Cierre aplicaciones del host o apague una VM fuera de las pruebas integrales. |

## 12. Lista final de entrega

- [ ] `.env` existe solo en el servidor y no contiene marcadores `CAMBIAR_`.
- [ ] Web, API y MySQL aparecen `running`; API `/health` responde.
- [ ] Host `.1` y VMs `.10`, `.20`, `.30` tienen IP única; las tres responden ICMP y poseen el ID correcto.
- [ ] El consumo sostenido del host queda por debajo de 85 % con las tres VMs y Docker activos.
- [ ] Agentes envían cada 60 s y no comparten `deviceId`.
- [ ] Los roles se verificaron desde UI y API.
- [ ] Las 14 pruebas automatizadas y las 14 manuales fueron registradas.
- [ ] Desconexión, umbrales, recuperación, alerta atendida y CSV fueron demostrados.
- [ ] Existe respaldo probado y responsable operativo asignado.
- [ ] Producción usa HTTPS válido, firewall y secretos diferentes al laboratorio.

## 13. Referencias oficiales de configuración

- [Requisitos de Windows 11](https://learn.microsoft.com/es-es/windows/whats-new/windows-11-requirements)
- [Requisitos de hardware de Windows Server](https://learn.microsoft.com/en-us/windows-server/get-started/hardware-requirements)
- [Windows 11 Enterprise 25H2 Evaluation](https://www.microsoft.com/es-es/evalcenter/download-windows-11-enterprise)
- [Windows Server 2025 Evaluation](https://www.microsoft.com/es-es/evalcenter/download-windows-server-2025)
- [Xubuntu 26.04.1 LTS y descarga AMD64](https://cdimage.ubuntu.com/xubuntu/releases/26.04/release/)
- [Ciclo de soporte de Xubuntu 26.04](https://xubuntu.org/release/26.04/)
- [Configuración global de memoria y CPU de WSL 2](https://learn.microsoft.com/windows/wsl/wsl-config)
- [Red host-only de VirtualBox](https://download.virtualbox.org/virtualbox/7.1.12/UserManual.pdf)
- [Límites `mem_limit` y `cpus` de Docker Compose](https://docs.docker.com/reference/compose-file/services/)
