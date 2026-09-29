[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Windows', 'Linux')]
    [string]$Platform,

    [Parameter(Mandatory)]
    [ValidateRange(1, 2147483647)]
    [int]$DeviceId,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$Name,

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repository 'artifacts\agents'
}

$environmentPath = Join-Path $repository '.env'
if (-not (Test-Path -LiteralPath $environmentPath)) {
    throw "No existe $environmentPath. Inicialice primero el laboratorio."
}

$environment = @{}
Get-Content -LiteralPath $environmentPath | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') {
        $environment[$matches[1].Trim()] = $matches[2]
    }
}

$agentApiKey = $environment['AGENT_API_KEY']
if ([string]::IsNullOrWhiteSpace($agentApiKey)) {
    throw 'AGENT_API_KEY no existe o está vacío en .env.'
}

$target = Join-Path $OutputDirectory $Name
New-Item -ItemType Directory -Path $target -Force | Out-Null

$configuration = [ordered]@{
    apiUrl = 'http://192.168.56.1:8081/api/metrics/agent'
    apiKey = $agentApiKey
    deviceId = $DeviceId
    intervalSeconds = 60
    verifyTls = $true
}
$configurationPath = Join-Path $target 'agent.json'
[System.IO.File]::WriteAllText(
    $configurationPath,
    ($configuration | ConvertTo-Json) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false)
)

if ($Platform -eq 'Windows') {
    $agentExecutable = Join-Path $repository 'src\NetWatch.Agent\build\Release\netwatch-agent.exe'
    if (-not (Test-Path -LiteralPath $agentExecutable)) {
        throw "No existe el agente compilado: $agentExecutable"
    }

    Copy-Item -LiteralPath $agentExecutable -Destination (Join-Path $target 'netwatch-agent.exe') -Force
    Copy-Item -LiteralPath (Join-Path $repository 'deploy\windows\Install-NetWatchAgent.ps1') -Destination $target -Force
    Copy-Item -LiteralPath (Join-Path $repository 'deploy\windows\Uninstall-NetWatchAgent.ps1') -Destination $target -Force
    $instructions = @"
Copie esta carpeta a la VM Windows. Abra PowerShell como Administrador dentro de ella y ejecute:

Set-ExecutionPolicy -Scope Process Bypass
.\Install-NetWatchAgent.ps1 -AgentExecutable .\netwatch-agent.exe -AgentConfiguration .\agent.json
Get-ScheduledTaskInfo -TaskName 'NetWatch Agent'

Dispositivo NetWatch: $DeviceId
"@
}
else {
    $sourceTarget = Join-Path $target 'NetWatch.Agent'
    $includeTarget = Join-Path $sourceTarget 'include'
    $sourceCodeTarget = Join-Path $sourceTarget 'src'
    New-Item -ItemType Directory -Path $includeTarget -Force | Out-Null
    New-Item -ItemType Directory -Path $sourceCodeTarget -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repository 'src\NetWatch.Agent\CMakeLists.txt') -Destination $sourceTarget -Force
    Get-ChildItem -LiteralPath (Join-Path $repository 'src\NetWatch.Agent\include') -File |
        Copy-Item -Destination $includeTarget -Force
    Get-ChildItem -LiteralPath (Join-Path $repository 'src\NetWatch.Agent\src') -File |
        Copy-Item -Destination $sourceCodeTarget -Force
    Copy-Item -LiteralPath (Join-Path $repository 'deploy\linux\install-agent.sh') -Destination $target -Force
    Copy-Item -LiteralPath (Join-Path $repository 'deploy\linux\netwatch-agent.service') -Destination $target -Force
    $instructions = @"
Copie esta carpeta a Xubuntu. Abra una terminal dentro de ella y ejecute:

sudo apt update
sudo apt install -y build-essential cmake libcurl4-openssl-dev ca-certificates
cmake -S ./NetWatch.Agent -B ./NetWatch.Agent/build -DCMAKE_BUILD_TYPE=Release
cmake --build ./NetWatch.Agent/build --parallel
chmod +x ./install-agent.sh
sudo ./install-agent.sh ./NetWatch.Agent/build/netwatch-agent ./agent.json
systemctl status netwatch-agent --no-pager

Dispositivo NetWatch: $DeviceId
"@
}

[System.IO.File]::WriteAllText(
    (Join-Path $target 'LEAME.txt'),
    $instructions + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host "Paquete $Platform creado en: $target" -ForegroundColor Green
Write-Host 'Contiene una clave privada; no lo comparta ni lo suba a Git.' -ForegroundColor Yellow
